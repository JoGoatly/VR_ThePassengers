using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The line 13 bus depot where every night ends: a fenced yard next to the road with a big
/// bus garage (parked buses, workshop), an office building (office with the service PC,
/// locker room, toilet, archive) and a burnt-out bus wreck in the back yard.
/// Lots to find: items, notes and 13 secrets (easter eggs).
/// Clocking out at the office PC ends the night.
/// </summary>
public class Depot : MonoBehaviour
{
    /// <summary>Number of easter eggs in the depot.</summary>
    public const int SecretCount = 13;

    public ForestRoad road;
    public SideAreas side;
    public BoardingManager game;

    [Header("Models")]
    [Tooltip("Bus model for the parked buses and the wreck")]
    public GameObject busModel;

    [Header("Materials (empty = taken from the road / side areas)")]
    public Material asphalt;
    public Material wood, concrete, metal, glow;
    [Tooltip("Screen material (PC monitor, TV)")]
    public Material screenMaterial;

    [Header("Sounds")]
    public AudioClip phoneRing, scare, vanish, coins, pickup, click, lightHum, knock, staticNoise;

    Transform root;
    readonly Dictionary<Material, MeshKit.Builder> geometry = new Dictionary<Material, MeshKit.Builder>();
    Material paint, darkMetal, whiteTile, rust, tint;
    SoundManager sound;

    // Layout (depot space: x along the road, against the driving direction; z away from the road).
    const float GateFrom = 4f, GateTo = 12f;              // gate in the front fence
    const float FenceZ = 5.5f;
    const float HallX0 = -28f, HallX1 = 2f, HallZ0 = 16f, HallZ1 = 44f, HallH = 7f;
    const float OffX0 = 8f, OffX1 = 26f, OffZ0 = 18f, OffZ1 = 34f, OffH = 3.2f;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (side == null) side = FindAnyObjectByType<SideAreas>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        sound = FindAnyObjectByType<SoundManager>();
        if (road != null) road.DepotBuilt += Build;
    }

    void OnDestroy()
    {
        if (road != null) road.DepotBuilt -= Build;
    }

    // ---------------------------------------------------------------- materials

    void PrepareMaterials()
    {
        if (asphalt == null && road != null) asphalt = road.road;
        if (side != null)
        {
            if (wood == null) wood = side.wood;
            if (concrete == null) concrete = side.concrete;
            if (metal == null) metal = side.metal;
            if (glow == null) glow = side.glow;
        }
        if (concrete == null && road != null) concrete = road.concrete;
        if (metal == null && road != null) metal = road.metal;
        if (wood == null && road != null) wood = road.wood;
        paint = Tinted(concrete, new Color(0.62f, 0.66f, 0.6f), "Depot Paint");
        darkMetal = Tinted(metal, new Color(0.35f, 0.36f, 0.38f), "Depot Dark Metal");
        whiteTile = Tinted(concrete, new Color(0.85f, 0.87f, 0.85f), "Depot Tiles");
        rust = Tinted(metal, new Color(0.45f, 0.25f, 0.15f), "Depot Rust");
        tint = Tinted(metal, new Color(0.25f, 0.4f, 0.55f), "Depot Blue");
    }

    static Material Tinted(Material source, Color color, string name)
    {
        if (source == null) return null;
        var m = new Material(source) { name = name };
        if (m.HasProperty("_MainColor")) m.SetColor("_MainColor", color);
        // Large flat walls smear with the PSX texture warping: keep them crisp.
        if (m.HasProperty("_AffineTextureWarpingWeight")) m.SetFloat("_AffineTextureWarpingWeight", 0f);
        return m;
    }

    Material TextMaterial(PixelCanvas canvas, bool glowing)
    {
        var source = glowing ? (screenMaterial != null ? screenMaterial : glow) : concrete;
        if (source == null) return null;
        var m = new Material(source) { name = "Depot Sign" };
        m.SetTexture("_MainTex", canvas.Texture);
        if (m.HasProperty("_MainColor")) m.SetColor("_MainColor", Color.white);
        if (m.HasProperty("_AffineTextureWarpingWeight")) m.SetFloat("_AffineTextureWarpingWeight", 0f);
        return m;
    }

    // ---------------------------------------------------------------- building

    void Build(ForestRoad.DepotSite site)
    {
        PrepareMaterials();
        geometry.Clear();
        root = new GameObject("Depot").transform;
        root.SetParent(site.chunk, true);
        root.SetPositionAndRotation(site.origin, site.rotation);

        BuildYard();
        BuildHall();
        BuildOffice();
        BuildBackYard();
        PlaceItems();
        PlaceDangers();

        // All static geometry: one mesh per material.
        foreach (var kv in geometry)
            if (kv.Key != null)
            {
                var go = MeshKit.Spawn("Depot " + kv.Key.name, root, kv.Value.ToMesh("Depot"), kv.Key, root.position, root.rotation, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
    }

    // ---------------------------------------------------------------- yard

    void BuildYard()
    {
        // Asphalt yard and the path from the road to the gate.
        Solid(new Vector3(0f, 0f, 28f), new Vector3(60f, 0.03f, 45f), asphalt, false);
        Solid(new Vector3((GateFrom + GateTo) * 0.5f, 0f, 2.6f), new Vector3(GateTo - GateFrom, 0.03f, 5.2f), asphalt, false);

        // Fence: posts and rails, a solid (invisible) collider along each side.
        Fence(new Vector3(-30f, 0f, FenceZ), new Vector3(GateFrom, 0f, FenceZ));
        Fence(new Vector3(GateTo, 0f, FenceZ), new Vector3(30f, 0f, FenceZ));
        Fence(new Vector3(-30f, 0f, FenceZ), new Vector3(-30f, 0f, 50f));
        Fence(new Vector3(30f, 0f, FenceZ), new Vector3(30f, 0f, 50f));
        Fence(new Vector3(-30f, 0f, 50f), new Vector3(30f, 0f, 50f));

        // Gate: two big posts, one wing open, the other hanging crooked.
        Solid(new Vector3(GateFrom, 0f, FenceZ), new Vector3(0.3f, 2.6f, 0.3f), darkMetal);
        Solid(new Vector3(GateTo, 0f, FenceZ), new Vector3(0.3f, 2.6f, 0.3f), darkMetal);
        Solid(new Vector3(GateFrom + 0.2f, 0.1f, FenceZ + 2f), new Vector3(0.08f, 1.9f, 4f), darkMetal, true, 8f);
        Solid(new Vector3(GateTo - 0.3f, 0.05f, FenceZ + 1.6f), new Vector3(0.08f, 1.9f, 3.4f), darkMetal, true, -25f);
        // Sign over the entrance.
        Solid(new Vector3(GateFrom, 2.6f, FenceZ), new Vector3(0.2f, 1.4f, 0.2f), darkMetal, false);
        Solid(new Vector3(GateTo, 2.6f, FenceZ), new Vector3(0.2f, 1.4f, 0.2f), darkMetal, false);
        Sign(new Vector3((GateFrom + GateTo) * 0.5f, 3.35f, FenceZ - 0.08f), Vector3.back, 7.6f, 1.1f,
             new[] { Loc.T("BETRIEBSHOF", "BUS DEPOT"), Loc.T("NACHTLINIE 13", "NIGHT LINE 13") }, new Color32(20, 45, 90, 255), new Color32(230, 230, 210, 255), false);

        // Yard lamps.
        YardLamp(new Vector3(-12f, 0f, 10f));
        YardLamp(new Vector3(18f, 0f, 11f));
        YardLamp(new Vector3(-24f, 0f, 47f), 0.4f);

        // Parking lines.
        for (int i = 0; i < 5; i++)
            Solid(new Vector3(-26f + i * 5.5f, 0.031f, 11f), new Vector3(0.15f, 0.005f, 5f), whiteTile, false);

        // Diesel pump at the hall corner.
        var pump = Solid(new Vector3(-2f, 0f, 11f), new Vector3(0.9f, 1.7f, 0.6f), tint);
        Solid(new Vector3(-2f, 1.7f, 11f), new Vector3(1.1f, 0.25f, 0.7f), darkMetal, false);
        Sign(new Vector3(-2f, 1.25f, 10.68f), Vector3.back, 0.6f, 0.3f, new[] { "13,13 L" }, new Color32(10, 20, 10, 255), new Color32(120, 255, 140, 255), true);
        var pumpUse = Interact(new Vector3(-2f, 1f, 10.3f), Loc.T("Zapfsäule ansehen", "Look at the fuel pump"), "Diesel",
            Loc.T("Die Anzeige steht auf 13,13 Liter. Egal wie oft du den Hebel drückst.\nDer Preis pro Liter: \"EINE FAHRT\".",
                  "The display shows 13.13 litres. No matter how often you pull the lever.\nPrice per litre: \"ONE RIDE\"."), "zapfsaeule");
        pumpUse.sound = click;

        // Tyre stacks and an oil drum near the hall.
        TyreStack(new Vector3(4f, 0f, 14f), 4);
        TyreStack(new Vector3(5.2f, 0f, 14.6f), 3);
        Drum(new Vector3(-29f, 0f, 14f), rust);
        Drum(new Vector3(-28.2f, 0f, 14.5f), tint);
    }

    void Fence(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float length = d.magnitude;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        int posts = Mathf.Max(1, Mathf.CeilToInt(length / 3f));
        for (int i = 0; i <= posts; i++)
            Solid(Vector3.Lerp(a, b, i / (float)posts), new Vector3(0.08f, 2.3f, 0.08f), darkMetal, false);
        Vector3 mid = (a + b) * 0.5f;
        foreach (float y in new[] { 0.3f, 1.2f, 2.1f })
            Solid(mid + Vector3.up * y, new Vector3(0.04f, 0.04f, length), darkMetal, false, yaw);
        // Barbed wire on top.
        Solid(mid + Vector3.up * 2.35f, new Vector3(0.02f, 0.02f, length), rust, false, yaw);
        Block(mid, new Vector3(0.3f, 2.6f, length), yaw);
    }

    void YardLamp(Vector3 p, float intensity = 1f)
    {
        Solid(p, new Vector3(0.14f, 6f, 0.14f), darkMetal);
        Solid(p + new Vector3(0f, 6f, 0.5f), new Vector3(0.35f, 0.12f, 1.1f), darkMetal, false);
        if (glow != null) Solid(p + new Vector3(0f, 5.95f, 0.8f), new Vector3(0.25f, 0.05f, 0.4f), glow, false);
        var l = Lamp(p + new Vector3(0f, 5.6f, 0.8f), new Color(1f, 0.7f, 0.4f), 3.2f * intensity, 16f);
        l.gameObject.AddComponent<FlickerLight>().flickerChance = 0.05f;
    }

    // ---------------------------------------------------------------- bus garage

    void BuildHall()
    {
        float x0 = HallX0, x1 = HallX1, z0 = HallZ0, z1 = HallZ1, h = HallH, t = 0.3f;
        float cx = (x0 + x1) * 0.5f, len = x1 - x0, depth = z1 - z0;
        Solid(new Vector3(cx, 0f, (z0 + z1) * 0.5f), new Vector3(len, 0.05f, depth), concrete, false);
        // Walls.
        Solid(new Vector3(cx, 0f, z1), new Vector3(len, h, t), paint);
        Solid(new Vector3(x0, 0f, (z0 + z1) * 0.5f), new Vector3(t, h, depth), paint);
        Solid(new Vector3(x1, 0f, (z0 + z1) * 0.5f), new Vector3(t, h, depth), paint);
        // Front with two big openings (-26..-18 and -14..-6) and a closed roller door (-4..0).
        WallX(z0, x0, x1, h, t, paint, (-26f, -18f, 5f), (-14f, -6f, 5f));
        Solid(new Vector3(-2f, 0f, z0 - 0.05f), new Vector3(4f, 4.2f, 0.12f), darkMetal);   // roller door (closed)
        for (int i = 0; i < 9; i++) Solid(new Vector3(-2f, 0.3f + i * 0.45f, z0 - 0.13f), new Vector3(4f, 0.04f, 0.04f), metal, false);
        // Roof.
        Solid(new Vector3(cx, h, (z0 + z1) * 0.5f), new Vector3(len + 0.6f, 0.25f, depth + 0.6f), darkMetal, false);
        // Roof beams.
        for (float z = z0 + 4f; z < z1; z += 6f)
            Solid(new Vector3(cx, h - 0.6f, z), new Vector3(len, 0.35f, 0.25f), rust, false);

        Sign(new Vector3(-10f, 5.9f, z0 - 0.2f), Vector3.back, 12f, 1.4f, new[] { Loc.T("WAGENHALLE  13", "BUS GARAGE  13") },
             new Color32(160, 150, 130, 255), new Color32(40, 30, 25, 255), false);

        // Floor markings of the bays.
        foreach (float bx in new[] { -26f, -18f, -14f, -6f })
            Solid(new Vector3(bx, 0.051f, 29f), new Vector3(0.15f, 0.005f, 24f), whiteTile, false);

        // Fluorescent tubes, one of them broken, one flickering.
        HallLight(new Vector3(-22f, h - 0.8f, 24f), 0f);
        HallLight(new Vector3(-10f, h - 0.8f, 24f), 0.15f);
        HallLight(new Vector3(-22f, h - 0.8f, 36f), -1f);
        HallLight(new Vector3(-10f, h - 0.8f, 36f), 0.05f);
        HallLight(new Vector3(-2f, h - 0.8f, 30f), 0f);
        if (lightHum != null) Loop(new Vector3(-12f, 5f, 30f), lightHum, 0.12f, 18f);

        // Parked buses.
        ParkedBus(new Vector3(-22f, 0f, 28f), 180f, new Color(0.9f, 0.9f, 0.9f));
        ParkedBus(new Vector3(-10f, 0.9f, 30f), 180f, new Color(0.75f, 0.8f, 0.85f));
        // The second one stands on a lift.
        foreach (float lz in new[] { 24f, 36f })
        foreach (float lx in new[] { -11.6f, -8.4f })
            Solid(new Vector3(lx, 0f, lz), new Vector3(0.3f, 0.9f, 0.5f), tint);
        Interact(new Vector3(-10f, 1f, 22.5f), Loc.T("Bus ansehen", "Look at the bus"), Loc.T("Wagen 12", "Bus 12"),
            Loc.T("Auf der Hebebühne. Im Fahrtenbuch hinter der Scheibe steht als letzter Eintrag:\n\"Waldfriedhof. 13 eingestiegen. 0 ausgestiegen.\"",
                  "On the lift. In the log book behind the windscreen the last entry reads:\n\"Waldfriedhof. 13 got on. 0 got off.\""));

        // Workshop corner: bench, tool board, shelves with tyres, drums.
        Solid(new Vector3(0.6f, 0f, 21.5f), new Vector3(1.2f, 0.95f, 3f), wood);
        Solid(new Vector3(1.75f, 1.2f, 21.5f), new Vector3(0.05f, 1.2f, 3f), wood, false);
        for (int i = 0; i < 6; i++) Solid(new Vector3(1.7f, 1.4f + (i % 2) * 0.4f, 20.4f + i * 0.4f), new Vector3(0.05f, 0.3f, 0.06f), metal, false);
        Solid(new Vector3(0.6f, 0.95f, 21f), new Vector3(0.35f, 0.15f, 0.25f), rust, false);
        Shelf(new Vector3(0.9f, 0f, 27f), 90f, 3.2f);
        Shelf(new Vector3(0.9f, 0f, 31f), 90f, 3.2f);
        for (int i = 0; i < 3; i++) TyreStack(new Vector3(0.9f, 0.05f + i * 0.9f, 26f + i * 0.6f), 1);
        Drum(new Vector3(-26.5f, 0f, 41.5f), rust);
        Drum(new Vector3(-25.6f, 0f, 42.2f), rust);
        Drum(new Vector3(-26.8f, 0f, 42.6f), tint);

        // Lost & found cage.
        Solid(new Vector3(-3f, 0f, 41f), new Vector3(3f, 2.2f, 0.06f), darkMetal);
        Solid(new Vector3(-4.5f, 0f, 42.5f), new Vector3(0.06f, 2.2f, 3f), darkMetal);
        Solid(new Vector3(-2.2f, 0f, 42.3f), new Vector3(1.2f, 0.6f, 0.8f), wood);
        Sign(new Vector3(-3f, 1.7f, 40.95f), Vector3.back, 1.4f, 0.35f, new[] { Loc.T("FUNDSACHEN", "LOST+FOUND") }, new Color32(200, 190, 150, 255), new Color32(30, 25, 20, 255), false);
        Interact(new Vector3(-2.2f, 0.8f, 41.7f), Loc.T("Fundkiste durchsuchen", "Search the lost and found box"), Loc.T("Fundsachen", "Lost and found"),
            Loc.T("Ein Regenschirm. Ein einzelner Kinderschuh. Ein Gebiss. 23 Fahrkarten, alle für dieselbe Fahrt:\nLinie 13, Waldfriedhof, 03:13 Uhr.\nUnd ein Namensschild: \"Horst\".",
                  "An umbrella. A single child's shoe. False teeth. 23 tickets, all for the same trip:\nLine 13, Waldfriedhof, 03:13.\nAnd a name tag: \"Horst\"."));

        // Easter egg: someone standing in the dark corner, facing the wall. Gone when you come close.
        var models = game != null && game.malePassengers != null && game.malePassengers.Length > 0 ? game.malePassengers : null;
        if (models != null)
        {
            var who = Instantiate(models[Random.Range(0, models.Length)], root);
            who.name = "Waiting Man";
            who.transform.localPosition = new Vector3(-27.2f, 0.05f, 43.2f);
            who.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);   // facing the back wall
            who.AddComponent<Passenger>();
            var trigger = who.AddComponent<ProximityTrigger>();
            trigger.radius = 4f;
            trigger.Triggered = trig =>
            {
                Play(vanish, trig.transform.position, 1f);
                bool first = Progress.FoundSecret("wartender");
                Progress.Save();
                if (game != null)
                    game.ShowToast(first ? Loc.T($"Da stand doch jemand...  (Geheimnis {Progress.SecretsFound}/{SecretCount})", $"Someone was standing there...  (secret {Progress.SecretsFound}/{SecretCount})")
                                         : Loc.T("Da stand doch jemand...", "Someone was standing there..."));
                Destroy(trig.gameObject);
            };
        }
    }

    void HallLight(Vector3 p, float flicker)
    {
        Solid(p + Vector3.up * 0.1f, new Vector3(0.3f, 0.08f, 1.6f), metal, false);
        if (flicker >= 0f && glow != null) Solid(p, new Vector3(0.14f, 0.05f, 1.4f), glow, false);
        if (flicker < 0f) return;   // broken
        var l = Lamp(p - Vector3.up * 0.4f, new Color(0.8f, 0.9f, 1f), 2.4f, 13f);
        if (flicker > 0f)
        {
            var f = l.gameObject.AddComponent<FlickerLight>();
            f.flickerChance = flicker;
        }
    }

    void ParkedBus(Vector3 p, float yaw, Color color, float tilt = 0f, bool wreck = false)
    {
        if (busModel == null)
        {
            // Without the model: a simple box bus.
            Solid(p, new Vector3(2.5f, 3f, 11.5f), wreck ? rust : tint, true, yaw);
            return;
        }
        var holder = new GameObject(wreck ? "Wreck" : "Parked Bus").transform;
        holder.SetParent(root, false);
        holder.localPosition = p;
        holder.localRotation = Quaternion.Euler(0f, yaw, tilt);
        var bus = Instantiate(busModel, holder);
        bus.transform.localPosition = Vector3.zero;
        bus.transform.localRotation = Quaternion.identity;
        foreach (var c in bus.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var mb in bus.GetComponentsInChildren<MonoBehaviour>()) Destroy(mb);

        // Fit: longest side along z, about 11.5 m long, standing on the ground.
        var bounds = LocalBounds(bus.transform, holder);
        if (bounds.size.x > bounds.size.z)
        {
            bus.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            bounds = LocalBounds(bus.transform, holder);
        }
        float scale = bounds.size.z > 0.01f ? 11.5f / bounds.size.z : 1f;
        bus.transform.localScale *= scale;
        bounds = LocalBounds(bus.transform, holder);
        bus.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        foreach (var r in bus.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials;
            foreach (var m in mats)
                if (m.HasProperty("_MainColor")) m.SetColor("_MainColor", m.GetColor("_MainColor") * color);
            r.materials = mats;
        }
        var col = holder.gameObject.AddComponent<BoxCollider>();
        bounds = LocalBounds(bus.transform, holder);
        col.center = bounds.center;
        col.size = bounds.size;
    }

    static Bounds LocalBounds(Transform model, Transform space)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            Bounds lb;
            Transform t = r.transform;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) lb = smr.sharedMesh.bounds;
            else if (mf != null && mf.sharedMesh != null) lb = mf.sharedMesh.bounds;
            else continue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(t.TransformPoint(corner));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ---------------------------------------------------------------- office building

    void BuildOffice()
    {
        float x0 = OffX0, x1 = OffX1, z0 = OffZ0, z1 = OffZ1, h = OffH, t = 0.2f;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        Solid(new Vector3(cx, 0f, cz), new Vector3(x1 - x0, 0.1f, z1 - z0), wood, false);
        Solid(new Vector3(cx, h, cz), new Vector3(x1 - x0 + 0.4f, 0.2f, z1 - z0 + 0.4f), darkMetal, false);
        // Outer walls, the front with the entrance door (x 11..12.4) and two windows.
        WallX(z0, x0, x1, h, t, paint, (11f, 12.4f, 2.1f));
        WallZ(x0, z0, z1, h, t, paint);
        WallZ(x1, z0, z1, h, t, paint);
        WallX(z1, x0, x1, h, t, paint);
        foreach (float wx in new[] { 14.5f, 21.5f })
            Solid(new Vector3(wx, 1.1f, z0 - 0.02f), new Vector3(1.6f, 1f, 0.26f), darkMetal, false);   // dark, dirty windows
        Sign(new Vector3(11.7f, 2.45f, z0 - 0.12f), Vector3.back, 1.4f, 0.4f, new[] { Loc.T("BÜRO", "OFFICE") }, new Color32(200, 200, 190, 255), new Color32(20, 30, 60, 255), false);
        // Light over the door.
        if (glow != null) Solid(new Vector3(11.7f, 2.75f, z0 - 0.25f), new Vector3(0.3f, 0.1f, 0.2f), glow, false);
        Lamp(new Vector3(11.7f, 2.6f, z0 - 0.8f), new Color(1f, 0.8f, 0.55f), 1.6f, 7f).gameObject.AddComponent<FlickerLight>();

        // Inner walls: office | locker room (door z 20..21.2), front rooms | archive + toilet.
        WallZ(17f, z0, 28f, h, 0.15f, paint, (20f, 21.2f, 2.1f));
        WallX(28f, x0, x1, h, 0.15f, paint, (9f, 10.2f, 2.1f), (24.8f, 25.8f, 2.1f));
        WallZ(20f, 28f, z1, h, 0.15f, whiteTile);

        BuildOfficeRoom();
        BuildLockerRoom();
        BuildToilet();
        BuildArchive();
    }

    void BuildOfficeRoom()
    {
        Lamp(new Vector3(12.5f, 2.8f, 23f), new Color(1f, 0.92f, 0.75f), 1.8f, 8f).gameObject.AddComponent<FlickerLight>().flickerChance = 0.03f;
        if (glow != null) Solid(new Vector3(12.5f, 3.02f, 23f), new Vector3(0.9f, 0.06f, 0.25f), glow, false);

        // Desk with the service PC.
        Solid(new Vector3(13f, 0.1f, 26.6f), new Vector3(2.2f, 0.75f, 0.9f), wood);
        Solid(new Vector3(13f, 0.1f, 25.4f), new Vector3(0.5f, 0.5f, 0.5f), darkMetal);   // chair
        Solid(new Vector3(13f, 0.6f, 25.2f), new Vector3(0.5f, 0.6f, 0.08f), darkMetal, false);
        Solid(new Vector3(12.8f, 0.85f, 26.8f), new Vector3(0.55f, 0.45f, 0.45f), whiteTile, false);   // monitor case
        var screenCanvas = new PixelCanvas(64, 48);
        screenCanvas.Clear(new Color32(6, 20, 8, 255));
        screenCanvas.Text(3, 3, "BH-13", new Color32(120, 255, 140, 255));
        screenCanvas.Text(3, 17, Loc.T("DIENST", "SHIFT"), new Color32(80, 190, 100, 255));
        screenCanvas.Text(3, 31, "> _", new Color32(120, 255, 140, 255));
        screenCanvas.Apply();
        SignQuad(new Vector3(12.8f, 1.08f, 26.565f), Vector3.back, 0.44f, 0.33f, TextMaterial(screenCanvas, true));
        Solid(new Vector3(12.8f, 0.85f, 26.3f), new Vector3(0.5f, 0.03f, 0.18f), whiteTile, false);   // keyboard
        var pc = new GameObject("Service PC");
        pc.transform.SetParent(root, false);
        pc.transform.localPosition = new Vector3(12.8f, 0.9f, 26f);
        var computer = pc.AddComponent<DepotComputer>();
        computer.radius = 1.6f;
        computer.clickSound = click;
        Lamp(new Vector3(12.8f, 1.1f, 26.2f), new Color(0.4f, 1f, 0.5f), 0.6f, 2.5f);

        // Desk phone: starts ringing when you come in.
        Solid(new Vector3(13.8f, 0.85f, 26.6f), new Vector3(0.25f, 0.1f, 0.2f), darkMetal, false);
        var phone = Interact(new Vector3(13.8f, 1f, 26.4f), Loc.T("Telefon", "Phone"), Loc.T("Telefon", "Phone"), null, "telefon");
        AudioSource ringing = null;
        phone.Action = _ =>
        {
            if (ringing != null) Destroy(ringing);
            ringing = null;
            phone.promptDe = "Telefon"; phone.promptEn = "Phone";
            return PhoneCall();
        };
        var ringTrigger = new GameObject("Phone Trigger").AddComponent<ProximityTrigger>();
        ringTrigger.transform.SetParent(root, false);
        ringTrigger.transform.localPosition = new Vector3(11.7f, 0f, 19f);
        ringTrigger.radius = 2.5f;
        ringTrigger.Triggered = _ =>
        {
            if (phoneRing == null || phone == null) return;
            ringing = phone.gameObject.AddComponent<AudioSource>();
            ringing.clip = phoneRing;
            ringing.loop = true;
            ringing.spatialBlend = 1f;
            ringing.minDistance = 1.5f;
            ringing.maxDistance = 25f;
            ringing.volume = 0.5f * GameSettings.Effects;
            ringing.Play();
            phone.promptDe = "Abheben"; phone.promptEn = "Answer";
        };

        // Filing cabinets: personnel files.
        Solid(new Vector3(8.5f, 0.1f, 26.8f), new Vector3(0.6f, 1.4f, 0.6f), darkMetal);
        Solid(new Vector3(8.5f, 0.1f, 25.9f), new Vector3(0.6f, 1.4f, 0.6f), darkMetal);
        Interact(new Vector3(8.9f, 1f, 26.3f), Loc.T("Personalakten lesen", "Read the personnel files"), Loc.T("Personalakten", "Personnel files"))
            .Action = _ => PersonnelFiles();

        // Coffee machine on a counter.
        Solid(new Vector3(16.4f, 0.1f, 19.2f), new Vector3(0.8f, 0.9f, 1.6f), wood);
        Solid(new Vector3(16.4f, 1f, 18.9f), new Vector3(0.4f, 0.55f, 0.4f), darkMetal, false);
        var coffee = Interact(new Vector3(16.1f, 1.1f, 18.9f), Loc.T("Kaffee machen", "Make coffee"), Loc.T("Kaffeemaschine", "Coffee machine"), null, "kaffee");
        coffee.sound = click;
        coffee.Action = _ =>
        {
            float minutes = game != null ? game.GameMinutes : 0f;
            bool afterMidnight = minutes >= 24 * 60 || minutes < 5 * 60;
            if (PlayerCombat.Instance != null) PlayerCombat.Instance.Heal(afterMidnight ? 10 : 25);
            return afterMidnight
                ? Loc.T("Die Maschine gurgelt lange. Heraus kommt etwas Dunkles, Dickes.\nEs ist warm. Es ist kein Kaffee. Du trinkst es trotzdem.",
                        "The machine gurgles for a long time. Out comes something dark and thick.\nIt is warm. It is not coffee. You drink it anyway.")
                : Loc.T("Kaffee. Schmeckt nach Diesel und alten Nächten. Du fühlst dich etwas besser.",
                        "Coffee. Tastes of diesel and old nights. You feel a bit better.");
        };

        // Wall calendar and whiteboard.
        Sign(new Vector3(8.12f, 1.6f, 22f), Vector3.right, 0.7f, 0.9f,
             new[] { Loc.T("OKTOBER", "OCTOBER"), "1994", "X X X X X", "X X X X X", "X X X 13" }, new Color32(220, 215, 200, 255), new Color32(120, 20, 20, 255), false);
        Interact(new Vector3(8.6f, 1.4f, 22f), Loc.T("Kalender ansehen", "Look at the calendar"), Loc.T("Wandkalender", "Wall calendar"),
            Loc.T("Oktober 1994. Jeder Tag ist durchgestrichen, bis zum 13. Danach nichts mehr.\nAuf dem 13. steht in winziger Schrift: \"heute\".",
                  "October 1994. Every day is crossed out up to the 13th. Nothing after that.\nOn the 13th, in tiny writing: \"today\"."), "kalender");
        Sign(new Vector3(11f, 1.6f, 27.9f), Vector3.back, 2.2f, 1.1f,
             new[] { Loc.T("TAGE OHNE UNFALL:", "DAYS WITHOUT ACCIDENT:"), "0", Loc.T("DIENSTPLAN: WEBER HAHN", "ROSTER: WEBER HAHN"), "BRAUN KELLER ???" }, new Color32(235, 235, 230, 255), new Color32(30, 40, 120, 255), false);

        // Group photo of the drivers.
        Sign(new Vector3(16.9f, 1.7f, 25f), Vector3.left, 0.9f, 0.6f, new[] { "LINIE 13", "1994", "x x x x x" }, new Color32(90, 80, 60, 255), new Color32(220, 210, 180, 255), false);
        Interact(new Vector3(16.4f, 1.5f, 25f), Loc.T("Foto ansehen", "Look at the photo"), Loc.T("Gruppenfoto", "Group photo"),
            Loc.T("Die Fahrer der Linie 13, 1994. Fünf Männer vor einem Bus. Die Gesichter sind zerkratzt.\nNur eines nicht, ganz rechts. Das Gesicht ist deins.",
                  "The drivers of line 13, 1994. Five men in front of a bus. The faces are scratched out.\nAll but one, on the far right. The face is yours."), "foto");
    }

    string PhoneCall()
    {
        return Progress.Day switch
        {
            1 => Loc.T("Rauschen. Dann eine Frauenstimme, sehr leise:\n\"Ist er schon da? ... Sag ihm, er soll nicht in den Keller gehen.\"\nAufgelegt.",
                       "Static. Then a woman's voice, very quiet:\n\"Is he there yet? ... Tell him not to go into the cellar.\"\nShe hangs up."),
            2 => Loc.T("\"Leitstelle. Wir sehen, dass Sie noch im Depot sind. Stempeln Sie aus.\"\nIm Hintergrund zählt jemand: \"...elf, zwölf, dreizehn.\"",
                       "\"Dispatch. We see you are still at the depot. Clock out.\"\nIn the background someone counts: \"...eleven, twelve, thirteen.\""),
            3 => Loc.T("Nur Atmen. Dann, ganz nah am Hörer:\n\"Du sitzt auf meinem Platz.\"",
                       "Only breathing. Then, very close to the receiver:\n\"You're sitting in my seat.\""),
            4 => Loc.T("\"Hier ist Horst. Ich bin nicht krank. Ich bin im Archiv. Ich komm hier nicht raus.\"",
                       "\"This is Horst. I'm not sick. I'm in the archive. I can't get out.\""),
            5 => Loc.T("Eine Kinderstimme: \"Papa? Fährst du heute wieder die 13?\"",
                       "A child's voice: \"Daddy? Are you driving the 13 again tonight?\""),
            6 => Loc.T("Deine eigene Stimme, vom Band:\n\"...wenn du das hörst, bin ich schon im Wald.\"",
                       "Your own voice, from a tape:\n\"...if you hear this, I'm already in the forest.\""),
            _ => Loc.T("\"Letzte Nacht. Weiterfahren. Nicht anhalten. Nicht ausstempeln.\" - J.K.",
                       "\"Last night. Keep driving. Don't stop. Don't clock out.\" - J.K."),
        };
    }

    string PersonnelFiles()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Story.Drivers.Length; i++)
        {
            var d = Story.Drivers[i];
            sb.AppendLine($"{d.name}  -  {d.badge}");
            sb.AppendLine(Progress.Data.drivers.Contains(i) ? Loc.T("  Status: gefunden. Akte geschlossen.", "  Status: found. File closed.")
                                                            : Loc.T("  Status: vermisst. Ersatz eingestellt.", "  Status: missing. Replacement hired."));
        }
        sb.AppendLine(Loc.T("\nDie letzte Akte trägt deinen Namen. Eintrittsdatum: morgen.",
                            "\nThe last file has your name on it. Start date: tomorrow."));
        return sb.ToString();
    }

    void BuildLockerRoom()
    {
        Lamp(new Vector3(21.5f, 2.8f, 23f), new Color(0.85f, 0.95f, 1f), 1.5f, 8f).gameObject.AddComponent<FlickerLight>().flickerChance = 0.1f;
        if (glow != null) Solid(new Vector3(21.5f, 3.02f, 23f), new Vector3(0.9f, 0.06f, 0.25f), glow, false);

        // Six lockers along the back wall.
        string[] names = { "WEBER", "HAHN", "BRAUN", "KELLER", "HORST", Loc.T("NEU", "NEW") };
        string[][] texts =
        {
            new[] { "Eine Thermoskanne. Sie ist noch warm. Auf dem Deckel eingeritzt: 1989.", "A thermos flask. It is still warm. Scratched into the lid: 1989." },
            new[] { "Ein Hochzeitsfoto. Auf allen anderen Fotos, die du je gesehen hast, schaut die Braut weg.\nAuf diesem schaut sie dich an.", "A wedding photo. On every other photo you have ever seen, the bride looks away.\nOn this one she looks at you." },
            new[] { "Eine Namensliste, eng beschrieben. Die meisten Namen sind durchgestrichen.\nDer letzte ist deiner. Noch nicht durchgestrichen.", "A list of names, tightly written. Most are crossed out.\nThe last one is yours. Not crossed out yet." },
            new[] { "Eine Straßenkarte. Hinter dem Betriebshof hat jemand mit Kuli eine Straße eingezeichnet, die es nicht gibt.\nDaneben steht: WEITER.", "A road map. Behind the depot someone drew a road in ballpoint that doesn't exist.\nNext to it: KEEP GOING." },
            new[] { "Horsts Jacke hängt noch drin. Die Taschen sind voller Fahrkarten, alle am selben Tag gestempelt.\nIn der Innentasche: etwas Geld.", "Horst's jacket is still in there. The pockets are full of tickets, all stamped on the same day.\nIn the inside pocket: some money." },
            new[] { "Dein Spind. Dein Name steht drauf, in derselben Handschrift wie auf den anderen.\nDrin liegt eine Uniform in deiner Größe. Und ein Verbandskasten, \"für später\".", "Your locker. Your name is on it, in the same handwriting as on the others.\nInside: a uniform in your size. And a first aid kit, \"for later\"." },
        };
        for (int i = 0; i < names.Length; i++)
        {
            float x = 18f + i * 1.05f;
            Solid(new Vector3(x, 0.1f, 27.6f), new Vector3(0.95f, 2f, 0.55f), i == 5 ? tint : darkMetal);
            Solid(new Vector3(x - 0.3f, 1.2f, 27.31f), new Vector3(0.04f, 0.2f, 0.02f), metal, false);
            Sign(new Vector3(x, 1.8f, 27.31f), Vector3.back, 0.8f, 0.18f, new[] { names[i] }, new Color32(210, 205, 190, 255), new Color32(25, 25, 25, 255), false);
            var locker = Interact(new Vector3(x, 1f, 27f), Loc.T("Spind öffnen", "Open the locker"), Loc.T("Spind ", "Locker ") + names[i],
                                  Loc.T(texts[i][0], texts[i][1]), i == 5 ? "spind" : null, 0.9f);
            locker.sound = knock;
            int index = i;
            bool looted = false;
            locker.Action = _ =>
            {
                if (looted) return null;
                looted = true;
                if (index == 4) { int money = 15 + Progress.Day * 5; Progress.AddMoney(money); Progress.ShiftFound += money; Play(coins, locker.transform.position, 0.9f); game?.ShowToast($"+{money} €"); }
                if (index == 5) { Progress.AddMedkits(1); Play(pickup, locker.transform.position, 0.9f); game?.ShowToast(Loc.T("+1 Verbandskasten", "+1 first aid kit")); }
                Progress.Save();
                return null;
            };
        }
        // Bench.
        Solid(new Vector3(21f, 0.1f, 25.4f), new Vector3(4f, 0.4f, 0.4f), wood);

        // Vending machine.
        Solid(new Vector3(25.4f, 0.1f, 20.5f), new Vector3(0.8f, 1.9f, 1f), Tinted(metal, new Color(0.6f, 0.1f, 0.1f), "Vending"));
        Sign(new Vector3(24.98f, 1.5f, 20.5f), Vector3.left, 0.8f, 0.7f, new[] { "SNACKS", "2 EUR", "A1 A2 A3", "B1 B2 13" }, new Color32(20, 20, 30, 255), new Color32(255, 220, 120, 255), true);
        var vending = Interact(new Vector3(24.8f, 1f, 20.5f), Loc.T("Snack ziehen (2 €)", "Buy a snack (2 €)"), Loc.T("Snackautomat", "Vending machine"));
        vending.sound = click;
        vending.Action = _ =>
        {
            if (Progress.Money < 2) return Loc.T("Nicht genug Geld.", "Not enough money.");
            Progress.AddMoney(-2);
            if (PlayerCombat.Instance != null) PlayerCombat.Instance.Heal(15);
            if (Random.value < 0.25f)
            {
                bool first = Progress.FoundSecret("automat");
                Progress.Save();
                return Loc.T("Es fällt kein Snack heraus. Sondern ein Fahrerausweis.\n\"VBN 1313 - LINIE 13 - SEIT MORGEN\". Das Foto darauf zeigt dich, schlafend.",
                             "No snack falls out. A driver's badge does.\n\"VBN 1313 - LINE 13 - SINCE TOMORROW\". The photo on it shows you, asleep.") +
                       (first ? Loc.T($"\n\n(Geheimnis gefunden: {Progress.SecretsFound} / {SecretCount})", $"\n\n(Secret found: {Progress.SecretsFound} / {SecretCount})") : "");
            }
            Progress.Save();
            string[] snacks = { Loc.T("Ein Schokoriegel, abgelaufen 1995. Schmeckt trotzdem.", "A chocolate bar, best before 1995. Tastes fine anyway."),
                                Loc.T("Eine Tüte Chips. Sie ist schon offen.", "A bag of crisps. It is already open."),
                                Loc.T("Kaugummi. Jemand hat schon draufgekaut.", "Chewing gum. Someone already chewed it.") };
            return snacks[Random.Range(0, snacks.Length)];
        };

        // Old TV on a wall shelf, showing static.
        Solid(new Vector3(19f, 1.6f, 18.35f), new Vector3(1f, 0.05f, 0.5f), wood, false);
        Solid(new Vector3(19f, 1.65f, 18.4f), new Vector3(0.7f, 0.55f, 0.5f), darkMetal, false);
        var tvCanvas = new PixelCanvas(32, 24);
        var noise = new System.Random(13);
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 32; x++)
        {
            byte g = (byte)noise.Next(40, 200);
            tvCanvas.Pixel(x, y, new Color32(g, g, g, 255));
        }
        tvCanvas.Apply();
        SignQuad(new Vector3(19f, 1.92f, 18.66f), Vector3.forward, 0.52f, 0.4f, TextMaterial(tvCanvas, true));
        var tvLight = Lamp(new Vector3(19f, 1.9f, 19.2f), new Color(0.7f, 0.8f, 1f), 0.9f, 4f);
        tvLight.gameObject.AddComponent<FlickerLight>().flickerChance = 0.4f;
        if (staticNoise != null) Loop(new Vector3(19f, 1.9f, 18.8f), staticNoise, 0.08f, 6f);
        var tv = Interact(new Vector3(19f, 1.4f, 19.3f), Loc.T("Fernseher ansehen", "Watch the TV"), Loc.T("Fernseher", "TV"), null, "fernseher");
        tv.Action = _ => Loc.T("Rauschen. Dann, für einen Moment, ein Bild: der Betriebshof, von oben.\nJemand steht vor dem Fernseher. Von hinten. Er trägt deine Jacke.\nRauschen.",
                               "Static. Then, for a moment, a picture: the depot from above.\nSomeone stands in front of the TV. From behind. He is wearing your jacket.\nStatic.");
    }

    void BuildToilet()
    {
        var l = Lamp(new Vector3(23f, 2.8f, 31f), new Color(0.9f, 1f, 0.9f), 1.2f, 6f);
        l.gameObject.AddComponent<FlickerLight>().flickerChance = 0.3f;
        Solid(new Vector3(23f, 0.1f, 31f), new Vector3(5.9f, 0.02f, 5.9f), whiteTile, false);
        // Sink and mirror.
        Solid(new Vector3(25.6f, 0.1f, 29.6f), new Vector3(0.5f, 0.85f, 0.6f), whiteTile);
        Solid(new Vector3(25.88f, 1.2f, 29.6f), new Vector3(0.04f, 0.8f, 0.6f), metal, false);
        Interact(new Vector3(25.3f, 1.4f, 29.6f), Loc.T("In den Spiegel sehen", "Look into the mirror"), Loc.T("Spiegel", "Mirror"), null, "spiegel")
            .Action = self =>
            {
                Play(scare, self.transform.position, 1f);
                return Loc.T("Du siehst dich. Müde. Blass.\nHinter dir, in der Kabine, steht jemand und sieht dich über deine Schulter an.\nDu drehst dich um. Die Kabine ist leer.",
                             "You see yourself. Tired. Pale.\nBehind you, in the stall, someone stands and looks at you over your shoulder.\nYou turn around. The stall is empty.");
            };
        // Stall with the rubber duck.
        Solid(new Vector3(21.3f, 0.1f, 31f), new Vector3(0.06f, 2f, 0.06f), whiteTile);
        Solid(new Vector3(21.3f, 0.1f, 32.5f), new Vector3(0.06f, 2f, 3f), whiteTile);
        Solid(new Vector3(20.7f, 0.1f, 33.3f), new Vector3(0.45f, 0.45f, 0.6f), whiteTile);
        Solid(new Vector3(20.7f, 0.55f, 33.35f), new Vector3(0.12f, 0.1f, 0.1f), Tinted(concrete, new Color(1f, 0.85f, 0.1f), "Duck"), false);
        Interact(new Vector3(20.8f, 0.7f, 33f), Loc.T("Was ist das?", "What is that?"), Loc.T("Quietscheente", "Rubber duck"),
            Loc.T("Eine gelbe Quietscheente. Auf der Unterseite steht mit Edding: \"HORST\".\nSie quietscht nicht. Sie flüstert.", "A yellow rubber duck. Written underneath in marker: \"HORST\".\nIt doesn't squeak. It whispers."), "ente", 1.2f);
        Sign(new Vector3(25.3f, 2.4f, 27.9f), Vector3.back, 0.6f, 0.3f, new[] { "WC" }, new Color32(200, 200, 200, 255), new Color32(20, 20, 20, 255), false);
    }

    void BuildArchive()
    {
        // No light in the archive: bring the flashlight.
        Sign(new Vector3(9.6f, 2.4f, 27.9f), Vector3.back, 1.6f, 0.4f, new[] { Loc.T("ARCHIV", "ARCHIVE"), Loc.T("ZUTRITT VERBOTEN", "NO ENTRY") }, new Color32(170, 20, 20, 255), new Color32(240, 230, 220, 255), false);
        for (int i = 0; i < 4; i++) Shelf(new Vector3(11.5f + i * 2.2f, 0.1f, 33.5f), 0f, 1.8f);
        Shelf(new Vector3(8.5f, 0.1f, 31f), 90f, 3f);
        for (int i = 0; i < 12; i++)
            Solid(new Vector3(10.9f + (i % 6) * 1.25f, 0.4f + (i / 6) * 0.8f, 33.5f), new Vector3(0.5f, 0.35f, 0.4f), wood, false);   // boxes
        // Tape recorder on a small table.
        Solid(new Vector3(14f, 0.1f, 30.5f), new Vector3(1.2f, 0.75f, 0.7f), wood);
        Solid(new Vector3(14f, 0.85f, 30.5f), new Vector3(0.4f, 0.12f, 0.25f), darkMetal, false);
        var tape = Interact(new Vector3(14f, 1f, 30.1f), Loc.T("Tonband abspielen", "Play the tape"), Loc.T("Tonband", "Tape"),
            Loc.T("\"Protokoll, 13. Oktober 1994. Der Fahrer der Linie 13 gibt an, er habe dreizehn Fahrgäste befördert.\nDer Bus war leer, als er am Waldfriedhof gefunden wurde. Der Fahrer auch.\nWir haben ihn trotzdem wieder eingestellt. Wir stellen sie immer wieder ein.\"",
                  "\"Record, 13 October 1994. The driver of line 13 states he carried thirteen passengers.\nThe bus was empty when it was found at the Waldfriedhof. So was the driver's seat.\nWe hired him again anyway. We always hire them again.\""), "tonband");
        tape.sound = staticNoise;
    }

    // ---------------------------------------------------------------- back yard

    void BuildBackYard()
    {
        // The burnt-out bus that was never scrapped.
        ParkedBus(new Vector3(17f, 0f, 42f), 78f, new Color(0.22f, 0.18f, 0.15f), 4f, true);
        Interact(new Vector3(17f, 1f, 38.6f), Loc.T("Wrack ansehen", "Look at the wreck"), Loc.T("Wagen 13", "Bus 13"),
            Loc.T("Ein ausgebrannter Bus. Auf dem verrußten Nummernschild: 13-1994.\nDrinnen stehen noch alle Sitze. Auf jedem ist der Gurt geschlossen.\nDreizehn Gurte. Der Fahrersitz ist sauber.",
                  "A burnt-out bus. On the sooty plate: 13-1994.\nAll the seats are still inside. Every seat belt is buckled.\nThirteen belts. The driver's seat is clean."), "wrack", 3f);
        TyreStack(new Vector3(24f, 0f, 47f), 5);
        TyreStack(new Vector3(25.2f, 0f, 46.2f), 2);
        Drum(new Vector3(9f, 0f, 47.5f), rust);
    }

    // ---------------------------------------------------------------- items and dangers

    void PlaceItems()
    {
        Vector3[] spots =
        {
            new Vector3(0.6f, 1.07f, 22.5f),    // workbench
            new Vector3(-2.2f, 0.62f, 42.3f),   // lost & found box
            new Vector3(-24f, 0.06f, 38f),      // hall floor
            new Vector3(13.6f, 0.86f, 26.7f),   // office desk
            new Vector3(16.4f, 1.01f, 19.8f),   // coffee counter
            new Vector3(21f, 0.51f, 25.4f),     // bench
            new Vector3(15f, 0.12f, 32.5f),     // archive
            new Vector3(11f, 0.12f, 29f),       // archive
            new Vector3(19f, 0.06f, 39f),       // back yard
        };
        int count = 4 + Random.Range(0, 3);
        var used = new HashSet<int>();
        for (int n = 0; n < count && used.Count < spots.Length; n++)
        {
            int i;
            do i = Random.Range(0, spots.Length); while (used.Contains(i));
            used.Add(i);
            Vector3 pos = root.TransformPoint(spots[i]);
            float r = Random.value;
            int note = NextNote();
            if (n == 0 && note >= 0) Pickup.Spawn(Pickup.Kind.Note, 0, note, pos, root, concrete, glow);
            else if (r < 0.3f) Pickup.Spawn(Pickup.Kind.Ammo, Random.Range(3, 7), 0, pos, root, metal, glow);
            else if (r < 0.55f) Pickup.Spawn(Pickup.Kind.Medkit, 1, 0, pos, root, concrete, glow);
            else Pickup.Spawn(Pickup.Kind.Money, Random.Range(10, 30) + Progress.Day * 5, 0, pos, root, wood, glow);
        }
    }

    static int NextNote()
    {
        for (int i = 0; i < Story.Notes.Length; i++)
            if (!Progress.Data.notes.Contains(i)) return i;
        return -1;
    }

    void PlaceDangers()
    {
        if (side == null) return;
        // From night 3 someone may be waiting in the archive, later also in the hall.
        if (Progress.Day >= 3 && Random.value < 0.25f + 0.08f * Progress.Day)
            side.SpawnDweller(root.TransformPoint(new Vector3(17.5f, 0.1f, 31.5f)), root);
        if (Progress.Day >= 5 && Random.value < 0.5f)
            side.SpawnDweller(root.TransformPoint(new Vector3(-16f, 0.1f, 42f)), root);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A box (pivot at the bottom centre, depot space), merged into the static geometry.</summary>
    GameObject Solid(Vector3 bottomCentre, Vector3 size, Material mat, bool collider = true, float yaw = 0f)
    {
        if (mat != null)
        {
            if (!geometry.TryGetValue(mat, out var mb)) geometry[mat] = mb = new MeshKit.Builder();
            AddBox(mb, Matrix4x4.TRS(bottomCentre, Quaternion.Euler(0f, yaw, 0f), Vector3.one), size);
        }
        return collider ? Block(bottomCentre, size, yaw) : null;
    }

    GameObject Block(Vector3 bottomCentre, Vector3 size, float yaw)
    {
        var go = new GameObject("Collider");
        go.transform.SetParent(root, false);
        go.transform.localPosition = bottomCentre;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        box.size = size;
        return go;
    }

    // Box made of tiles no bigger than ~3 m, so big walls are lit properly and not culled.
    static void AddBox(MeshKit.Builder mb, Matrix4x4 m, Vector3 size)
    {
        float hx = size.x * 0.5f, hz = size.z * 0.5f, h = size.y;
        Face(mb, m, new Vector3(-hx, 0, -hz), Vector3.right, Vector3.up, size.x, h);
        Face(mb, m, new Vector3(hx, 0, hz), Vector3.left, Vector3.up, size.x, h);
        Face(mb, m, new Vector3(hx, 0, -hz), Vector3.forward, Vector3.up, size.z, h);
        Face(mb, m, new Vector3(-hx, 0, hz), Vector3.back, Vector3.up, size.z, h);
        Face(mb, m, new Vector3(-hx, h, -hz), Vector3.right, Vector3.forward, size.x, size.z);
        Face(mb, m, new Vector3(-hx, 0, hz), Vector3.right, Vector3.back, size.x, size.z);
    }

    // One side of a box: normal = v x u (outwards), split into tiles.
    static void Face(MeshKit.Builder mb, Matrix4x4 m, Vector3 origin, Vector3 u, Vector3 v, float uLen, float vLen)
    {
        if (uLen <= 0f || vLen <= 0f) return;
        const float tile = 3f, uv = 2f;
        int nu = Mathf.Max(1, Mathf.CeilToInt(uLen / tile)), nv = Mathf.Max(1, Mathf.CeilToInt(vLen / tile));
        float du = uLen / nu, dv = vLen / nv;
        for (int i = 0; i < nu; i++)
        for (int j = 0; j < nv; j++)
        {
            Vector3 a = origin + u * (i * du) + v * (j * dv);
            Vector3 pa = m.MultiplyPoint3x4(a), pb = m.MultiplyPoint3x4(a + v * dv);
            Vector3 pc = m.MultiplyPoint3x4(a + u * du + v * dv), pd = m.MultiplyPoint3x4(a + u * du);
            float u0 = i * du / uv, u1 = (i + 1) * du / uv, v0 = j * dv / uv, v1 = (j + 1) * dv / uv;
            mb.Quad(pa, pb, pc, pd, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
        }
    }

    // Wall along x at depth z, with openings (from, to, height of the opening).
    void WallX(float z, float x0, float x1, float h, float t, Material mat, params (float start, float end, float top)[] gaps)
    {
        float x = x0;
        System.Array.Sort(gaps, (p, q) => p.start.CompareTo(q.start));
        foreach (var g in gaps)
        {
            if (g.start > x) Solid(new Vector3((x + g.start) * 0.5f, 0f, z), new Vector3(g.start - x, h, t), mat);
            Solid(new Vector3((g.start + g.end) * 0.5f, g.top, z), new Vector3(g.end - g.start, h - g.top, t), mat, false);
            x = g.end;
        }
        if (x1 > x) Solid(new Vector3((x + x1) * 0.5f, 0f, z), new Vector3(x1 - x, h, t), mat);
    }

    // Wall along z at x, with openings.
    void WallZ(float x, float z0, float z1, float h, float t, Material mat, params (float start, float end, float top)[] gaps)
    {
        float z = z0;
        System.Array.Sort(gaps, (p, q) => p.start.CompareTo(q.start));
        foreach (var g in gaps)
        {
            if (g.start > z) Solid(new Vector3(x, 0f, (z + g.start) * 0.5f), new Vector3(t, h, g.start - z), mat);
            Solid(new Vector3(x, g.top, (g.start + g.end) * 0.5f), new Vector3(t, h - g.top, g.end - g.start), mat, false);
            z = g.end;
        }
        if (z1 > z) Solid(new Vector3(x, 0f, (z + z1) * 0.5f), new Vector3(t, h, z1 - z), mat);
    }

    void Shelf(Vector3 p, float yaw, float length)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Solid(p, new Vector3(length, 0.04f, 0.5f), darkMetal, false, yaw);
        for (int i = 1; i <= 3; i++) Solid(p + Vector3.up * (i * 0.7f), new Vector3(length, 0.04f, 0.5f), darkMetal, false, yaw);
        foreach (float s in new[] { -0.5f, 0.5f })
            Solid(p + q * new Vector3(s * length, 0f, 0f), new Vector3(0.05f, 2.2f, 0.5f), darkMetal, false, yaw);
        Block(p, new Vector3(length, 2.2f, 0.5f), yaw);
    }

    void TyreStack(Vector3 p, int count)
    {
        for (int i = 0; i < count; i++)
            Solid(p + Vector3.up * (i * 0.3f), new Vector3(0.9f, 0.28f, 0.9f), darkMetal, false, i * 20f);
        Block(p, new Vector3(0.9f, count * 0.3f, 0.9f), 0f);
    }

    void Drum(Vector3 p, Material mat)
    {
        Solid(p, new Vector3(0.6f, 0.9f, 0.6f), mat, true, Random.Range(0f, 90f));
    }

    Light Lamp(Vector3 localPos, Color color, float intensity, float range)
    {
        var l = new GameObject("Depot Light").AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.transform.localPosition = localPos;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    void Loop(Vector3 localPos, AudioClip clip, float volume, float maxDistance)
    {
        var go = new GameObject("Depot Sound");
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.volume = volume * GameSettings.Effects;
        src.Play();
    }

    void Play(AudioClip clip, Vector3 worldPos, float volume)
    {
        if (sound != null && clip != null) sound.PlayWorld(clip, worldPos, volume, 0.6f);
    }

    // Something to use with E; the texts are already in the chosen language.
    Inspectable Interact(Vector3 localPos, string prompt, string title, string text = null, string secret = null, float radius = 1.7f)
    {
        var go = new GameObject("Inspectable");
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        return Inspectable.Add(go, prompt, prompt, title, title, text, text, secret, radius);
    }

    // A sign with pixel text on a quad facing 'facing' (depot space).
    void Sign(Vector3 centre, Vector3 facing, float width, float height, string[] lines, Color32 bg, Color32 fg, bool glowing)
    {
        int longest = 1;
        foreach (var line in lines) longest = Mathf.Max(longest, line.Length);
        int pw = longest * PixelFont.CellWidth + 8;
        int ph = lines.Length * (PixelFont.CellHeight + 1) + 6;
        // Keep the texel aspect close to the sign's aspect.
        float aspect = width / height;
        if (pw / (float)ph < aspect) pw = Mathf.CeilToInt(ph * aspect);
        else ph = Mathf.CeilToInt(pw / aspect);
        var canvas = new PixelCanvas(pw, ph);
        canvas.Clear(bg);
        int top = (ph - lines.Length * (PixelFont.CellHeight + 1)) / 2;
        for (int i = 0; i < lines.Length; i++)
        {
            int x = (pw - PixelCanvas.TextWidth(lines[i])) / 2;
            canvas.Text(x, top + i * (PixelFont.CellHeight + 1), lines[i], fg);
        }
        canvas.Apply();
        SignQuad(centre, facing, width, height, TextMaterial(canvas, glowing));
    }

    void SignQuad(Vector3 centre, Vector3 facing, float width, float height, Material mat)
    {
        if (mat == null) return;
        var mb = new MeshKit.Builder();
        float w = width * 0.5f, h = height * 0.5f;
        // Built facing -z, then turned so its front looks along 'facing'.
        mb.Quad(new Vector3(-w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(w, h, 0f), new Vector3(w, -h, 0f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var go = MeshKit.Spawn("Sign", root, mb.ToMesh("Sign"), mat, root.position, root.rotation, false);
        go.transform.localPosition = centre;
        go.transform.localRotation = Quaternion.LookRotation(-facing);
    }
}
