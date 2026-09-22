using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless two-lane country road through a dark forest (right-hand traffic).
/// The road is generated in chunks ahead of the bus and removed behind it:
/// long straights, now and then a gentle curve, and every few hundred metres a bus stop
/// on the right. A forest floor with a collider follows the bus.
/// The road starts at this object's origin with the bus in the right lane heading +Z.
/// </summary>
public class ForestRoad : MonoBehaviour
{
    [Header("Road")]
    public float laneWidth = 3.25f;
    public float shoulderWidth = 1.5f;
    public float sampleSpacing = 2f;
    public int samplesPerChunk = 20;
    public float generateAhead = 260f;
    public float keepBehind = 120f;

    [Header("Layout")]
    public Vector2 straightLength = new Vector2(180f, 480f);
    public Vector2 curveAngle = new Vector2(12f, 50f);
    public Vector2 curveRadius = new Vector2(110f, 240f);
    public Vector2 stopSpacing = new Vector2(380f, 650f);
    public float firstStopDistance = 160f;
    public string[] stopNames =
    {
        "Waldfriedhof", "Forsthaus Eichgrund", "Alte Sägemühle", "Am Moor", "Schwarzer Weiher",
        "Köhlerhütte", "Wolfsschlucht", "Kreuzweg", "Hünengrab", "Birkenhain", "Steinbruch", "Totenweg",
    };

    [Header("Forest")]
    public int seed = 666;
    [Tooltip("Trees per chunk and side")]
    public int treesPerSide = 26;
    public float forestDepth = 55f;

    [Header("Materials")]
    public Material road;
    public Material shoulder;
    public Material forestFloor;
    public Material bark;
    public Material needles;
    public Material wood;
    public Material concrete;
    public Material metal;
    public Material busStopSign;
    public Material guidePost;

    [Header("Night")]
    [Tooltip("Optional: lamp at every bus stop")]
    public bool stopLamps = true;
    public Color lampColor = new Color(1f, 0.68f, 0.3f);
    [Tooltip("Weak on purpose: just enough to notice the stop in the dark")]
    public float lampIntensity = 3f;
    public float lampRange = 8f;

    public IReadOnlyList<BusStop> Stops => stops;

    // Samples of the centre line (world space).
    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> tangents = new List<Vector3>();
    readonly List<float> distances = new List<float>();
    readonly List<bool> onCurve = new List<bool>();
    int firstSampleIndex;           // global index of points[0]

    readonly List<BusStop> stops = new List<BusStop>();
    readonly Queue<(GameObject go, float endDistance)> chunks = new Queue<(GameObject, float)>();
    int builtUpTo;                  // global sample index up to which chunks exist

    // Generator state.
    System.Random rng;
    Vector3 genPos;
    float genHeading;               // radians, 0 = +Z, positive = turning right
    float segmentLeft;
    float segmentTurnRate;          // radians per metre (0 = straight)
    float nextStopAt;

    Transform bus;
    int nearestHint;
    Transform ground;
    Mesh trunkMesh, coneMesh, postMesh;

    float HalfRoad => laneWidth;

    void Awake()
    {
        rng = new System.Random(seed);
        genPos = transform.TransformPoint(new Vector3(-laneWidth * 0.5f, 0f, -60f));
        genHeading = Mathf.Atan2(transform.forward.x, transform.forward.z);
        segmentLeft = 260f;
        segmentTurnRate = 0f;
        nextStopAt = 60f + firstStopDistance;

        trunkMesh = MeshKit.Prism(0.22f, 3f, 5, 1f);
        coneMesh = MeshKit.Cone(1f, 1f, 7);
        postMesh = MeshKit.Box(new Vector3(0.12f, 1f, 0.12f), 1f);

        var bc = FindAnyObjectByType<BusController>();
        bus = bc != null ? bc.transform : null;

        BuildGround();
        UpdateRoad();
    }

    void Update() => UpdateRoad();

    // ---------------------------------------------------------------- public API

    /// <summary>Distance along the road of the point closest to a world position.</summary>
    public float ArcLengthAt(Vector3 world)
    {
        if (points.Count == 0) return 0f;
        int best = Mathf.Clamp(nearestHint, 0, points.Count - 1);
        float bestD = (points[best] - world).sqrMagnitude;
        // Local search around the last result (the bus moves continuously).
        for (int step = 0; step < 2; step++)
        {
            int dir = step == 0 ? 1 : -1;
            for (int i = best + dir; i >= 0 && i < points.Count; i += dir)
            {
                float d = (points[i] - world).sqrMagnitude;
                if (d > bestD) break;
                bestD = d;
                best = i;
            }
        }
        nearestHint = best;
        return distances[best];
    }

    public float BusArcLength => bus != null ? ArcLengthAt(bus.position) : 0f;

    /// <summary>Distance from the centre line to the forest edge.</summary>
    public float EdgeOffset => HalfRoad + shoulderWidth;

    /// <summary>Point and direction on the centre line at distance s (only where the road exists).</summary>
    public bool TrySample(float s, out Vector3 position, out Vector3 tangent)
    {
        position = tangent = Vector3.zero;
        if (points.Count < 2 || s < distances[0] || s >= distances[distances.Count - 1]) return false;
        int i = Mathf.Clamp((int)((s - distances[0]) / sampleSpacing), 0, points.Count - 2);
        float t = Mathf.Clamp01((s - distances[i]) / sampleSpacing);
        position = Vector3.Lerp(points[i], points[i + 1], t);
        tangent = Vector3.Slerp(tangents[i], tangents[i + 1], t).normalized;
        return true;
    }

    // ---------------------------------------------------------------- generation

    void UpdateRoad()
    {
        float busS = BusArcLength;

        // Grow the centre line and build chunks up to generateAhead.
        while (points.Count == 0 || distances[distances.Count - 1] < busS + generateAhead + sampleSpacing * samplesPerChunk)
            AddSample();
        while (builtUpTo + samplesPerChunk < firstSampleIndex + points.Count - 1 &&
               distances[builtUpTo - firstSampleIndex] < busS + generateAhead)
            BuildChunk(builtUpTo, builtUpTo + samplesPerChunk);

        // Remove chunks far behind.
        while (chunks.Count > 0 && chunks.Peek().endDistance < busS - keepBehind)
        {
            var c = chunks.Dequeue();
            Destroy(c.go);
        }
        stops.RemoveAll(s => s == null);

        // Drop samples nobody needs anymore (far behind, and not needed by an unbuilt chunk).
        int keepFrom = 0;
        while (keepFrom < points.Count - 1 &&
               distances[keepFrom] < busS - keepBehind - 20f &&
               firstSampleIndex + keepFrom < builtUpTo - samplesPerChunk)
            keepFrom++;
        if (keepFrom > 50)
        {
            points.RemoveRange(0, keepFrom);
            tangents.RemoveRange(0, keepFrom);
            distances.RemoveRange(0, keepFrom);
            onCurve.RemoveRange(0, keepFrom);
            firstSampleIndex += keepFrom;
            nearestHint = Mathf.Max(0, nearestHint - keepFrom);
        }

        FollowGround();
    }

    void AddSample()
    {
        if (points.Count > 0)
        {
            genHeading += segmentTurnRate * sampleSpacing;
            genPos += new Vector3(Mathf.Sin(genHeading), 0f, Mathf.Cos(genHeading)) * sampleSpacing;
        }
        segmentLeft -= sampleSpacing;
        if (segmentLeft <= 0f) NextSegment();

        float s = points.Count == 0 ? 0f : distances[distances.Count - 1] + sampleSpacing;
        points.Add(genPos);
        tangents.Add(new Vector3(Mathf.Sin(genHeading), 0f, Mathf.Cos(genHeading)));
        distances.Add(s);
        onCurve.Add(segmentTurnRate != 0f);
    }

    void NextSegment()
    {
        if (segmentTurnRate != 0f || rng.NextDouble() < 0.25)
        {
            // Straight.
            segmentTurnRate = 0f;
            segmentLeft = Range(straightLength);
        }
        else
        {
            float angle = Range(curveAngle) * Mathf.Deg2Rad * (rng.NextDouble() < 0.5 ? -1f : 1f);
            float radius = Range(curveRadius);
            segmentLeft = Mathf.Abs(angle) * radius;
            segmentTurnRate = angle / segmentLeft;
        }
    }

    float Range(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);
    float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static Vector3 Right(Vector3 t) => Vector3.Cross(Vector3.up, t).normalized;

    void BuildChunk(int from, int to)
    {
        int a = from - firstSampleIndex, b = to - firstSampleIndex;
        var chunk = new GameObject($"Road Chunk {from}");
        chunk.transform.SetParent(transform, false);
        chunk.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Road and gravel shoulders (subdivided across so headlights light them nicely).
        var roadMb = new MeshKit.Builder();
        for (int k = 0; k < 4; k++)
            Strip(roadMb, a, b, -HalfRoad + k * HalfRoad * 0.5f, -HalfRoad + (k + 1) * HalfRoad * 0.5f, 0.02f, 2f * HalfRoad, 8f, (k * 0.25f));
        MeshKit.Spawn("Road", chunk.transform, roadMb.ToMesh("Road"), road, Vector3.zero, Quaternion.identity, false);

        var shoulderMb = new MeshKit.Builder();
        Strip(shoulderMb, a, b, -HalfRoad - shoulderWidth, -HalfRoad, 0.015f, 2f, 2f, 0f);
        Strip(shoulderMb, a, b, HalfRoad, HalfRoad + shoulderWidth, 0.015f, 2f, 2f, 0f);
        MeshKit.Spawn("Shoulder", chunk.transform, shoulderMb.ToMesh("Shoulder"), shoulder, Vector3.zero, Quaternion.identity, false);

        BuildForest(chunk.transform, a, b);
        BuildGuidePosts(chunk.transform, a, b);

        // Bus stop(s) in this chunk.
        for (int i = a; i < b; i++)
        {
            if (distances[i] < nextStopAt) continue;
            if (onCurve[i] || (i + 6 < onCurve.Count && onCurve[i + 6]) || (i >= 6 && onCurve[i - 6])) { nextStopAt += sampleSpacing; continue; }
            BuildBusStop(chunk.transform, i);
            nextStopAt = distances[i] + Range(stopSpacing);
        }

        chunks.Enqueue((chunk, distances[b]));
        builtUpTo = to;
    }

    void Strip(MeshKit.Builder mb, int a, int b, float offsetA, float offsetB, float y, float uTile, float vTile, float uOffset)
    {
        float width = (offsetB - offsetA) / uTile;
        for (int i = a; i < b; i++)
        {
            Vector3 ri = Right(tangents[i]), rj = Right(tangents[i + 1]);
            Vector3 p0 = points[i] + Vector3.up * y, p1 = points[i + 1] + Vector3.up * y;
            float v0 = distances[i] / vTile, v1 = distances[i + 1] / vTile;
            mb.Quad(p0 + ri * offsetA, p1 + rj * offsetA, p1 + rj * offsetB, p0 + ri * offsetB,
                    new Vector2(uOffset, v0), new Vector2(uOffset, v1), new Vector2(uOffset + width, v1), new Vector2(uOffset + width, v0));
        }
    }

    void BuildForest(Transform parent, int a, int b)
    {
        var trunks = new MeshKit.Builder();
        var crowns = new MeshKit.Builder();
        float edge = HalfRoad + shoulderWidth;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int n = 0; n < treesPerSide; n++)
            {
                int i = rng.Next(a, b);
                float along = Range(0f, sampleSpacing);
                // More trees close to the road, thinning out into the dark.
                float t = (float)rng.NextDouble();
                float offset = edge + 2.5f + t * t * forestDepth;
                Vector3 pos = points[i] + tangents[i] * along + Right(tangents[i]) * offset * side;
                if (TooCloseToRoad(pos, i, edge + 2f)) continue;

                float h = Range(7f, 15f);
                float w = h * Range(0.22f, 0.3f);
                var rot = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                trunks.AddMesh(trunkMesh, pos, rot, new Vector3(1f, h * 0.12f, 1f));
                int layers = 3 + rng.Next(2);
                for (int l = 0; l < layers; l++)
                {
                    float f = l / (float)layers;
                    float r = w * (1f - f * 0.75f);
                    crowns.AddMesh(coneMesh, pos + Vector3.up * (h * 0.22f + f * h * 0.62f), rot, new Vector3(r, h * 0.36f, r));
                }

                // Trees right next to the road block the bus.
                if (offset < edge + 12f)
                {
                    var col = new GameObject("Tree Collider").AddComponent<CapsuleCollider>();
                    col.transform.SetParent(parent, false);
                    col.transform.position = pos + Vector3.up * 2f;
                    col.radius = 0.3f;
                    col.height = 4f;
                }
            }
        }
        MeshKit.Spawn("Trunks", parent, trunks.ToMesh("Trunks"), bark, Vector3.zero, Quaternion.identity, false);
        MeshKit.Spawn("Crowns", parent, crowns.ToMesh("Crowns"), needles, Vector3.zero, Quaternion.identity, false);
    }

    bool TooCloseToRoad(Vector3 pos, int around, float minDistance)
    {
        // Curves can bring other parts of the road close; check neighbouring samples.
        int from = Mathf.Max(0, around - 40), to = Mathf.Min(points.Count - 1, around + 40);
        float min2 = minDistance * minDistance;
        for (int i = from; i <= to; i += 2)
        {
            Vector3 d = points[i] - pos;
            d.y = 0f;
            if (d.sqrMagnitude < min2) return true;
        }
        return false;
    }

    // German "Leitpfosten": white posts with a black band every 50 m on both sides.
    void BuildGuidePosts(Transform parent, int a, int b)
    {
        if (guidePost == null) return;
        var posts = new MeshKit.Builder();
        for (int i = a; i < b; i++)
        {
            float s0 = distances[i], s1 = distances[i + 1];
            if (Mathf.Floor(s1 / 50f) == Mathf.Floor(s0 / 50f)) continue;
            Vector3 r = Right(tangents[i]);
            var rot = Quaternion.LookRotation(tangents[i]);
            posts.AddMesh(postMesh, points[i] + r * (HalfRoad + shoulderWidth - 0.2f), rot, Vector3.one);
            posts.AddMesh(postMesh, points[i] - r * (HalfRoad + shoulderWidth - 0.2f), rot, Vector3.one);
        }
        if (posts.VertexCount > 0)
            MeshKit.Spawn("Guide Posts", parent, posts.ToMesh("Guide Posts"), guidePost, Vector3.zero, Quaternion.identity, false);
    }

    void BuildBusStop(Transform parent, int i)
    {
        Vector3 p = points[i], t = tangents[i], r = Right(t);
        Quaternion along = Quaternion.LookRotation(t);
        string stopName = stopNames.Length > 0 ? stopNames[stops.Count % stopNames.Length] : "Haltestelle";
        if (stops.Count >= stopNames.Length) stopName = stopNames[rng.Next(stopNames.Length)];

        var root = new GameObject("BusStop " + stopName).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(p + r * (HalfRoad + shoulderWidth + 1.2f), Quaternion.LookRotation(-r));

        float padY = 0.12f;
        Vector3 padCentre = p + r * (HalfRoad + shoulderWidth + 1.6f);
        MeshKit.Spawn("Platform", root, MeshKit.Box(new Vector3(3.2f, padY, 10f), 1f), concrete, padCentre, along, false);

        // Wooden shelter at the back of the platform, open towards the road.
        Vector3 back = p + r * (HalfRoad + shoulderWidth + 3.0f) + Vector3.up * padY;
        MeshKit.Spawn("Shelter Back", root, MeshKit.Box(new Vector3(0.1f, 2.2f, 3.6f), 1f), wood, back, along, true);
        MeshKit.Spawn("Shelter Roof", root, MeshKit.Box(new Vector3(1.6f, 0.1f, 4f), 1f), wood, back - r * 0.7f + Vector3.up * 2.2f, along, true);
        MeshKit.Spawn("Shelter Side", root, MeshKit.Box(new Vector3(1.3f, 2.2f, 0.08f), 1f), wood, back - r * 0.6f + t * 1.8f, along, true);
        MeshKit.Spawn("Bench", root, MeshKit.Box(new Vector3(0.4f, 0.45f, 2.2f), 1f), wood, back - r * 0.35f, along, true);

        // "H" sign where the front door should stop.
        Vector3 pole = p + r * (HalfRoad + shoulderWidth + 0.4f) + t * 5f + Vector3.up * padY;
        MeshKit.Spawn("Sign Pole", root, MeshKit.Prism(0.04f, 2.6f, 6, 1f), metal, pole, Quaternion.identity, true);
        MeshKit.Spawn("Sign", root, MeshKit.Box(new Vector3(0.6f, 0.6f, 0.03f), 0.6f), busStopSign, pole + Vector3.up * 2.1f, Quaternion.LookRotation(-t), false);

        if (stopLamps)
        {
            Vector3 lampPos = p + r * (HalfRoad + shoulderWidth + 0.6f) - t * 4f + Vector3.up * padY;
            MeshKit.Spawn("Lamp Pole", root, MeshKit.Prism(0.06f, 4.2f, 6, 1f), metal, lampPos, Quaternion.identity, true);
            MeshKit.Spawn("Lamp Head", root, MeshKit.Box(new Vector3(0.5f, 0.12f, 0.25f), 1f), metal, lampPos + Vector3.up * 4.2f - r * 0.2f, Quaternion.LookRotation(r), false);
            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = lampPos + Vector3.up * 4f - r * 0.4f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = lampRange;
            light.intensity = lampIntensity;
            light.color = lampColor;
            light.shadows = LightShadows.None;
            lightGo.AddComponent<FlickerLight>();
        }

        var waitPoint = new GameObject("WaitPoint").transform;
        waitPoint.SetParent(root, false);
        waitPoint.SetPositionAndRotation(p + r * (HalfRoad + shoulderWidth + 0.8f) + Vector3.up * padY, Quaternion.LookRotation(-r));

        var stop = root.gameObject.AddComponent<BusStop>();
        stop.stopName = stopName;
        stop.waitPoint = waitPoint;
        stop.arcLength = distances[i];
        stop.roadDirection = t;
        stops.Add(stop);
    }

    // ---------------------------------------------------------------- ground

    void BuildGround()
    {
        // 300 x 300 m, 3 m grid so vertex-lit headlights still look OK.
        const float size = 300f, cell = 3f;
        int n = Mathf.RoundToInt(size / cell);
        var mb = new MeshKit.Builder();
        float half = size * 0.5f;
        for (int x = 0; x < n; x++)
        for (int z = 0; z < n; z++)
        {
            float x0 = -half + x * cell, z0 = -half + z * cell;
            var a = new Vector3(x0, 0, z0);
            var b = new Vector3(x0, 0, z0 + cell);
            var c = new Vector3(x0 + cell, 0, z0 + cell);
            var d = new Vector3(x0 + cell, 0, z0);
            mb.Quad(a, b, c, d, new Vector2(x0, z0) / 4f, new Vector2(x0, z0 + cell) / 4f,
                    new Vector2(x0 + cell, z0 + cell) / 4f, new Vector2(x0 + cell, z0) / 4f);
        }
        var go = MeshKit.Spawn("Forest Floor", transform, mb.ToMesh("Forest Floor"), forestFloor, Vector3.zero, Quaternion.identity, false);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, -0.5f, 0f);
        col.size = new Vector3(size, 1f, size);
        ground = go.transform;
    }

    void FollowGround()
    {
        if (ground == null || bus == null) return;
        // Snap to the texture tile so the floor texture doesn't swim.
        const float snap = 12f;
        Vector3 p = bus.position;
        ground.position = new Vector3(Mathf.Round(p.x / snap) * snap, 0f, Mathf.Round(p.z / snap) * snap);
    }
}
