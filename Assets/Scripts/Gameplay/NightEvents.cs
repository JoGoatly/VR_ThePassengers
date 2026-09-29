using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Things that go wrong during the night, spread over the seven nights. Each one has to be
/// fixed in person with a small game:
///   Radio check      - dispatch wants a code confirmed: stop, use the radio (Simon)
///   Fallen tree      - a tree blocks the road: get out and saw it (A/D in turns)
///   Engine failure   - the engine dies, smoke at the back: cables + starter
///   Flat tyre        - bang, the bus pulls to one side and crawls: change the wheel (nuts)
///   Blown fuse       - all lights out: fuse box behind the driver's seat, inside the bus
///   Hand prints      - something slams against the windscreen: wipe it clean outside
///   Collapse         - a passenger collapses in their seat: walk over, CPR, in time
/// </summary>
public class NightEvents : MonoBehaviour
{
    public static NightEvents Instance { get; private set; }

    public enum Kind { RadioCheck, FallenTree, EngineFailure, FlatTire, BlownFuse, Handprints, Collapse }

    public BoardingManager game;
    public BusController bus;
    public ForestRoad road;
    public BusLights lights;

    [Header("Sounds")]
    public AudioClip radioBeep, treeFall, engineStall, tireBang, fuseSpark, handSlam, help, fixedSound;

    [Header("Money")]
    public int radioReward = 10, radioFine = 15, rescueReward = 30, deathFine = 25;

    /// <summary>What E does in the driver's seat right now (null = stand up).</summary>
    public string SeatPrompt => current == Kind.RadioCheck && active ? Loc.T("Funkgerät bedienen", "Use the radio") : null;

    Kind current;
    bool active;
    readonly List<Kind> tonight = new List<Kind>();
    int nextIndex;
    float nextAllowedAt, deadline;
    string objective;
    GameObject marker;          // repair point in the world
    GameObject blocker;         // fallen tree
    Passenger patient;
    SoundManager sound;
    MiniGames games;
    bool started;

    // Smoke puffs from the engine.
    readonly List<(Transform t, float born)> smoke = new List<(Transform, float)>();
    float nextPuff;
    Material smokeMaterial, logMaterial, glowMaterial;

    // Hand prints on the screen while driving.
    readonly List<Vector3> prints = new List<Vector3>();   // x, y, size (virtual screen)

    void Awake() => Instance = this;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (lights == null && bus != null) lights = bus.GetComponent<BusLights>();
        sound = FindAnyObjectByType<SoundManager>();
        games = FindAnyObjectByType<MiniGames>();
        if (road != null)
        {
            logMaterial = road.bark;
            if (road.concrete != null)
            {
                smokeMaterial = new Material(road.concrete) { name = "Smoke" };
                if (smokeMaterial.HasProperty("_MainColor")) smokeMaterial.SetColor("_MainColor", new Color(0.3f, 0.3f, 0.32f));
            }
            glowMaterial = road.lampGlow;
        }
    }

    static Kind[] Plan(int day) => day switch
    {
        1 => new[] { Kind.RadioCheck },
        2 => new[] { Kind.FallenTree, Kind.BlownFuse },
        3 => new[] { Kind.FlatTire, Kind.RadioCheck },
        4 => new[] { Kind.EngineFailure, Kind.Collapse },
        5 => new[] { Kind.Handprints, Kind.FallenTree, Kind.BlownFuse },
        6 => new[] { Kind.EngineFailure, Kind.FlatTire, Kind.Collapse },
        _ => new[] { Kind.Handprints, Kind.EngineFailure, Kind.FallenTree, Kind.Collapse },
    };

    void Update()
    {
        if (game == null || bus == null || GameUI.MenuOpen) return;
        if (!started)
        {
            started = true;
            tonight.AddRange(Plan(Progress.Day));
            nextAllowedAt = Time.time + 50f;
        }
        UpdateSmoke();
        if (active) { UpdateActive(); return; }

        if (nextIndex >= tonight.Count || game.ServingDone) return;
        int quota = DayManager.QuotaFor(Progress.Day);
        int threshold = Mathf.FloorToInt((nextIndex + 1f) * quota / (tonight.Count + 1f));
        bool driving = game.CurrentPhase == BoardingManager.Phase.Driving && !GameUI.PlayerOutside && !GameUI.InBus && bus.SpeedKmh > 12f;
        if (!driving || Time.time < nextAllowedAt || game.Decisions < threshold) return;
        // Somewhere on the next stretch, not right away.
        if (Random.value > Time.deltaTime / 12f) return;

        var kind = tonight[nextIndex];
        if (kind == Kind.Collapse && SeatedRider() == null) { nextAllowedAt = Time.time + 20f; return; }
        nextIndex++;
        Begin(kind);
    }

    // ---------------------------------------------------------------- start

    void Begin(Kind kind)
    {
        current = kind;
        active = true;
        deadline = -1f;
        switch (kind)
        {
            case Kind.RadioCheck:
                Play(radioBeep, bus.transform.position, 0.9f);
                objective = Loc.T("FUNK: Die Leitstelle verlangt einen Funkcheck. Anhalten und Funkgerät bedienen.",
                                  "RADIO: Dispatch wants a radio check. Stop and use the radio.");
                deadline = Time.time + 150f;
                break;

            case Kind.FallenTree:
                if (!PlaceTree()) { active = false; return; }
                Play(treeFall, blocker.transform.position, 1f);
                objective = Loc.T("Ein Baum ist auf die Straße gestürzt! Anhalten, aussteigen und ihn zersägen.",
                                  "A tree fell onto the road! Stop, get out and saw it apart.");
                break;

            case Kind.EngineFailure:
                bus.engineDead = true;
                Play(engineStall, bus.transform.position, 1f);
                marker = RepairPoint(bus.transform, new Vector3(0f, 1f, -6.1f), 1.8f, Loc.T("Motor reparieren", "Repair the engine"), RepairEngine);
                objective = Loc.T("Motorschaden! Der Motor ist hinten - aussteigen und reparieren.",
                                  "Engine failure! The engine is at the back - get out and repair it.");
                break;

            case Kind.FlatTire:
            {
                Play(tireBang, bus.transform.position, 1f);
                var wheel = FindChild(bus.transform, "Wheel_FL") ?? FindChild(bus.transform, "Wheel_FR");
                Vector3 local = wheel != null ? bus.transform.InverseTransformPoint(wheel.position) : new Vector3(1.2f, 0.5f, 3.4f);
                float side = Mathf.Sign(local.x == 0f ? 1f : local.x);
                bus.speedLimitKmh = 18f;
                bus.steerPull = 0.22f * side;
                marker = RepairPoint(bus.transform, new Vector3(local.x + side * 0.9f, 0.6f, local.z), 1.2f, Loc.T("Reifen wechseln", "Change the tyre"), RepairTire);
                objective = Loc.T("Reifenpanne! Der Bus zieht zur Seite. Anhalten und das Rad wechseln.",
                                  "Flat tyre! The bus pulls to one side. Stop and change the wheel.");
                break;
            }

            case Kind.BlownFuse:
            {
                Play(fuseSpark, bus.transform.position, 1f);
                if (lights != null) lights.PowerCut = true;
                var inside = BusInterior.Of(bus.transform);
                Vector3 box = inside != null && inside.Valid ? inside.FuseBox : new Vector3(-0.6f, 1.4f, 3.2f);
                marker = RepairPoint(bus.transform, box, 1.2f, Loc.T("Sicherungskasten öffnen", "Open the fuse box"), RepairFuses);
                objective = Loc.T("Stromausfall! Anhalten, aufstehen - der Sicherungskasten ist hinter dem Fahrersitz.",
                                  "Power cut! Stop, stand up - the fuse box is behind the driver's seat.");
                break;
            }

            case Kind.Handprints:
                prints.Clear();
                for (int i = 0; i < 9; i++)
                    prints.Add(new Vector3(Random.Range(0.15f, 0.85f), Random.Range(0.12f, 0.6f), Random.Range(28f, 46f)));
                for (int i = 0; i < 3; i++) Invoke(nameof(Slam), 0.25f + i * 0.45f);
                marker = RepairPoint(bus.transform, new Vector3(0.6f, 1.2f, 6.1f), 1.8f, Loc.T("Scheibe putzen", "Clean the windscreen"), RepairWindscreen);
                objective = Loc.T("Etwas hat gegen die Scheibe geschlagen... Anhalten, aussteigen und die Scheibe vorne putzen.",
                                  "Something slammed against the windscreen... Stop, get out and clean it at the front.");
                break;

            case Kind.Collapse:
            {
                patient = SeatedRider();
                if (patient == null) { active = false; return; }
                patient.Slump = 1f;
                Play(help, patient.transform.position, 1f);
                marker = RepairPoint(patient.transform, Vector3.zero, 1.3f, Loc.T("Erste Hilfe leisten", "Give first aid"), Rescue, true);
                marker.transform.position = patient.transform.position + Vector3.up * 0.8f;
                string name = patient.Card != null ? patient.Card.FirstName : "?";
                objective = Loc.T($"{name} ist zusammengebrochen! Sofort anhalten und helfen!", $"{name} collapsed! Stop right away and help!");
                deadline = Time.time + 100f;
                break;
            }
        }
        game.ShowToast("! " + objective);
    }

    void Slam() => Play(handSlam, bus.transform.TransformPoint(new Vector3(0f, 1.8f, 5.6f)), 1f);

    Passenger SeatedRider()
    {
        var list = new List<Passenger>();
        foreach (var r in game.Riders)
            if (r != null && r.Sitting && r.CurrentState == Passenger.State.Riding) list.Add(r);
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }

    // ---------------------------------------------------------------- while active

    void UpdateActive()
    {
        if (current == Kind.Collapse && patient == null) { End(false); return; }
        if (deadline > 0f && Time.time > deadline && !MiniGames.Running)
        {
            if (current == Kind.RadioCheck)
            {
                Fine(radioFine, Loc.T("Funkcheck verpasst", "Missed the radio check"));
                End(false);
            }
            else if (current == Kind.Collapse)
            {
                Fine(deathFine, Loc.T("Der Fahrgast hat es nicht geschafft", "The passenger didn't make it"));
                game.RemoveRider(patient);
                End(false);
            }
        }
    }

    /// <summary>E in the driver's seat while the radio check is on (bus standing).</summary>
    public void UseSeatAction()
    {
        if (!active || current != Kind.RadioCheck || games == null) return;
        games.Play(MiniGames.Kind.RadioCode, Loc.T("Funkcheck - Signal einstellen", "Radio check - tune the signal"), ok =>
        {
            if (!ok) return;
            Progress.AddMoney(radioReward);
            Progress.ShiftEarned += radioReward;
            Progress.Save();
            game.ShowToast(Progress.Day >= 5 ? Loc.T($"Funkcheck bestätigt. +{radioReward} €  ...\"Wir hören dich.\"", $"Radio check confirmed. +{radioReward} €  ...\"We hear you.\"")
                                             : Loc.T($"Funkcheck bestätigt. +{radioReward} €", $"Radio check confirmed. +{radioReward} €"));
            End(true);
        }, Difficulty);
    }

    int Difficulty => Progress.Day <= 2 ? 1 : Progress.Day <= 5 ? 2 : 3;

    void RepairEngine()
    {
        games.Play(MiniGames.Kind.Wires, Loc.T("Motor - Kabel wieder anschließen", "Engine - reconnect the cables"), ok =>
        {
            if (!ok) return;
            games.Play(MiniGames.Kind.Starter, Loc.T("Motor - anlassen", "Engine - start it"), ok2 =>
            {
                if (!ok2) return;
                bus.engineDead = false;
                game.ShowToast(Loc.T("Der Motor läuft wieder.", "The engine is running again."));
                End(true);
            }, Difficulty);
        }, Difficulty);
    }

    void RepairTire()
    {
        games.Play(MiniGames.Kind.WheelBolts, Loc.T("Rad wechseln", "Change the wheel"), ok =>
        {
            if (!ok) return;
            bus.speedLimitKmh = float.MaxValue;
            bus.steerPull = 0f;
            game.ShowToast(Loc.T("Reserverad montiert.", "Spare wheel fitted."));
            End(true);
        }, Difficulty);
    }

    void RepairFuses()
    {
        games.Play(MiniGames.Kind.Fuses, Loc.T("Sicherungskasten", "Fuse box"), ok =>
        {
            if (!ok) return;
            if (lights != null) lights.PowerCut = false;
            game.ShowToast(Loc.T("Licht ist wieder an.", "The lights are back on."));
            End(true);
        }, Difficulty);
    }

    void RepairWindscreen()
    {
        games.Play(MiniGames.Kind.Wipe, Loc.T("Windschutzscheibe putzen", "Clean the windscreen"), ok =>
        {
            if (!ok) return;
            prints.Clear();
            game.ShowToast(Loc.T("Die Scheibe ist sauber. Die Abdrücke waren von innen.", "The windscreen is clean. The prints were on the inside."));
            End(true);
        }, Difficulty);
    }

    void SawTree()
    {
        games.Play(MiniGames.Kind.Saw, Loc.T("Baum zersägen", "Saw the tree"), ok =>
        {
            if (!ok) return;
            if (blocker != null)
            {
                // Two halves at the roadside.
                Vector3 c = blocker.transform.position;
                Vector3 r = blocker.transform.right;
                SpawnLog(c + r * (road.laneWidth + 2.6f), blocker.transform.rotation);
                SpawnLog(c - r * (road.laneWidth + 2.6f), blocker.transform.rotation);
                Destroy(blocker);
            }
            game.ShowToast(Loc.T("Die Straße ist frei.", "The road is clear."));
            End(true);
        }, Difficulty);
    }

    void Rescue()
    {
        games.Play(MiniGames.Kind.Cpr, Loc.T("Erste Hilfe", "First aid"), ok =>
        {
            if (patient == null) return;
            if (!ok) return;
            patient.Slump = 0f;
            Progress.AddMoney(rescueReward);
            Progress.ShiftEarned += rescueReward;
            Progress.Save();
            bool odd = patient.Card != null && patient.Card.Truth != Discrepancy.None;
            game.ShowToast(odd ? Loc.T($"Er atmet wieder. Er flüstert: \"Du hättest mich lassen sollen.\" +{rescueReward} €", $"He's breathing again. He whispers: \"You should have let me go.\" +{rescueReward} €")
                               : Loc.T($"Der Fahrgast atmet wieder. Danke! +{rescueReward} €", $"The passenger is breathing again. Thank you! +{rescueReward} €"));
            End(true);
        }, Difficulty);
    }

    void End(bool solved)
    {
        active = false;
        objective = null;
        deadline = -1f;
        if (marker != null) Destroy(marker);
        marker = null;
        patient = null;
        if (solved) Play(fixedSound, Camera.main != null ? Camera.main.transform.position : bus.transform.position, 0.7f);
        nextAllowedAt = Time.time + 45f;
    }

    void Fine(int amount, string why)
    {
        Progress.AddMoney(-amount);
        Progress.ShiftFines += amount;
        Progress.Save();
        game.ShowToast($"{why}: -{amount} €");
    }

    // ---------------------------------------------------------------- world objects

    // Something to use with E (glinting a little so it can be found).
    GameObject RepairPoint(Transform parent, Vector3 local, float radius, string prompt, System.Action use, bool noGlint = false)
    {
        var go = new GameObject("Repair Point");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var i = Inspectable.Add(go, prompt, prompt, prompt, prompt, null, null, null, radius);
        i.Action = _ => { if (!MiniGames.Running && games != null) use(); return null; };
        if (!noGlint && glowMaterial != null)
        {
            var g = MeshKit.Spawn("Glint", go.transform, MeshKit.Box(new Vector3(0.06f, 0.06f, 0.06f), 0.1f), glowMaterial, go.transform.position, Quaternion.identity, false);
            g.AddComponent<GlintBlink>();
        }
        return go;
    }

    bool PlaceTree()
    {
        if (road == null) return false;
        float s = road.BusArcLength + 95f;
        if (!road.TrySample(s, out Vector3 p, out Vector3 t)) return false;
        Vector3 right = Vector3.Cross(Vector3.up, t).normalized;
        blocker = new GameObject("Fallen Tree");
        blocker.transform.SetPositionAndRotation(p, Quaternion.LookRotation(t));
        float length = road.laneWidth * 2f + 5f;
        var trunk = MeshKit.Spawn("Trunk", blocker.transform, MeshKit.Prism(0.38f, length, 7, 1f), logMaterial, p, Quaternion.identity, false);
        trunk.transform.localPosition = new Vector3(length * 0.5f, 0.38f, 0f);
        trunk.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // lying across the road
        foreach (float x in new[] { -2.5f, -0.5f, 1.8f })
        {
            var branch = MeshKit.Spawn("Branch", blocker.transform, MeshKit.Prism(0.08f, 1.6f, 5, 1f), logMaterial, p, Quaternion.identity, false);
            branch.transform.localPosition = new Vector3(x, 0.5f, 0.1f);
            branch.transform.localRotation = Quaternion.Euler(Random.Range(-60f, 60f), Random.Range(0f, 180f), Random.Range(20f, 70f));
        }
        var col = blocker.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.6f, 0f);
        col.size = new Vector3(length, 1.2f, 0.9f);
        // Where you saw: at the right edge of the road, on the bus' side.
        var point = RepairPoint(blocker.transform, new Vector3(road.laneWidth * 0.5f, 0.5f, -1.2f), 2.2f, Loc.T("Baum zersägen", "Saw the tree"), SawTree);
        point.transform.SetParent(blocker.transform, false);
        return true;
    }

    void SpawnLog(Vector3 position, Quaternion rotation)
    {
        var log = MeshKit.Spawn("Log", null, MeshKit.Prism(0.38f, 2.5f, 7, 1f), logMaterial, position + Vector3.up * 0.38f, rotation * Quaternion.Euler(0f, 0f, 90f), false);
        Destroy(log, 120f);
    }

    void UpdateSmoke()
    {
        if (bus != null && bus.engineDead && smokeMaterial != null && Time.time > nextPuff)
        {
            nextPuff = Time.time + 0.18f;
            var puff = MeshKit.Spawn("Smoke", null, MeshKit.Box(new Vector3(0.35f, 0.35f, 0.35f), 0.5f), smokeMaterial,
                bus.transform.TransformPoint(new Vector3(Random.Range(-0.5f, 0.5f), 2.9f, -5.3f)), Random.rotation, false);
            smoke.Add((puff.transform, Time.time));
        }
        for (int i = smoke.Count - 1; i >= 0; i--)
        {
            var (t, born) = smoke[i];
            float age = Time.time - born;
            if (t == null || age > 2.5f)
            {
                if (t != null) Destroy(t.gameObject);
                smoke.RemoveAt(i);
                continue;
            }
            t.position += (Vector3.up * 1.2f + Vector3.right * 0.3f) * Time.deltaTime;
            t.localScale = Vector3.one * (1f + age * 1.4f);
        }
    }

    // ---------------------------------------------------------------- HUD

    void OnGUI()
    {
        if (GameUI.MenuOpen || !active || objective == null) return;
        // Hand prints on the glass, seen from the driver's seat.
        if (current == Kind.Handprints && !GameUI.PlayerOutside && !GameUI.InBus && prints.Count > 0)
        {
            float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
            foreach (var hp in prints)
            {
                float x = hp.x * w, y = hp.y * h, s = hp.z;
                var col = new Color(0.3f, 0.02f, 0.02f, 0.75f);
                RetroGUI.Fill(new Rect(x - s * 0.35f, y, s * 0.7f, s * 0.8f), col);
                for (int f = 0; f < 4; f++)
                    RetroGUI.Fill(new Rect(x - s * 0.35f + f * s * 0.19f, y - s * 0.55f, s * 0.13f, s * 0.55f), col);
                RetroGUI.Fill(new Rect(x + s * 0.35f, y + s * 0.15f, s * 0.35f, s * 0.13f), col);
            }
        }
        bool blink = Mathf.Repeat(Time.time, 1f) < 0.6f;
        string text = (blink ? "! " : "  ") + objective;
        if (deadline > 0f) text += $"  ({Mathf.Max(0, Mathf.CeilToInt(deadline - Time.time))} s)";
        RetroGUI.ShadowLabel(new Rect(8, 22, RetroGUI.VirtualWidth - 16, 14), text, new Color(1f, 0.55f, 0.35f), true, TextAnchor.UpperLeft);
    }

    void Play(AudioClip clip, Vector3 position, float volume)
    {
        if (sound != null && clip != null) sound.PlayWorld(clip, position, volume, 0.4f);
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = FindChild(child, name);
            if (f != null) return f;
        }
        return null;
    }
}
