using UnityEngine;

/// <summary>
/// Life along the road: a petrol station, a diner, a kiosk with a phone box and a motel now
/// and then (lit up in the dark forest, with things to look at, buy and find), and under
/// every bridge something that shouldn't be there.
/// </summary>
public class RoadsidePlaces : MonoBehaviour
{
    public ForestRoad road;
    public SideAreas side;
    public BoardingManager game;

    [Header("Materials (empty = from the side areas / road)")]
    public Material wood, concrete, metal, glow;
    [Tooltip("Glowing screens and neon signs")]
    public Material screenMaterial;

    [Header("Sounds")]
    public AudioClip jukeboxSong, phoneRing, hum, coins, click, scare, fire, river;

    Material floor, paint, darkMetal, whiteTile, blackTile, red, chrome, darkWood, coldGlow;
    SoundManager sound;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (side == null) side = FindAnyObjectByType<SideAreas>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        sound = FindAnyObjectByType<SoundManager>();
        if (road == null) return;
        road.PlaceBuilt += BuildPlace;
        road.RiverBuilt += BuildUnderBridge;
    }

    void OnDestroy()
    {
        if (road == null) return;
        road.PlaceBuilt -= BuildPlace;
        road.RiverBuilt -= BuildUnderBridge;
    }

    void PrepareMaterials()
    {
        if (paint != null) return;
        if (side != null)
        {
            if (wood == null) wood = side.wood;
            if (concrete == null) concrete = side.concrete;
            if (metal == null) metal = side.metal;
            if (glow == null) glow = side.glow;
        }
        if (concrete == null) concrete = road.concrete;
        if (metal == null) metal = road.metal;
        if (wood == null) wood = road.wood;
        if (glow == null) glow = road.lampGlow;
        if (screenMaterial == null) screenMaterial = glow;
        floor = PropKit.Tinted(concrete, new Color(0.55f, 0.55f, 0.53f), "Forecourt");
        paint = PropKit.Tinted(concrete, new Color(0.8f, 0.8f, 0.76f), "White Paint");
        darkMetal = PropKit.Tinted(metal, new Color(0.35f, 0.36f, 0.38f), "Dark Metal");
        whiteTile = PropKit.Tinted(concrete, new Color(0.9f, 0.9f, 0.88f), "White Tiles");
        blackTile = PropKit.Tinted(concrete, new Color(0.12f, 0.12f, 0.13f), "Black Tiles");
        red = PropKit.Tinted(metal, new Color(0.7f, 0.1f, 0.08f), "Red");
        chrome = PropKit.Tinted(metal, new Color(0.75f, 0.78f, 0.8f), "Chrome");
        darkWood = PropKit.Tinted(wood, new Color(0.45f, 0.35f, 0.28f), "Dark Wood");
        coldGlow = glow != null ? new Material(glow) { name = "Cold Glow" } : null;
        if (coldGlow != null && coldGlow.HasProperty("_MainColor")) coldGlow.SetColor("_MainColor", new Color(0.6f, 0.85f, 1f));
    }

    PropKit Kit(Transform root) => new PropKit(root, concrete, screenMaterial);

    void Play(AudioClip clip, Vector3 pos, float volume = 0.8f)
    {
        if (sound != null && clip != null) sound.PlayWorld(clip, pos, volume, 0.6f);
    }

    bool Pay(int euros)
    {
        if (Progress.Money < euros) return false;
        Progress.AddMoney(-euros);
        Progress.Save();
        return true;
    }

    static void Heal(int amount)
    {
        if (PlayerCombat.Instance != null) PlayerCombat.Instance.Heal(amount);
    }

    void RandomItem(Transform parent, Vector3 world)
    {
        float r = Random.value;
        if (r < 0.3f) Pickup.Spawn(Pickup.Kind.Money, Random.Range(8, 25) + Progress.Day * 3, 0, world, parent, wood, glow);
        else if (r < 0.55f) Pickup.Spawn(Pickup.Kind.Ammo, Random.Range(2, 6), 0, world, parent, metal, glow);
        else if (r < 0.8f) Pickup.Spawn(Pickup.Kind.Medkit, 1, 0, world, parent, concrete, glow);
    }

    // ---------------------------------------------------------------- places

    void BuildPlace(ForestRoad.PlaceSite site)
    {
        PrepareMaterials();
        var root = new GameObject("Roadside Place").transform;
        root.SetParent(site.chunk, true);
        root.SetPositionAndRotation(site.origin, site.rotation);
        var kit = Kit(root);
        switch (site.kind)
        {
            case 0: if (!PetrolStationModel(kit)) PetrolStation(kit); break;
            case 1: Diner(kit); break;
            case 2: Kiosk(kit); break;
            default: Motel(kit); break;
        }
        kit.Finish();
    }

    // ---- petrol station (model from the Gas_station pack)

    bool PetrolStationModel(PropKit k)
    {
        var holder = new GameObject("Petrol Station").transform;
        holder.SetParent(k.root, false);
        var model = PsxConvert.Spawn("Gas_station/Models/Gas_station", holder, "Gas_station/Textures");
        if (model == null) { Destroy(holder.gameObject); return false; }
        // The demo scenery around it (backdrops, trees, road, ground) is not needed here.
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == model.transform) continue;
            string n = t.name;
            if (n.StartsWith("Background") || n.StartsWith("Tree") || n.StartsWith("Bush") || n.StartsWith("Road") || n == "Ground" ||
                n.StartsWith("AsphaltDamaged") || n.StartsWith("Electric") || n.StartsWith("Cable") || n.StartsWith("Plant"))
                DestroyImmediate(t.gameObject);
        }
        // Pumps towards the road, 3 m from its edge.
        holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var b = PsxConvert.LocalBounds(model.transform, k.root);
        holder.localPosition += new Vector3(-b.center.x, -b.min.y + 0.02f, 3f - b.min.z);
        PsxConvert.AddColliders(model, 0.3f);
        foreach (var mc in model.GetComponentsInChildren<MeshCollider>())
            if (mc.name.StartsWith("Door") || mc.name.StartsWith("Glass") || mc.name.StartsWith("Carpet") || mc.name.StartsWith("Lamp")) Destroy(mc);
        k.Solid(new Vector3(0f, 0f, ForestRoad.PlaceDepth * 0.5f), new Vector3(ForestRoad.PlaceWidth, 0.03f, ForestRoad.PlaceDepth), floor, false);

        // Pumps you can use.
        var used = new System.Collections.Generic.List<Vector3>();
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("Fuel_pump") && !t.name.StartsWith("Dispenser")) continue;
            Vector3 p = t.position;
            if (Mathf.Abs(p.y - k.root.position.y) > 3f || used.Exists(u => (u - p).sqrMagnitude < 6f)) continue;
            used.Add(p);
            AddPump(k.root, new Vector3(p.x, k.root.position.y + 1f, p.z));
        }
        if (used.Count == 0)
        {
            AddPump(k.root, k.root.TransformPoint(new Vector3(-3f, 1f, 8f)));
            AddPump(k.root, k.root.TransformPoint(new Vector3(3f, 1f, 8f)));
        }

        // Cash desk: the shop. Lights in the shop and under the roof.
        Transform desk = null, roof = null;
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            if (desk == null && (t.name.StartsWith("Management") || t.name.StartsWith("Checker"))) desk = t;
            if (roof == null && t.name.StartsWith("The_ceiling")) roof = t;
        }
        Vector3 shop = desk != null ? k.root.InverseTransformPoint(desk.position) : new Vector3(0f, 1f, 30f);
        var buy = k.Interact(new Vector3(shop.x, 1.1f, shop.z), Loc.T("Einkaufen", "Shop"), Loc.T("Kasse", "Checkout"), null, 2.2f);
        buy.Action = _ => { ShopMenu.Open(Loc.T("TANKSTELLE - SHOP", "PETROL STATION - SHOP"), ShopMenu.PetrolStationItems()); return null; };
        k.Lamp(new Vector3(shop.x, 3f, shop.z), new Color(0.95f, 0.97f, 1f), 2.4f, 14f, 0.04f);
        k.Lamp(new Vector3(shop.x - 5f, 3f, shop.z + 3f), new Color(0.95f, 0.97f, 1f), 1.8f, 10f);
        Vector3 canopy = roof != null ? k.root.InverseTransformPoint(roof.position) : new Vector3(0f, 6f, 9f);
        k.Lamp(new Vector3(canopy.x - 4f, 5.5f, canopy.z), new Color(0.95f, 0.97f, 1f), 3.5f, 16f);
        k.Lamp(new Vector3(canopy.x + 4f, 5.5f, canopy.z), new Color(0.95f, 0.97f, 1f), 3.5f, 16f, 0.06f);
        k.Loop(new Vector3(shop.x, 1.5f, shop.z), hum, 0.08f, 12f);
        k.Interact(new Vector3(shop.x + 1.5f, 1.2f, shop.z - 1f), Loc.T("Zeitung lesen", "Read the newspaper"), Loc.T("Zeitung", "Newspaper"), Headline(), 1.6f);
        RandomItem(k.root, k.root.TransformPoint(new Vector3(shop.x - 3f, 0.1f, shop.z + 2f)));
        return true;
    }

    void AddPump(Transform root, Vector3 world)
    {
        var go = new GameObject("Fuel Pump");
        go.transform.SetParent(root, true);
        go.transform.position = world;
        var pump = go.AddComponent<FuelPump>();
        pump.radius = 1.9f;
        pump.pumpLoop = hum;
    }

    // ---- petrol station (fallback without the model)

    void PetrolStation(PropKit k)
    {
        k.Solid(new Vector3(0f, 0f, 14f), new Vector3(40f, 0.05f, 28f), floor, false);
        // Canopy over the pumps.
        k.Solid(new Vector3(0f, 4.6f, 10f), new Vector3(16f, 0.4f, 9f), paint, false);
        k.Solid(new Vector3(0f, 4.55f, 8f), new Vector3(14f, 0.05f, 0.3f), glow, false);
        k.Solid(new Vector3(0f, 4.55f, 12f), new Vector3(14f, 0.05f, 0.3f), glow, false);
        foreach (float x in new[] { -6.5f, 6.5f }) foreach (float z in new[] { 7f, 13f })
            k.Solid(new Vector3(x, 0f, z), new Vector3(0.4f, 4.6f, 0.4f), paint);
        k.Sign(new Vector3(0f, 4.8f, 5.47f), Vector3.back, 9f, 0.4f, new[] { "TANKE 13" }, new Color32(160, 20, 15, 255), new Color32(255, 240, 220, 255), true);
        k.Lamp(new Vector3(-3f, 4.2f, 10f), new Color(0.95f, 0.97f, 1f), 3.2f, 13f);
        k.Lamp(new Vector3(3f, 4.2f, 10f), new Color(0.95f, 0.97f, 1f), 3.2f, 13f, 0.06f);

        // Two pumps.
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -3f : 3f;
            k.Solid(new Vector3(x, 0f, 10f), new Vector3(1.2f, 0.15f, 3.4f), concrete);
            k.Solid(new Vector3(x, 0.15f, 10f), new Vector3(0.7f, 1.6f, 0.5f), red);
            k.Sign(new Vector3(x, 1.4f, 9.74f), Vector3.back, 0.5f, 0.25f, new[] { i == 0 ? "1,13" : "13,13" }, new Color32(10, 20, 10, 255), new Color32(120, 255, 140, 255), true);
            k.Interact(new Vector3(x, 1f, 9.3f), Loc.T("Zapfsäule ansehen", "Look at the pump"), Loc.T($"Zapfsäule {i + 1}", $"Pump {i + 1}"),
                i == 0 ? Loc.T("Preis pro Liter: 1,13. Zuletzt getankt: 13.10.1994, 03:13 Uhr. Wagen 13.", "Price per litre: 1.13. Last fill: 13 Oct 1994, 03:13. Bus 13.")
                       : Loc.T("Die Anzeige zählt rückwärts. Als würde jemand den Tank leer trinken.", "The display counts backwards. As if someone were drinking the tank empty."));
        }

        // Price sign at the road.
        k.Solid(new Vector3(17f, 0f, 2.5f), new Vector3(0.3f, 7f, 0.3f), darkMetal);
        k.Sign(new Vector3(17f, 6.2f, 2.3f), Vector3.back, 2.6f, 1.8f, new[] { "TANKE 13", "SUPER 1,13", "DIESEL 1,13", Loc.T("24H GEÖFFNET", "OPEN 24H") }, new Color32(20, 20, 30, 255), new Color32(255, 200, 60, 255), true);
        k.Lamp(new Vector3(17f, 6f, 1.6f), new Color(1f, 0.8f, 0.4f), 1.5f, 6f);

        // Shop.
        float z0 = 18f, z1 = 28f, h = 3.4f;
        k.Solid(new Vector3(0f, 0.05f, 23f), new Vector3(16f, 0.06f, 10f), whiteTile, false);
        k.Solid(new Vector3(0f, h, 23f), new Vector3(16.4f, 0.25f, 10.4f), darkMetal, false);
        k.WallX(z0, -8f, 8f, h, 0.2f, paint, (-7f, -2f, 2.5f), (-0.7f, 0.7f, 2.2f), (2f, 7f, 2.5f));
        k.Solid(new Vector3(-4.5f, 0f, z0), new Vector3(5f, 1f, 0.2f), paint);
        k.Solid(new Vector3(4.5f, 0f, z0), new Vector3(5f, 1f, 0.2f), paint);
        k.WallX(z1, -8f, 8f, h, 0.2f, paint);
        k.WallZ(-8f, z0, z1, h, 0.2f, paint);
        k.WallZ(8f, z0, z1, h, 0.2f, paint);
        k.Sign(new Vector3(0f, 2.8f, z0 - 0.12f), Vector3.back, 3f, 0.5f, new[] { Loc.T("SHOP", "SHOP") }, new Color32(20, 60, 140, 255), new Color32(255, 255, 255, 255), true);
        k.Lamp(new Vector3(0f, 3.0f, 23f), new Color(0.9f, 0.95f, 1f), 2.2f, 10f, 0.05f);
        k.Solid(new Vector3(0f, h - 0.1f, 23f), new Vector3(0.3f, 0.06f, 3f), glow, false);

        // Counter with a bell, coffee, shelves, a fridge.
        k.Solid(new Vector3(5f, 0.05f, 21.5f), new Vector3(3f, 1f, 0.8f), darkWood);
        k.Solid(new Vector3(5.8f, 1.05f, 21.5f), new Vector3(0.45f, 0.3f, 0.4f), darkMetal, false);
        k.Interact(new Vector3(4.5f, 1.2f, 21f), Loc.T("Klingeln", "Ring the bell"), Loc.T("Klingel", "Bell"),
            Loc.T("Ding. Niemand kommt. Aus dem Lager hinten hört man jemanden atmen. Ganz ruhig. Ganz nah an der Tür.",
                  "Ding. Nobody comes. From the store room at the back you hear someone breathing. Very calm. Very close to the door."), 1.4f)
            .sound = click;
        var coffee = k.Interact(new Vector3(6f, 1.2f, 21f), Loc.T("Kaffee (1 €)", "Coffee (1 €)"), Loc.T("Kaffee", "Coffee"), null, 1.2f);
        coffee.Action = _ =>
        {
            if (!Pay(1)) return Loc.T("Nicht genug Geld.", "Not enough money.");
            Heal(20);
            return Loc.T("Der Automat brummt. Der Kaffee ist heiß und bitter. Es geht dir besser.", "The machine hums. The coffee is hot and bitter. You feel better.");
        };
        for (int i = 0; i < 3; i++)
            k.Solid(new Vector3(-3.5f, 0.05f, 21.5f + i * 1.6f), new Vector3(5f, 1.6f, 0.5f), darkMetal);
        var snack = k.Interact(new Vector3(-3.5f, 1f, 22.4f), Loc.T("Snack kaufen (2 €)", "Buy a snack (2 €)"), Loc.T("Regal", "Shelf"));
        snack.Action = _ =>
        {
            if (!Pay(2)) return Loc.T("Nicht genug Geld.", "Not enough money.");
            Heal(15);
            Play(coins, snack.transform.position);
            return Loc.T("Du legst das Geld auf den Tresen. Irgendwer wird es schon nehmen.", "You put the money on the counter. Someone will take it.");
        };
        k.Solid(new Vector3(-3f, 0.05f, 27.4f), new Vector3(6f, 2.1f, 0.7f), chrome);
        k.Solid(new Vector3(-3f, 0.3f, 27.03f), new Vector3(5.6f, 1.6f, 0.04f), coldGlow, false);
        k.Lamp(new Vector3(-3f, 1.2f, 26.4f), new Color(0.6f, 0.85f, 1f), 1.2f, 4f);
        k.Loop(new Vector3(-3f, 1f, 27f), hum, 0.08f, 8f);
        bool drank = false;
        k.Interact(new Vector3(-3f, 1.2f, 26.3f), Loc.T("Kühlregal öffnen", "Open the fridge"), Loc.T("Kühlregal", "Fridge")).Action = _ =>
        {
            if (drank) return Loc.T("Alle Flaschen tragen dasselbe Etikett: WALDQUELLE. Abgefüllt 1994.", "All the bottles have the same label: FOREST SPRING. Bottled 1994.");
            drank = true;
            Heal(30);
            return Loc.T("Alle Flaschen tragen dasselbe Etikett: WALDQUELLE. Abgefüllt 1994.\nDu trinkst eine. Sie schmeckt nach Erde - und du fühlst dich stärker.",
                         "All the bottles have the same label: FOREST SPRING. Bottled 1994.\nYou drink one. It tastes of earth - and you feel stronger.");
        };
        k.Interact(new Vector3(1.8f, 1f, 18.8f), Loc.T("Zeitung lesen", "Read the newspaper"), Loc.T("Zeitung", "Newspaper"), Headline());
        RandomItem(k.root, k.root.TransformPoint(new Vector3(-3.5f, 1.06f, 24.7f)));
    }

    // ---- diner

    void Diner(PropKit k)
    {
        k.Solid(new Vector3(0f, 0f, 12f), new Vector3(40f, 0.05f, 24f), floor, false);
        for (int i = 0; i < 6; i++) k.Solid(new Vector3(-15f + i * 4f, 0.051f, 6f), new Vector3(0.12f, 0.005f, 4.5f), whiteTile, false);
        float x0 = -9f, x1 = 9f, z0 = 14f, z1 = 26f, h = 3.6f;
        // Chequered floor.
        for (int ix = 0; ix < 9; ix++)
        for (int iz = 0; iz < 6; iz++)
            k.Solid(new Vector3(x0 + 1f + ix * 2f, 0.05f, z0 + 1f + iz * 2f), new Vector3(2f, 0.05f, 2f), (ix + iz) % 2 == 0 ? whiteTile : blackTile, false);
        k.Solid(new Vector3(0f, h, 20f), new Vector3(18.6f, 0.25f, 12.6f), chrome, false);
        k.WallX(z0, x0, x1, h, 0.2f, chrome, (-8f, -1f, 2.7f), (1f, 4f, 2.7f), (5f, 6.4f, 2.3f));
        k.Solid(new Vector3(-4.5f, 0f, z0), new Vector3(7f, 1f, 0.2f), chrome);
        k.Solid(new Vector3(2.5f, 0f, z0), new Vector3(3f, 1f, 0.2f), chrome);
        k.WallX(z1, x0, x1, h, 0.2f, chrome);
        k.WallZ(x0, z0, z1, h, 0.2f, chrome);
        k.WallZ(x1, z0, z1, h, 0.2f, chrome);
        k.Solid(new Vector3(0f, 0.9f, z0 - 0.05f), new Vector3(18f, 0.12f, 0.1f), red, false);   // red stripe

        // Neon sign on the roof.
        foreach (float x in new[] { -3f, 3f }) k.Solid(new Vector3(x, h, z0 + 0.5f), new Vector3(0.12f, 1.2f, 0.12f), darkMetal, false);
        k.Sign(new Vector3(0f, h + 1.6f, z0 + 0.4f), Vector3.back, 8f, 1.5f, new[] { "DINER", Loc.T("24 STUNDEN", "24 HOURS") }, new Color32(15, 5, 20, 255), new Color32(255, 70, 160, 255), true);
        k.Lamp(new Vector3(0f, h + 1.4f, z0 - 1f), new Color(1f, 0.3f, 0.6f), 2.5f, 12f, 0.15f);

        // Inside: counter with stools, booths at the windows, a jukebox.
        k.Lamp(new Vector3(-4f, 3.2f, 20f), new Color(1f, 0.85f, 0.6f), 2f, 9f);
        k.Lamp(new Vector3(4f, 3.2f, 20f), new Color(1f, 0.85f, 0.6f), 2f, 9f, 0.05f);
        k.Solid(new Vector3(-2f, 0.1f, 23.6f), new Vector3(10f, 1.05f, 0.8f), red);
        k.Solid(new Vector3(-2f, 1.15f, 23.6f), new Vector3(10.2f, 0.06f, 1f), chrome, false);
        for (int i = 0; i < 6; i++) k.Solid(new Vector3(-6f + i * 1.6f, 0.1f, 22.6f), new Vector3(0.4f, 0.75f, 0.4f), red);
        k.Sign(new Vector3(-2f, 2.6f, 25.85f), Vector3.back, 3.4f, 1f, new[] { Loc.T("TAGESKARTE", "TODAY"), Loc.T("SUPPE 13", "SOUP 13"), Loc.T("KAFFEE 2", "COFFEE 2") }, new Color32(20, 20, 20, 255), new Color32(255, 230, 180, 255), true);
        for (int i = 0; i < 3; i++)
        {
            float x = -7f + i * 3f;
            k.Solid(new Vector3(x, 0.1f, 15.4f), new Vector3(1.2f, 0.75f, 0.8f), chrome);
            foreach (float bs in new[] { -1f, 1f })
            {
                k.Solid(new Vector3(x + bs * 0.8f, 0.1f, 15.4f), new Vector3(0.5f, 0.5f, 1f), red);
                k.Solid(new Vector3(x + bs * 1.1f, 0.1f, 15.4f), new Vector3(0.12f, 1.1f, 1f), red, false);
            }
        }
        var coffee = k.Interact(new Vector3(-2f, 1.3f, 22.6f), Loc.T("Kaffee bestellen (2 €)", "Order coffee (2 €)"), Loc.T("Tresen", "Counter"), null, 1.6f);
        coffee.Action = _ =>
        {
            if (!Pay(2)) return Loc.T("Nicht genug Geld.", "Not enough money.");
            Heal(25);
            return Loc.T("Niemand steht hinter dem Tresen. Die Kanne ist trotzdem heiß. Neben der Tasse liegt schon die Rechnung - mit deinem Namen.",
                         "Nobody is behind the counter. The pot is hot anyway. Next to the cup there's already a bill - with your name on it.");
        };

        // Jukebox.
        k.Solid(new Vector3(8f, 0.1f, 24.8f), new Vector3(0.9f, 1.5f, 0.6f), red);
        k.Solid(new Vector3(8f, 0.6f, 24.48f), new Vector3(0.7f, 0.8f, 0.04f), glow, false);
        AudioSource music = null;
        var box = k.Interact(new Vector3(8f, 1f, 24.2f), Loc.T("Jukebox (1 €)", "Jukebox (1 €)"), "Jukebox", null, 1.4f);
        box.Action = self =>
        {
            if (music != null) { Destroy(music.gameObject); music = null; return null; }
            if (!Pay(1)) return Loc.T("Nicht genug Geld.", "Not enough money.");
            if (jukeboxSong == null) return null;
            var go = new GameObject("Jukebox Music");
            go.transform.SetParent(self.transform, false);
            music = go.AddComponent<AudioSource>();
            music.clip = jukeboxSong;
            music.loop = true;
            music.spatialBlend = 1f;
            music.maxDistance = 25f;
            music.rolloffMode = AudioRolloffMode.Linear;
            music.volume = 0.5f * GameSettings.Music;
            music.Play();
            return Loc.T("Alle Titel auf der Liste heißen gleich: \"Nachtlinie\". Die Jukebox spielt ihn trotzdem gern.",
                         "Every song on the list has the same title: \"Night Line\". The jukebox plays it happily anyway.");
        };

        // A guest in the last booth - or just their cup.
        Vector3 seat = new Vector3(-0.2f, 0.6f, 15.4f);
        var models = game != null ? game.malePassengers : null;
        if (models != null && models.Length > 0 && Random.value < 0.7f)
        {
            var guest = Instantiate(models[Random.Range(0, models.Length)], k.root);
            guest.name = "Diner Guest";
            var p = guest.AddComponent<Passenger>();
            p.SitDown(seat, Vector3.left, k.root);
            string[] lines =
            {
                Loc.T("\"Der Kaffee hier ist seit 1994 kalt. Aber er schmeckt.\"", "\"The coffee here has been cold since 1994. But it tastes good.\""),
                Loc.T("\"Sie sind der neue Fahrer, oder? Setzen Sie sich nicht zu lange. Man steht hier nicht mehr auf.\"", "\"You're the new driver, aren't you? Don't sit down too long. People don't get up again here.\""),
                Loc.T("\"Ich warte auf den Bus. Den letzten. Er hält hier nicht, aber ich warte trotzdem.\"", "\"I'm waiting for the bus. The last one. It doesn't stop here, but I'm waiting anyway.\""),
            };
            k.Interact(new Vector3(-1.2f, 1f, 16.6f), Loc.T("Mit dem Gast reden", "Talk to the guest"), Loc.T("Gast", "Guest"), lines[Random.Range(0, lines.Length)], 1.6f);
        }
        else
        {
            k.Solid(new Vector3(-1f, 0.85f, 15.4f), new Vector3(0.12f, 0.12f, 0.12f), whiteTile, false);
            k.Interact(new Vector3(-1f, 1f, 16.2f), Loc.T("Tasse ansehen", "Look at the cup"), Loc.T("Tasse", "Cup"),
                Loc.T("Der Kaffee dampft noch. Der Platz ist leer. Auf der Serviette steht: \"Bin gleich zurück - Karl\".",
                      "The coffee is still steaming. The seat is empty. On the napkin: \"Back in a minute - Karl\"."), 1.4f);
        }
        RandomItem(k.root, k.root.TransformPoint(new Vector3(6f, 1.2f, 23.6f)));
    }

    // ---- kiosk with a phone box

    void Kiosk(PropKit k)
    {
        k.Solid(new Vector3(0f, 0f, 8f), new Vector3(24f, 0.05f, 12f), floor, false);
        float x0 = -3f, x1 = 3f, z0 = 6f, z1 = 11f, h = 2.7f;
        k.Solid(new Vector3(0f, 0.05f, 8.5f), new Vector3(6f, 0.05f, 5f), darkWood, false);
        k.Tilted(new Vector3(0f, h, 8.5f), new Vector3(7f, 0.15f, 6f), darkMetal, new Vector3(-6f, 0f, 0f));
        k.WallX(z0, x0, x1, h, 0.15f, wood, (-2f, 2f, 2.1f));
        k.Solid(new Vector3(0f, 0f, z0), new Vector3(4f, 1.05f, 0.15f), wood);
        k.Solid(new Vector3(0f, 1.6f, z0 - 0.05f), new Vector3(4f, 0.5f, 0.08f), darkMetal, false);   // half open shutter
        k.WallX(z1, x0, x1, h, 0.15f, wood);
        k.WallZ(x0, z0, z1, h, 0.15f, wood);
        k.WallZ(x1, z0, z1, h, 0.15f, wood);
        k.Sign(new Vector3(0f, 2.4f, z0 - 0.1f), Vector3.back, 5f, 0.5f, new[] { Loc.T("KIOSK - ZEITUNGEN - TABAK", "KIOSK - PAPERS - TOBACCO") }, new Color32(200, 30, 20, 255), new Color32(255, 240, 200, 255), true);
        k.Lamp(new Vector3(0f, 2.2f, 8f), new Color(1f, 0.8f, 0.5f), 1.6f, 7f, 0.1f);
        k.Solid(new Vector3(0f, 1.05f, 6.3f), new Vector3(3.6f, 0.08f, 0.6f), wood, false);   // counter in the hatch
        k.Interact(new Vector3(0f, 1.3f, 5.5f), Loc.T("Zeitung lesen", "Read the newspaper"), Loc.T("Zeitung", "Newspaper"), Headline(), 1.5f);
        k.Interact(new Vector3(1.5f, 1.3f, 5.5f), Loc.T("In den Kiosk schauen", "Look into the kiosk"), "Kiosk",
            Loc.T("Drinnen brennt Licht. Auf dem Hocker liegt eine Strickjacke, noch warm. Das Radio läuft ganz leise - es sagt die Haltestellen deiner Linie an. In der richtigen Reihenfolge.",
                  "The light is on inside. A cardigan lies on the stool, still warm. The radio plays very quietly - it announces the stops of your line. In the right order."), 1.5f);

        // Cigarette machine.
        k.Solid(new Vector3(-4.2f, 0.05f, 7f), new Vector3(0.8f, 1.8f, 0.5f), red);
        k.Interact(new Vector3(-4.2f, 1f, 6.4f), Loc.T("Zigarettenautomat", "Cigarette machine"), Loc.T("Zigarettenautomat", "Cigarette machine"),
            Loc.T("Alle Fächer sind leer. Bis auf eins. Darauf steht: \"LETZTE\".", "Every slot is empty. Except one. It says: \"LAST ONE\"."), 1.3f);

        // Phone box.
        float px = 7f, pz = 6f;
        foreach (float dx in new[] { -0.6f, 0.6f }) foreach (float dz in new[] { -0.6f, 0.6f })
            k.Solid(new Vector3(px + dx, 0.05f, pz + dz), new Vector3(0.1f, 2.4f, 0.1f), red);
        k.Solid(new Vector3(px, 2.45f, pz), new Vector3(1.4f, 0.2f, 1.4f), red, false);
        k.Solid(new Vector3(px, 2.2f, pz - 0.65f), new Vector3(1.2f, 0.2f, 0.05f), glow, false);
        k.Solid(new Vector3(px, 1.1f, pz + 0.55f), new Vector3(0.35f, 0.5f, 0.15f), darkMetal, false);
        k.Lamp(new Vector3(px, 2.1f, pz), new Color(1f, 0.95f, 0.8f), 1f, 4f, 0.2f);
        AudioSource ring = null;
        if (phoneRing != null && Random.value < 0.6f)
        {
            var go = new GameObject("Phone Ring");
            go.transform.SetParent(k.root, false);
            go.transform.localPosition = new Vector3(px, 1.2f, pz);
            ring = go.AddComponent<AudioSource>();
            ring.clip = phoneRing;
            ring.loop = true;
            ring.spatialBlend = 1f;
            ring.maxDistance = 30f;
            ring.rolloffMode = AudioRolloffMode.Linear;
            ring.volume = 0.35f * GameSettings.Effects;
            ring.Play();
        }
        k.Interact(new Vector3(px, 1.2f, pz), Loc.T("Hörer abnehmen", "Pick up the phone"), Loc.T("Telefonzelle", "Phone box")).Action = _ =>
        {
            if (ring != null) { Destroy(ring.gameObject); ring = null; }
            string[] calls =
            {
                Loc.T("Eine Stimme liest die Nummer deines Busses vor. Dann deinen Namen. Dann legt sie auf.", "A voice reads out the number of your bus. Then your name. Then it hangs up."),
                Loc.T("\"Hallo? Hallo! Bitte halten Sie nicht am Waldfriedhof. Bitte! Die stehen da nicht zum Einsteigen...\"", "\"Hello? Hello! Please don't stop at the Waldfriedhof. Please! They're not waiting to get on...\""),
                Loc.T("Nur Rauschen. Darunter, sehr leise, das Geräusch eines Busmotors. Deines Busmotors.", "Only static. Beneath it, very quietly, the sound of a bus engine. Your bus engine."),
            };
            return calls[Random.Range(0, calls.Length)];
        };

        // Bench and bins.
        k.Solid(new Vector3(-8f, 0.05f, 4f), new Vector3(2f, 0.45f, 0.5f), wood);
        k.Solid(new Vector3(-10f, 0.05f, 4f), new Vector3(0.5f, 0.9f, 0.5f), darkMetal);
        RandomItem(k.root, k.root.TransformPoint(new Vector3(-8f, 0.52f, 4f)));
    }

    // ---- motel

    void Motel(PropKit k)
    {
        k.Solid(new Vector3(0f, 0f, 10f), new Vector3(42f, 0.05f, 22f), floor, false);
        float x0 = -18f, x1 = 14f, z0 = 12f, z1 = 20f, h = 3f;
        k.Solid(new Vector3(-2f, h, 16f), new Vector3(x1 - x0 + 0.4f, 0.25f, 8.4f), darkWood, false);
        k.Solid(new Vector3(-2f, h - 0.1f, 11f), new Vector3(x1 - x0, 0.12f, 2f), darkWood, false);   // walkway roof
        for (float x = x0 + 1f; x <= x1; x += 5f) k.Solid(new Vector3(x, 0f, 10.2f), new Vector3(0.15f, h - 0.1f, 0.15f), darkWood, false);
        k.WallX(z1, x0, x1, h, 0.2f, paint);
        k.WallZ(x0, z0, z1, h, 0.2f, paint);
        k.WallZ(x1, z0, z1, h, 0.2f, paint);

        // Six rooms, numbers 8 to 13; only 13 is open.
        var gaps = new (float, float, float)[6];
        for (int i = 0; i < 6; i++) { float dx = x0 + 2.5f + i * 5.3f; gaps[i] = (dx - 0.55f, dx + 0.55f, 2.2f); }
        k.WallX(z0, x0, x1, h, 0.2f, paint, gaps);
        for (int i = 0; i < 6; i++)
        {
            float dx = x0 + 2.5f + i * 5.3f;
            int number = 8 + i;
            k.Sign(new Vector3(dx, 2.5f, z0 - 0.12f), Vector3.back, 0.5f, 0.3f, new[] { number.ToString() }, new Color32(200, 180, 120, 255), new Color32(30, 20, 10, 255), false);
            if (i > 0) k.WallZ(dx - 2.65f, z0, z1, h, 0.15f, paint);
            if (number != 13) { k.Solid(new Vector3(dx, 0f, z0), new Vector3(1.1f, 2.2f, 0.12f), darkWood); continue; }
            // Room 13.
            k.Solid(new Vector3(dx + 1f, 0.05f, 18.2f), new Vector3(1.6f, 0.55f, 2.2f), whiteTile);
            k.Solid(new Vector3(dx - 1.8f, 0.05f, 19.4f), new Vector3(0.9f, 0.7f, 0.5f), darkWood);
            k.Solid(new Vector3(dx - 1.8f, 0.75f, 19.4f), new Vector3(0.6f, 0.45f, 0.4f), darkMetal, false);
            k.Solid(new Vector3(dx - 1.8f, 0.85f, 19.18f), new Vector3(0.45f, 0.3f, 0.02f), coldGlow, false);
            k.Lamp(new Vector3(dx - 1.8f, 1f, 18.6f), new Color(0.6f, 0.7f, 1f), 0.8f, 4f, 0.5f);
            k.Interact(new Vector3(dx + 0.2f, 0.8f, 17f), Loc.T("Zettel auf dem Bett", "Note on the bed"), Loc.T("Zimmer 13", "Room 13"),
                Loc.T("\"Zimmer 13 ist immer frei. Der letzte Gast hat nie bezahlt. Er ist auch nie abgereist.\nWenn du das liest: Schau nicht unter das Bett.\"",
                      "\"Room 13 is always free. The last guest never paid. He never checked out either.\nIf you read this: don't look under the bed.\""), 1.6f);
            k.Interact(new Vector3(dx + 1f, 0.3f, 16.8f), Loc.T("Unter das Bett schauen", "Look under the bed"), Loc.T("Unter dem Bett", "Under the bed"), null, 1.2f)
                .Action = self =>
                {
                    Play(scare, self.transform.position, 1f);
                    return Loc.T("Ein Gesicht. Direkt vor deinem. Die Augen offen. Es lächelt. Du blinzelst - und da ist nur Staub.",
                                 "A face. Right in front of yours. Eyes open. It smiles. You blink - and there's only dust.");
                };
            RandomItem(k.root, k.root.TransformPoint(new Vector3(dx - 1.8f, 0.8f, 19.1f)));
        }
        for (int i = 0; i < 4; i++) k.Lamp(new Vector3(x0 + 4f + i * 8f, 2.7f, 11f), new Color(1f, 0.75f, 0.45f), 1.1f, 6f, 0.12f);

        // Office with the reception.
        float ox = 18f;
        k.Solid(new Vector3(ox, 0.05f, 16f), new Vector3(7f, 0.05f, 8f), darkWood, false);
        k.Solid(new Vector3(ox, h, 16f), new Vector3(7.4f, 0.25f, 8.4f), darkWood, false);
        k.WallX(z0, ox - 3.5f, ox + 3.5f, h, 0.2f, paint, (ox - 0.6f, ox + 0.6f, 2.2f));
        k.WallX(z1, ox - 3.5f, ox + 3.5f, h, 0.2f, paint);
        k.WallZ(ox + 3.5f, z0, z1, h, 0.2f, paint);
        k.Solid(new Vector3(ox, 0.05f, 16.5f), new Vector3(3f, 1.05f, 0.7f), darkWood);
        k.Lamp(new Vector3(ox, 2.6f, 15f), new Color(1f, 0.8f, 0.55f), 1.4f, 6f);
        k.Interact(new Vector3(ox, 1.2f, 15.6f), Loc.T("Schlüsselbrett ansehen", "Look at the key board"), Loc.T("Rezeption", "Reception"),
            Loc.T("Alle Schlüssel hängen am Brett. Nur Nummer 13 fehlt. Im Gästebuch steht als letzter Eintrag, in deiner Handschrift: \"Nachtlinie 13 - eine Nacht\".",
                  "All the keys hang on the board. Only number 13 is missing. The last entry in the guest book, in your handwriting: \"Night line 13 - one night\"."), 1.6f);

        // Sign at the road.
        k.Solid(new Vector3(-16f, 0f, 2.5f), new Vector3(0.3f, 6f, 0.3f), darkMetal);
        k.Sign(new Vector3(-16f, 5.3f, 2.3f), Vector3.back, 3f, 1.8f, new[] { "MOTEL", "WALDRUH", Loc.T("ZIMMER FREI", "VACANCY") }, new Color32(10, 25, 20, 255), new Color32(90, 255, 170, 255), true);
        k.Lamp(new Vector3(-16f, 5f, 1.6f), new Color(0.4f, 1f, 0.7f), 1.6f, 7f, 0.1f);
    }

    string Headline()
    {
        return Progress.Day switch
        {
            1 => Loc.T("\"NACHTLINIE 13 WIEDER IN BETRIEB - Neuer Fahrer gefunden\"", "\"NIGHT LINE 13 RUNNING AGAIN - New driver found\""),
            2 => Loc.T("\"Fahrgast seit Dienstag vermisst - zuletzt an der Haltestelle Wolfsschlucht gesehen\"", "\"Passenger missing since Tuesday - last seen at the Wolfsschlucht stop\""),
            3 => Loc.T("\"Wer fährt eigentlich die 13? Leitstelle verweigert jede Auskunft\"", "\"Who actually drives the 13? Dispatch refuses to comment\""),
            4 => Loc.T("\"Rätsel im Wald: Bushaltestellen, die auf keiner Karte stehen\"", "\"Mystery in the forest: bus stops that are on no map\""),
            5 => Loc.T("\"Busfahrer Horst L. weiter verschwunden - Familie bittet um Hinweise\"", "\"Bus driver Horst L. still missing - family asks for help\""),
            6 => Loc.T("\"Fotos zeigen: Der Bus der Linie 13 fährt ohne Licht - und ohne Fahrer\"", "\"Photos show: the line 13 bus drives without lights - and without a driver\""),
            _ => Loc.T("Die Zeitung ist von morgen. Auf der Titelseite: ein Foto von dir. Darunter: \"VERMISST\".", "The paper is from tomorrow. On the front page: a photo of you. Underneath: \"MISSING\"."),
        };
    }

    // ---------------------------------------------------------------- under the bridges

    void BuildUnderBridge(ForestRoad.RiverSite site)
    {
        PrepareMaterials();
        var root = new GameObject("Under The Bridge").transform;
        root.SetParent(site.chunk, true);
        root.SetPositionAndRotation(site.centre, Quaternion.LookRotation(site.along));
        var k = Kit(root);
        k.Loop(new Vector3(0f, site.waterY - site.roadY + 0.3f, 0f), river, 0.6f, 40f);
        float bank = Random.value < 0.5f ? -1f : 1f;       // which river bank
        float z = bank * 10.5f;

        // Two of four things.
        int first = Random.Range(0, 4), second = (first + 1 + Random.Range(0, 3)) % 4;
        foreach (int thing in new[] { first, second })
        {
            float x = thing == first ? -2f : 2.5f;
            switch (thing)
            {
                case 0: Camp(k, site, new Vector3(x, 0f, z)); break;
                case 1: Graffiti(k, site, bank); break;
                case 2: Shrine(k, site, new Vector3(x, 0f, z + bank * 1.5f)); break;
                default: Floating(k, site, bank); break;
            }
        }
        if (Random.value < 0.6f) RandomItem(root, Ground(site, root, new Vector3(0f, 0.1f, z + bank * 2f)));
        if (side != null && Progress.Day >= 3 && Random.value < 0.25f + 0.05f * Progress.Day)
            side.SpawnDweller(Ground(site, root, new Vector3(3f, 0.05f, z + bank * 3f)), root);
        k.Finish();
    }

    // A point on the valley floor (root space in, world out).
    Vector3 Ground(ForestRoad.RiverSite site, Transform root, Vector3 local)
    {
        Vector3 world = root.TransformPoint(new Vector3(local.x, 0f, local.z));
        world.y = Mathf.Max(road.GroundHeight(world), site.waterY) + local.y;
        return world;
    }

    Vector3 Local(ForestRoad.RiverSite site, Transform root, Vector3 local) => root.InverseTransformPoint(Ground(site, root, local));

    void Camp(PropKit k, ForestRoad.RiverSite site, Vector3 at)
    {
        Vector3 p = Local(site, k.root, at);
        k.Tilted(p + new Vector3(-0.5f, 0f, 0f), new Vector3(0.08f, 1.5f, 2f), darkWood, new Vector3(0f, 0f, -35f));
        k.Tilted(p + new Vector3(0.5f, 0f, 0f), new Vector3(0.08f, 1.5f, 2f), darkWood, new Vector3(0f, 0f, 35f));
        Vector3 firePlace = Local(site, k.root, at + new Vector3(2f, 0f, 0f));
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            k.Solid(firePlace + new Vector3(Mathf.Cos(a) * 0.4f, 0f, Mathf.Sin(a) * 0.4f), new Vector3(0.18f, 0.15f, 0.18f), concrete, false);
        }
        k.Solid(firePlace, new Vector3(0.3f, 0.25f, 0.3f), glow, false);
        k.Lamp(firePlace + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.15f), 2.2f, 9f, 0.35f);
        k.Loop(firePlace, fire, 0.25f, 10f);
        k.Solid(p + new Vector3(0f, 0f, 1.6f), new Vector3(0.8f, 0.12f, 1.9f), red, false);   // sleeping bag
        k.Interact(p + new Vector3(0f, 0.5f, 1.2f), Loc.T("Tagebuch lesen", "Read the diary"), Loc.T("Tagebuch", "Diary"),
            Loc.T("\"Der Bus hält hier nie. Aber jede Nacht steigen Leute aus dem Fluss und gehen zur Haltestelle hoch.\nSie sind nass. Sie frieren nicht.\nHeute hat mich einer gefragt, ob ich mitkomme.\"",
                  "\"The bus never stops here. But every night people climb out of the river and walk up to the stop.\nThey are wet. They're not cold.\nTonight one of them asked if I was coming along.\""), 1.8f);
    }

    void Graffiti(PropKit k, ForestRoad.RiverSite site, float bank)
    {
        // On the pillar on this bank, facing the water.
        float x = site.right == Vector3.zero ? 3f : (road.EdgeOffset - 1.4f);
        float y = Local(site, k.root, new Vector3(x, 0f, bank * 9f)).y + 1.6f;
        k.Sign(new Vector3(x, y, bank * 8.45f), new Vector3(0f, 0f, -bank), 1.2f, 1.6f, new[] { "LINIE 13", Loc.T("FÄHRT", "NEVER"), Loc.T("NIE", "COMES"), Loc.T("ZURÜCK", "BACK") },
            new Color32(120, 118, 112, 255), new Color32(200, 20, 20, 255), false);
        k.Interact(new Vector3(x, y - 1f, bank * 7.6f), Loc.T("Graffiti ansehen", "Look at the graffiti"), "Graffiti",
            Loc.T("Darunter, kleiner, viele Striche. Jeder Strich ein Name. Der letzte ist frisch - die Farbe tropft noch.",
                  "Below it, smaller, many tally marks. Every mark a name. The last one is fresh - the paint is still dripping."), 1.8f);
    }

    void Shrine(PropKit k, ForestRoad.RiverSite site, Vector3 at)
    {
        Vector3 p = Local(site, k.root, at);
        k.Solid(p, new Vector3(1.2f, 0.5f, 0.6f), concrete, false);
        for (int i = 0; i < 5; i++) k.Solid(p + new Vector3(-0.45f + i * 0.22f, 0.5f, 0f), new Vector3(0.06f, 0.14f, 0.06f), glow, false);
        k.Lamp(p + Vector3.up * 0.9f, new Color(1f, 0.7f, 0.35f), 1.2f, 5f, 0.25f);
        k.Interact(p + Vector3.up * 0.7f, Loc.T("Fotos ansehen", "Look at the photos"), Loc.T("Kerzen", "Candles"),
            Loc.T("Kerzen und Fotos, mit Steinen beschwert. Auf den Fotos: Leute an Bushaltestellen, nachts, von hinten fotografiert.\nAuf einem erkennst du deinen Bus. Am Steuer sitzt niemand.",
                  "Candles and photos, held down with stones. The photos: people at bus stops, at night, taken from behind.\nOn one you recognise your bus. Nobody is at the wheel."), 1.8f);
    }

    void Floating(PropKit k, ForestRoad.RiverSite site, float bank)
    {
        float wy = site.waterY - site.roadY;
        k.Solid(new Vector3(-4f, wy - 0.1f, bank * 2f), new Vector3(0.7f, 0.22f, 1.7f), darkMetal, false);
        k.Interact(Local(site, k.root, new Vector3(-4f, 0.5f, bank * 7.5f)), Loc.T("Was treibt da?", "What's floating there?"), Loc.T("Im Wasser", "In the water"),
            Loc.T("Ein Sakko der Verkehrsbetriebe treibt im Wasser, der Stoff aufgebläht. Am Revers ein Namensschild.\nDein Name. Das Schild ist alt und verrostet.",
                  "A transit company jacket floats in the water, the fabric puffed up. A name tag on the lapel.\nYour name. The tag is old and rusty."), 2.5f);
    }
}
