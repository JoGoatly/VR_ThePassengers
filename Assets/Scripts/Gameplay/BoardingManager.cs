using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Game loop at the bus stops:
///   1. A passenger waits at each stop ahead of the bus.
///   2. Stop with the front door at the stop and open the doors (F).
///   3. The passenger walks to the door and shows the ID card (E to look at it).
///   4. Check the name in the register on the on-board computer (Tab), read the mails.
///   5. Let them in (J) or turn them away (N). Until then the bus can't drive off.
/// </summary>
public class BoardingManager : MonoBehaviour
{
    public enum Phase { Driving, PassengerComing, AwaitingDecision, PassengerEntering }

    [Header("References")]
    public TownBuilder town;
    public BusController bus;
    public GameObject[] malePassengers;
    public GameObject[] femalePassengers;

    [Header("Bus interior (in bus space)")]
    [Tooltip("Height of the bus floor above the road")]
    public float floorHeight = 0.62f;
    [Tooltip("Where passengers stand while riding (x = across, z = along the bus)")]
    public Vector3[] standingSpots =
    {
        new Vector3(-0.3f, 0, 2.6f), new Vector3(0.3f, 0, 2.0f), new Vector3(-0.3f, 0, 1.2f), new Vector3(0.3f, 0, 0.4f),
        new Vector3(-0.3f, 0, -0.4f), new Vector3(0.3f, 0, -1.2f), new Vector3(-0.3f, 0, -2.0f), new Vector3(0.3f, 0, -2.8f),
        new Vector3(-0.3f, 0, -3.6f), new Vector3(0.3f, 0, -4.4f),
    };

    [Header("Rules")]
    public float spawnDistance = 200f;
    public float despawnBehind = 50f;
    [Tooltip("How far the front door may be from the passenger along the road")]
    public float stopTolerance = 7f;
    public float stopCooldown = 60f;
    public int seed = 1311;

    public CitizenRegistry Registry { get; private set; }
    public MailBox Mail { get; } = new MailBox();
    public Phase CurrentPhase { get; private set; } = Phase.Driving;
    public IdCard PendingCard => CurrentPhase == Phase.AwaitingDecision && active != null ? active.Card : null;
    public Texture PendingPortrait => portrait;
    public int Correct { get; private set; }
    public int Wrong { get; private set; }
    public string ClockText => FormatClock(GameMinutes);

    readonly HashSet<Discrepancy> knownRules = new HashSet<Discrepancy>
    {
        Discrepancy.None, Discrepancy.Expired, Discrepancy.WrongBirthDate, Discrepancy.WrongIdNumber, Discrepancy.NotRegistered,
    };
    readonly List<Passenger> riders = new List<Passenger>();
    readonly List<(float time, System.Action action)> scheduled = new List<(float, System.Action)>();
    Passenger active;
    BusStop activeStop;
    Vector3 doorLocal;        // front door centre in bus space
    RenderTexture portrait;
    Camera portraitCamera;
    System.Random rng;
    int decisions;
    string toast;
    float toastUntil;

    float GameMinutes => 23 * 60 + 40 + Time.timeSinceLevelLoad / 8f;

    void Start()
    {
        rng = new System.Random(seed);
        Registry = new CitizenRegistry(seed);
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (town == null) town = FindAnyObjectByType<TownBuilder>();

        doorLocal = FindDoorCentre();
        Mail.Received += m => ShowToast("Neue Mail: " + m.Subject);
        SendWelcomeMails();
    }

    Vector3 FindDoorCentre()
    {
        var doors = bus.GetComponentsInChildren<Transform>()
            .Where(t => t.name == "Door_FL" || t.name == "Door_FR").ToList();
        if (doors.Count == 0) return new Vector3(-1.3f, 0, 4.3f);
        Vector3 sum = Vector3.zero;
        foreach (var d in doors)
        {
            var r = d.GetComponent<Renderer>();
            sum += bus.transform.InverseTransformPoint(r != null ? r.bounds.center : d.position);
        }
        var c = sum / doors.Count;
        return new Vector3(c.x, 0, c.z);
    }

    // ------------------------------------------------------------------ update

    void Update()
    {
        RunScheduled();
        UpdateSpawns();
        HandleKeys();

        switch (CurrentPhase)
        {
            case Phase.Driving:
                TryStartBoarding();
                break;
        }

        bool busy = CurrentPhase != Phase.Driving;
        bus.throttleLockReason = busy ? "Fahrgast an der Tür" : null;
        bus.doorsLocked = busy;
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || GameUI.ComputerOpen) return;

        if (kb.eKey.wasPressedThisFrame && PendingCard != null) GameUI.IdCardOpen = !GameUI.IdCardOpen;
        if (PendingCard != null)
        {
            if (kb.jKey.wasPressedThisFrame) Decide(true);
            else if (kb.nKey.wasPressedThisFrame) Decide(false);
        }
    }

    void UpdateSpawns()
    {
        if (town == null || town.Stops == null) return;
        float busS = town.ArcLengthAt(bus.transform.position);

        foreach (var stop in town.Stops)
        {
            float ahead = town.DistanceAhead(busS, stop.arcLength);
            float behind = town.PathLength - ahead;
            var waiting = stop.WaitingPassenger;

            if (waiting == null)
            {
                if (Time.time >= stop.CooldownUntil && ahead < spawnDistance && ahead > 25f)
                    stop.WaitingPassenger = SpawnPassenger(stop);
            }
            else if (waiting != active && behind > despawnBehind && behind < town.PathLength * 0.5f)
            {
                // Bus has left the stop: remove whoever is still standing there.
                Destroy(waiting.gameObject);
                stop.WaitingPassenger = null;
                stop.CooldownUntil = Time.time + stopCooldown;
            }
        }
    }

    Passenger SpawnPassenger(BusStop stop)
    {
        var card = Registry.CreatePassengerCard(knownRules);
        var pool = card.Gender == Gender.Male ? malePassengers : femalePassengers;
        if (pool == null || pool.Length == 0) pool = malePassengers != null && malePassengers.Length > 0 ? malePassengers : femalePassengers;
        if (pool == null || pool.Length == 0) return null;

        var prefab = pool[rng.Next(pool.Length)];
        var go = Instantiate(prefab, stop.waitPoint.position, stop.waitPoint.rotation);
        go.name = "Passenger " + card.FullName;
        var p = go.AddComponent<Passenger>();
        p.Card = card;
        p.Stop = stop;
        return p;
    }

    BusStop StopAtDoor()
    {
        if (town == null) return null;
        Vector3 doorWorld = bus.transform.TransformPoint(doorLocal);
        foreach (var stop in town.Stops)
        {
            var p = stop.WaitingPassenger;
            if (p == null || p.CurrentState != Passenger.State.Waiting) continue;
            Vector3 d = stop.waitPoint.position - doorWorld;
            d.y = 0f;
            float along = Mathf.Abs(Vector3.Dot(d, stop.roadDirection));
            float across = Vector3.ProjectOnPlane(d, stop.roadDirection).magnitude;
            if (along < stopTolerance && across < 6f) return stop;
        }
        return null;
    }

    /// <summary>The stop the bus is standing at, for the HUD.</summary>
    public BusStop NearbyStop => StopAtDoor();

    void TryStartBoarding()
    {
        var stop = StopAtDoor();
        if (stop == null) return;
        if (Mathf.Abs(bus.Speed) > 0.3f || !bus.DoorsFullyOpen) return;

        activeStop = stop;
        active = stop.WaitingPassenger;
        active.CurrentState = Passenger.State.WalkingToDoor;
        CurrentPhase = Phase.PassengerComing;

        // Walk to just outside the front door (bus space, the bus doesn't move now).
        active.SetSpace(bus.transform);
        Vector3 start = bus.transform.InverseTransformPoint(active.transform.position);
        var outside = new Vector3(doorLocal.x - 0.75f, start.y, doorLocal.z);
        active.WalkPath(new[] { outside }, bus.transform, () =>
        {
            active.CurrentState = Passenger.State.AtDoor;
            CurrentPhase = Phase.AwaitingDecision;
            RenderPortrait(active);
            ShowToast("Fahrgast zeigt den Ausweis  [E]");
        }, faceAtEnd: Vector3.right);
    }

    // ------------------------------------------------------------------ decision

    public void Decide(bool letIn)
    {
        if (CurrentPhase != Phase.AwaitingDecision || active == null) return;
        GameUI.IdCardOpen = false;

        var p = active;
        var card = p.Card;
        bool shouldBoard = card.Truth == Discrepancy.None;
        bool correct = letIn == shouldBoard;
        if (correct) Correct++; else Wrong++;
        decisions++;
        ScheduleFeedback(card, letIn, correct);
        UnlockRules();

        activeStop.CooldownUntil = Time.time + stopCooldown;

        if (letIn)
        {
            CurrentPhase = Phase.PassengerEntering;
            p.CurrentState = Passenger.State.Boarding;
            Vector3 spot = FreeSpot();
            var path = new[]
            {
                new Vector3(doorLocal.x + 0.45f, floorHeight, doorLocal.z),
                new Vector3(0f, floorHeight, doorLocal.z - 1.0f),
                new Vector3(spot.x, floorHeight, spot.z),
            };
            p.WalkPath(path, bus.transform, () =>
            {
                p.CurrentState = Passenger.State.Riding;
                activeStop.WaitingPassenger = null;
                riders.Add(p);
                active = null;
                CurrentPhase = Phase.Driving;
                if (card.Truth == Discrepancy.Deceased) Schedule(Random.Range(25f, 45f), () => Vanish(p));
            }, faceAtEnd: spot.x < 0 ? Vector3.right : Vector3.left);
        }
        else
        {
            // Back to the stop; the bus may leave right away.
            p.CurrentState = Passenger.State.Leaving;
            p.SetSpace(null);
            p.WalkPath(new[] { activeStop.waitPoint.position }, null, null, activeStop.waitPoint.forward);
            active = null;
            CurrentPhase = Phase.Driving;
        }
    }

    Vector3 FreeSpot()
    {
        if (riders.Count >= standingSpots.Length)
        {
            // Bus is full: the oldest rider got off somewhere.
            var oldest = riders[0];
            riders.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
        foreach (var spot in standingSpots)
        {
            bool taken = riders.Any(r => r != null &&
                Vector3.Distance(bus.transform.InverseTransformPoint(r.transform.position), new Vector3(spot.x, floorHeight, spot.z)) < 0.4f);
            if (!taken) return spot;
        }
        return standingSpots[0];
    }

    void Vanish(Passenger p)
    {
        if (p == null) return;
        riders.Remove(p);
        Destroy(p.gameObject);
        ShowToast("...");
    }

    void UnlockRules()
    {
        if (decisions == 2 && knownRules.Add(Discrepancy.Wanted))
        {
            Schedule(4f, () => Mail.Send("Leitstelle", "NEU: Fahndungsliste im Register",
                "Ab sofort sind im Einwohnerregister auch Personen mit dem Status GESUCHT markiert.\n\n" +
                "Gesuchte Personen dürfen NICHT befördert werden. Weisen Sie sie ab, auch wenn der Ausweis gültig ist.\n\n" +
                "Leitstelle Nachtlinie 13", ClockText));
        }
        if (decisions == 4 && knownRules.Add(Discrepancy.Deceased))
        {
            Schedule(5f, () => Mail.Send("Leitstelle", "DRINGEND: Status VERSTORBEN",
                "Fahrer der Nachtlinie melden Fahrgäste, deren Ausweise auf Personen ausgestellt sind, " +
                "die im Register als VERSTORBEN geführt werden.\n\n" +
                "Die Ausweise sind echt. Die Daten stimmen. Lassen Sie diese Personen trotzdem NICHT einsteigen.\n" +
                "Sprechen Sie sie nicht an. Schließen Sie die Türen und fahren Sie weiter.\n\n" +
                "Diese Mail nach dem Lesen löschen.", ClockText));
        }
    }

    void ScheduleFeedback(IdCard card, bool letIn, bool correct)
    {
        if (correct)
        {
            if (!letIn && card.Truth == Discrepancy.Wanted)
                Schedule(8f, () => Mail.Send("Polizei Revier Nord", "Danke für Ihre Meldung",
                    $"Die abgewiesene Person ({card.FullName}) wurde kurz darauf festgenommen. Danke für Ihre Aufmerksamkeit.", ClockText));
            return;
        }

        string subject, body;
        if (!letIn)
        {
            subject = "Beschwerde eines Fahrgasts";
            body = $"Frau/Herr {card.LastName} hat sich beschwert, an der Haltestelle ohne Grund abgewiesen worden zu sein. " +
                   "Die Ausweisdaten waren korrekt.\n\nBitte prüfen Sie sorgfältiger. Jeder abgewiesene Fahrgast kostet uns Geld.";
        }
        else
        {
            subject = "Vorfall auf Ihrer Linie";
            switch (card.Truth)
            {
                case Discrepancy.Expired:
                    body = $"Bei einer Kontrolle wurde {card.FullName} mit abgelaufenem Ausweis in Ihrem Bus angetroffen. Abmahnung folgt.";
                    break;
                case Discrepancy.Wanted:
                    body = $"Sie haben {card.FullName} befördert. Die Person steht auf der Fahndungsliste. Die Polizei wird sich bei Ihnen melden.";
                    break;
                case Discrepancy.Deceased:
                    subject = "Wir haben Sie gewarnt";
                    body = $"{card.FullName}.\nVerstorben.\nIn Ihrem Bus.\n\nZählen Sie Ihre Fahrgäste.";
                    break;
                case Discrepancy.NotRegistered:
                    body = $"Eine Person namens \"{card.FullName}\" existiert in keinem Register der Stadt. " +
                           "Fahrgäste berichten, sie habe während der Fahrt die ganze Zeit Sie angestarrt.";
                    break;
                default:
                    body = $"Die Ausweisdaten von {card.FullName} stimmten nicht mit dem Register überein " +
                           $"({(card.Truth == Discrepancy.WrongBirthDate ? "Geburtsdatum" : "Ausweisnummer")}). Das war eine Fälschung.";
                    break;
            }
        }
        if (Wrong == 3) body += "\n\nDies ist Ihre dritte Verfehlung heute Nacht. Wir beobachten Sie.";
        Schedule(Random.Range(6f, 12f), () => Mail.Send("Leitstelle", subject, body, ClockText));
    }

    void SendWelcomeMails()
    {
        Mail.Send("Leitstelle", "Ihre erste Nachtschicht - Linie 13",
            "Willkommen bei den Verkehrsbetrieben.\n\n" +
            "Jeder Fahrgast zeigt beim Einsteigen seinen Personalausweis. Prüfen Sie ihn im Register (Reiter REGISTER).\n\n" +
            "Einsteigen darf nur, wer:\n" +
            " - im Register mit genau diesem Namen eingetragen ist,\n" +
            " - das gleiche Geburtsdatum und die gleiche Ausweisnummer hat wie im Register,\n" +
            " - einen gültigen Ausweis hat (Ablaufdatum nach dem " + CitizenRegistry.Today.ToString("dd.MM.yyyy") + ").\n\n" +
            "Alle anderen weisen Sie ab. Fahren Sie erst weiter, wenn der Fahrgast versorgt ist.\n\n" +
            "Gute Fahrt.\nLeitstelle Nachtlinie 13", ClockText);
        Schedule(20f, () => Mail.Send("Horst (Kollege)", "Tipp",
            "Hey, du fährst jetzt die 13? Kleiner Tipp: Tippfehler im Namen sind kein Zufall. " +
            "Und wenn einer an der Friedhofstraße einsteigen will... schau lieber zweimal ins Register.\n\nHorst", ClockText));
    }

    // ------------------------------------------------------------------ portrait

    void RenderPortrait(Passenger p)
    {
        if (portrait == null)
        {
            portrait = new RenderTexture(96, 112, 24) { filterMode = FilterMode.Point, name = "Passenger Portrait" };
            var camGo = new GameObject("Portrait Camera") { hideFlags = HideFlags.HideAndDontSave };
            portraitCamera = camGo.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.fieldOfView = 22f;
            portraitCamera.nearClipPlane = 0.05f;
            portraitCamera.farClipPlane = 30f;
            portraitCamera.targetTexture = portrait;
        }

        Transform head = p.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head"));
        Vector3 face = head != null ? head.position + Vector3.up * 0.08f : p.transform.position + Vector3.up * 1.6f;
        Vector3 forward = p.transform.forward;
        portraitCamera.transform.position = face + forward * 1.1f;
        portraitCamera.transform.LookAt(face);
        portraitCamera.Render();
    }

    // ------------------------------------------------------------------ helpers

    void Schedule(float delay, System.Action action) => scheduled.Add((Time.time + delay, action));

    void RunScheduled()
    {
        for (int i = scheduled.Count - 1; i >= 0; i--)
        {
            if (Time.time < scheduled[i].time) continue;
            var a = scheduled[i].action;
            scheduled.RemoveAt(i);
            a?.Invoke();
        }
    }

    public void ShowToast(string text)
    {
        toast = text;
        toastUntil = Time.time + 4f;
    }

    static string FormatClock(float minutes)
    {
        int m = Mathf.FloorToInt(minutes) % (24 * 60);
        return $"{m / 60:00}:{m % 60:00}";
    }

    // ------------------------------------------------------------------ HUD

    void OnGUI()
    {
        if (GameUI.ComputerOpen) return;
        float w = RetroGUI.VirtualWidth;
        var white = new Color(1f, 0.95f, 0.8f);

        // Next stop.
        if (town != null && town.Stops != null && town.Stops.Count > 0)
        {
            float busS = town.ArcLengthAt(bus.transform.position);
            var next = town.Stops.OrderBy(s => town.DistanceAhead(busS, s.arcLength)).First();
            float dist = town.DistanceAhead(busS, next.arcLength);
            if (dist > town.PathLength - 15f) dist = 0f;
            RetroGUI.ShadowLabel(new Rect(0, 6, w, 14), $"Nächste Haltestelle: {next.stopName}  ({dist:0} m)", white);
        }

        string prompt = null;
        var stopHere = StopAtDoor();
        switch (CurrentPhase)
        {
            case Phase.Driving:
                if (stopHere != null)
                    prompt = Mathf.Abs(bus.Speed) > 0.3f ? "Anhalten" : bus.doorsOpen ? "..." : "Türen öffnen  [F]";
                else if (!bus.DoorsFullyClosed)
                    prompt = "Türen schließen  [F]";
                break;
            case Phase.PassengerComing:
                prompt = "Fahrgast kommt zur Tür";
                break;
            case Phase.AwaitingDecision:
                prompt = "Ausweis [E]   Computer [Tab]   Einsteigen [J]   Abweisen [N]";
                break;
            case Phase.PassengerEntering:
                prompt = "Fahrgast steigt ein";
                break;
        }
        if (prompt != null) RetroGUI.ShadowLabel(new Rect(0, 300, w, 14), prompt, new Color(1f, 0.85f, 0.3f));

        if (Mail.UnreadCount > 0)
            RetroGUI.ShadowLabel(new Rect(w - 170, 6, 160, 14), $"MAIL: {Mail.UnreadCount} ungelesen  [Tab]", new Color(0.6f, 1f, 0.7f), true, TextAnchor.UpperRight);

        if (toast != null && Time.time < toastUntil)
            RetroGUI.ShadowLabel(new Rect(0, 24, w, 14), toast, new Color(0.7f, 0.9f, 1f), false);
    }
}
