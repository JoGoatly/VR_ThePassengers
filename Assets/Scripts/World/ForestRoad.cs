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

    [Header("Hills and rivers")]
    [Tooltip("How far the road goes up and down (metres)")]
    public float hillHeight = 7f;
    public float hillLength = 420f;
    [Tooltip("Hills of the forest floor beside the road (metres)")]
    public float terrainHills = 9f;
    public Vector2 riverSpacing = new Vector2(750f, 1300f);
    public float firstRiver = 650f;
    public float riverDepth = 6.5f;
    [Tooltip("Water surface of the rivers (empty = dark unlit copy of the lamp glow)")]
    public Material water;

    [Header("Roadside places (petrol station, diner, kiosk, motel)")]
    public Vector2 placeSpacing = new Vector2(480f, 850f);
    public float firstPlace = 330f;

    [Header("Forest")]
    public int seed = 666;
    [Tooltip("Trees per chunk and side")]
    public int treesPerSide = 26;
    public float forestDepth = 55f;

    [System.Serializable]
    public class TreeModel
    {
        public GameObject model;
        [Tooltip("How often this tree appears compared to the others")]
        public float weight = 1f;
        [Tooltip("Height range in metres")]
        public Vector2 height = new Vector2(7f, 13f);
    }

    [Header("Tree models (Retro Tree Pack); empty = simple cone trees")]
    public TreeModel[] treeModels;
    [Tooltip("Materials for the tree models, matched by name to the model's own materials")]
    public Material[] treeMaterials;

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
    [Tooltip("Unlit, no fog, no draw distance: the lamp head is visible from far away")]
    public Material lampGlow;

    [Header("Night")]
    [Tooltip("Optional: lamp at every bus stop")]
    public bool stopLamps = true;
    public Color lampColor = new Color(1f, 0.68f, 0.3f);
    [Tooltip("Weak on purpose: just enough to notice the stop in the dark")]
    public float lampIntensity = 3f;
    public float lampRange = 8f;

    public IReadOnlyList<BusStop> Stops => stops;

    [Header("Side paths into the forest (to houses)")]
    [Tooltip("Metres between dirt tracks leading off the road")]
    public Vector2 sidePathSpacing = new Vector2(650f, 1100f);
    public float firstSidePath = 420f;
    [Tooltip("How far into the forest the house stands (from the road edge)")]
    public float sidePathLength = 30f;

    /// <summary>A dirt track leading off the right side of the road to a house.</summary>
    public class SidePath
    {
        public Vector3 start;       // at the road edge
        public Vector3 direction;   // away from the road
        public Vector3 house;       // centre of the house clearing
        public float arcLength;
        public Transform chunk;
    }

    /// <summary>The bus depot at the end of the shift: a big yard on the right side of the road.</summary>
    public class DepotSite
    {
        public Vector3 origin;       // road edge, middle of the yard
        public Quaternion rotation;  // +Z away from the road, +X against the driving direction
        public float arcLength;      // where the bus stops (depot bus stop)
        public Transform chunk;
    }

    /// <summary>Size of the depot yard: along the road and away from it.</summary>
    public const float DepotLength = 60f, DepotDepth = 50f;

    public event System.Action<DepotSite> DepotBuilt;
    public DepotSite Depot { get; private set; }
    bool depotRequested;
    float depotRequestedAt;

    /// <summary>
    /// Build the depot on the next suitable straight ahead. Bus stops after lastStopAt are
    /// removed and no new ones are built until the depot stands (end of the route).
    /// </summary>
    public void RequestDepot(float lastStopAt = float.MaxValue)
    {
        if (depotRequested || Depot != null) return;
        depotRequested = true;
        depotRequestedAt = BusArcLength;
        for (int i = stops.Count - 1; i >= 0; i--)
        {
            var st = stops[i];
            if (st == null || st.arcLength <= lastStopAt) continue;
            Destroy(st.gameObject);
            stops.RemoveAt(i);
        }
        // Keep the road straight for a while so the yard fits.
        segmentTurnRate = 0f;
        segmentLeft = Mathf.Max(segmentLeft, DepotLength + 120f);
    }

    /// <summary>Forget the current depot (driven past it): the next request builds a new one.</summary>
    public void ForgetDepot()
    {
        Depot = null;
        depotRequested = false;
    }

    bool NearDepot(float s, float margin) => Depot != null && Mathf.Abs(s - Depot.arcLength) < DepotLength * 0.5f + margin;

    /// <summary>A river crossing under a bridge.</summary>
    public class RiverSite
    {
        public Vector3 centre;     // road centre on the bridge
        public Vector3 along;      // road direction (flat)
        public Vector3 right;      // across the road
        public float roadY, bedY, waterY;
        public Transform chunk;
    }

    /// <summary>A roadside place (petrol station, diner, ...): +Z away from the road.</summary>
    public class PlaceSite
    {
        public Vector3 origin;     // road edge, middle of the place
        public Quaternion rotation;
        public float arcLength;
        public int kind;
        public Transform chunk;
    }

    public const float PlaceWidth = 42f, PlaceDepth = 44f;
    const float RiverHalfWidth = 7f, ValleyHalfWidth = 24f;

    public event System.Action<RiverSite> RiverBuilt;
    public event System.Action<PlaceSite> PlaceBuilt;
    readonly List<float> rivers = new List<float>();
    readonly List<PlaceSite> places = new List<PlaceSite>();
    float nextRiverAt, nextPlaceAt;
    int placesPlanned;

    public event System.Action<SidePath> SidePathBuilt;
    public event System.Action<BusStop> StopBuilt;
    public IReadOnlyList<SidePath> SidePaths => sidePaths;

    readonly List<SidePath> sidePaths = new List<SidePath>();
    int stopsBuilt;

    /// <summary>Name of the n-th stop of the route (the names repeat in a fixed order).</summary>
    public string StopNameAt(int index) => stopNames.Length > 0 ? stopNames[index % stopNames.Length] : "Haltestelle";
    float nextSidePathAt;

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
    Mesh trunkMesh, coneMesh, postMesh;

    // A tree model broken into one piece per material, pivot at the bottom centre.
    class TreePart { public Vector3[] v, n; public Vector2[] uv; public int[] tris; public Material mat; }
    class BakedTree
    {
        public List<TreePart> parts;
        public GameObject prefab;           // used as is when its mesh can't be read
        public Vector3 foot;
        public float height, radius, weight;
        public Vector2 heightRange;
    }

    // Fallback when the scene has no tree list: the Retro Tree Pack in Resources/RetroTrees.
    const string TreePackPath = "RetroTrees";
    static readonly (string name, float weight, float min, float max)[] DefaultTrees =
    {
        ("tree_rt_1", 3f, 6f, 11f), ("tree_rt_3", 3f, 8f, 14f), ("tree_rt_2_1", 1.5f, 9f, 15f), ("tree_rt_2", 1.5f, 9f, 15f),
        ("tree_rt_4", 0.6f, 9f, 13f), ("dead_tree_rt_1", 0.7f, 7f, 12f), ("dead_tree_rt_2", 0.7f, 5f, 9f), ("small_tree_rt_1", 1.5f, 1.5f, 3.5f),
    };
    readonly List<BakedTree> bakedTrees = new List<BakedTree>();
    float treeWeightSum;

    float HalfRoad => laneWidth;

    void Awake()
    {
        rng = new System.Random(seed);
        genPos = transform.TransformPoint(new Vector3(-laneWidth * 0.5f, 0f, -60f));
        genHeading = Mathf.Atan2(transform.forward.x, transform.forward.z);
        segmentLeft = 260f;
        segmentTurnRate = 0f;
        nextStopAt = 60f + firstStopDistance;
        nextSidePathAt = 60f + firstSidePath;
        nextRiverAt = 60f + firstRiver;
        nextPlaceAt = 60f + firstPlace;

        trunkMesh = MeshKit.Prism(0.22f, 3f, 5, 1f);
        coneMesh = MeshKit.Cone(1f, 1f, 7);
        postMesh = MeshKit.Box(new Vector3(0.12f, 1f, 0.12f), 1f);
        BakeTrees();

        var bc = FindAnyObjectByType<BusController>();
        bus = bc != null ? bc.transform : null;

        UpdateRoad();
    }

    void Update()
    {
        if (Tutorial.Active) return;    // the driving test is on the practice ground
        UpdateRoad();
    }

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
        // A little extra ahead, so the depot yard (which reaches into the next chunks) can be planned.
        while (points.Count == 0 || distances[distances.Count - 1] < busS + generateAhead + sampleSpacing * samplesPerChunk + DepotLength + 20f)
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

        rivers.RemoveAll(r => r < busS - keepBehind - 60f);
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
        // Up and down hills; the tangent follows the slope.
        genPos.y = transform.position.y + RoadHeightAt(s);
        float slope = (RoadHeightAt(s + 1f) - RoadHeightAt(s - 1f)) * 0.5f;
        points.Add(genPos);
        tangents.Add(new Vector3(Mathf.Sin(genHeading), slope, Mathf.Cos(genHeading)).normalized);
        distances.Add(s);
        onCurve.Add(segmentTurnRate != 0f);

        // Now and then a river crosses under the road (on a straight).
        if (s >= nextRiverAt && segmentTurnRate == 0f && segmentLeft > 25f)
        {
            rivers.Add(s + 20f);
            nextRiverAt = s + Range(riverSpacing);
        }
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

    // Height of the road: flat at the start, then long gentle hills.
    float RoadHeightAt(float s)
    {
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(140f, 380f, s));
        float h = (Mathf.PerlinNoise(s / hillLength, 3.7f) - 0.5f) * 2f * hillHeight
                + (Mathf.PerlinNoise(s / (hillLength * 0.35f), 9.1f) - 0.5f) * 2f * hillHeight * 0.25f;
        return h * fade;
    }

    bool NearRiver(float s, float margin)
    {
        foreach (var r in rivers) if (Mathf.Abs(s - r) < margin) return true;
        return false;
    }

    bool NearPlace(float s, float margin)
    {
        foreach (var pl in places) if (pl.chunk != null && Mathf.Abs(s - pl.arcLength) < margin) return true;
        return false;
    }

    // 1 in the river bed, 0 outside the valley.
    float Valley(float s)
    {
        float v = 0f;
        foreach (var r in rivers)
        {
            float d = Mathf.Abs(s - r);
            if (d < ValleyHalfWidth) v = Mathf.Max(v, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiverHalfWidth, ValleyHalfWidth, d)));
        }
        return v;
    }

    // Rolling forest floor beside the road.
    float Hills(Vector3 p)
    {
        return (Mathf.PerlinNoise(p.x * 0.017f + 37f, p.z * 0.017f + 11f) - 0.5f) * 2f * terrainHills
             + (Mathf.PerlinNoise(p.x * 0.07f + 5f, p.z * 0.07f + 3f) - 0.5f) * 1.6f;
    }

    // Ground height at a point beside the road: level with the road near it, hills further
    // away, flat at houses / the depot / roadside places, a valley at rivers.
    float TerrainY(Vector3 p, float s, float lateral, float roadY)
    {
        float y = roadY - 0.04f;
        float bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 34f, lateral));
        if (bank > 0f) y += Hills(p) * bank;
        float w = FlatWeight(p, out float flatY);
        if (w > 0f) y = Mathf.Lerp(y, flatY - 0.03f, w);
        float v = Valley(s);
        if (v > 0f) y = Mathf.Lerp(y, roadY - riverDepth, v);
        return y;
    }

    /// <summary>Height of the ground at a world position (near the road).</summary>
    public float GroundHeight(Vector3 world)
    {
        if (points.Count == 0) return world.y;
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 d = points[i] - world;
            d.y = 0f;
            float dd = d.sqrMagnitude;
            if (dd < bestD) { bestD = dd; best = i; }
        }
        float lateral = Vector3.Dot(world - points[best], Right(tangents[best]));
        return TerrainY(world, distances[best], Mathf.Abs(lateral), points[best].y);
    }

    // How much a point belongs to a flat place (1 inside, fading out over 10 m) and its height.
    float FlatWeight(Vector3 p, out float height)
    {
        float best = 0f;
        height = 0f;
        foreach (var sp in sidePaths)
        {
            if (sp.chunk == null) continue;
            Vector3 d = p - sp.start;
            d.y = 0f;
            float along = Vector3.Dot(d, sp.direction);
            float across = (d - sp.direction * along).magnitude;
            float dx = Mathf.Max(0f, Mathf.Max(-3f - along, along - (sidePathLength + 2f)));
            float dy = Mathf.Max(0f, across - 5f);
            Consider(Mathf.Min(Mathf.Sqrt(dx * dx + dy * dy), Mathf.Max(0f, Flat(p - sp.house).magnitude - 14f)), sp.start.y, ref best, ref height);
        }
        if (Depot != null && Depot.chunk != null)
        {
            Vector3 l = Quaternion.Inverse(Depot.rotation) * (p - Depot.origin);
            float dx = Mathf.Max(0f, Mathf.Abs(l.x) - (DepotLength * 0.5f + 3f));
            float dz = Mathf.Max(0f, Mathf.Max(-EdgeOffset - l.z, l.z - (DepotDepth + 3f)));
            Consider(Mathf.Sqrt(dx * dx + dz * dz), Depot.origin.y, ref best, ref height);
        }
        foreach (var pl in places)
        {
            if (pl.chunk == null) continue;
            Vector3 l = Quaternion.Inverse(pl.rotation) * (p - pl.origin);
            float dx = Mathf.Max(0f, Mathf.Abs(l.x) - PlaceWidth * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Max(-EdgeOffset - l.z, l.z - PlaceDepth));
            Consider(Mathf.Sqrt(dx * dx + dz * dz), pl.origin.y, ref best, ref height);
        }
        return best;
    }

    static void Consider(float outside, float y, ref float best, ref float height)
    {
        float w = 1f - Mathf.SmoothStep(0f, 1f, outside / 10f);
        if (w > best) { best = w; height = y; }
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
        var roadGo = MeshKit.Spawn("Road", chunk.transform, roadMb.ToMesh("Road"), road, Vector3.zero, Quaternion.identity, false);
        roadGo.AddComponent<MeshCollider>().sharedMesh = roadGo.GetComponent<MeshFilter>().sharedMesh;

        var shoulderMb = new MeshKit.Builder();
        Strip(shoulderMb, a, b, -HalfRoad - shoulderWidth, -HalfRoad, 0.015f, 2f, 2f, 0f);
        Strip(shoulderMb, a, b, HalfRoad, HalfRoad + shoulderWidth, 0.015f, 2f, 2f, 0f);
        var shoulderGo = MeshKit.Spawn("Shoulder", chunk.transform, shoulderMb.ToMesh("Shoulder"), shoulder, Vector3.zero, Quaternion.identity, false);
        shoulderGo.AddComponent<MeshCollider>().sharedMesh = shoulderGo.GetComponent<MeshFilter>().sharedMesh;

        DepotSite depot = depotRequested && Depot == null ? PlanDepot(chunk.transform, a) : null;
        SidePath path = PlanSidePath(chunk.transform, a, b);
        PlaceSite place = PlanPlace(chunk.transform, a, b);
        BuildTerrain(chunk.transform, a, b);
        BuildForest(chunk.transform, a, b);
        var bridges = new List<RiverSite>();
        foreach (var r in rivers)
            if (r >= distances[a] && r < distances[b]) bridges.Add(BuildBridge(chunk.transform, a + Mathf.RoundToInt((r - distances[a]) / sampleSpacing)));
        BuildGuidePosts(chunk.transform, a, b);

        // Bus stop(s) in this chunk.
        for (int i = a; i < b; i++)
        {
            if (distances[i] < nextStopAt) continue;
            if (depotRequested || NearDepot(distances[i], 25f) || NearRiver(distances[i], 45f) || NearPlace(distances[i], 40f)) continue;
            if (onCurve[i] || (i + 6 < onCurve.Count && onCurve[i + 6]) || (i >= 6 && onCurve[i - 6])) { nextStopAt += sampleSpacing; continue; }
            BuildBusStop(chunk.transform, i);
            nextStopAt = distances[i] + Range(stopSpacing);
        }

        chunks.Enqueue((chunk, distances[b]));
        builtUpTo = to;
        if (path != null) SidePathBuilt?.Invoke(path);
        if (place != null) PlaceBuilt?.Invoke(place);
        foreach (var br in bridges) RiverBuilt?.Invoke(br);
        if (depot != null)
        {
            // The depot's own bus stop, before the gate.
            int k = Mathf.Clamp(a + Mathf.RoundToInt(24f / sampleSpacing), a, points.Count - 1);
            var stop = BuildBusStop(chunk.transform, k, Loc.T("Betriebshof", "Depot"));
            stop.Visited = true;
            depot.arcLength = stop.arcLength;
            DepotBuilt?.Invoke(depot);
        }
    }

    // The yard starts at this chunk and reaches into the next ones: only on a long straight,
    // away from bus stops and side paths.
    DepotSite PlanDepot(Transform chunk, int a)
    {
        float s0 = distances[a];
        // The yard starts 14 m into this chunk, so its flat ground never reaches back into
        // the chunk before (already built).
        const float lead = 14f;
        if (distances[distances.Count - 1] < s0 + lead + DepotLength + 20f) return null;
        // Only on a straight - unless it takes too long, then anywhere.
        bool relaxed = BusArcLength > depotRequestedAt + 250f;
        for (int k = a; k < points.Count && !relaxed; k++)
        {
            if (distances[k] > s0 + lead + DepotLength + 20f) break;
            if (onCurve[k]) return null;
        }
        foreach (var st in stops)
            if (st != null && st.arcLength > s0 - 25f) return null;
        foreach (var sp in sidePaths)
            if (sp.chunk != null && sp.arcLength > s0 - 30f) return null;
        if (NearRiver(s0 + DepotLength * 0.5f, DepotLength * 0.5f + 40f) || NearPlace(s0 + DepotLength * 0.5f, DepotLength * 0.5f + 30f)) return null;
        if (Mathf.Abs(nextStopAt - s0) < 20f) nextStopAt = s0 + DepotLength + 30f;

        Vector3 t = tangents[a], r = Right(t);
        Depot = new DepotSite
        {
            origin = points[Mathf.Min(points.Count - 1, a + Mathf.RoundToInt((lead + DepotLength * 0.5f) / sampleSpacing))] + r * EdgeOffset,
            rotation = Quaternion.LookRotation(r),
            arcLength = s0 + lead + DepotLength * 0.5f,
            chunk = chunk,
        };
        depotRequested = false;
        return Depot;
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
        if (bakedTrees.Count == 0) { BuildConeForest(parent, a, b); return; }

        var builders = new Dictionary<Material, MeshKit.Builder>();
        float edge = HalfRoad + shoulderWidth;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int n = 0; n < treesPerSide; n++)
            {
                var tree = PickTree();
                float h = Range(tree.heightRange);
                float scale = h / tree.height;
                float width = Range(0.85f, 1.15f);

                int i = rng.Next(a, b);
                float along = Range(0f, sampleSpacing);
                // More trees close to the road, thinning out into the dark.
                float t = (float)rng.NextDouble();
                // Wide crowns keep their distance so they don't hang over the road.
                float crown = tree.radius * scale * width * 0.75f;
                float offset = Mathf.Max(edge + 2.5f + t * t * forestDepth, edge + 1f + crown);
                Vector3 pos = points[i] + tangents[i] * along + Right(tangents[i]) * offset * side;
                if (TooCloseToRoad(pos, i, edge + 1f + crown * 0.8f) || InClearing(pos)) continue;
                if (Valley(distances[i]) > 0.8f) continue;   // not in the river
                pos.y = TerrainY(pos, distances[i], offset, points[i].y);

                var m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, Range(0f, 360f), 0f), new Vector3(scale * width, scale, scale * width));
                if (tree.prefab != null) PlaceTreeCopy(tree, parent, m);
                foreach (var part in tree.parts)
                {
                    if (part.mat == null) continue;
                    if (!builders.TryGetValue(part.mat, out var mb)) builders[part.mat] = mb = new MeshKit.Builder();
                    mb.AddArrays(part.v, part.n, part.uv, part.tris, m);
                }

                // Trees right next to the road block the bus (bushes don't).
                if (offset < edge + 12f && h > 3f)
                {
                    var col = new GameObject("Tree Collider").AddComponent<CapsuleCollider>();
                    col.transform.SetParent(parent, false);
                    col.transform.position = pos + Vector3.up * 2f;
                    col.radius = 0.3f;
                    col.height = 4f;
                }
            }
        }
        foreach (var kv in builders)
            MeshKit.Spawn("Trees " + kv.Key.name, parent, kv.Value.ToMesh("Trees"), kv.Key, Vector3.zero, Quaternion.identity, false);
    }

    BakedTree PickTree()
    {
        float r = (float)rng.NextDouble() * treeWeightSum;
        foreach (var tree in bakedTrees)
        {
            r -= tree.weight;
            if (r <= 0f) return tree;
        }
        return bakedTrees[bakedTrees.Count - 1];
    }

    // Reads the tree models once: one vertex list per material, pivot moved to the bottom centre.
    void BakeTrees()
    {
        bakedTrees.Clear();
        treeWeightSum = 0f;
        if (treeMaterials == null || treeMaterials.Length == 0 || System.Array.TrueForAll(treeMaterials, m => m == null))
            treeMaterials = Resources.LoadAll<Material>(TreePackPath + "/Materials");
        if (treeModels == null || !System.Array.Exists(treeModels, t => t != null && t.model != null))
            treeModels = LoadTreePack();

        foreach (var tm in treeModels)
        {
            if (tm == null || tm.model == null || tm.weight <= 0f) continue;
            var root = tm.model.transform;
            var parts = new List<TreePart>();
            bool readable = true;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var mf in tm.model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                // Bounds from the mesh bounds (works even when the mesh can't be read).
                var b = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    min = Vector3.Min(min, corner);
                    max = Vector3.Max(max, corner);
                }
                if (!mesh.isReadable) { readable = false; continue; }
                var renderer = mf.GetComponent<MeshRenderer>();
                var imported = renderer != null ? renderer.sharedMaterials : null;
                Matrix4x4 local = toRoot;
                Matrix4x4 normalMatrix = local.inverse.transpose;
                var v = mesh.vertices;
                var nrm = mesh.normals;
                var uv = mesh.uv;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    // Only the vertices this material uses.
                    var src = mesh.GetTriangles(sub);
                    var remap = new Dictionary<int, int>();
                    var pv = new List<Vector3>(); var pn = new List<Vector3>(); var puv = new List<Vector2>();
                    var pt = new int[src.Length];
                    for (int k = 0; k < src.Length; k++)
                    {
                        int idx = src[k];
                        if (!remap.TryGetValue(idx, out int j))
                        {
                            j = pv.Count;
                            remap[idx] = j;
                            pv.Add(local.MultiplyPoint3x4(v[idx]));
                            pn.Add(idx < nrm.Length ? normalMatrix.MultiplyVector(nrm[idx]).normalized : Vector3.up);
                            puv.Add(idx < uv.Length ? uv[idx] : Vector2.zero);
                        }
                        pt[k] = j;
                    }
                    if (pt.Length == 0) continue;
                    parts.Add(new TreePart { v = pv.ToArray(), n = pn.ToArray(), uv = puv.ToArray(), tris = pt, mat = TreeMaterialFor(imported, sub) });
                }
            }
            float height = max.y - min.y;
            if (height < 0.01f || float.IsInfinity(height)) continue;

            // Move the pivot to the foot of the trunk.
            Vector3 foot = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
            float radius = Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f;
            var baked = new BakedTree { foot = foot, height = height, radius = radius, weight = tm.weight, heightRange = tm.height };
            if (readable && parts.Count > 0)
            {
                foreach (var p in parts) for (int k = 0; k < p.v.Length; k++) p.v[k] -= foot;
                baked.parts = parts;
            }
            else
            {
                // Mesh not readable (Read/Write off): place copies of the model instead of merging.
                baked.parts = new List<TreePart>();
                baked.prefab = tm.model;
            }
            bakedTrees.Add(baked);
            treeWeightSum += tm.weight;
        }
        if (bakedTrees.Count == 0) Debug.LogWarning("ForestRoad: no tree models found, using the simple cone trees.");
    }

    static TreeModel[] LoadTreePack()
    {
        var models = Resources.LoadAll<GameObject>(TreePackPath);
        var list = new List<TreeModel>();
        foreach (var d in DefaultTrees)
        {
            var m = System.Array.Find(models, g => g.name == d.name);
            if (m != null) list.Add(new TreeModel { model = m, weight = d.weight, height = new Vector2(d.min, d.max) });
        }
        return list.ToArray();
    }

    // A single copy of a tree model (only when its mesh can't be merged).
    void PlaceTreeCopy(BakedTree tree, Transform parent, Matrix4x4 m)
    {
        var holder = new GameObject("Tree").transform;
        holder.SetParent(parent, false);
        holder.SetPositionAndRotation(m.GetColumn(3), m.rotation);
        holder.localScale = m.lossyScale;
        var copy = Instantiate(tree.prefab, holder);
        copy.transform.localPosition = -tree.foot;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;
        foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in copy.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = TreeMaterialFor(r.sharedMaterials, i) ?? mats[i];
            r.sharedMaterials = mats;
        }
    }

    // The own material of the same name (longest match, so "tree_rt_2_1" beats "tree_rt_2").
    Material TreeMaterialFor(Material[] imported, int sub)
    {
        string name = imported != null && sub < imported.Length && imported[sub] != null ? imported[sub].name : "";
        Material best = null;
        if (treeMaterials != null)
            foreach (var m in treeMaterials)
                if (m != null && name.Contains(m.name) && (best == null || m.name.Length > best.name.Length)) best = m;
        if (best != null) return best;
        return name.ToLowerInvariant().Contains("bark") ? bark : needles;
    }

    // Fallback without tree models: trunks with stacked cones.
    void BuildConeForest(Transform parent, int a, int b)
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
                float t = (float)rng.NextDouble();
                float offset = edge + 2.5f + t * t * forestDepth;
                Vector3 pos = points[i] + tangents[i] * along + Right(tangents[i]) * offset * side;
                if (TooCloseToRoad(pos, i, edge + 2f) || InClearing(pos)) continue;
                if (Valley(distances[i]) > 0.8f) continue;
                pos.y = TerrainY(pos, distances[i], offset, points[i].y);

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

    // ---------------------------------------------------------------- terrain

    // The forest floor of a chunk: a grid along the road, 80 m to both sides, with a collider.
    void BuildTerrain(Transform parent, int a, int b)
    {
        float e = EdgeOffset;
        float[] half = { 2.5f, e, e + 2f, e + 4.5f, e + 8f, e + 12f, e + 17f, e + 23f, e + 30f, e + 39f, e + 50f, e + 63f, e + 78f };
        var cols = new List<float>();
        for (int k = half.Length - 1; k >= 0; k--) cols.Add(-half[k]);
        cols.Add(0f);
        foreach (var h in half) cols.Add(h);

        int rows = b - a + 1;
        var grid = new Vector3[rows, cols.Count];
        for (int i = 0; i < rows; i++)
        {
            int k = a + i;
            Vector3 r = Right(tangents[k]);
            for (int c = 0; c < cols.Count; c++)
            {
                float o = cols[c];
                Vector3 p = points[k] + r * o;
                float lateral = Mathf.Abs(o);
                float y = TerrainY(p, distances[k], lateral, points[k].y);
                if (lateral < e - 0.01f) y = Mathf.Min(y, points[k].y - 0.3f);   // under the road
                p.y = y;
                grid[i, c] = p;
            }
        }
        var mb = new MeshKit.Builder();
        for (int i = 0; i < rows - 1; i++)
        for (int c = 0; c < cols.Count - 1; c++)
        {
            Vector3 p0 = grid[i, c], p1 = grid[i + 1, c], p2 = grid[i + 1, c + 1], p3 = grid[i, c + 1];
            mb.Quad(p0, p1, p2, p3, new Vector2(p0.x, p0.z) / 4f, new Vector2(p1.x, p1.z) / 4f, new Vector2(p2.x, p2.z) / 4f, new Vector2(p3.x, p3.z) / 4f);
        }
        var go = MeshKit.Spawn("Terrain", parent, mb.ToMesh("Terrain"), forestFloor, Vector3.zero, Quaternion.identity, false);
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    // A bridge over a river: deck, railings, pillars and the water in the valley below.
    RiverSite BuildBridge(Transform parent, int k)
    {
        k = Mathf.Clamp(k, 0, points.Count - 1);
        Vector3 p = points[k], t = Flat(tangents[k]).normalized, r = Right(tangents[k]);
        float e = EdgeOffset, length = 34f;
        float bed = p.y - riverDepth;
        var site = new RiverSite { centre = p, along = t, right = r, roadY = p.y, bedY = bed, waterY = bed + 0.8f, chunk = parent };
        var root = new GameObject("Bridge").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(p, Quaternion.LookRotation(t));

        Part(root, "Deck", new Vector3(0f, -0.75f, 0f), new Vector3(e * 2f + 0.6f, 0.72f, length), concrete, false);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (e - 0.1f);
            Part(root, "Kerb", new Vector3(x, -0.05f, 0f), new Vector3(0.35f, 0.25f, length), concrete, true);
            Part(root, "Rail", new Vector3(x, 0.95f, 0f), new Vector3(0.1f, 0.1f, length), metal, true);
            Part(root, "Rail Low", new Vector3(x, 0.5f, 0f), new Vector3(0.06f, 0.06f, length), metal, false);
            for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += 2f)
                Part(root, "Post", new Vector3(x, 0f, z), new Vector3(0.08f, 1f, 0.08f), metal, false);
            foreach (float z in new[] { -9f, 9f })
                Part(root, "Pillar", new Vector3(side * (e - 1.4f), -(p.y - bed), z), new Vector3(1f, p.y - bed - 0.75f, 1f), concrete, false);
        }

        // Water: a grid (so the far ends don't vanish) across the whole valley.
        Material waterMat = water;
        if (waterMat == null && lampGlow != null)
        {
            waterMat = new Material(lampGlow) { name = "River Water" };
            if (waterMat.HasProperty("_MainColor")) waterMat.SetColor("_MainColor", new Color(0.04f, 0.07f, 0.1f));
            water = waterMat;
        }
        var mb = new MeshKit.Builder();
        const float span = 90f, cell = 6f;
        for (float x = -span; x < span; x += cell)
        for (float z = -RiverHalfWidth; z < RiverHalfWidth; z += RiverHalfWidth)
        {
            Vector3 a0 = new Vector3(x, 0f, z), a1 = new Vector3(x, 0f, z + RiverHalfWidth), a2 = new Vector3(x + cell, 0f, z + RiverHalfWidth), a3 = new Vector3(x + cell, 0f, z);
            mb.Quad(a0, a1, a2, a3, new Vector2(a0.x, a0.z) / 6f, new Vector2(a1.x, a1.z) / 6f, new Vector2(a2.x, a2.z) / 6f, new Vector2(a3.x, a3.z) / 6f);
        }
        var w = MeshKit.Spawn("River", root, mb.ToMesh("River"), waterMat, root.position, root.rotation, false);
        w.transform.localPosition = new Vector3(0f, site.waterY - p.y, 0f);
        w.transform.localRotation = Quaternion.identity;
        return site;
    }

    static GameObject Part(Transform root, string name, Vector3 localBottom, Vector3 size, Material mat, bool collider)
    {
        var go = MeshKit.Spawn(name, root, MeshKit.Box(size, 1f), mat, root.position, root.rotation, collider);
        go.transform.localPosition = localBottom;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    // A petrol station, diner, kiosk or motel next to a straight piece of road.
    PlaceSite PlanPlace(Transform chunk, int a, int b)
    {
        int i = a + Mathf.RoundToInt((PlaceWidth * 0.5f + 11f) / sampleSpacing);
        if (i + 16 >= points.Count) return null;
        float s = distances[i];
        if (s < nextPlaceAt || depotRequested || NearDepot(s, 70f) || NearRiver(s, 70f)) return null;
        for (int k = a; k <= i + 16; k++) if (onCurve[k]) return null;
        foreach (var st in stops) if (st != null && Mathf.Abs(st.arcLength - s) < 45f) return null;
        if (Mathf.Abs(nextStopAt - s) < 45f) return null;
        foreach (var sp in sidePaths) if (sp.chunk != null && Mathf.Abs(sp.arcLength - s) < 70f) return null;

        nextPlaceAt = s + Range(placeSpacing);
        float side = rng.NextDouble() < 0.5 ? -1f : 1f;
        Vector3 r = Right(tangents[i]) * side;
        var site = new PlaceSite
        {
            origin = points[i] + r * EdgeOffset,
            rotation = Quaternion.LookRotation(r),
            arcLength = s,
            // Every other place (and the first one each night) is a petrol station.
            kind = placesPlanned++ % 2 == 0 ? 0 : 1 + rng.Next(3),
            chunk = chunk,
        };
        places.Add(site);
        places.RemoveAll(pl => pl.chunk == null);
        return site;
    }

    // A dirt track on a straight piece of road, away from bus stops.
    SidePath PlanSidePath(Transform chunk, int a, int b)
    {
        // Far enough into the chunk that the flat clearing doesn't reach the chunk before.
        int i = Mathf.Min(b, a + 13);
        if (distances[i] < nextSidePathAt || !Features.Has(Feature.SidePaths)) return null;
        if (depotRequested || NearDepot(distances[i], 60f) || NearRiver(distances[i], 60f) || NearPlace(distances[i], 70f)) return null;
        for (int k = a; k <= b; k++) if (onCurve[k]) return null;
        foreach (var st in stops)
            if (st != null && Mathf.Abs(st.arcLength - distances[i]) < 60f) return null;
        if (Mathf.Abs(nextStopAt - distances[i]) < 60f) return null;

        nextSidePathAt = distances[i] + Range(sidePathSpacing);
        Vector3 r = Right(tangents[i]);
        float edge = HalfRoad + shoulderWidth;
        var path = new SidePath
        {
            start = points[i] + r * edge,
            direction = r,
            house = points[i] + r * (edge + sidePathLength),
            arcLength = distances[i],
            chunk = chunk,
        };
        sidePaths.Add(path);
        sidePaths.RemoveAll(sp => sp.chunk == null);
        return path;
    }

    /// <summary>Is this point on a dirt track or in a house clearing (no trees there)?</summary>
    public bool InClearing(Vector3 pos, float margin = 0f)
    {
        foreach (var pl in places)
        {
            if (pl.chunk == null) continue;
            Vector3 l = Quaternion.Inverse(pl.rotation) * (pos - pl.origin);
            if (Mathf.Abs(l.x) < PlaceWidth * 0.5f + margin && l.z > -EdgeOffset - margin && l.z < PlaceDepth + margin) return true;
        }
        if (Depot != null && Depot.chunk != null)
        {
            Vector3 local = Quaternion.Inverse(Depot.rotation) * (pos - Depot.origin);
            if (Mathf.Abs(local.x) < DepotLength * 0.5f + 3f + margin && local.z > -EdgeOffset - margin && local.z < DepotDepth + 3f + margin)
                return true;
        }
        foreach (var sp in sidePaths)
        {
            if (sp.chunk == null) continue;
            Vector3 d = pos - sp.start;
            d.y = 0f;
            float along = Vector3.Dot(d, sp.direction);
            float across = (d - sp.direction * along).magnitude;
            if (along > -3f && along < sidePathLength + 2f && across < 5f + margin) return true;
            Vector3 h = pos - sp.house;
            h.y = 0f;
            if (h.magnitude < 14f + margin) return true;
        }
        return false;
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

    [Header("Bus stop model")]
    [Tooltip("Turn the bus stop model around if its open side faces the forest")]
    public bool flipBusStopModel;
    GameObject busStopTemplate;
    bool busStopTemplateTried;

    // The shelter from Resources/BusStop (the pack's scene model: only its bus stop is used).
    public bool BusStopModel(Transform root, Vector3 centre, Vector3 awayFromRoad)
    {
        if (!busStopTemplateTried)
        {
            busStopTemplateTried = true;
            var scene = PsxConvert.Spawn("BusStop/Models/Stop", transform, "BusStop/Textures");
            if (scene != null)
            {
                Transform stop = null;
                foreach (var t in scene.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Bus_stop")) { stop = t; break; }
                if (stop != null)
                {
                    busStopTemplate = new GameObject("Bus Stop Template");
                    busStopTemplate.transform.SetParent(transform, false);
                    stop.SetParent(busStopTemplate.transform, true);
                    stop.localPosition = Vector3.zero;
                    busStopTemplate.SetActive(false);
                }
                Destroy(scene);
            }
        }
        if (busStopTemplate == null) return false;

        var holder = new GameObject("Shelter").transform;
        holder.SetParent(root, false);
        holder.SetPositionAndRotation(centre, Quaternion.LookRotation(flipBusStopModel ? awayFromRoad : -awayFromRoad));
        var copy = Instantiate(busStopTemplate, holder);
        copy.SetActive(true);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        // Long side along the road, standing on the platform, about 4 m long.
        var b = PsxConvert.LocalBounds(copy.transform, holder);
        if (b.size.z > b.size.x)
        {
            copy.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            b = PsxConvert.LocalBounds(copy.transform, holder);
        }
        float length = Mathf.Max(b.size.x, 0.01f);
        if (length > 12f || length < 1.5f) { copy.transform.localScale *= 4f / length; b = PsxConvert.LocalBounds(copy.transform, holder); }
        copy.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        PsxConvert.AddColliders(copy, 0.4f);
        return true;
    }

    BusStop BuildBusStop(Transform parent, int i, string specialName = null)
    {
        Vector3 p = points[i], t = tangents[i], r = Right(t);
        Quaternion along = Quaternion.LookRotation(t);
        // Special stops (the depot) are not part of the numbered route.
        int stopIndex = specialName != null ? -1 : stopsBuilt++;
        string stopName = specialName ?? StopNameAt(stopIndex);

        var root = new GameObject("BusStop " + stopName).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(p + r * (HalfRoad + shoulderWidth + 1.2f), Quaternion.LookRotation(-r));

        float padY = 0.12f;
        Vector3 padCentre = p + r * (HalfRoad + shoulderWidth + 1.6f);
        MeshKit.Spawn("Platform", root, MeshKit.Box(new Vector3(3.2f, padY, 10f), 1f), concrete, padCentre, along, false);

        // Shelter: the bus stop model from the pack, or a simple wooden one.
        Vector3 back = p + r * (HalfRoad + shoulderWidth + 3.0f) + Vector3.up * padY;
        if (!BusStopModel(root, back - r * 0.9f, r))
        {
            MeshKit.Spawn("Shelter Back", root, MeshKit.Box(new Vector3(0.1f, 2.2f, 3.6f), 1f), wood, back, along, true);
            MeshKit.Spawn("Shelter Roof", root, MeshKit.Box(new Vector3(1.6f, 0.1f, 4f), 1f), wood, back - r * 0.7f + Vector3.up * 2.2f, along, true);
            MeshKit.Spawn("Shelter Side", root, MeshKit.Box(new Vector3(1.3f, 2.2f, 0.08f), 1f), wood, back - r * 0.6f + t * 1.8f, along, true);
            MeshKit.Spawn("Bench", root, MeshKit.Box(new Vector3(0.4f, 0.45f, 2.2f), 1f), wood, back - r * 0.35f, along, true);
        }

        // "H" sign where the front door should stop.
        Vector3 pole = p + r * (HalfRoad + shoulderWidth + 0.4f) + t * 5f + Vector3.up * padY;
        MeshKit.Spawn("Sign Pole", root, MeshKit.Prism(0.04f, 2.6f, 6, 1f), metal, pole, Quaternion.identity, true);
        MeshKit.Spawn("Sign", root, MeshKit.Box(new Vector3(0.6f, 0.6f, 0.03f), 0.6f), busStopSign, pole + Vector3.up * 2.1f, Quaternion.LookRotation(-t), false);

        if (stopLamps)
        {
            Vector3 lampPos = p + r * (HalfRoad + shoulderWidth + 0.6f) - t * 4f + Vector3.up * padY;
            MeshKit.Spawn("Lamp Pole", root, MeshKit.Prism(0.06f, 4.2f, 6, 1f), metal, lampPos, Quaternion.identity, true);
            MeshKit.Spawn("Lamp Head", root, MeshKit.Box(new Vector3(0.5f, 0.12f, 0.25f), 1f), metal, lampPos + Vector3.up * 4.2f - r * 0.2f, Quaternion.LookRotation(r), false);
            if (lampGlow != null)
                MeshKit.Spawn("Lamp Glow", root, MeshKit.Box(new Vector3(0.36f, 0.05f, 0.18f), 1f), lampGlow, lampPos + Vector3.up * 4.14f - r * 0.2f, Quaternion.LookRotation(r), false);
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
        stop.index = stopIndex;
        stop.waitPoint = waitPoint;
        stop.arcLength = distances[i];
        stop.roadDirection = t;
        stops.Add(stop);
        StopBuilt?.Invoke(stop);
        return stop;
    }

    // ---------------------------------------------------------------- ground

}
