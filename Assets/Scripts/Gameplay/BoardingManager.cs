using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Game loop at the bus stops:
///   1. A passenger waits at each stop ahead of the bus.
///   2. Stop with the front door at the stop and open the doors (F).
///   3. The passenger steps in right next to the driver and shows the ID card (E hides it).
///   4. Check the name in the register on the on-board computer (Tab), read the mails.
///   5. Let them in (J) or turn them away (N). Until then the bus can't drive off.
/// </summary>
public class BoardingManager : MonoBehaviour
{
    public enum Phase { Driving, PassengerComing, AwaitingDecision, PassengerEntering, PassengerLeaving }

    [Header("References")]
    public ForestRoad road;
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

    [Tooltip("Where a passenger stands while being checked: x = how far in from the door, y = forward(+)/back(-)")]
    public Vector2 entryOffset = new Vector2(0.85f, 0.3f);

    [Header("Rules")]
    public float spawnDistance = 200f;
    public float despawnBehind = 50f;
    [Tooltip("How far the front door may be from the passenger along the road")]
    public float stopTolerance = 7f;
    public int seed = 1311;

    public CitizenRegistry Registry { get; private set; }
    public MailBox Mail { get; } = new MailBox();
    public Phase CurrentPhase { get; private set; } = Phase.Driving;
    public IdCard PendingCard => CurrentPhase == Phase.AwaitingDecision && active != null ? active.Card : null;
    public Texture PendingPortrait => portrait;
    /// <summary>The passenger being checked right now.</summary>
    public Passenger PendingPassenger => PendingCard != null ? active : null;
    public int Correct { get; private set; }
    public int Wrong { get; private set; }
    public string ClockText => FormatClock(GameMinutes);

    /// <summary>Raised after a decision (true = let in).</summary>
    public event System.Action<bool> Decided;
    /// <summary>Raised when a passenger disappears from the bus.</summary>
    public event System.Action<Vector3> PassengerVanished;

    readonly HashSet<Discrepancy> knownRules = new HashSet<Discrepancy>
    {
        Discrepancy.None, Discrepancy.Expired, Discrepancy.WrongBirthDate, Discrepancy.WrongIdNumber, Discrepancy.NotRegistered,
        Discrepancy.WrongExpiry,
    };
    readonly List<Passenger> riders = new List<Passenger>();
    readonly List<Passenger> leaving = new List<Passenger>();   // walking to the rear door
    Vector3 rearDoorLocal;
    float nextExitAt;
    int lastPassedStop = -1;

    /// <summary>Someone wants to get off at the next stop (the "stop" button was pressed).</summary>
    public bool StopRequested { get; private set; }
    // Who got on tonight (and with which model), for passengers who come a second time.
    readonly List<(IdCard card, GameObject prefab)> boardedTonight = new List<(IdCard, GameObject)>();
    readonly Dictionary<IdCard, GameObject> prefabOf = new Dictionary<IdCard, GameObject>();
    readonly List<(float time, System.Action action)> scheduled = new List<(float, System.Action)>();
    Passenger active;
    BusStop activeStop;
    Vector3 doorLocal;        // front door centre in bus space
    Vector3 driverLocal;      // driver seat in bus space
    readonly HashSet<int> storyDone = new HashSet<int>();
    RenderTexture portrait;
    Camera portraitCamera;
    System.Random rng;
    int decisions;
    bool started;
    string toast;
    float toastUntil;

    static string Dispatch => Loc.T("Leitstelle", "Dispatch");

    public float GameMinutes => 23 * 60 + 40 + Time.timeSinceLevelLoad / 8f;

    void Start()
    {
        rng = new System.Random(seed);
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (road == null) road = FindAnyObjectByType<ForestRoad>();

        doorLocal = FindDoorCentre();
        rearDoorLocal = FindDoorCentre("Door_BL", "Door_BR", new Vector3(doorLocal.x, 0f, -2.5f));
        var seat = bus.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "DriverSeat");
        driverLocal = seat != null ? bus.transform.InverseTransformPoint(seat.position) : new Vector3(-0.83f, 0f, 4.15f);
        driverLocal.y = 0f;
        Mail.Received += m => ShowToast(Loc.T("Neue Mail: ", "New mail: ") + m.Subject);
    }

    // The shift starts when the start menu is closed (the language is known by then).
    void BeginShift()
    {
        started = true;
        Registry = new CitizenRegistry(seed + Progress.Day * 101);
        UnlockRules();
        SendWelcomeMails();
    }

    bool policeOnTheWay;

    /// <summary>The police were called for the passenger at the door: no decision until they arrive.</summary>
    public bool PoliceOnTheWay => policeOnTheWay;

    public void PoliceCalled() => policeOnTheWay = true;

    /// <summary>The patrol car stopped behind the bus. Returns true if the passenger was wanted and is taken away.</summary>
    public bool PoliceArrived(Vector3 carDoor)
    {
        policeOnTheWay = false;
        if (CurrentPhase != Phase.AwaitingDecision || active == null) return false;
        var card = active.Card;
        if (card.Truth != Discrepancy.Wanted) return false;   // false alarm: the passenger is still waiting

        var p = active;
        Correct++;
        decisions++;
        Decided?.Invoke(false);
        Judged?.Invoke(true, card);
        Schedule(6f, () => Mail.Send(Loc.T("Polizei Revier Nord", "Police, North Precinct"), Loc.T("Festnahme", "Arrest"),
            Loc.T($"{card.FullName} wurde dank Ihres Anrufs festgenommen. Vielen Dank.", $"{card.FullName} was arrested thanks to your call. Thank you."), ClockText));
        p.CurrentState = Passenger.State.Leaving;
        p.SetSpace(null);
        p.WalkPath(new[] { carDoor }, null, () => { if (p != null) Destroy(p.gameObject); });
        if (activeStop != null) activeStop.WaitingPassenger = null;
        active = null;
        CurrentPhase = Phase.Driving;
        return true;
    }

    /// <summary>Raised after a decision: was it right, and whose card was it.</summary>
    public event System.Action<bool, IdCard> Judged;

    /// <summary>Set when tonight's passengers are done: no more passengers at the stops.</summary>
    [System.NonSerialized] public bool ServingDone;

    /// <summary>How many passengers were dealt with tonight.</summary>
    public int Decisions => decisions;

    Vector3 FindDoorCentre() => FindDoorCentre("Door_FL", "Door_FR", new Vector3(-1.3f, 0, 4.3f));

    Vector3 FindDoorCentre(string a, string b, Vector3 fallback)
    {
        var doors = bus.GetComponentsInChildren<Transform>()
            .Where(t => t.name == a || t.name == b).ToList();
        if (doors.Count == 0) return fallback;
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
        if (!started)
        {
            if (GameUI.MenuOpen) return;
            BeginShift();
        }
        RunScheduled();
        UpdateSpawns();
        HandleKeys();
        UpdateExits();

        switch (CurrentPhase)
        {
            case Phase.Driving:
                TryStartBoarding();
                break;
        }

        bool busy = CurrentPhase != Phase.Driving;
        bool exiting = leaving.Count > 0;
        bus.throttleLockReason = busy ? Loc.T("Fahrgast an der Tür", "Passenger at the door")
                               : exiting ? Loc.T("Fahrgäste steigen aus", "Passengers getting off") : null;
        bus.doorsLocked = busy || exiting;
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || GameUI.TerminalTyping || GameUI.DialogueOpen) return;

        if (GameKeys.Pressed(GameAction.Interact) && PendingCard != null) GameUI.IdCardHidden = !GameUI.IdCardHidden;
        if (PendingCard != null && !policeOnTheWay)
        {
            if (GameKeys.Pressed(GameAction.LetIn)) Decide(true);
            else if (GameKeys.Pressed(GameAction.TurnAway)) Decide(false);
        }
    }

    void UpdateSpawns()
    {
        if (road == null) return;
        float busS = road.BusArcLength;

        foreach (var stop in road.Stops)
        {
            if (stop == null) continue;
            float ahead = stop.arcLength - busS;
            var waiting = stop.WaitingPassenger;

            // One passenger per stop, spawned before the bus sees the stop.
            if (waiting == null && !stop.Visited && !ServingDone && ahead < spawnDistance && ahead > 25f)
            {
                stop.WaitingPassenger = SpawnPassenger(stop);
                stop.Visited = true;
            }
            else if (waiting != null && waiting != active && ahead < -despawnBehind)
            {
                // Bus has left the stop: remove whoever is still standing there.
                Destroy(waiting.gameObject);
                stop.WaitingPassenger = null;
            }
        }
    }

    Passenger SpawnPassenger(BusStop stop)
    {
        int story = NextStoryIndex();
        IdCard card;
        GameObject prefab = null;
        if (story < 0 && boardedTonight.Count >= 2 && Random.value < 0.1f + 0.02f * Progress.Day)
        {
            // Someone who is already sitting in the bus stands at the stop again - same face, same papers.
            var original = boardedTonight[Random.Range(0, boardedTonight.Count)];
            card = Registry.CreateDuplicate(original.card);
            prefab = original.prefab;
        }
        else
        {
            card = Registry.CreatePassengerCard(knownRules, story >= 0 ? StoryPassengers[story].truth : (Discrepancy?)null, 0.3 + 0.05 * Progress.Day, Progress.Day >= 3);
        }
        if (story >= 0)
        {
            card.StoryIndex = story;
            card.IntroLines = StoryPassengers[story].lines;
        }
        var pool = card.Gender == Gender.Male ? malePassengers : femalePassengers;
        if (pool == null || pool.Length == 0) pool = malePassengers != null && malePassengers.Length > 0 ? malePassengers : femalePassengers;
        if (pool == null || pool.Length == 0) return null;

        if (prefab == null) prefab = pool[rng.Next(pool.Length)];
        prefabOf[card] = prefab;

        // Where they get off: one to four stops further. The answer names that stop.
        card.DestinationIndex = stop.index + Random.Range(1, 5);
        string toStop = Loc.T("Zur Haltestelle ", "To the stop ");
        if (card.SaidDestination != null && card.SaidDestination.StartsWith(toStop))
            card.SaidDestination = toStop + road.StopNameAt(card.DestinationIndex) + ".";
        var go = Instantiate(prefab, stop.waitPoint.position, stop.waitPoint.rotation);
        go.name = "Passenger " + card.FullName;
        var p = go.AddComponent<Passenger>();
        p.Card = card;
        p.Stop = stop;
        return p;
    }

    // ------------------------------------------------------------------ getting off

    void UpdateExits()
    {
        if (road == null) return;
        riders.RemoveAll(r => r == null);
        leaving.RemoveAll(r => r == null);
        float busS = road.BusArcLength;
        float doorAhead = doorLocal.z;

        // Next stop ahead: does anyone want to get off there?
        BusStop next = null;
        float best = float.MaxValue;
        foreach (var st in road.Stops)
        {
            if (st == null) continue;
            float d = st.arcLength - busS - doorAhead;
            if (d > -10f && d < best) { best = d; next = st; }
        }
        StopRequested = next != null && riders.Any(r => r.CurrentState == Passenger.State.Riding && r.Card != null &&
                                                          r.Card.DestinationIndex >= 0 && r.Card.DestinationIndex <= next.index);

        // Drove past someone's stop: they complain and get off at the next one.
        foreach (var st in road.Stops)
        {
            if (st == null || st.index <= lastPassedStop || st.arcLength > busS - 25f) continue;
            lastPassedStop = st.index;
            int missed = riders.Count(r => r.CurrentState == Passenger.State.Riding && r.Card != null && r.Card.DestinationIndex == st.index);
            if (missed > 0)
            {
                Progress.AddMoney(-10 * missed);
                ShowToast(Loc.T($"Haltestelle {st.stopName} verpasst: -{10 * missed} €", $"Missed the stop {st.stopName}: -{10 * missed} €"));
            }
        }

        // Standing at a stop with open doors: riders who are there (or past it) get off at the back.
        if (Mathf.Abs(bus.Speed) > 0.3f || !bus.DoorsFullyOpen || Time.time < nextExitAt) return;
        var here = StopNearBus();
        if (here == null) return;
        var rider = riders.FirstOrDefault(r => r.CurrentState == Passenger.State.Riding && r.Card != null &&
                                               r.Card.DestinationIndex >= 0 && r.Card.DestinationIndex <= here.index);
        if (rider == null) return;
        nextExitAt = Time.time + 1.4f;
        StartExit(rider, here);
    }

    BusStop StopNearBus()
    {
        Vector3 doorWorld = bus.transform.TransformPoint(doorLocal);
        foreach (var stop in road.Stops)
        {
            if (stop == null) continue;
            Vector3 d = stop.waitPoint.position - doorWorld;
            d.y = 0f;
            if (Mathf.Abs(Vector3.Dot(d, stop.roadDirection)) < stopTolerance + 4f && Vector3.ProjectOnPlane(d, stop.roadDirection).magnitude < 6f)
                return stop;
        }
        return null;
    }

    void StartExit(Passenger p, BusStop stop)
    {
        riders.Remove(p);
        leaving.Add(p);
        p.CurrentState = Passenger.State.Leaving;
        float side = DoorSide;
        var path = new[]
        {
            new Vector3(0f, floorHeight, rearDoorLocal.z + 0.6f),
            new Vector3(rearDoorLocal.x - side * 0.45f, floorHeight, rearDoorLocal.z),
            new Vector3(rearDoorLocal.x, floorHeight * 0.5f, rearDoorLocal.z),
            new Vector3(rearDoorLocal.x + side * 0.8f, 0f, rearDoorLocal.z),
        };
        p.WalkPath(path, bus.transform, () =>
        {
            // Out of the bus: walk off into the dark and disappear.
            leaving.Remove(p);
            p.SetSpace(null);
            Vector3 outside = p.transform.position;
            Vector3 away = bus.transform.right * side * 6f - stop.roadDirection * 3f;
            p.WalkPath(new[] { outside + away, outside + away * 2.2f }, null, () => { if (p != null) Destroy(p.gameObject); });
            Destroy(p.gameObject, 25f);
        });
    }

    // ------------------------------------------------------------------ story

    // The first passengers of the night are scripted: two real ones (the first explains the
    // job), then one that is not what it seems.
    static (Discrepancy truth, string[] lines)[] StoryPassengers => new[]
    {
        (Discrepancy.None, new[]
        {
            Loc.T("Oh. Ein neues Gesicht.", "Oh. A new face."),
            Loc.T("Sie sehen anders aus als der letzte Busfahrer. ...Der war auf einmal nicht mehr da.",
                  "You look different from the last driver. ...He was just gone one day."),
            Loc.T("Die wechseln oft auf der 13. Keiner weiß so recht, wohin.",
                  "They change a lot on the 13. Nobody really knows where they go."),
            Loc.T($"Na, egal. Hier, mein Ausweis. ({GameKeys.Name(GameAction.Interact)} zum Ausblenden)", $"Anyway. Here's my ID. ({GameKeys.Name(GameAction.Interact)} to hide it)"),
            Loc.T("Schauen Sie im Computer nach. Unter REGISTER meinen Namen eingeben.",
                  "Check it on the computer. Type my name under REGISTER."),
            Loc.T("Geburtsdatum, Ausweisnummer, gültig bis... das muss alles genau stimmen.",
                  "Date of birth, ID number, valid until... it all has to match exactly."),
            Loc.T($"Sie können mich auch etwas fragen. ({GameKeys.Name(GameAction.Talk)})", $"You can ask me something, too. ({GameKeys.Name(GameAction.Talk)})"),
            Loc.T("Was ich sage, sollte zum Register passen. Sonst stimmt was nicht mit mir.",
                  "What I say should match the register. If not, something is wrong with me."),
            Loc.T($"Wenn alles passt, lassen Sie mich rein. ({GameKeys.Name(GameAction.LetIn)})", $"If everything is fine, let me in. ({GameKeys.Name(GameAction.LetIn)})"),
            Loc.T($"Wenn nicht, schicken Sie mich weg. ({GameKeys.Name(GameAction.TurnAway)}) Ich nehm's Ihnen nicht übel.",
                  $"If not, send me away. ({GameKeys.Name(GameAction.TurnAway)}) I won't hold it against you."),
            Loc.T("Und lesen Sie Ihre Mails. Die Leitstelle schreibt nicht ohne Grund.",
                  "And read your mails. Dispatch doesn't write without a reason."),
        }),
        (Discrepancy.None, new[]
        {
            Loc.T("Abend. Schon wieder ein Neuer, hm?", "Evening. Another new one, huh?"),
            Loc.T("Ihr Vorgänger hat zwei Wochen durchgehalten. Der davor nur eine Nacht.",
                  "Your predecessor lasted two weeks. The one before him just one night."),
            Loc.T("Seinen Bus haben sie am Waldfriedhof gefunden. Türen offen, Licht an. Keiner drin.",
                  "They found his bus at the Waldfriedhof. Doors open, lights on. Nobody inside."),
            Loc.T("...Aber Sie machen das bestimmt gut. Hier, mein Ausweis.", "...But I'm sure you'll do fine. Here, my ID."),
        }),
        (Discrepancy.NotRegistered, new[]
        {
            Loc.T("Guten Abend.", "Good evening."),
            Loc.T("Ich fahre jeden Abend mit dieser Linie. Seit Jahren schon.", "I take this line every night. For years now."),
            Loc.T("Die anderen Fahrer kennen mich alle. Sie lassen mich immer einsteigen.",
                  "The other drivers all know me. They always let me on."),
        }),
    };

    int NextStoryIndex()
    {
        if (Progress.Day != 1) return -1;   // the scripted passengers belong to the first night
        for (int i = 0; i < 3; i++)
        {
            if (storyDone.Contains(i)) continue;
            // Still waiting at a stop? Then that one keeps the role.
            bool waiting = road.Stops.Any(st => st != null && st.WaitingPassenger != null &&
                                                st.WaitingPassenger.Card != null && st.WaitingPassenger.Card.StoryIndex == i);
            if (!waiting) return i;
            return -1;   // keep the order: the next story passenger only after this one
        }
        return -1;
    }

    // ------------------------------------------------------------------ boarding

    BusStop StopAtDoor()
    {
        if (road == null) return null;
        Vector3 doorWorld = bus.transform.TransformPoint(doorLocal);
        foreach (var stop in road.Stops)
        {
            if (stop == null) continue;
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

    /// <summary>Front door centre in bus space (at road height).</summary>
    public Vector3 DoorLocal => doorLocal;

    /// <summary>+1 if the door is on the bus' right side, -1 if on the left.</summary>
    float DoorSide => doorLocal.x >= 0f ? 1f : -1f;

    /// <summary>The stop the bus is standing at, for the HUD.</summary>
    public BusStop NearbyStop => StopAtDoor();

    void TryStartBoarding()
    {
        var stop = StopAtDoor();
        if (stop == null) return;
        if (Mathf.Abs(bus.Speed) > 0.3f || !bus.DoorsFullyOpen || GameUI.PlayerOutside) return;

        activeStop = stop;
        active = stop.WaitingPassenger;
        active.CurrentState = Passenger.State.WalkingToDoor;
        CurrentPhase = Phase.PassengerComing;

        // Up the steps and right next to the driver (bus space, the bus doesn't move now).
        active.SetSpace(bus.transform);
        Vector3 start = bus.transform.InverseTransformPoint(active.transform.position);
        float side = DoorSide;
        var outside = new Vector3(doorLocal.x + side * 0.75f, start.y, doorLocal.z);
        var step = new Vector3(doorLocal.x, floorHeight * 0.5f, doorLocal.z);
        var front = EntrySpot;
        var toDriver = driverLocal - new Vector3(front.x, 0f, front.z);
        active.WalkPath(new[] { outside, step, front }, bus.transform, () =>
        {
            active.CurrentState = Passenger.State.AtDoor;
            CurrentPhase = Phase.AwaitingDecision;
            RenderPortrait(active);
            ShowToast(Loc.T("Fahrgast zeigt den Ausweis", "Passenger shows the ID"));
        }, faceAtEnd: toDriver);
    }

    /// <summary>Where the passenger stands while being checked: top of the steps, facing the driver.</summary>
    Vector3 EntrySpot => new Vector3(doorLocal.x - DoorSide * entryOffset.x, floorHeight, doorLocal.z + entryOffset.y);

    // ------------------------------------------------------------------ decision

    public void Decide(bool letIn)
    {
        if (CurrentPhase != Phase.AwaitingDecision || active == null) return;

        var p = active;
        var card = p.Card;
        bool shouldBoard = card.Truth == Discrepancy.None;
        // Wanted persons are neither let in nor sent away: the police must be called.
        bool correct = card.Truth != Discrepancy.Wanted && letIn == shouldBoard;
        if (correct) Correct++; else Wrong++;
        if (card.StoryIndex >= 0) storyDone.Add(card.StoryIndex);
        decisions++;
        ScheduleFeedback(card, letIn, correct);
        Decided?.Invoke(letIn);
        Judged?.Invoke(correct, card);

        // Boarding stamps the ticket - a second one with the same number is already stamped.
        if (letIn && card.HasTicket && string.IsNullOrEmpty(card.TicketStamp))
            card.TicketStamp = $"{CitizenRegistry.Today:dd.MM.} {ClockText}";
        if (letIn && card.Truth != Discrepancy.Duplicate && prefabOf.TryGetValue(card, out var model))
            boardedTonight.Add((card, model));

        if (letIn)
        {
            CurrentPhase = Phase.PassengerEntering;
            p.CurrentState = Passenger.State.Boarding;
            Vector3 spot = FreeSpot();
            var path = new[]
            {
                new Vector3(DoorSide * 0.25f, floorHeight, doorLocal.z - 1.2f),
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
            // Down the steps and back to the stop; the bus may leave once they are out.
            p.CurrentState = Passenger.State.Leaving;
            CurrentPhase = Phase.PassengerLeaving;
            var stop = activeStop;
            var outside = new[]
            {
                new Vector3(doorLocal.x, floorHeight * 0.5f, doorLocal.z),
                new Vector3(doorLocal.x + DoorSide * 0.9f, 0f, doorLocal.z),
            };
            p.WalkPath(outside, bus.transform, () =>
            {
                p.SetSpace(null);
                p.WalkPath(new[] { stop.waitPoint.position }, null, null, stop.waitPoint.forward);
                active = null;
                CurrentPhase = Phase.Driving;
            });
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
        PassengerVanished?.Invoke(p.transform.position);
        Destroy(p.gameObject);
        ShowToast("...");
    }

    void UnlockRules()
    {
        if (Progress.Day >= 2 && knownRules.Add(Discrepancy.Wanted) && Progress.Day == 2)
        {
            Schedule(4f, () => Mail.Send(Dispatch, Loc.T("NEU: Fahndungsliste im Register", "NEW: Wanted list in the register"),
                Loc.T("Ab sofort sind im Einwohnerregister auch Personen mit dem Status GESUCHT markiert.\n\n" +
                "Gesuchte Personen dürfen NICHT befördert werden - und auch nicht einfach weggeschickt werden.\n" +
                "Öffnen Sie Ihr Diensthandy (" + GameKeys.Name(GameAction.Phone) + "), rufen Sie die Polizei (110) und halten Sie die Person an der Tür fest, " +
                "bis der Streifenwagen da ist. Für jede Festnahme gibt es eine Prämie. Fehlalarme kosten Geld.\n\n" +
                "Leitstelle Nachtlinie 13",
                "From now on the register also marks people with the status WANTED.\n\n" +
                "Wanted persons must NOT be transported - and must not simply be sent away either.\n" +
                "Open your work phone (" + GameKeys.Name(GameAction.Phone) + "), call the police (110) and keep the person at the door " +
                "until the patrol car arrives. Every arrest earns a bonus. False alarms cost money.\n\n" +
                "Dispatch, Night Line 13"), ClockText));
        }
        if (Progress.Day >= 3 && knownRules.Add(Discrepancy.Doppelganger) && Progress.Day == 3)
        {
            Schedule(5f, () => Mail.Send(Dispatch, Loc.T("Anomalien auf Linie 13", "Anomalies on line 13"),
                Loc.T("Es wurden Fahrgäste gemeldet, deren Papiere einwandfrei sind, die aber einfache Fragen über ihr " +
                "eigenes Leben falsch beantworten: Geburtsdatum, Wohnort, Beruf.\n\n" +
                "Das sind nicht die Personen, für die sie sich ausgeben.\n\n" +
                "Sprechen Sie jeden Fahrgast an (" + GameKeys.Name(GameAction.Talk) + ") und vergleichen Sie die Antworten mit dem Register. " +
                "Stimmt eine Antwort nicht: NICHT einsteigen lassen.\n\nLeitstelle Nachtlinie 13",
                "Passengers have been reported whose papers are flawless, but who get simple questions about their " +
                "own life wrong: date of birth, home, job.\n\n" +
                "They are not the people they claim to be.\n\n" +
                "Talk to every passenger (" + GameKeys.Name(GameAction.Talk) + ") and compare the answers with the register. " +
                "If an answer is wrong: do NOT let them on.\n\nDispatch, Night Line 13"), ClockText));
        }
        if (Progress.Day >= 3)
        {
            knownRules.Add(Discrepancy.TicketWrongNight);
            knownRules.Add(Discrepancy.TicketUsed);
            knownRules.Add(Discrepancy.TicketWrongDirection);
            if (Progress.Day == 3)
                Schedule(12f, () => Mail.Send(Dispatch, Loc.T("NEU: Fahrscheinkontrolle", "NEW: Ticket check"), Loc.T(
                    "Ab heute zeigt jeder Fahrgast zusätzlich seinen Fahrschein (oben links).\n\n" +
                    "Ein Fahrschein ist nur gültig, wenn:\n" +
                    " - er für die heutige Nacht ausgestellt ist (Datum wie am Armaturenbrett),\n" +
                    " - er noch NICHT entwertet ist (kein roter Stempel),\n" +
                    " - die Fahrtrichtung ENDSTATION ist - das ist die Richtung der Linie 13.\n\n" +
                    "Ungültiger Fahrschein: NICHT einsteigen lassen. Beim Einsteigen wird der Fahrschein automatisch entwertet.\n\n" +
                    "Leitstelle Nachtlinie 13",
                    "From today every passenger also shows a ticket (top left).\n\n" +
                    "A ticket is only valid if:\n" +
                    " - it is issued for tonight (date as on the dashboard),\n" +
                    " - it is NOT stamped yet (no red stamp),\n" +
                    " - the direction is ENDSTATION - that is the direction of line 13.\n\n" +
                    "Invalid ticket: do NOT let them on. The ticket is stamped automatically when boarding.\n\n" +
                    "Dispatch, Night Line 13"), ClockText));
        }
        if (Progress.Day >= 4 && knownRules.Add(Discrepancy.Deceased) && Progress.Day == 4)
        {
            Schedule(5f, () => Mail.Send(Dispatch, Loc.T("DRINGEND: Status VERSTORBEN", "URGENT: Status DECEASED"),
                Loc.T("Fahrer der Nachtlinie melden Fahrgäste, deren Ausweise auf Personen ausgestellt sind, " +
                "die im Register als VERSTORBEN geführt werden.\n\n" +
                "Die Ausweise sind echt. Die Daten stimmen. Lassen Sie diese Personen trotzdem NICHT einsteigen.\n" +
                "Sprechen Sie sie nicht an. Schließen Sie die Türen und fahren Sie weiter.\n\n" +
                "Diese Mail nach dem Lesen löschen.",
                "Night line drivers report passengers whose IDs belong to people " +
                "listed as DECEASED in the register.\n\n" +
                "The IDs are real. The data is correct. Do NOT let these people on anyway.\n" +
                "Do not talk to them. Close the doors and drive on.\n\n" +
                "Delete this mail after reading."), ClockText));
        }
    }

    void ScheduleFeedback(IdCard card, bool letIn, bool correct)
    {
        if (correct) return;

        string subject, body;
        if (!letIn && card.Truth == Discrepancy.Wanted)
        {
            subject = Loc.T("Gesuchte Person entkommen", "Wanted person escaped");
            body = Loc.T($"Sie haben {card.FullName} einfach gehen lassen. Die Person steht auf der Fahndungsliste.\n\n" +
                         "Bei GESUCHTEN Personen rufen Sie über Ihr Diensthandy die Polizei (110) und halten die Person an der Tür fest.",
                         $"You simply let {card.FullName} go. This person is on the wanted list.\n\n" +
                         "For WANTED persons call the police (110) on your work phone and keep the person at the door.");
        }
        else if (!letIn)
        {
            subject = Loc.T("Beschwerde eines Fahrgasts", "Passenger complaint");
            body = Loc.T($"Frau/Herr {card.LastName} hat sich beschwert, an der Haltestelle ohne Grund abgewiesen worden zu sein. " +
                   "Die Ausweisdaten waren korrekt.\n\nBitte prüfen Sie sorgfältiger. Jeder abgewiesene Fahrgast kostet uns Geld.",
                   $"Mr/Ms {card.LastName} complained about being turned away at the stop for no reason. " +
                   "The ID data was correct.\n\nPlease check more carefully. Every passenger turned away costs us money.");
        }
        else
        {
            subject = Loc.T("Vorfall auf Ihrer Linie", "Incident on your line");
            switch (card.Truth)
            {
                case Discrepancy.Expired:
                    body = Loc.T($"Bei einer Kontrolle wurde {card.FullName} mit abgelaufenem Ausweis in Ihrem Bus angetroffen. Abmahnung folgt.",
                                 $"During an inspection {card.FullName} was found in your bus with an expired ID. A warning will follow.");
                    break;
                case Discrepancy.Wanted:
                    body = Loc.T($"Sie haben {card.FullName} befördert. Die Person steht auf der Fahndungsliste. Die Polizei wird sich bei Ihnen melden.",
                                 $"You transported {card.FullName}. This person is on the wanted list. The police will contact you.");
                    break;
                case Discrepancy.Deceased:
                    subject = Loc.T("Wir haben Sie gewarnt", "We warned you");
                    body = Loc.T($"{card.FullName}.\nVerstorben.\nIn Ihrem Bus.\n\nZählen Sie Ihre Fahrgäste.",
                                 $"{card.FullName}.\nDeceased.\nIn your bus.\n\nCount your passengers.");
                    break;
                case Discrepancy.Doppelganger:
                    subject = Loc.T("Wer ist in Ihrem Bus?", "Who is in your bus?");
                    body = Loc.T($"Die echte {card.FullName} lag zur Zeit Ihrer Fahrt zu Hause im Bett und hat das Haus nicht verlassen.\n\n" +
                           "Wen haben Sie mitgenommen?",
                           $"The real {card.FullName} was at home in bed at the time of your trip and never left the house.\n\n" +
                           "Who did you pick up?");
                    break;
                case Discrepancy.WrongExpiry:
                    body = Loc.T($"Der Ausweis von {card.FullName} war gefälscht: das Ablaufdatum stimmte nicht mit dem Register überein.",
                                 $"The ID of {card.FullName} was forged: the expiry date did not match the register.");
                    break;
                case Discrepancy.TicketWrongNight:
                case Discrepancy.TicketUsed:
                case Discrepancy.TicketWrongDirection:
                    subject = Loc.T("Fahrscheinkontrolle", "Ticket inspection");
                    string why = card.Truth == Discrepancy.TicketWrongNight ? Loc.T("für eine andere Nacht ausgestellt", "issued for another night")
                               : card.Truth == Discrepancy.TicketUsed ? Loc.T("bereits entwertet", "already stamped")
                               : Loc.T("für die falsche Fahrtrichtung", "for the wrong direction");
                    body = Loc.T($"Der Fahrschein von {card.FullName} war {why}. Schwarzfahrer werden Ihnen vom Lohn abgezogen.",
                                 $"The ticket of {card.FullName} was {why}. Fare dodgers are deducted from your wages.");
                    break;
                case Discrepancy.Duplicate:
                    subject = Loc.T("Zwei Fahrgäste, ein Name", "Two passengers, one name");
                    body = Loc.T($"{card.FullName} ist heute Nacht zweimal in Ihren Bus gestiegen. Beide sitzen noch drin.\n\n" +
                                 "Jede Person fährt nur EINMAL pro Nacht. Merken Sie sich, wen Sie einsteigen lassen - der Suchverlauf im Register hilft.",
                                 $"{card.FullName} got on your bus twice tonight. Both of them are still sitting in it.\n\n" +
                                 "Every person rides only ONCE per night. Remember who you let on - the search history in the register helps.");
                    break;
                case Discrepancy.NotRegistered:
                    body = Loc.T($"Eine Person namens \"{card.FullName}\" existiert in keinem Register der Stadt. " +
                           "Fahrgäste berichten, sie habe während der Fahrt die ganze Zeit Sie angestarrt.",
                           $"A person named \"{card.FullName}\" does not exist in any register of the town. " +
                           "Passengers report that it stared at you the whole ride.");
                    break;
                default:
                    body = Loc.T($"Die Ausweisdaten von {card.FullName} stimmten nicht mit dem Register überein " +
                           $"({(card.Truth == Discrepancy.WrongBirthDate ? "Geburtsdatum" : "Ausweisnummer")}). Das war eine Fälschung.",
                           $"The ID data of {card.FullName} did not match the register " +
                           $"({(card.Truth == Discrepancy.WrongBirthDate ? "date of birth" : "ID number")}). It was a forgery.");
                    break;
            }
        }
        if (Wrong == 3) body += Loc.T("\n\nDies ist Ihre dritte Verfehlung heute Nacht. Wir beobachten Sie.", "\n\nThis is your third mistake tonight. We are watching you.");
        Schedule(Random.Range(6f, 12f), () => Mail.Send(Dispatch, subject, body, ClockText));
    }

    void SendWelcomeMails()
    {
        if (Progress.Day > 1)
        {
            SendNightMails();
            return;
        }
        string today = CitizenRegistry.Today.ToString("dd.MM.yyyy");
        Mail.Send(Dispatch, Loc.T("Ihre erste Nachtschicht - Linie 13", "Your first night shift - Line 13"), Loc.T(
            "Willkommen bei den Verkehrsbetrieben.\n\n" +
            "Jeder Fahrgast zeigt beim Einsteigen seinen Personalausweis. Prüfen Sie ihn im Register (Reiter REGISTER).\n\n" +
            "Einsteigen darf nur, wer:\n" +
            " - im Register mit genau diesem Namen eingetragen ist,\n" +
            " - das gleiche Geburtsdatum, die gleiche Ausweisnummer und das gleiche Ablaufdatum hat wie im Register,\n" +
            " - einen gültigen Ausweis hat (Ablaufdatum nach dem " + today + "),\n" +
            " - heute Nacht noch NICHT mit Ihnen gefahren ist. Niemand steigt zweimal ein. (Suchverlauf im Register!)\n\n" +
            "Alle anderen weisen Sie ab. Fahren Sie erst weiter, wenn der Fahrgast versorgt ist.\n\n" +
            "Gute Fahrt.\nLeitstelle Nachtlinie 13",
            "Welcome to the transport company.\n\n" +
            "Every passenger shows their ID card when boarding. Check it in the register (tab REGISTER).\n\n" +
            "Only those may board who:\n" +
            " - are listed in the register with exactly this name,\n" +
            " - have the same date of birth, ID number and expiry date as in the register,\n" +
            " - have a valid ID (expiry date after " + today + "),\n" +
            " - have NOT ridden with you tonight yet. Nobody gets on twice. (Search history in the register!)\n\n" +
            "Turn everyone else away. Only drive on once the passenger has been dealt with.\n\n" +
            "Have a good trip.\nDispatch, Night Line 13"), ClockText);
        Schedule(20f, () => Mail.Send(Loc.T("Horst (Kollege)", "Horst (colleague)"), Loc.T("Tipp", "Tip"), Loc.T(
            "Hey, du fährst jetzt die 13? Kleiner Tipp: Tippfehler im Namen sind kein Zufall. " +
            "Und wenn einer am Waldfriedhof einsteigen will... schau lieber zweimal ins Register. Und halt nicht an, wenn da draußen jemand zwischen den Bäumen steht.\n\nHorst",
            "Hey, you're driving the 13 now? Small tip: typos in names are no accident. " +
            "And if someone wants to get on at the Waldfriedhof... better check the register twice. And don't stop if someone is standing out there between the trees.\n\nHorst"), ClockText));
    }

    // Nights 2-7: a short reminder of all rules and a story mail.
    void SendNightMails()
    {
        int day = Progress.Day;
        string today = CitizenRegistry.Today.ToString("dd.MM.yyyy");
        string rulesDe = " - Name genau wie im Register\n - Geburtsdatum, Ausweisnummer und Ablaufdatum wie im Register\n - Ausweis gültig (nach dem " + today + ")";
        string rulesEn = " - name exactly as in the register\n - date of birth, ID number and expiry date as in the register\n - valid ID (after " + today + ")";
        if (day >= 2) { rulesDe += "\n - Status GESUCHT: Polizei rufen (Handy, 110), nicht abweisen"; rulesEn += "\n - status WANTED: call the police (phone, 110), don't turn away"; }
        if (day >= 3) { rulesDe += "\n - falsche Antworten auf Fragen (Doppelgänger): abweisen"; rulesEn += "\n - wrong answers to questions (doppelganger): turn away"; }
        if (day >= 4) { rulesDe += "\n - Status VERSTORBEN: abweisen"; rulesEn += "\n - status DECEASED: turn away"; }
        if (day >= 3) { rulesDe += "\n - Fahrschein: heutige Nacht, nicht entwertet, Richtung ENDSTATION"; rulesEn += "\n - ticket: tonight, not stamped, direction ENDSTATION"; }
        rulesDe += "\n - wer heute Nacht schon eingestiegen ist, steigt nicht noch einmal ein (Suchverlauf!)";
        rulesEn += "\n - whoever already got on tonight does not get on again (search history!)";
        Mail.Send(Dispatch, Loc.T($"Nacht {day} - Dienstanweisung", $"Night {day} - Instructions"), Loc.T(
            $"Heute Nacht: {DayManager.QuotaFor(day)} Fahrgäste. Danach zum Depot.\n\nEinsteigen darf nur, wer:\n" + rulesDe +
            "\n\nRichtige Entscheidungen werden vergütet, Fehler werden vom Lohn abgezogen.\n\nLeitstelle Nachtlinie 13",
            $"Tonight: {DayManager.QuotaFor(day)} passengers. Then to the depot.\n\nOnly those may board who have:\n" + rulesEn +
            "\n\nCorrect decisions are paid, mistakes are deducted from your wages.\n\nDispatch, Night Line 13"), ClockText);

        string from = Loc.T("Horst (Kollege)", "Horst (colleague)");
        switch (day)
        {
            case 2:
                Schedule(25f, () => Mail.Send(from, Loc.T("Das Radio", "The radio"), Loc.T(
                    "Hast du auch das Rauschen auf 66,6 gehört? Wenn das Radio von selbst angeht: mach es aus. Und halt an der nächsten Haltestelle nicht.\n\nHorst",
                    "Did you hear the noise on 66.6 too? If the radio turns on by itself: switch it off. And don't stop at the next bus stop.\n\nHorst"), ClockText));
                break;
            case 3:
                Schedule(25f, () => Mail.Send(from, Loc.T("Das Haus im Wald", "The house in the woods"), Loc.T(
                    "Neben der Strecke gehen manchmal Feldwege in den Wald, mit einer Laterne am Anfang. Ich war in einem der Häuser. " +
                    "Im Keller lag ein Fahrerausweis. Karl Weber. Der ist '95 verschwunden.\n\nNimm dir was zum Wehren mit. Im Laden am Computer gibt's was.\n\nHorst",
                    "Sometimes dirt tracks lead off the route into the forest, with a lantern at the start. I went into one of the houses. " +
                    "There was a driver's badge in the cellar. Karl Weber. He disappeared in '95.\n\nTake something to defend yourself. The shop on the computer has stuff.\n\nHorst"), ClockText));
                break;
            case 4:
                Schedule(20f, () => Mail.Send(Dispatch, Loc.T("Personalmitteilung", "Staff notice"), Loc.T(
                    "Ihr Kollege Horst ist nicht mehr im Dienst. Stellen Sie keine Fragen.\n\nLeitstelle",
                    "Your colleague Horst is no longer on duty. Do not ask questions.\n\nDispatch"), ClockText));
                break;
            case 5:
                Schedule(40f, () => Mail.Send("H.", "...", Loc.T(
                    "sie sind nicht die fahrgäste. WIR sind die fahrgäste. such die keller. such alle vier.",
                    "they are not the passengers. WE are the passengers. search the cellars. find all four."), ClockText));
                break;
            case 6:
                Schedule(20f, () => Mail.Send(Dispatch, Loc.T("Anweisung", "Instruction"), Loc.T(
                    "Betreten Sie keine Gebäude entlang der Strecke. Verlassen Sie den Bus nicht. Das ist eine Anweisung.\n\nLeitstelle",
                    "Do not enter any buildings along the route. Do not leave the bus. This is an instruction.\n\nDispatch"), ClockText));
                break;
            case 7:
                Schedule(20f, () => Mail.Send(Dispatch, Loc.T("Letzte Schicht", "Last shift"), Loc.T(
                    "Dies ist Ihre letzte Schicht. Halten Sie danach am Depot. Danach sind Sie frei.\n\nLeitstelle",
                    "This is your last shift. Stop at the depot afterwards. Then you are free.\n\nDispatch"), ClockText));
                break;
        }
    }

    // ------------------------------------------------------------------ portrait

    // Passport photo: only the passenger is rendered (own layer), and because the night
    // volume is not on that layer, the photo is evenly lit without fog on a grey background.
    const int PortraitLayer = 31;

    void RenderPortrait(Passenger p)
    {
        if (portrait == null)
        {
            portrait = new RenderTexture(256, 144, 24) { filterMode = FilterMode.Point, name = "Passenger Portrait" };
            var camGo = new GameObject("Portrait Camera") { hideFlags = HideFlags.HideAndDontSave };
            portraitCamera = camGo.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.fieldOfView = 18f;
            portraitCamera.nearClipPlane = 0.05f;
            portraitCamera.farClipPlane = 10f;
            portraitCamera.cullingMask = 1 << PortraitLayer;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = new Color(0.55f, 0.56f, 0.58f);
            portraitCamera.targetTexture = portrait;
        }

        Transform head = p.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head"));
        Vector3 face = head != null ? head.position + Vector3.up * 0.06f : p.transform.position + Vector3.up * 1.6f;
        Vector3 forward = p.transform.forward;
        portraitCamera.transform.position = face + forward * 1.2f;
        portraitCamera.transform.LookAt(face);

        var parts = p.GetComponentsInChildren<Transform>(true);
        var layers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) { layers[i] = parts[i].gameObject.layer; parts[i].gameObject.layer = PortraitLayer; }
        try { portraitCamera.Render(); }
        finally { for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i]; }
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
        if (!started) return;
        float w = RetroGUI.VirtualWidth;
        var white = new Color(1f, 0.95f, 0.8f);

        // Next stop.
        if (road != null)
        {
            float busS = road.BusArcLength;
            BusStop next = null;
            float dist = float.MaxValue;
            foreach (var st in road.Stops)
            {
                if (st == null) continue;
                float d = st.arcLength - busS;
                if (d > -8f && d < dist) { dist = d; next = st; }
            }
            if (next != null)
                RetroGUI.ShadowLabel(new Rect(0, 6, w, 14), Loc.T("Nächste Haltestelle: ", "Next stop: ") + $"{next.stopName}  ({Mathf.Max(0f, dist):0} m)", white);
        }

        string prompt = null;
        var stopHere = StopAtDoor();
        switch (CurrentPhase)
        {
            case Phase.Driving:
                if (leaving.Count > 0)
                    prompt = Loc.T("Fahrgäste steigen hinten aus...", "Passengers getting off at the back...");
                else if (stopHere != null)
                    prompt = Mathf.Abs(bus.Speed) > 0.3f ? Loc.T("Anhalten", "Stop the bus") : bus.doorsOpen ? Loc.T("Türen öffnen sich...", "Doors opening...") : Loc.T("Türen öffnen  ", "Open doors  ") + GameKeys.Tag(GameAction.Doors);
                else if (!bus.DoorsFullyClosed)
                    prompt = Loc.T("Türen schließen  ", "Close doors  ") + GameKeys.Tag(GameAction.Doors);
                break;
            case Phase.PassengerComing:
                prompt = Loc.T("Fahrgast kommt zur Tür", "Passenger coming to the door");
                break;
            case Phase.AwaitingDecision:
                prompt = policeOnTheWay ? Loc.T("Die Polizei ist unterwegs...", "The police are on their way...") :
                    Loc.T("Ausweis prüfen, Ansprechen ", "Check the ID, talk ") + GameKeys.Tag(GameAction.Talk) + "  -  " + Loc.T("Einlassen ", "Let in ") + GameKeys.Tag(GameAction.LetIn) + "   " + Loc.T("Abweisen ", "Turn away ") + GameKeys.Tag(GameAction.TurnAway);
                break;
            case Phase.PassengerEntering:
                prompt = Loc.T("Fahrgast steigt ein", "Passenger getting on");
                break;
            case Phase.PassengerLeaving:
                prompt = Loc.T("Fahrgast steigt aus", "Passenger getting off");
                break;
        }
        if (prompt != null) RetroGUI.ShadowLabel(new Rect(0, 300, w, 14), prompt, new Color(1f, 0.85f, 0.3f));

        if (Mail.UnreadCount > 0)
            RetroGUI.ShadowLabel(new Rect(w - 170, 6, 160, 14), Loc.T($"MAIL: {Mail.UnreadCount} ungelesen", $"MAIL: {Mail.UnreadCount} unread"), new Color(0.6f, 1f, 0.7f), true, TextAnchor.UpperRight);

        if (toast != null && Time.time < toastUntil)
            RetroGUI.ShadowLabel(new Rect(0, 24, w, 14), toast, new Color(0.7f, 0.9f, 1f), false);
    }
}
