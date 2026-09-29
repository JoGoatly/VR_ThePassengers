using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Home after the shift: a trailer in the trailer park (model from the pack), far away from
/// the road. After clocking out you are taken there: write the report on the old PC (and pay
/// for tomorrow's energy drinks), then go to bed - which ends the night.
/// You can also look around, go outside and find things. Night by night the place gets
/// darker, dirtier and stranger.
/// </summary>
public class Home : MonoBehaviour
{
    public static Home Instance { get; private set; }

    public BoardingManager game;
    public Vector3 homePosition = new Vector3(0f, -2000f, 0f);
    public AudioClip keyboardSound, knockSound, staticNoise, scare, doorSound;

    /// <summary>Money needed tonight for tomorrow's energy drinks (more every night).</summary>
    public static int DrinkQuota => 10 + 5 * Progress.Day;
    public static int DrinksDelivered => 3 + (Progress.Day >= 4 ? 1 : 0);

    DayManager days;
    Transform root;
    Vector3 spawn, spawnLook;
    bool built, reportDone, drinksPaid;
    AudioSource music;
    SoundManager sound;
    PropKit kit;

    // Report on the PC.
    bool pcOpen;
    int pcFrame, answerPassengers = -1, answerIncidents = -1;
    int[] passengerChoices;

    void Awake() => Instance = this;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        sound = FindAnyObjectByType<SoundManager>();
        GameUI.AtHome = false;
    }

    void OnDestroy() => GameUI.AtHome = false;

    // ---------------------------------------------------------------- going home

    public void GoHome(DayManager from)
    {
        days = from;
        var onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (onFoot == null || onFoot.Walker == null) { days.FinishNight(); return; }
        if (!built) Build();
        if (root == null) { days.FinishNight(); return; }
        PlayerCombat.Fade(2f);
        onFoot.TeleportTo(spawn, spawnLook);
        if (onFoot.Flashlight != null) onFoot.Flashlight.enabled = false;
        GameUI.AtHome = true;
        var clip = Resources.Load<AudioClip>("Music/home");
        if (clip != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.volume = (0.22f - 0.02f * Progress.Day) * GameSettings.Music;
            music.pitch = 1f - 0.02f * (Progress.Day - 1);   // a little slower and sadder every night
            music.Play();
        }
        game.ShowToast(Loc.T("Zuhause. Schreib den Bericht am PC, dann geh ins Bett.", "Home. Write the report on the PC, then go to bed."));
    }

    void Play(AudioClip clip, Vector3 at, float volume = 0.8f)
    {
        if (sound != null && clip != null) sound.PlayWorld(clip, at, volume, 0.6f);
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        built = true;
        root = new GameObject("Home - Trailer Park").transform;
        root.position = homePosition;
        var park = PsxConvert.Spawn("Trailer_Park/Models/Trailer_Park", root, "Trailer_Park/Textures");
        if (park == null) { Destroy(root.gameObject); root = null; return; }
        kit = new PropKit(root, Resources.Load<Material>("PSX/PsxLit"), Resources.Load<Material>("PSX/PsxGlow"));

        // Colliders for everything you could bump into (not the trees and plants).
        foreach (var mf in park.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.name;
            if (n.StartsWith("Tree") || n.StartsWith("Plant") || n.StartsWith("Background") || n.StartsWith("Cable") || n.StartsWith("cable") || n.StartsWith("Door")) continue;
            if (mf.sharedMesh == null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        // Our trailer: the first one; its bed, table, TV, door.
        Transform home = Find(park.transform, "Home", root.position);
        Vector3 centre = home != null ? home.GetComponent<Renderer>() != null ? home.GetComponent<Renderer>().bounds.center : home.position : root.position;
        Transform bed = Find(park.transform, "Bed", centre);
        Transform table = Find(park.transform, "Table", centre);
        Transform tv = Find(park.transform, "TV", centre);
        Transform door = Find(park.transform, "Door", centre);
        Transform fridge = Find(park.transform, "Refrigerator", centre);
        Transform photo = Find(park.transform, "Photo_frame", centre);
        Transform clock = Find(park.transform, "alarm_clock", centre);

        // Start just inside the door, looking in.
        Vector3 doorPos = door != null ? door.position : centre;
        Vector3 inward = Flat(centre - doorPos).normalized;
        if (inward.sqrMagnitude < 0.01f) inward = Vector3.forward;
        spawn = Floor(doorPos + inward * 1.2f);
        spawnLook = inward;

        Degrade(park, centre);

        // Light in the trailer: warm and tidy in the beginning, then weaker and flickering.
        float dread = DayManager.Dread;
        kit.Lamp(root.InverseTransformPoint(centre + Vector3.up * 1.2f), Color.Lerp(new Color(1f, 0.85f, 0.6f), new Color(0.7f, 0.8f, 0.7f), dread),
                 Mathf.Lerp(2.4f, 0.9f, dread), 9f, Mathf.Lerp(0.02f, 0.35f, dread));
        if (bed != null) kit.Lamp(root.InverseTransformPoint(bed.position + Vector3.up * 1.4f), new Color(1f, 0.75f, 0.5f), Mathf.Lerp(1.2f, 0.5f, dread), 5f, dread * 0.3f);

        // The PC for the report, on the table.
        Vector3 pcPos = table != null ? table.position : centre + inward * 2f;
        Vector3 pcLocal = root.InverseTransformPoint(Floor(pcPos));
        kit.Solid(pcLocal + new Vector3(0f, 0.78f, 0f), new Vector3(0.45f, 0.38f, 0.4f), Resources.Load<Material>("PSX/PsxLit"), false);
        kit.Solid(pcLocal + new Vector3(0f, 0.86f, -0.21f), new Vector3(0.36f, 0.26f, 0.02f), Resources.Load<Material>("PSX/PsxGlow"), false);
        kit.Lamp(pcLocal + new Vector3(0f, 1f, -0.5f), new Color(0.5f, 0.9f, 0.6f), 0.7f, 3f);
        var pc = kit.Interact(pcLocal + new Vector3(0f, 1f, 0f), Loc.T("Bericht schreiben", "Write the report"), "PC", null, 1.8f);
        pc.Action = _ => { OpenPc(); return null; };

        // The bed: sleep = end of the night.
        Vector3 bedLocal = root.InverseTransformPoint(bed != null ? bed.position : centre - inward * 2f);
        var sleep = kit.Interact(bedLocal + Vector3.up * 0.5f, Loc.T("Schlafen gehen", "Go to sleep"), Loc.T("Bett", "Bed"), null, 2f);
        sleep.Action = _ => Sleep();

        EasterEggs(park, centre, tv, fridge, photo, clock, bed);
        kit.Finish();
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // Floor below a point (the trailer floor, or the ground outside).
    Vector3 Floor(Vector3 p)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (var h in hits)
            if (h.point.y < p.y + 1.4f && h.point.y > best) best = h.point.y;
        if (best > float.MinValue) p.y = best + 0.05f;
        return p;
    }

    // Nearest child whose name starts with 'prefix'.
    static Transform Find(Transform parent, string prefix, Vector3 near)
    {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(prefix)) continue;
            float d = (t.position - near).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // ---------------------------------------------------------------- darker every night

    void Degrade(GameObject park, Vector3 centre)
    {
        float dread = DayManager.Dread;
        int day = Progress.Day;
        // Everything gets dirtier and greyer.
        var tint = Color.Lerp(Color.white, new Color(0.55f, 0.55f, 0.45f), dread);
        var done = new HashSet<Material>();
        foreach (var r in park.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && done.Add(m) && m.HasProperty("_MainColor")) m.SetColor("_MainColor", m.GetColor("_MainColor") * tint);

        // Rubbish piles up: bags, cans, papers, energy drink cans.
        var bag = Find(park.transform, "Garbage_bag", centre);
        int bags = (day - 1) * 2;
        for (int i = 0; i < bags && bag != null; i++)
        {
            var copy = Instantiate(bag.gameObject, root);
            copy.transform.position = Floor(centre + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-4f, 4f)));
            copy.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        var paper = PropKit.Tinted(Resources.Load<Material>("PSX/PsxLit"), new Color(0.85f, 0.83f, 0.75f), "Paper");
        var can = PropKit.Tinted(Resources.Load<Material>("PSX/PsxLit"), new Color(0.2f, 0.7f, 0.3f), "Can");
        for (int i = 0; i < (day - 1) * 6; i++)
        {
            Vector3 p = root.InverseTransformPoint(Floor(centre + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-5f, 5f))));
            if (i % 2 == 0) kit.Solid(p, new Vector3(0.21f, 0.005f, 0.3f), paper, false, Random.Range(0f, 360f));
            else kit.Solid(p, new Vector3(0.07f, 0.13f, 0.07f), can, false, Random.Range(0f, 360f));
        }

        // From night 4: "13" scratched into the wall. From night 5: the TV is on by itself.
        if (day >= 4)
            kit.Sign(root.InverseTransformPoint(centre + Vector3.up * 1.6f), Vector3.forward, 1f, 0.8f, new[] { "13", day >= 6 ? Loc.T("KOMM", "COME") : "" },
                     new Color32(150, 145, 130, 255), new Color32(90, 10, 10, 255), false);
    }

    // ---------------------------------------------------------------- things to find

    void EasterEggs(GameObject park, Vector3 centre, Transform tv, Transform fridge, Transform photo, Transform clock, Transform bed)
    {
        int day = Progress.Day;
        if (tv != null)
        {
            Vector3 p = root.InverseTransformPoint(tv.position);
            if (day >= 5)
            {
                kit.Lamp(p + new Vector3(0f, 0.3f, 0f), new Color(0.7f, 0.8f, 1f), 1f, 4f, 0.5f);
                kit.Loop(p, staticNoise, 0.12f, 6f);
            }
            kit.Interact(p + Vector3.up * 0.4f, Loc.T("Fernseher", "TV"), Loc.T("Fernseher", "TV"), day switch
            {
                1 => Loc.T("Nachtprogramm. Ein Kochsendung-Wiederholung. Gemütlich.", "Night programme. A cooking show rerun. Cosy."),
                2 => Loc.T("Nachrichten: \"...Nachtlinie 13 wieder in Betrieb. Die Leitstelle dankt dem neuen Fahrer.\"", "News: \"...night line 13 running again. Dispatch thanks the new driver.\""),
                3 => Loc.T("Nachrichten: \"...eine Frau wird seit gestern vermisst. Sie wurde zuletzt an einer Bushaltestelle gesehen.\"", "News: \"...a woman has been missing since yesterday. She was last seen at a bus stop.\""),
                4 => Loc.T("Nur Rauschen. Wenn man lange genug hinsieht, formt es Buchstaben: L-I-N-I-E.", "Only static. If you look long enough, it forms letters: L-I-N-E."),
                5 => Loc.T("Der Fernseher läuft, obwohl der Stecker gezogen ist. Er zeigt deinen Wohnwagen. Von draußen.", "The TV is on although it's unplugged. It shows your trailer. From outside."),
                6 => Loc.T("Auf dem Bildschirm: der Bus. Du sitzt am Steuer. Du drehst dich im Bild zu dir um.", "On the screen: the bus. You are at the wheel. On the screen you turn around to look at yourself."),
                _ => Loc.T("Der Fernseher zeigt nur noch eine Zahl: 13.", "The TV only shows a number now: 13."),
            }, 1.8f);
        }
        if (fridge != null)
        {
            bool taken = false;
            kit.Interact(root.InverseTransformPoint(fridge.position) + Vector3.up * 0.3f, Loc.T("Kühlschrank öffnen", "Open the fridge"), Loc.T("Kühlschrank", "Fridge"), null, 1.6f).Action = _ =>
            {
                if (taken || day == 1) return Loc.T("Leer. Bis auf ein Glas Gurken.", "Empty. Except a jar of pickles.");
                taken = true;
                Progress.Data.energyDrinks++;
                Progress.Save();
                return day >= 5 ? Loc.T("Ein Energy-Drink. Daneben liegt etwas in Frischhaltefolie, das du nicht gekauft hast. (+1 Energy-Drink)", "An energy drink. Next to it lies something in cling film you never bought. (+1 energy drink)")
                                : Loc.T("Ein letzter Energy-Drink ganz hinten. (+1)", "One last energy drink at the back. (+1)");
            };
        }
        if (photo != null)
            kit.Interact(root.InverseTransformPoint(photo.position), Loc.T("Foto ansehen", "Look at the photo"), Loc.T("Foto", "Photo"),
                day <= 2 ? Loc.T("Deine Familie am See. Alle lachen.", "Your family at the lake. Everyone is laughing.")
                : day <= 4 ? Loc.T("Deine Familie am See. Eine Person im Hintergrund, die dir nie aufgefallen ist. Sie trägt eine Busfahrer-Uniform.", "Your family at the lake. A person in the background you never noticed. He wears a bus driver's uniform.")
                : Loc.T("Deine Familie am See. Die Gesichter sind weg. Nur der Mann in Uniform ist noch da - und er steht jetzt vorne.", "Your family at the lake. The faces are gone. Only the man in uniform is left - and now he stands in front."), 1.5f);
        if (clock != null)
            kit.Interact(root.InverseTransformPoint(clock.position), Loc.T("Wecker", "Alarm clock"), Loc.T("Wecker", "Alarm clock"),
                day >= 6 ? Loc.T("Der Wecker ist auf 03:13 gestellt. Du hast ihn nie gestellt.", "The alarm is set to 03:13. You never set it.")
                         : Loc.T("Der Wecker ist auf 22:30 gestellt. Zeit für die nächste Schicht.", "The alarm is set to 22:30. Time for the next shift."), 1.3f);
        if (bed != null && day >= 3)
        {
            kit.Interact(root.InverseTransformPoint(bed.position) + new Vector3(0f, 0.2f, 0f), Loc.T("Unter das Bett schauen", "Look under the bed"), Loc.T("Unter dem Bett", "Under the bed"), null, 1.4f).Action = self =>
            {
                if (day >= 6) Play(scare, self.transform.position, 1f);
                return day >= 6 ? Loc.T("Eine Busfahrer-Mütze. Deine liegt auf dem Stuhl. Diese hier ist nass. Und warm.", "A bus driver's cap. Yours is on the chair. This one is wet. And warm.")
                                : Loc.T("Staub, ein Socken und ein Fahrschein der Linie 13. Gestempelt heute Nacht.", "Dust, a sock and a line 13 ticket. Stamped tonight.");
            };
        }

        // The neighbours: knock on the other trailers' doors.
        int n = 0;
        foreach (var t in park.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("Door") || t.name.StartsWith("Door_Mesh") || (t.position - centre).sqrMagnitude < 36f) continue;
            if (n++ >= 3) break;
            int which = n;
            kit.Interact(root.InverseTransformPoint(t.position), Loc.T("Klopfen", "Knock"), Loc.T("Nachbar", "Neighbour"), null, 1.6f).Action = self =>
            {
                Play(knockSound, self.transform.position, 1f);
                return (which, day) switch
                {
                    (1, <= 3) => Loc.T("\"Ach, der Busfahrer. Fährst du immer noch die 13? Der vor dir hat auch hier gewohnt. In deinem Wagen.\"", "\"Oh, the bus driver. Still driving the 13? The one before you lived here too. In your trailer.\""),
                    (1, _) => Loc.T("Niemand öffnet. Durch das Fenster siehst du: der Wohnwagen ist leer. Seit Jahren.", "Nobody answers. Through the window you see: the trailer is empty. For years."),
                    (2, <= 4) => Loc.T("Ein Kind öffnet einen Spalt. \"Mama sagt, ich soll nicht mit dem Fahrer reden.\" Tür zu.", "A child opens a crack. \"Mum says I shouldn't talk to the driver.\" Door shut."),
                    (2, _) => Loc.T("Hinter der Tür flüstert es: \"Steig nicht ein. Steig nicht ein. Steig nicht ein.\"", "Behind the door a whisper: \"Don't get on. Don't get on. Don't get on.\""),
                    _ => day >= 5 ? Loc.T("Die Tür ist offen. Drinnen: ein Stapel Fahrscheine der Linie 13, bis zur Decke.", "The door is open. Inside: a pile of line 13 tickets, up to the ceiling.")
                                  : Loc.T("Ein alter Mann: \"Nachtschicht? Pass auf dich auf, Junge.\"", "An old man: \"Night shift? Take care of yourself, lad.\""),
                };
            };
        }

        // Late nights: someone stands outside the window and is gone when you go out.
        if (day >= 6 && game != null && game.malePassengers != null && game.malePassengers.Length > 0)
        {
            var who = Instantiate(game.malePassengers[0], root);
            who.transform.position = Floor(centre + new Vector3(4f, 0f, 3f));
            who.transform.rotation = Quaternion.LookRotation(Flat(centre - who.transform.position));
            who.AddComponent<Passenger>();
            var trig = who.AddComponent<ProximityTrigger>();
            trig.radius = 5f;
            trig.Triggered = x => { Play(scare, x.transform.position, 0.9f); Destroy(x.gameObject); };
        }
    }

    // ---------------------------------------------------------------- report and bed

    string Sleep()
    {
        if (!reportDone) return Loc.T("Erst den Bericht am PC schreiben. Die Leitstelle wartet.", "Write the report on the PC first. Dispatch is waiting.");
        if (!drinksPaid)
        {
            var menu = FindAnyObjectByType<MainMenu>();
            menu?.ShowGameOver(Loc.T("EWIGER SCHLAF", "ETERNAL SLEEP"),
                Loc.T($"Du konntest dir die Energy-Drinks für die nächste Schicht nicht leisten ({DrinkQuota} €).\nDu legst dich hin. Nur kurz.\nDu bist nie wieder aufgewacht.",
                      $"You couldn't afford the energy drinks for the next shift ({DrinkQuota} €).\nYou lie down. Just for a moment.\nYou never woke up again."));
            return null;
        }
        PlayerCombat.Fade(2.5f);
        GameUI.AtHome = false;
        if (music != null) Destroy(music);
        days?.FinishNight();
        return null;
    }

    void OpenPc()
    {
        if (reportDone) { game.ShowToast(Loc.T("Der Bericht ist schon verschickt.", "The report has been sent already.")); return; }
        pcOpen = true;
        pcFrame = Time.frameCount;
        GameUI.PcOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        int real = game != null ? game.Decisions : 0;
        passengerChoices = new[] { real, real + 1 + Random.Range(0, 2), Mathf.Max(0, real - 1 - Random.Range(0, 2)) };
        for (int i = 0; i < 3; i++) { int j = Random.Range(0, 3); (passengerChoices[i], passengerChoices[j]) = (passengerChoices[j], passengerChoices[i]); }
        answerPassengers = answerIncidents = -1;
    }

    void ClosePc()
    {
        pcOpen = false;
        GameUI.PcOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!pcOpen || Time.frameCount <= pcFrame + 1) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) ClosePc();
    }

    void OnGUI()
    {
        if (!GameUI.AtHome || GameUI.MenuOpen) return;
        if (!pcOpen)
        {
            if (!reportDone)
                RetroGUI.ShadowLabel(new Rect(8, 22, 400, 14), Loc.T("! Bericht am PC schreiben, dann ins Bett.", "! Write the report on the PC, then go to bed."), new Color(1f, 0.75f, 0.4f), true, TextAnchor.UpperLeft);
            return;
        }
        GUI.depth = -340;
        float w = RetroGUI.VirtualWidth;
        var box = new Rect(w / 2 - 210, 40, 420, 270);
        RetroGUI.Panel(box, 5);
        var ink = new Color(0.6f, 1f, 0.65f);
        var dim = new Color(0.4f, 0.7f, 0.45f);
        RetroGUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 24, 14), Loc.T($"SCHICHTBERICHT - NACHT {Progress.Day}", $"SHIFT REPORT - NIGHT {Progress.Day}"), ink, true);

        // 1: how many passengers were checked?
        RetroGUI.Label(new Rect(box.x + 12, box.y + 30, box.width - 24, 12), Loc.T("1. Wie viele Fahrgäste haben Sie heute kontrolliert?", "1. How many passengers did you check tonight?"), ink, false, true);
        for (int i = 0; i < 3; i++)
            if (RetroGUI.Button(new Rect(box.x + 20 + i * 70, box.y + 44, 60, 16), passengerChoices[i].ToString(),
                    answerPassengers == i ? new Color(0.2f, 0.45f, 0.25f) : new Color(0.08f, 0.14f, 0.09f), ink)) answerPassengers = i;

        // 2: incidents.
        RetroGUI.Label(new Rect(box.x + 12, box.y + 70, box.width - 24, 12), Loc.T("2. Besondere Vorkommnisse?", "2. Any incidents?"), ink, false, true);
        string[] incidents = { Loc.T("Keine", "None"), Loc.T("Technische Probleme", "Technical problems"), Loc.T("Unerklärliches", "Unexplained things") };
        for (int i = 0; i < 3; i++)
            if (RetroGUI.Button(new Rect(box.x + 20 + i * 128, box.y + 84, 120, 16), incidents[i],
                    answerIncidents == i ? new Color(0.2f, 0.45f, 0.25f) : new Color(0.08f, 0.14f, 0.09f), ink)) answerIncidents = i;

        // 3: energy drinks for tomorrow.
        RetroGUI.Label(new Rect(box.x + 12, box.y + 112, box.width - 24, 24),
            Loc.T($"3. Energy-Drinks für die nächste Schicht bestellen: {DrinksDelivered} Dosen - {DrinkQuota} €\n   Ohne Energy-Drinks überstehst du keine Nacht.",
                  $"3. Order energy drinks for the next shift: {DrinksDelivered} cans - {DrinkQuota} €\n   Without energy drinks you won't survive a night."), ink, false, true);
        if (!drinksPaid)
        {
            bool afford = Progress.Money >= DrinkQuota;
            if (RetroGUI.Button(new Rect(box.x + 20, box.y + 140, 180, 18), Loc.T($"BESTELLEN ({DrinkQuota} €)", $"ORDER ({DrinkQuota} €)"),
                    afford ? new Color(0.2f, 0.4f, 0.22f) : new Color(0.3f, 0.08f, 0.06f), ink) && afford)
            {
                Progress.AddMoney(-DrinkQuota);
                Progress.Data.energyDrinks += DrinksDelivered;
                Progress.Save();
                drinksPaid = true;
            }
            if (!afford) RetroGUI.Label(new Rect(box.x + 210, box.y + 142, 200, 12), Loc.T($"Kontostand: {Progress.Money} € - zu wenig!", $"Balance: {Progress.Money} € - not enough!"), new Color(1f, 0.4f, 0.3f), false, true);
        }
        else RetroGUI.Label(new Rect(box.x + 20, box.y + 142, 300, 12), Loc.T("Bestellt. Wird vor die Tür gestellt.", "Ordered. Will be left at the door."), dim, false, true);

        RetroGUI.Label(new Rect(box.x + 12, box.y + 170, box.width - 24, 12), Loc.T($"Kontostand: {Progress.Money} €", $"Balance: {Progress.Money} €"), dim, false, true);
        bool complete = answerPassengers >= 0 && answerIncidents >= 0;
        if (RetroGUI.Button(new Rect(box.x + 20, box.yMax - 34, 180, 20), Loc.T("BERICHT ABSCHICKEN", "SEND REPORT"), complete ? new Color(0.25f, 0.5f, 0.28f) : new Color(0.1f, 0.12f, 0.1f), ink) && complete)
        {
            bool correct = passengerChoices[answerPassengers] == (game != null ? game.Decisions : 0);
            int money = correct ? 5 : -10;
            Progress.AddMoney(money);
            Progress.Save();
            reportDone = true;
            Play(keyboardSound, Camera.main != null ? Camera.main.transform.position : root.position, 0.6f);
            ClosePc();
            game.ShowToast(correct ? Loc.T("Bericht verschickt. Die Leitstelle ist zufrieden. +5 €", "Report sent. Dispatch is satisfied. +5 €")
                                   : Loc.T("Bericht verschickt. Die Zahlen stimmen nicht. -10 €", "Report sent. The numbers don't add up. -10 €"));
        }
        RetroGUI.Label(new Rect(box.x, box.yMax - 12, box.width - 10, 10), Loc.T("Schließen [ESC]", "Close [ESC]"), dim, false, true, TextAnchor.UpperRight);
    }
}
