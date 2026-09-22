using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a small PSX town at runtime: a closed bus route (rounded rectangle, left-hand
/// traffic, stops on the left), sidewalks with curbs, houses, trees, street lamps and bus stops.
/// The route starts at this object's position heading +Z in the left lane.
/// In edit mode the route and the stops are drawn as gizmos.
/// </summary>
public class TownBuilder : MonoBehaviour
{
    [Header("Route")]
    public float straightLength = 360f;
    public float loopWidth = 200f;
    public float cornerRadius = 45f;
    public float laneWidth = 3.5f;
    public float sidewalkWidth = 3f;
    public float sidewalkHeight = 0.1f;
    [Tooltip("Distance between road samples in metres")]
    public float sampleSpacing = 3f;

    [Header("Bus stops")]
    public string[] busStopNames =
    {
        "Lindenplatz", "Alte Mühle", "Friedhofstraße", "Bahnhof Nord", "Am Wasserturm", "Klinikum", "Rosenweg",
    };
    public int busStopCount = 5;
    [Tooltip("Distance from the start to the first stop in metres")]
    public float firstStopDistance = 110f;

    [Header("Scenery")]
    public int seed = 1998;
    public int parkTrees = 70;

    [Header("Materials")]
    public Material road;
    public Material sidewalk;
    public Material[] facades;
    public Material concrete;
    public Material metal;
    public Material leaves;
    public Material bark;
    public Material busStopSign;

    public IReadOnlyList<BusStop> Stops => stops;
    public float PathLength => pathLength;

    readonly List<BusStop> stops = new List<BusStop>();
    Vector3[] points;
    Vector3[] tangents;
    bool[] onCurve;
    float[] distances;
    float pathLength;
    Transform root;
    System.Random rng;

    float HalfRoad => laneWidth;

    void Awake()
    {
        Build();
    }

    public void Build()
    {
        GeneratePath();
        rng = new System.Random(seed);

        root = new GameObject("Town (generated)").transform;
        root.SetParent(transform, false);

        BuildRoad();
        var stopDistances = PlaceStops();
        BuildBuildings(stopDistances);
        BuildTreesAndLamps(stopDistances);
    }

    // ---------------------------------------------------------------- path

    void GeneratePath()
    {
        var pts = new List<Vector3>();
        var curve = new List<bool>();
        float x0 = laneWidth * 0.5f;       // centre line, so the left lane is at x = 0
        float x1 = x0 + loopWidth;
        float z0 = -straightLength * 0.5f, z1 = straightLength * 0.5f;
        float r = Mathf.Min(cornerRadius, loopWidth * 0.5f - 1f, straightLength * 0.5f - 1f);

        // Clockwise seen from above: up the left side, right along the top, down, left along the bottom.
        AddLine(pts, curve, new Vector3(x0, 0, z0 + r), new Vector3(x0, 0, z1 - r));
        AddArc(pts, curve, new Vector3(x0 + r, 0, z1 - r), r, 180f, 90f);
        AddLine(pts, curve, new Vector3(x0 + r, 0, z1), new Vector3(x1 - r, 0, z1));
        AddArc(pts, curve, new Vector3(x1 - r, 0, z1 - r), r, 90f, 0f);
        AddLine(pts, curve, new Vector3(x1, 0, z1 - r), new Vector3(x1, 0, z0 + r));
        AddArc(pts, curve, new Vector3(x1 - r, 0, z0 + r), r, 0f, -90f);
        AddLine(pts, curve, new Vector3(x1 - r, 0, z0), new Vector3(x0 + r, 0, z0));
        AddArc(pts, curve, new Vector3(x0 + r, 0, z0 + r), r, -90f, -180f);

        // Start the route next to the bus (this object's origin).
        int start = 0;
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = (pts[i] - new Vector3(x0, 0, 0)).sqrMagnitude;
            if (d < best) { best = d; start = i; }
        }

        int n = pts.Count;
        points = new Vector3[n];
        onCurve = new bool[n];
        for (int i = 0; i < n; i++)
        {
            points[i] = transform.TransformPoint(pts[(start + i) % n]);
            onCurve[i] = curve[(start + i) % n];
        }

        tangents = new Vector3[n];
        distances = new float[n + 1];
        for (int i = 0; i < n; i++)
        {
            tangents[i] = (points[(i + 1) % n] - points[(i - 1 + n) % n]).normalized;
            distances[i + 1] = distances[i] + Vector3.Distance(points[i], points[(i + 1) % n]);
        }
        pathLength = distances[n];
    }

    void AddLine(List<Vector3> pts, List<bool> curve, Vector3 a, Vector3 b)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / sampleSpacing));
        for (int i = 0; i < steps; i++)
        {
            pts.Add(Vector3.Lerp(a, b, i / (float)steps));
            curve.Add(false);
        }
    }

    void AddArc(List<Vector3> pts, List<bool> curve, Vector3 centre, float radius, float fromDeg, float toDeg)
    {
        float length = Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad * radius;
        int steps = Mathf.Max(2, Mathf.RoundToInt(length / sampleSpacing));
        for (int i = 0; i < steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
            pts.Add(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius);
            curve.Add(true);
        }
    }

    /// <summary>Position and direction on the route centre line at arc length s.</summary>
    public void Sample(float s, out Vector3 position, out Vector3 tangent, out bool curve)
    {
        s = Mathf.Repeat(s, pathLength);
        int n = points.Length;
        int i = System.Array.BinarySearch(distances, s);
        if (i < 0) i = ~i - 1;
        i = Mathf.Clamp(i, 0, n - 1);
        float segment = distances[i + 1] - distances[i];
        float t = segment > 0f ? (s - distances[i]) / segment : 0f;
        position = Vector3.Lerp(points[i], points[(i + 1) % n], t);
        tangent = Vector3.Slerp(tangents[i], tangents[(i + 1) % n], t).normalized;
        curve = onCurve[i] || onCurve[(i + 1) % n];
    }

    /// <summary>Arc length of the route point closest to a world position.</summary>
    public float ArcLengthAt(Vector3 worldPosition)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i] - worldPosition).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return distances[best];
    }

    /// <summary>Distance you still have to drive from 'from' to 'to' (both arc lengths).</summary>
    public float DistanceAhead(float from, float to) => Mathf.Repeat(to - from, pathLength);

    Vector3 Right(Vector3 tangent) => Vector3.Cross(Vector3.up, tangent).normalized;

    // ---------------------------------------------------------------- road

    void BuildRoad()
    {
        float roadY = 0.03f;
        float curbY = roadY + sidewalkHeight;
        float outer = HalfRoad + sidewalkWidth;

        var roadMesh = Strip(-HalfRoad, HalfRoad, roadY, roadY, 2f * HalfRoad, 8f);
        MeshKit.Spawn("Road", root, roadMesh, road, Vector3.zero, Quaternion.identity, false);

        var walk = new MeshKit.Builder();
        AddStrip(walk, -outer, -HalfRoad, curbY, curbY, 2f, 2f);   // left (outer) sidewalk
        AddStrip(walk, HalfRoad, outer, curbY, curbY, 2f, 2f);     // right (inner) sidewalk
        AddCurb(walk, -HalfRoad, roadY, curbY, true);
        AddCurb(walk, HalfRoad, roadY, curbY, false);
        MeshKit.Spawn("Sidewalks", root, walk.ToMesh("Sidewalks"), sidewalk, Vector3.zero, Quaternion.identity, false);
    }

    Mesh Strip(float offsetA, float offsetB, float yA, float yB, float uTile, float vTile)
    {
        var mb = new MeshKit.Builder();
        AddStrip(mb, offsetA, offsetB, yA, yB, uTile, vTile);
        return mb.ToMesh("Strip");
    }

    // Quads between two lines parallel to the route (offsets along the route's right vector).
    void AddStrip(MeshKit.Builder mb, float offsetA, float offsetB, float yA, float yB, float uTile, float vTile)
    {
        int n = points.Length;
        float width = (offsetB - offsetA) / uTile;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 ri = Right(tangents[i]), rj = Right(tangents[j]);
            Vector3 a = points[i] + ri * offsetA + Vector3.up * yA;
            Vector3 b = points[j] + rj * offsetA + Vector3.up * yA;
            Vector3 c = points[j] + rj * offsetB + Vector3.up * yB;
            Vector3 d = points[i] + ri * offsetB + Vector3.up * yB;
            float v0 = distances[i] / vTile, v1 = distances[i + 1] / vTile;
            mb.Quad(a, b, c, d, new Vector2(0, v0), new Vector2(0, v1), new Vector2(width, v1), new Vector2(width, v0));
        }
    }

    void AddCurb(MeshKit.Builder mb, float offset, float bottom, float top, bool facesRight)
    {
        int n = points.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 pi = points[i] + Right(tangents[i]) * offset;
            Vector3 pj = points[j] + Right(tangents[j]) * offset;
            float v0 = distances[i] / 2f, v1 = distances[i + 1] / 2f;
            float h = (top - bottom) / 2f;
            if (facesRight)
                mb.Quad(pi + Vector3.up * bottom, pi + Vector3.up * top, pj + Vector3.up * top, pj + Vector3.up * bottom,
                        new Vector2(v0, 0), new Vector2(v0, h), new Vector2(v1, h), new Vector2(v1, 0));
            else
                mb.Quad(pj + Vector3.up * bottom, pj + Vector3.up * top, pi + Vector3.up * top, pi + Vector3.up * bottom,
                        new Vector2(v1, 0), new Vector2(v1, h), new Vector2(v0, h), new Vector2(v0, 0));
        }
    }

    // ---------------------------------------------------------------- bus stops

    List<float> PlaceStops()
    {
        var result = new List<float>();
        int count = Mathf.Max(0, busStopCount);
        for (int k = 0; k < count; k++)
        {
            float s = firstStopDistance + k * pathLength / count;
            // Stops only on straights, with some room to the next curve.
            for (int guard = 0; guard < 200 && (OnCurveNear(s, 20f)); guard++) s += sampleSpacing;
            result.Add(Mathf.Repeat(s, pathLength));

            string stopName = busStopNames.Length > 0 ? busStopNames[k % busStopNames.Length] : "Haltestelle " + (k + 1);
            BuildBusStop(stopName, Mathf.Repeat(s, pathLength));
        }
        return result;
    }

    bool OnCurveNear(float s, float margin)
    {
        for (float d = -margin; d <= margin; d += sampleSpacing)
        {
            Sample(s + d, out _, out _, out bool curve);
            if (curve) return true;
        }
        return false;
    }

    void BuildBusStop(string stopName, float s)
    {
        Sample(s, out Vector3 p, out Vector3 t, out _);
        Vector3 left = -Right(t);
        float curbY = 0.03f + sidewalkHeight;
        Quaternion alongRoad = Quaternion.LookRotation(t);

        var stopRoot = new GameObject("BusStop " + stopName).transform;
        stopRoot.SetParent(root, false);
        stopRoot.SetPositionAndRotation(p + left * (HalfRoad + 1.1f) + Vector3.up * curbY, Quaternion.LookRotation(-left));

        // Shelter at the back of the sidewalk.
        Vector3 back = p + left * (HalfRoad + sidewalkWidth - 0.35f) + Vector3.up * curbY;
        MeshKit.Spawn("Shelter Wall", stopRoot, MeshKit.Box(new Vector3(0.08f, 2.3f, 4f), 1f), metal, back, alongRoad, true);
        MeshKit.Spawn("Shelter Roof", stopRoot, MeshKit.Box(new Vector3(1.6f, 0.08f, 4.4f), 1f), metal,
                      back - left * 0.7f + Vector3.up * 2.3f, alongRoad, true);
        MeshKit.Spawn("Shelter Side", stopRoot, MeshKit.Box(new Vector3(1.2f, 2.3f, 0.06f), 1f), metal,
                      back - left * 0.6f + t * 2f, alongRoad, true);
        MeshKit.Spawn("Bench", stopRoot, MeshKit.Box(new Vector3(0.45f, 0.45f, 2f), 1f), concrete,
                      back - left * 0.35f, alongRoad, true);

        // "H" sign facing the oncoming bus.
        Vector3 pole = p + left * (HalfRoad + 0.35f) + t * 4f + Vector3.up * curbY;
        MeshKit.Spawn("Sign Pole", stopRoot, MeshKit.Prism(0.04f, 2.6f, 6, 1f), metal, pole, Quaternion.identity, true);
        MeshKit.Spawn("Sign", stopRoot, MeshKit.Box(new Vector3(0.6f, 0.6f, 0.03f), 0.6f), busStopSign,
                      pole + Vector3.up * 2.2f, Quaternion.LookRotation(-t), false);

        var waitPoint = new GameObject("WaitPoint").transform;
        waitPoint.SetParent(stopRoot, false);
        waitPoint.SetPositionAndRotation(p + left * (HalfRoad + 1.3f) + Vector3.up * curbY, Quaternion.LookRotation(-left));

        var stop = stopRoot.gameObject.AddComponent<BusStop>();
        stop.stopName = stopName;
        stop.waitPoint = waitPoint;
        stop.arcLength = s;
        stop.roadDirection = t;
        stops.Add(stop);
    }

    // ---------------------------------------------------------------- scenery

    bool NearStop(float s, List<float> stopDistances, float margin)
    {
        foreach (float d in stopDistances)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(s / pathLength * 360f, d / pathLength * 360f)) / 360f * pathLength;
            if (delta < margin) return true;
        }
        return false;
    }

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    void BuildBuildings(List<float> stopDistances)
    {
        if (facades == null || facades.Length == 0) return;
        float edge = HalfRoad + sidewalkWidth;

        // Two rows of houses on the outer (left) side of the route.
        for (int row = 0; row < 2; row++)
        {
            float s = Range(0f, 10f);
            while (s < pathLength)
            {
                float width = Range(8f, 18f);
                float depth = Range(8f, 14f);
                Sample(s + width * 0.5f, out Vector3 p, out Vector3 t, out bool curve);
                if (!curve && !OnCurveNear(s + width * 0.5f, width) && rng.NextDouble() > 0.15)
                {
                    float floors = row == 0 ? Mathf.Round(Range(2f, 5f)) : Mathf.Round(Range(4f, 8f));
                    float setback = row == 0 ? Range(3f, 7f) : Range(22f, 30f);
                    Vector3 pos = p - Right(t) * (edge + setback + depth * 0.5f);
                    var mat = facades[rng.Next(facades.Length)];
                    MeshKit.Spawn("House", root, MeshKit.Box(new Vector3(depth, floors * 3f, width), 3f), mat,
                                  pos, Quaternion.LookRotation(t), true);
                }
                s += width + Range(2f, 8f);
            }
        }

        // A few small houses on the inner side, away from the stops.
        float si = Range(20f, 40f);
        while (si < pathLength)
        {
            Sample(si, out Vector3 p, out Vector3 t, out bool curve);
            if (!curve && !OnCurveNear(si, 15f) && !NearStop(si, stopDistances, 25f))
            {
                float width = Range(8f, 12f), depth = Range(7f, 10f);
                Vector3 pos = p + Right(t) * (edge + Range(6f, 10f) + depth * 0.5f);
                MeshKit.Spawn("House", root, MeshKit.Box(new Vector3(depth, Mathf.Round(Range(1f, 3f)) * 3f, width), 3f),
                              facades[rng.Next(facades.Length)], pos, Quaternion.LookRotation(t), true);
            }
            si += Range(45f, 90f);
        }
    }

    void BuildTreesAndLamps(List<float> stopDistances)
    {
        float edge = HalfRoad + sidewalkWidth;
        float curbY = 0.03f + sidewalkHeight;

        // Street lamps on the outer sidewalk.
        var lampPole = MeshKit.Prism(0.06f, 5f, 6, 1f);
        var lampArm = MeshKit.Box(new Vector3(0.1f, 0.1f, 1.2f), 1f);
        for (float s = 5f; s < pathLength; s += 28f)
        {
            if (NearStop(s, stopDistances, 8f)) continue;
            Sample(s, out Vector3 p, out Vector3 t, out _);
            Vector3 r = Right(t);
            Vector3 pos = p - r * (edge - 0.4f) + Vector3.up * curbY;
            MeshKit.Spawn("Lamp", root, lampPole, metal, pos, Quaternion.identity, true);
            MeshKit.Spawn("Lamp Arm", root, lampArm, metal, pos + Vector3.up * 4.9f + r * 0.5f, Quaternion.LookRotation(r), false);
        }

        // Tree line on the inner side.
        for (float s = 8f; s < pathLength; s += Range(9f, 16f))
        {
            if (NearStop(s, stopDistances, 10f)) continue;
            Sample(s, out Vector3 p, out Vector3 t, out _);
            SpawnTree(p + Right(t) * (edge + Range(1.5f, 4f)));
        }

        // Park inside the loop.
        Vector3 centre = transform.TransformPoint(new Vector3(laneWidth * 0.5f + loopWidth * 0.5f, 0, 0));
        float hx = loopWidth * 0.5f - edge - 25f, hz = straightLength * 0.5f - edge - 25f;
        for (int i = 0; i < parkTrees; i++)
        {
            var local = new Vector3(Range(-hx, hx), 0, Range(-hz, hz));
            SpawnTree(centre + transform.rotation * local);
        }
    }

    void SpawnTree(Vector3 pos)
    {
        float height = Range(3f, 5.5f);
        var tree = MeshKit.Spawn("Tree", root, MeshKit.Prism(0.18f, height * 0.5f, 5, 1f), bark,
                                 pos, Quaternion.Euler(0, Range(0f, 360f), 0), true);
        MeshKit.Spawn("Leaves", tree.transform, MeshKit.Blob(Range(1.3f, 2.2f), height * 0.75f), leaves,
                      pos + Vector3.up * height * 0.35f, tree.transform.rotation, false);
    }

    // ---------------------------------------------------------------- editor preview

    void OnDrawGizmos()
    {
        if (Application.isPlaying && points != null) return;
        GeneratePath();
        Gizmos.color = Color.yellow;
        for (int i = 0; i < points.Length; i++)
            Gizmos.DrawLine(points[i], points[(i + 1) % points.Length]);

        Gizmos.color = Color.green;
        int count = Mathf.Max(0, busStopCount);
        for (int k = 0; k < count; k++)
        {
            float s = firstStopDistance + k * pathLength / count;
            for (int guard = 0; guard < 200 && OnCurveNear(s, 20f); guard++) s += sampleSpacing;
            Sample(s, out Vector3 p, out Vector3 t, out _);
            Gizmos.DrawWireCube(p - Right(t) * (HalfRoad + 1.3f) + Vector3.up, new Vector3(1.5f, 2f, 1.5f));
        }
    }
}
