using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The driving test ground (Verkehrsübungsplatz) for the tutorial: a flat fenced asphalt
/// yard far away from the forest, with a painted circuit, a slalom of traffic cones and two
/// bus stops. Built at runtime only while the driving test runs.
///
/// Circuit (yard space, seen from above): up the left straight (x = -30, +z), round the top,
/// down the right straight (x = +30, -z) through the slalom to stop A, round the bottom and
/// up again to stop B.
/// </summary>
public class TrainingYard : MonoBehaviour
{
    public class Stop
    {
        public string name;
        public Vector3 sign;        // world position of the "H" sign: the front door belongs here
        public Vector3 direction;   // driving direction along the platform
        public Vector3 kerbOut;     // from the lane towards the platform
    }

    class Gate { public Vector3 centre; public Rigidbody left, right; }

    const float LaneHalf = 4f;
    const float Straight = 30f;     // x of the two straights
    const float TopZ = 80f, BottomZ = -100f, Radius = 30f;

    public Vector3 StartPosition { get; private set; }
    public Quaternion StartRotation { get; private set; }
    public Stop StopA { get; private set; }
    public Stop StopB { get; private set; }

    /// <summary>Count slalom gates (only while the slalom task runs).</summary>
    public bool TrackGates { get; set; }
    public int GateCount => gates.Count;
    public int NextGate { get; private set; }
    public int MissedGates { get; private set; }
    public bool SlalomDone => NextGate >= gates.Count;
    public int ConesHit { get; private set; }

    readonly List<Gate> gates = new List<Gate>();
    readonly List<Rigidbody> cones = new List<Rigidbody>();
    readonly Dictionary<Rigidbody, Vector3> coneRest = new Dictionary<Rigidbody, Vector3>();
    readonly HashSet<Rigidbody> knocked = new HashSet<Rigidbody>();
    Material coneMat, coneNextMat, white, asphalt, grass, metal, fence;
    ForestRoad road;
    BusController bus;
    PropKit kit;

    public static TrainingYard Build(ForestRoad road, BusController bus, Vector3 origin)
    {
        var go = new GameObject("Training Yard");
        go.transform.position = origin;
        var yard = go.AddComponent<TrainingYard>();
        yard.road = road;
        yard.bus = bus;
        yard.Create();
        return yard;
    }

    Vector3 W(float x, float y, float z) => transform.TransformPoint(new Vector3(x, y, z));

    void Create()
    {
        var lit = Resources.Load<Material>("PSX/PsxLit");
        var glow = Resources.Load<Material>("PSX/PsxGlow");
        kit = new PropKit(transform, lit, glow != null ? glow : lit);
        Material baseConcrete = road != null && road.concrete != null ? road.concrete : lit;
        asphalt = PropKit.Tinted(baseConcrete, new Color(0.42f, 0.42f, 0.44f), "Yard Asphalt");
        grass = road != null && road.forestFloor != null ? PropKit.Tinted(road.forestFloor, new Color(0.55f, 0.75f, 0.45f), "Yard Grass") : PropKit.Tinted(lit, new Color(0.3f, 0.45f, 0.22f), "Yard Grass");
        white = PropKit.Tinted(lit, new Color(0.92f, 0.92f, 0.88f), "Yard Paint");
        coneMat = PropKit.Tinted(lit, new Color(1f, 0.42f, 0.08f), "Cone");
        coneNextMat = PropKit.Tinted(glow != null ? glow : lit, new Color(0.35f, 1f, 0.35f), "Cone Next");
        metal = road != null && road.metal != null ? road.metal : PropKit.Tinted(lit, new Color(0.55f, 0.56f, 0.58f), "Metal");
        fence = PropKit.Tinted(lit, new Color(0.62f, 0.64f, 0.6f), "Fence");

        // Ground: grass all around, the asphalt yard on top (mesh colliders: the bus drives on them).
        Ground("Grass", new Vector3(500f, 1f, 600f), new Vector3(0f, -0.06f, -10f), grass, 8f);
        Ground("Asphalt", new Vector3(130f, 1f, 300f), new Vector3(0f, 0f, -10f), asphalt, 6f);

        PaintCircuit();
        Slalom();
        StopA = BuildStop("A", new Vector3(Straight, 0f, -50f), Vector3.back);
        StopB = BuildStop("B", new Vector3(-Straight, 0f, 30f), Vector3.forward);
        Fence(65f, 140f, -160f);
        Office();
        kit.Finish();

        StartPosition = W(-Straight, 0f, -92f);
        StartRotation = transform.rotation;
        HighlightGates();
    }

    void Ground(string name, Vector3 size, Vector3 topCentre, Material mat, float uv)
    {
        var mesh = MeshKit.Box(size, uv);
        var go = MeshKit.Spawn(name, transform, mesh, mat, W(topCentre.x, topCentre.y - size.y, topCentre.z), transform.rotation, false);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // ---------------------------------------------------------------- markings

    List<Vector3> Centreline()
    {
        var pts = new List<Vector3>();
        for (float z = BottomZ; z < TopZ; z += 4f) pts.Add(new Vector3(-Straight, 0f, z));
        for (float a = 180f; a > 0f; a -= 6f) pts.Add(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * Radius, 0f, TopZ + Mathf.Sin(a * Mathf.Deg2Rad) * Radius));
        for (float z = TopZ; z > BottomZ; z -= 4f) pts.Add(new Vector3(Straight, 0f, z));
        for (float a = 0f; a > -180f; a -= 6f) pts.Add(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * Radius, 0f, BottomZ + Mathf.Sin(a * Mathf.Deg2Rad) * Radius));
        return pts;
    }

    void PaintCircuit()
    {
        var mb = new MeshKit.Builder();
        var pts = Centreline();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
            Vector3 dir = (b - a).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, dir);
            Strip(mb, a + r * LaneHalf, b + r * LaneHalf, 0.18f);
            Strip(mb, a - r * LaneHalf, b - r * LaneHalf, 0.18f);
            if (i % 2 == 0) Strip(mb, a, a + (b - a) * 0.5f, 0.12f);   // dashed middle
        }
        // Start line and arrows on the straights.
        Strip(mb, new Vector3(-Straight - LaneHalf, 0f, -88f), new Vector3(-Straight + LaneHalf, 0f, -88f), 0.4f);
        Arrow(mb, new Vector3(-Straight, 0f, -40f), Vector3.forward);
        Arrow(mb, new Vector3(-Straight, 0f, 60f), Vector3.forward);
        Arrow(mb, new Vector3(Straight, 0f, 74f), Vector3.back);
        Arrow(mb, new Vector3(Straight, 0f, -80f), Vector3.back);
        MeshKit.Spawn("Markings", transform, mb.ToMesh("Markings"), white, W(0f, 0.015f, 0f), transform.rotation, false);
    }

    static void Strip(MeshKit.Builder mb, Vector3 a, Vector3 b, float width)
    {
        Vector3 dir = b - a;
        if (dir.sqrMagnitude < 1e-4f) return;
        Vector3 r = Vector3.Cross(Vector3.up, dir.normalized) * width * 0.5f;
        mb.Quad(a - r, b - r, b + r, a + r, width, dir.magnitude, 1f);
    }

    static void Arrow(MeshKit.Builder mb, Vector3 at, Vector3 dir)
    {
        Vector3 r = Vector3.Cross(Vector3.up, dir);
        Strip(mb, at - dir * 2f, at + dir * 0.6f, 0.35f);
        mb.Tri(at + dir * 1.8f, at + dir * 0.4f + r * 0.8f, at + dir * 0.4f - r * 0.8f);
        mb.Tri(at + dir * 1.8f, at + dir * 0.4f - r * 0.8f, at + dir * 0.4f + r * 0.8f);
    }

    // ---------------------------------------------------------------- cones

    void Slalom()
    {
        // Gates on the right straight, alternately left and right of the lane centre.
        float[] zs = { 62f, 48f, 34f, 20f, 6f };
        for (int i = 0; i < zs.Length; i++)
        {
            float cx = Straight + (i % 2 == 0 ? 1.8f : -1.8f);
            var g = new Gate { centre = new Vector3(cx, 0f, zs[i]) };
            g.left = Cone(new Vector3(cx - 2.9f, 0f, zs[i]));
            g.right = Cone(new Vector3(cx + 2.9f, 0f, zs[i]));
            gates.Add(g);
        }
        // A few cone lines along the edges of the bends and at the stops.
        for (int i = 0; i < 6; i++) Cone(new Vector3(-Straight + LaneHalf + 0.6f, 0f, TopZ - 30f + i * 5f));
        for (int i = 0; i < 4; i++) Cone(new Vector3(Straight - 6f, 0f, -36f + i * 3f));
    }

    Rigidbody Cone(Vector3 local)
    {
        var go = MeshKit.Spawn("Cone", transform, MeshKit.Cone(0.22f, 0.7f, 8), coneMat, W(local.x, 0.02f, local.z), transform.rotation, true);
        var foot = MeshKit.Spawn("Cone Foot", go.transform, MeshKit.Box(new Vector3(0.5f, 0.04f, 0.5f), 1f), coneMat, go.transform.position, go.transform.rotation, false);
        foot.transform.localPosition = Vector3.zero;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.6f;
        cones.Add(rb);
        coneRest[rb] = go.transform.position;
        return rb;
    }

    void HighlightGates()
    {
        for (int i = 0; i < gates.Count; i++)
        {
            var mat = TrackGates && i == NextGate ? coneNextMat : coneMat;
            Paint(gates[i].left, mat);
            Paint(gates[i].right, mat);
        }
    }

    static void Paint(Rigidbody cone, Material mat)
    {
        if (cone == null) return;
        foreach (var r in cone.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
    }

    // ---------------------------------------------------------------- stops

    Stop BuildStop(string name, Vector3 laneCentre, Vector3 dir)
    {
        Vector3 r = Vector3.Cross(Vector3.up, dir);     // right of the driving direction: the kerb side
        var root = new GameObject("Yard Stop " + name).transform;
        root.SetParent(transform, false);
        Vector3 kerb = laneCentre + r * LaneHalf;
        Quaternion along = transform.rotation * Quaternion.LookRotation(dir);

        // Platform with a kerb.
        MeshKit.Spawn("Platform", root, MeshKit.Box(new Vector3(3.2f, 0.14f, 14f), 1f), road != null && road.concrete != null ? road.concrete : white,
                      transform.TransformPoint(kerb + r * 1.6f), along, false);

        // Shelter (model from the pack, or a simple one).
        Vector3 back = transform.TransformPoint(kerb + r * 2.6f + Vector3.up * 0.14f);
        Vector3 away = transform.TransformDirection(r);
        if (road == null || !road.BusStopModel(root, back, away))
        {
            kit.Solid(kerb + r * 3.1f + Vector3.up * 0.14f, new Vector3(0.1f, 2.2f, 3.6f), fence, true);
            kit.Solid(kerb + r * 2.5f + Vector3.up * 2.34f, new Vector3(1.4f, 0.1f, 4f), fence, false);
        }

        // "H" sign where the front door belongs.
        Vector3 pole = kerb + r * 0.4f + dir * 5f + Vector3.up * 0.14f;
        MeshKit.Spawn("Sign Pole", root, MeshKit.Prism(0.04f, 2.6f, 6, 1f), metal, transform.TransformPoint(pole), transform.rotation, true);
        var signMat = road != null && road.busStopSign != null ? road.busStopSign : coneNextMat;
        MeshKit.Spawn("Sign", root, MeshKit.Box(new Vector3(0.6f, 0.6f, 0.03f), 0.6f), signMat, transform.TransformPoint(pole + Vector3.up * 2.1f), transform.rotation * Quaternion.LookRotation(-dir), false);
        kit.Sign(kerb + r * 1.6f - dir * 6.5f + Vector3.up * 1.6f, -dir, 1.6f, 0.6f,
                 new[] { Loc.T("ÜBUNG", "PRACTICE"), Loc.T("HALT ", "STOP ") + name }, new Color32(250, 210, 40, 255), new Color32(20, 30, 20, 255), false);
        kit.Solid(kerb + r * 1.6f - dir * 6.5f, new Vector3(0.08f, 1.3f, 0.08f), fence, false);

        // A painted box on the road where the bus should stand.
        var mb = new MeshKit.Builder();
        Vector3 front = kerb - r * 1.4f + dir * 5.5f, rear = kerb - r * 1.4f - dir * 6.5f;
        Strip(mb, front - r * 1.4f, front + r * 1.4f, 0.15f);
        Strip(mb, rear - r * 1.4f, rear + r * 1.4f, 0.15f);
        Strip(mb, front - r * 1.4f, rear - r * 1.4f, 0.1f);
        MeshKit.Spawn("Stop Box", root, mb.ToMesh("Stop Box"), PropKit.Tinted(white, new Color(1f, 0.85f, 0.2f), "Stop Paint"), W(0f, 0.02f, 0f), transform.rotation, false);

        return new Stop
        {
            name = name,
            sign = transform.TransformPoint(pole),
            direction = transform.TransformDirection(dir),
            kerbOut = transform.TransformDirection(r),
        };
    }

    // ---------------------------------------------------------------- surroundings

    void Fence(float halfX, float maxZ, float minZ)
    {
        float h = 1.6f;
        for (float x = -halfX; x <= halfX; x += 5f)
        {
            kit.Solid(new Vector3(x, 0f, maxZ), new Vector3(0.12f, h, 0.12f), metal, false);
            kit.Solid(new Vector3(x, 0f, minZ), new Vector3(0.12f, h, 0.12f), metal, false);
        }
        for (float z = minZ; z <= maxZ; z += 5f)
        {
            kit.Solid(new Vector3(-halfX, 0f, z), new Vector3(0.12f, h, 0.12f), metal, false);
            kit.Solid(new Vector3(halfX, 0f, z), new Vector3(0.12f, h, 0.12f), metal, false);
        }
        float w = halfX * 2f, l = maxZ - minZ, cz = (maxZ + minZ) * 0.5f;
        foreach (float y in new[] { 0.5f, 1.4f })
        {
            kit.Solid(new Vector3(0f, y, maxZ), new Vector3(w, 0.06f, 0.05f), fence, false);
            kit.Solid(new Vector3(0f, y, minZ), new Vector3(w, 0.06f, 0.05f), fence, false);
            kit.Solid(new Vector3(-halfX, y, cz), new Vector3(0.05f, 0.06f, l), fence, false);
            kit.Solid(new Vector3(halfX, y, cz), new Vector3(0.05f, 0.06f, l), fence, false);
        }
        // One solid wall each side so the bus can't leave the yard.
        kit.Block(new Vector3(0f, 0f, maxZ), new Vector3(w, 3f, 0.4f));
        kit.Block(new Vector3(0f, 0f, minZ), new Vector3(w, 3f, 0.4f));
        kit.Block(new Vector3(-halfX, 0f, cz), new Vector3(0.4f, 3f, l));
        kit.Block(new Vector3(halfX, 0f, cz), new Vector3(0.4f, 3f, l));
    }

    void Office()
    {
        var walls = PropKit.Tinted(white, new Color(0.85f, 0.82f, 0.72f), "Office Walls");
        var roof = PropKit.Tinted(white, new Color(0.5f, 0.22f, 0.18f), "Office Roof");
        Vector3 at = new Vector3(-52f, 0f, -132f);
        kit.Solid(at, new Vector3(8f, 3.2f, 6f), walls, true);
        kit.Solid(at + Vector3.up * 3.2f, new Vector3(8.6f, 0.3f, 6.6f), roof, false);
        kit.Solid(at + new Vector3(4.02f, 0f, 1f), new Vector3(0.05f, 2.1f, 1f), fence, false);
        kit.Sign(at + new Vector3(4.1f, 2.6f, -0.8f), Vector3.right, 3.6f, 0.8f,
                 new[] { Loc.T("FAHRSCHULE", "DRIVING SCHOOL"), Loc.T("VERKEHRSÜBUNGSPLATZ", "PRACTICE GROUND") }, new Color32(30, 60, 140, 255), new Color32(240, 240, 240, 255), false);
        // A few parked traffic cones and a barrier by the office.
        for (int i = 0; i < 3; i++) Cone(new Vector3(-48f + i * 0.6f, 0f, -124f));
        kit.Solid(new Vector3(-40f, 0f, -140f), new Vector3(3f, 0.9f, 0.3f), coneMat, true);
    }

    // ---------------------------------------------------------------- tracking

    void Update()
    {
        // Knocked over or pushed away cones.
        foreach (var c in cones)
        {
            if (c == null || knocked.Contains(c)) continue;
            if (Vector3.Distance(c.position, coneRest[c]) > 0.35f || Vector3.Dot(c.transform.up, transform.up) < 0.8f)
            {
                knocked.Add(c);
                ConesHit++;
            }
        }

        if (!TrackGates || bus == null || SlalomDone) return;
        Vector3 p = transform.InverseTransformPoint(bus.transform.position);
        if (Mathf.Abs(p.x - Straight) > 12f) return;    // only on the right straight
        var g = gates[NextGate];
        if (Mathf.Abs(p.z - g.centre.z) < 1.5f && Mathf.Abs(p.x - g.centre.x) < 2.9f)
        {
            NextGate++;
            HighlightGates();
        }
        else if (p.z < g.centre.z - 2f && Vector3.Dot(bus.transform.forward, transform.forward) < -0.5f)
        {
            MissedGates++;
            NextGate++;
            HighlightGates();
        }
    }

    /// <summary>Start the slalom again (from the first gate).</summary>
    public void ResetSlalom()
    {
        NextGate = 0;
        MissedGates = 0;
        HighlightGates();
    }

    /// <summary>Distance of a point to a stop: along the platform and across (from the sign).</summary>
    public static void Measure(Stop stop, Vector3 point, out float along, out float across)
    {
        Vector3 d = stop.sign - point;
        d.y = 0f;
        along = Vector3.Dot(d, stop.direction);
        across = Mathf.Abs(Vector3.Dot(d, stop.kerbOut));
    }
}
