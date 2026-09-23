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
    Transform ground;
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

        trunkMesh = MeshKit.Prism(0.22f, 3f, 5, 1f);
        coneMesh = MeshKit.Cone(1f, 1f, 7);
        postMesh = MeshKit.Box(new Vector3(0.12f, 1f, 0.12f), 1f);
        BakeTrees();

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

        SidePath path = PlanSidePath(chunk.transform, a, b);
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
        if (path != null) SidePathBuilt?.Invoke(path);
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

    // A dirt track on a straight piece of road, away from bus stops.
    SidePath PlanSidePath(Transform chunk, int a, int b)
    {
        int i = (a + b) / 2;
        if (distances[i] < nextSidePathAt) return null;
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

    void BuildBusStop(Transform parent, int i)
    {
        Vector3 p = points[i], t = tangents[i], r = Right(t);
        Quaternion along = Quaternion.LookRotation(t);
        int stopIndex = stopsBuilt++;
        string stopName = StopNameAt(stopIndex);

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
