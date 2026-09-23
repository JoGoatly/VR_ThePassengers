using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Things off the main route: items lying at bus stops, and at the end of the dirt tracks an
/// abandoned house - money, items and notes inside, sometimes someone who lives there, and in
/// some cellars one of the missing drivers of line 13.
/// </summary>
public class SideAreas : MonoBehaviour
{
    public ForestRoad road;

    [Header("Materials")]
    public Material wood;
    public Material concrete;
    public Material metal;
    public Material gravel;
    [Tooltip("Unlit glow (lamp bulbs, lanterns, item glints)")]
    public Material glow;

    [Header("Characters")]
    [Tooltip("People living in the houses (e.g. Monster / Killer)")]
    public GameObject[] dwellers;
    [Tooltip("Bodies of the missing drivers (empty = male passenger models)")]
    public GameObject[] corpses;

    [Header("Sounds")]
    public AudioClip growl, hit, vanish, trapdoor, pickupSound, coinSound;

    [Header("Chances")]
    [Range(0f, 1f)] public float stopItemChance = 0.3f;

    /// <summary>Places where the player may walk off the road (tracks, clearings, cellars).</summary>
    public static bool IsInside(Vector3 position)
    {
        return instance != null && instance.road != null && instance.road.InClearing(position, 4f);
    }

    static SideAreas instance;
    readonly HashSet<int> driversPlaced = new HashSet<int>();
    readonly HashSet<int> notesPlaced = new HashSet<int>();

    void Awake() => instance = this;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (road == null) return;
        road.StopBuilt += OnStopBuilt;
        Pickup.Collected += OnCollected;
        road.SidePathBuilt += BuildHouse;
        // Big flat walls and the long track smear with the PSX texture warping - keep them crisp.
        wood = Crisp(wood);
        concrete = Crisp(concrete);
        gravel = Crisp(gravel);
        if (corpses == null || corpses.Length == 0)
        {
            var game = FindAnyObjectByType<BoardingManager>();
            if (game != null) corpses = game.malePassengers;
        }
    }

    void OnDestroy() => Pickup.Collected -= OnCollected;

    void OnCollected(Pickup p)
    {
        var sm = FindAnyObjectByType<SoundManager>();
        var clip = p.kind == Pickup.Kind.Money ? coinSound : pickupSound;
        if (sm != null && clip != null) sm.PlayWorld(clip, p.transform.position, 0.9f, 0.2f);
    }

    // ---------------------------------------------------------------- items at bus stops

    void OnStopBuilt(BusStop stop)
    {
        if (Random.value > stopItemChance + 0.03f * Progress.Day) return;
        // On the bench in the shelter.
        Vector3 back = stop.waitPoint.position - stop.waitPoint.forward * 1.9f;
        Vector3 pos = back + Vector3.up * 0.5f + stop.roadDirection * Random.Range(-0.8f, 0.8f);
        SpawnRandomItem(pos, stop.transform, true);
    }

    void SpawnRandomItem(Vector3 pos, Transform parent, bool allowNote)
    {
        float r = Random.value;
        int note = allowNote ? NextNote() : -1;
        if (note >= 0 && r < 0.2f)
            Pickup.Spawn(Pickup.Kind.Note, 0, note, pos, parent, concrete, glow);
        else if (r < 0.4f && Progress.Owns("pistol"))
            Pickup.Spawn(Pickup.Kind.Ammo, Random.Range(3, 7), 0, pos, parent, metal, glow);
        else if (r < 0.6f)
            Pickup.Spawn(Pickup.Kind.Medkit, 1, 0, pos, parent, concrete, glow);
        else
            Pickup.Spawn(Pickup.Kind.Money, Random.Range(10, 35) + Progress.Day * 5, 0, pos, parent, wood, glow);
    }

    int NextNote()
    {
        for (int i = 0; i < Story.Notes.Length; i++)
            if (!Progress.Data.notes.Contains(i) && !notesPlaced.Contains(i)) { notesPlaced.Add(i); return i; }
        return -1;
    }

    int NextDriver()
    {
        for (int i = 0; i < Story.Drivers.Length; i++)
            if (!Progress.Data.drivers.Contains(i) && !driversPlaced.Contains(i)) return i;
        return -1;
    }

    // ---------------------------------------------------------------- houses

    void BuildHouse(ForestRoad.SidePath path)
    {
        var root = new GameObject("Side Path").transform;
        root.SetParent(path.chunk, true);
        Vector3 r = path.direction;

        // Dirt track from the road to the house, and a lantern at the road so the player sees it.
        BuildTrack(root, path.start - r * 1.2f, path.house - r * 4.2f, r);
        Vector3 across = Vector3.Cross(Vector3.up, r).normalized;
        Vector3 lantern = path.start + r * 0.8f + across * 2.4f;
        MeshKit.Spawn("Lantern Post", root, MeshKit.Prism(0.06f, 1.6f, 5, 1f), wood, lantern, Quaternion.identity, true);
        if (glow != null) MeshKit.Spawn("Lantern", root, MeshKit.Box(new Vector3(0.18f, 0.22f, 0.18f), 0.3f), glow, lantern + Vector3.up * 1.7f, Quaternion.identity, false);
        AddLight(root, lantern + Vector3.up * 1.7f, new Color(1f, 0.6f, 0.25f), 2.4f, 10f, true);
        // A second, dimmer lantern at the house door.
        Vector3 doorLamp = path.house - r * 4.6f + across * 1.1f;
        if (glow != null) MeshKit.Spawn("Door Lamp", root, MeshKit.Box(new Vector3(0.14f, 0.18f, 0.14f), 0.3f), glow, doorLamp + Vector3.up * 2.2f, Quaternion.identity, false);
        AddLight(root, doorLamp + Vector3.up * 2.1f, new Color(1f, 0.55f, 0.25f), 1.4f, 7f, true);

        // House facing the road.
        Vector3 c = path.house;
        Quaternion facing = Quaternion.LookRotation(-r);   // front (door) towards the road
        var house = new GameObject("House").transform;
        house.SetParent(root, true);
        house.SetPositionAndRotation(c, facing);

        const float W = 7f, D = 8f, H = 2.8f, T = 0.15f;
        Box(house, "Floor", new Vector3(W, 0.1f, D), new Vector3(0, 0.05f, 0), wood, true);
        Box(house, "Back Wall", new Vector3(W, H, T), new Vector3(0, H / 2, -D / 2), wood, true);
        Box(house, "Left Wall", new Vector3(T, H, D), new Vector3(-W / 2, H / 2, 0), wood, true);
        Box(house, "Right Wall", new Vector3(T, H, D), new Vector3(W / 2, H / 2, 0), wood, true);
        float door = 1.3f, side = (W - door) / 2f;
        Box(house, "Front Left", new Vector3(side, H, T), new Vector3(-W / 2 + side / 2, H / 2, D / 2), wood, true);
        Box(house, "Front Right", new Vector3(side, H, T), new Vector3(W / 2 - side / 2, H / 2, D / 2), wood, true);
        Box(house, "Above Door", new Vector3(door, H - 2.1f, T), new Vector3(0, 2.1f + (H - 2.1f) / 2, D / 2), wood, true);
        // Gable roof.
        float roofW = W / 2f / Mathf.Cos(30f * Mathf.Deg2Rad) + 0.4f;
        BoxRot(house, "Roof L", new Vector3(roofW, 0.12f, D + 0.6f), new Vector3(-W / 4, H + 1f, 0), Quaternion.Euler(0, 0, 30f), wood);
        BoxRot(house, "Roof R", new Vector3(roofW, 0.12f, D + 0.6f), new Vector3(W / 4, H + 1f, 0), Quaternion.Euler(0, 0, -30f), wood);
        BoxRot(house, "Ceiling", new Vector3(W, 0.08f, D), new Vector3(0, H, 0), Quaternion.identity, wood);

        // Furniture.
        Box(house, "Table", new Vector3(1.4f, 0.75f, 0.9f), new Vector3(1.6f, 0.375f, 0.5f), wood, true);
        Box(house, "Bed", new Vector3(1.0f, 0.5f, 2.0f), new Vector3(-2.6f, 0.25f, -2.6f), concrete, true);
        Box(house, "Shelf", new Vector3(1.6f, 1.8f, 0.4f), new Vector3(2.2f, 0.9f, -3.6f), wood, true);
        Box(house, "Chair", new Vector3(0.45f, 0.9f, 0.45f), new Vector3(0.6f, 0.45f, 0.9f), wood, true);

        // A weak, flickering bulb.
        Vector3 bulb = house.TransformPoint(new Vector3(0, H - 0.25f, 0));
        if (glow != null) MeshKit.Spawn("Bulb", house, MeshKit.Box(new Vector3(0.1f, 0.12f, 0.1f), 0.2f), glow, bulb, Quaternion.identity, false);
        AddLight(house, bulb - Vector3.up * 0.1f, new Color(1f, 0.75f, 0.45f), 1.6f, 7f, true);

        // Loot on the table, bed and shelf.
        Vector3[] spots = { new Vector3(1.6f, 0.8f, 0.5f), new Vector3(-2.6f, 0.55f, -2.2f), new Vector3(2.2f, 1.81f, -3.6f), new Vector3(-2f, 0.12f, 2.4f) };
        int items = Random.Range(1, 4);
        for (int i = 0; i < items; i++)
            SpawnRandomItem(house.TransformPoint(spots[(i + Random.Range(0, spots.Length)) % spots.Length]), house, i == 0);

        // The cellar with one of the missing drivers.
        int driver = Progress.Day >= 2 && Random.value < 0.7f ? NextDriver() : -1;
        if (driver >= 0) BuildCellar(house, driver);

        // Sometimes someone is home.
        float dwellerChance = Progress.Day <= 1 ? 0f : 0.15f + 0.11f * Progress.Day;
        if (Random.value < dwellerChance) SpawnDweller(house.TransformPoint(new Vector3(-1.5f, 0.1f, -1.5f)), house);
    }

    void BuildCellar(Transform house, int driver)
    {
        driversPlaced.Add(driver);
        const float W = 5f, D = 5f, H = 2.5f, T = 0.2f;
        var cellar = new GameObject("Cellar").transform;
        cellar.SetParent(house, false);
        cellar.localPosition = new Vector3(0f, -10f, -1.5f);
        cellar.localRotation = Quaternion.identity;

        Box(cellar, "Floor", new Vector3(W, 0.2f, D), new Vector3(0, -0.1f, 0), concrete, true);
        Box(cellar, "Ceiling", new Vector3(W, 0.2f, D), new Vector3(0, H + 0.1f, 0), concrete, true);
        Box(cellar, "Wall N", new Vector3(W, H, T), new Vector3(0, H / 2, D / 2), concrete, true);
        Box(cellar, "Wall S", new Vector3(W, H, T), new Vector3(0, H / 2, -D / 2), concrete, true);
        Box(cellar, "Wall W", new Vector3(T, H, D), new Vector3(-W / 2, H / 2, 0), concrete, true);
        Box(cellar, "Wall E", new Vector3(T, H, D), new Vector3(W / 2, H / 2, 0), concrete, true);
        Box(cellar, "Ladder", new Vector3(0.5f, H, 0.08f), new Vector3(-1.8f, H / 2, D / 2 - 0.15f), wood, false);

        Vector3 bulb = cellar.TransformPoint(new Vector3(0.5f, H - 0.2f, 0f));
        if (glow != null) MeshKit.Spawn("Bulb", cellar, MeshKit.Box(new Vector3(0.08f, 0.1f, 0.08f), 0.2f), glow, bulb, Quaternion.identity, false);
        AddLight(cellar, bulb - Vector3.up * 0.1f, new Color(0.9f, 0.85f, 0.7f), 1.3f, 6f, true);

        // Trapdoor in the house floor and the ladder back up.
        Vector3 trap = house.TransformPoint(new Vector3(-1.8f, 0.11f, -0.2f));
        MeshKit.Spawn("Trapdoor", house, MeshKit.Box(new Vector3(0.9f, 0.02f, 0.9f), 0.5f), concrete, trap, house.rotation, false);
        var down = new GameObject("Trapdoor Use").AddComponent<Trapdoor>();
        down.transform.SetParent(house, false);
        down.transform.position = trap;
        down.target = cellar.TransformPoint(new Vector3(-1.8f, 0.05f, D / 2 - 0.9f));
        down.lookDirection = -house.forward;
        down.down = true;
        down.sound = trapdoor;

        var up = new GameObject("Ladder Use").AddComponent<Trapdoor>();
        up.transform.SetParent(cellar, false);
        up.transform.localPosition = new Vector3(-1.8f, 0.5f, D / 2 - 0.6f);
        up.target = trap + house.forward * 1.0f + Vector3.up * 0.05f;
        up.lookDirection = house.forward;
        up.down = false;
        up.sound = trapdoor;

        // The missing driver.
        if (corpses != null && corpses.Length > 0)
        {
            var body = Instantiate(corpses[driver % corpses.Length], cellar);
            body.transform.localPosition = new Vector3(1.2f, 0.12f, -0.6f);
            body.transform.localRotation = Quaternion.Euler(-90f, 70f, 0f);
            foreach (var a in body.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        }
        Pickup.Spawn(Pickup.Kind.DriverBadge, 0, driver, cellar.TransformPoint(new Vector3(0.6f, 0.05f, 0.4f)), cellar, metal, glow);
        if (Random.value < 0.5f) SpawnRandomItem(cellar.TransformPoint(new Vector3(-1.5f, 0.05f, -1.8f)), cellar, true);

        // From the third night, something waits down there.
        if (Progress.Day >= 3 && Random.value < 0.5f)
            SpawnDweller(cellar.TransformPoint(new Vector3(1.6f, 0f, 1.4f)), cellar);
    }

    void SpawnDweller(Vector3 position, Transform parent)
    {
        if (dwellers == null || dwellers.Length == 0) return;
        var go = Instantiate(dwellers[Random.Range(0, dwellers.Length)], position, parent.rotation * Quaternion.Euler(0, 180f, 0), parent);
        go.name = "Dweller";
        go.AddComponent<Passenger>();
        var d = go.AddComponent<Dweller>();
        d.growl = growl;
        d.hitSound = hit;
        d.vanish = vanish;
        d.Died += pos => Pickup.Spawn(Pickup.Kind.Money, Random.Range(20, 60), 0, pos + Vector3.up * 0.1f, parent, wood, glow);
    }

    // Track as many small quads (like the road), so it is lit properly and does not vanish
    // at a distance. Slightly uneven edges look like a real dirt track.
    void BuildTrack(Transform parent, Vector3 from, Vector3 to, Vector3 dir)
    {
        var mb = new MeshKit.Builder();
        Vector3 across = Vector3.Cross(Vector3.up, dir).normalized;
        float length = Vector3.Distance(from, to);
        int segments = Mathf.Max(2, Mathf.CeilToInt(length / 1.5f));
        float prevL = 1.7f, prevR = 1.7f;
        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
            Vector3 a = Vector3.Lerp(from, to, t0) + Vector3.up * 0.02f, b = Vector3.Lerp(from, to, t1) + Vector3.up * 0.02f;
            float wl = 1.5f + Random.Range(-0.25f, 0.3f), wr = 1.5f + Random.Range(-0.25f, 0.3f);
            if (i == 0) { prevL = 2.6f; prevR = 2.6f; }   // wider where it meets the road
            float v0 = t0 * length / 2f, v1 = t1 * length / 2f;
            // Left half and right half (two quads across for nicer vertex lighting).
            mb.Quad(a - across * prevL, b - across * wl, b, a, new Vector2(0, v0), new Vector2(0, v1), new Vector2(0.8f, v1), new Vector2(0.8f, v0));
            mb.Quad(a, b, b + across * wr, a + across * prevR, new Vector2(0.8f, v0), new Vector2(0.8f, v1), new Vector2(1.6f, v1), new Vector2(1.6f, v0));
            prevL = wl; prevR = wr;
        }
        MeshKit.Spawn("Track", parent, mb.ToMesh("Track"), gravel, Vector3.zero, Quaternion.identity, false);
    }

    static Material Crisp(Material m)
    {
        if (m == null) return null;
        var copy = new Material(m) { name = m.name + " (crisp)" };
        if (copy.HasProperty("_AffineTextureWarpingWeight")) copy.SetFloat("_AffineTextureWarpingWeight", 0f);
        return copy;
    }

    // ---------------------------------------------------------------- helpers

    // localCentre is the centre of the box (MeshKit boxes have their pivot at the bottom).
    static void Box(Transform parent, string name, Vector3 size, Vector3 localCentre, Material mat, bool collider)
    {
        var go = MeshKit.Spawn(name, parent, MeshKit.Box(size, 1f), mat, parent.position, parent.rotation, collider);
        go.transform.localPosition = localCentre - new Vector3(0f, size.y * 0.5f, 0f);
        go.transform.localRotation = Quaternion.identity;
    }

    static void BoxRot(Transform parent, string name, Vector3 size, Vector3 localCentre, Quaternion localRot, Material mat)
    {
        var go = MeshKit.Spawn(name, parent, MeshKit.Box(size, 1f), mat, parent.position, parent.rotation, false);
        go.transform.localRotation = localRot;
        go.transform.localPosition = localCentre - localRot * new Vector3(0f, size.y * 0.5f, 0f);
    }

    static void AddLight(Transform parent, Vector3 position, Color color, float intensity, float range, bool flicker)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(parent, true);
        l.transform.position = position;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker) l.gameObject.AddComponent<FlickerLight>();
    }
}
