using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The driving test before the first night - its own scene (Assets/Scenes/Tutorial.unity),
/// a practice ground in daylight whose cones, gates, stops and route are placed in the scene.
/// The examiner stands next to the driver and says step by step what to do: driving,
/// braking, following the route, the slalom, lights, stopping precisely at stop A, the doors,
/// getting up in the bus - and finally stop B against the clock.
/// Passed = back to the main scene: the newspaper, then the first night.
/// </summary>
[DefaultExecutionOrder(-100)]   // decides before the other systems run
public class Tutorial : MonoBehaviour
{
    /// <summary>The driving test is running (night systems wait).</summary>
    public static bool Active { get; private set; }

    public BusController bus;
    public BoardingManager game;
    public MainMenu menu;
    public Examiner examiner;
    public CourseRoute route;
    public CourseStop stopA, stopB;
    public AudioClip stepSound, passSound;
    [Tooltip("Acceleration factor on the practice ground (1 = like at night)")]
    public float learnerAcceleration = 1f;
    [Tooltip("Top speed on the practice ground (km/h) - only in the driving test")]
    public float learnerSpeedLimit = 40f;
    [Tooltip("Seconds for the last task (to stop B)")]
    public float timedLimit = 90f;

    enum Step { Welcome, Throttle, Brake, Slalom, Lights, StopAtStop, OpenDoors, CloseDoors, StandUp, SitDown, Timed, Passed }

    Step step;
    float stepStart, timedDeadline;
    int lightPresses, nextGate, missedGates;
    CourseStop stopTarget;
    CourseGate[] gates;
    TrafficCone[] cones;
    string feedback;
    float feedbackUntil, nextHintAt;
    string hint;
    BusLights lights;
    SoundManager sound;
    bool started, decided;
    AudioSource music;
    readonly Dictionary<CourseGate, float> lastSide = new Dictionary<CourseGate, float>();
    GuideArrow arrows;

    void Awake()
    {
        Active = false;
        BusLights.Daylight = true;   // this component only lives in the driving test scene: daylight, no fog from the first frame
    }

    void OnDestroy()
    {
        Active = false;
        BusLights.Daylight = false;
    }

    // Starts when the menu is closed (in this scene usually right away).
    void Begin()
    {
        decided = true;
        Active = true;
        BusLights.Daylight = true;
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (menu == null) menu = FindAnyObjectByType<MainMenu>();
        if (examiner == null) examiner = FindAnyObjectByType<Examiner>();
        if (route == null) route = FindAnyObjectByType<CourseRoute>();
        var stops = FindObjectsByType<CourseStop>(FindObjectsSortMode.None);
        if (stopA == null) stopA = stops.FirstOrDefault(s => s.stopName == "A");
        if (stopB == null) stopB = stops.FirstOrDefault(s => s.stopName == "B");
        foreach (var s in stops) s.SetTarget(false);
        gates = FindObjectsByType<CourseGate>(FindObjectsSortMode.None).OrderBy(g => g.order).ToArray();
        cones = FindObjectsByType<TrafficCone>(FindObjectsSortMode.None);
        lights = bus != null ? bus.GetComponent<BusLights>() : null;
        sound = FindAnyObjectByType<SoundManager>();
        if (lights != null) lights.Switched += _ => lightPresses++;
        HighlightGate();
        // Learner mode: gentle acceleration and a speed limit on the practice ground.
        if (bus != null)
        {
            bus.acceleration *= learnerAcceleration;
            bus.speedLimitKmh = learnerSpeedLimit;
        }
    }

    void Update()
    {
        if (!decided && !GameUI.MenuOpen && !OfficeIntro.Running && GameUI.ClosedFrame != Time.frameCount) Begin();
        if (OfficeIntro.Running) return;
        if (!Active || GameUI.MenuOpen || bus == null || OfficeIntro.Running) return;
        if (!started)
        {
            started = true;
            if (game != null) game.ServingDone = true;    // nobody waits at the stops today
            Next(Step.Welcome);
            var clip = Resources.Load<AudioClip>("Music/tutorial");
            if (clip != null)
            {
                music = gameObject.AddComponent<AudioSource>();
                music.clip = clip;
                music.loop = true;
                music.volume = 0.25f * GameSettings.Music;
                music.Play();
            }
        }
        if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame && !GameUI.TerminalTyping) { Finish(); return; }

        float speed = bus.SpeedKmh;
        float since = Time.time - stepStart;
        TrackGates();
        UpdateHint();
        switch (step)
        {
            case Step.Welcome:
                // Not F (that's the doors): giving gas starts the test, or it starts by itself.
                if (since > 10f || (since > 1f && GameKeys.Held(GameAction.Forward))) Next(Step.Throttle);
                break;
            case Step.Throttle:
                if (speed > 20f) Next(Step.Brake);
                break;
            case Step.Brake:
                // Steering is learned right away on the way to the slalom (follow the arrows).
                if (speed < 3f) Next(gates.Length > 0 ? Step.Slalom : Step.Lights);
                break;
            case Step.Slalom:
                if (nextGate >= gates.Length)
                {
                    if (missedGates > 1)
                    {
                        Say(Loc.T($"{missedGates} Tore verpasst. Noch eine Runde - nochmal durch den Slalom!", $"{missedGates} gates missed. One more lap - the slalom again!"));
                        nextGate = 0;
                        missedGates = 0;
                        HighlightGate();
                    }
                    else
                    {
                        Say(missedGates == 0 ? Loc.T("Sauber durch den Slalom!", "Clean through the slalom!") : Loc.T("Ein Tor verpasst - geht noch.", "One gate missed - acceptable."));
                        Next(Step.Lights);
                    }
                }
                break;
            case Step.Lights:
                if (lightPresses >= 2) Next(Step.StopAtStop);
                break;
            case Step.StopAtStop:
                if (Mathf.Abs(bus.Speed) < 0.3f && !bus.doorsOpen && NearTarget()) Next(Step.OpenDoors);
                break;
            case Step.OpenDoors:
                if (Mathf.Abs(bus.Speed) > 1f) { Next(Step.StopAtStop); break; }
                if (bus.DoorsFullyOpen) Next(RateStop() ? Step.CloseDoors : Step.StopAtStop);
                break;
            case Step.CloseDoors:
                if (bus.DoorsFullyClosed) Next(Step.StandUp);
                break;
            case Step.StandUp:
                if (GameUI.InBus) Next(Step.SitDown);
                break;
            case Step.SitDown:
                if (!GameUI.InBus && !GameUI.PlayerOutside) Next(Step.Timed);
                break;
            case Step.Timed:
                if (Mathf.Abs(bus.Speed) < 0.3f && bus.DoorsFullyOpen && NearTarget())
                {
                    RateStop();
                    Next(Step.Passed);
                }
                else if (Time.time > timedDeadline)
                {
                    Say(Loc.T("Zu langsam! Nachts wären Sie jetzt gefeuert. Noch einmal - die Zeit läuft.", "Too slow! At night you'd be fired now. Once more - the clock is running."));
                    timedDeadline = Time.time + timedLimit;
                }
                break;
            case Step.Passed:
                if (since > 7f) Finish();
                break;
        }
    }

    void Next(Step s)
    {
        step = s;
        stepStart = Time.time;
        if (s == Step.Slalom) { nextGate = 0; missedGates = 0; lastSide.Clear(); }
        if (s == Step.Lights) lightPresses = 0;
        if (stopTarget != null) stopTarget.SetTarget(false);
        stopTarget = s == Step.StopAtStop || s == Step.OpenDoors ? stopA : s == Step.Timed ? stopB : null;
        if (stopTarget != null) stopTarget.SetTarget(true);
        if (s == Step.Timed) timedDeadline = Time.time + timedLimit;
        HighlightGate();
        Play(s == Step.Passed ? passSound : stepSound);
        if (examiner != null)
        {
            examiner.Talk(3.5f);
            if (s == Step.Slalom && gates.Length > 0) examiner.PointAt(gates[0].Centre);
            if (stopTarget != null) examiner.PointAt(stopTarget.SignPoint);
        }
    }

    // ---------------------------------------------------------------- slalom

    void HighlightGate()
    {
        if (gates == null) return;
        for (int i = 0; i < gates.Length; i++) gates[i].Highlight(step == Step.Slalom && i == nextGate);
    }

    // A gate counts when the bus crosses its line (in the gate's direction): between the cones =
    // passed, beside them = missed. Either way the next gate is up, so the test never gets stuck.
    void TrackGates()
    {
        if (step != Step.Slalom || gates == null || nextGate >= gates.Length) return;
        var g = gates[nextGate];
        Vector3 p = bus.transform.position;
        float side = g.Side(p), across = g.Across(p);
        if (lastSide.TryGetValue(g, out float before) && before < 0f && side >= 0f && across < 25f)
        {
            if (across > g.HalfWidth) { missedGates++; Say(Loc.T("Tor verpasst!", "Gate missed!")); }
            else Say(Loc.T($"Tor {nextGate + 1} - gut!", $"Gate {nextGate + 1} - good!"));
            nextGate++;
            HighlightGate();
            if (nextGate < gates.Length && examiner != null) examiner.PointAt(gates[nextGate].Centre, 2f);
        }
        lastSide[g] = side;
    }

    // ---------------------------------------------------------------- route hints

    // Arrows over the lane show where to drive: along the route, to the next gate, to the stop.
    void UpdateHint()
    {
        hint = null;
        if (arrows == null) arrows = GuideArrow.Create(new Color(0.35f, 1f, 0.45f));
        bool show = route != null && route.Count >= 3 && step != Step.Welcome && step != Step.Passed
                    && !GameUI.InBus && !GameUI.PlayerOutside
                    && step != Step.OpenDoors && step != Step.CloseDoors && step != Step.StandUp && step != Step.SitDown;
        if (!show) { arrows.Hide(); return; }

        Vector3 bp = bus.transform.position;
        Vector3? target = null;
        if (step == Step.Slalom && gates != null && nextGate < gates.Length) target = gates[nextGate].Centre;
        else if (stopTarget != null) target = stopTarget.SignPoint - stopTarget.transform.right * 2.5f;   // the lane next to the sign

        var pts = new Vector3[3];
        var dirs = new Vector3[3];
        int n = 0;
        if (target.HasValue)
        {
            Vector3 to = target.Value - bp; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 35f && Vector3.Dot(to, bus.transform.forward) > 0f)
            {
                // Close: straight to the target, the last arrow on it.
                if (dist < 4f) { arrows.Hide(); return; }
                for (; n < 3; n++)
                {
                    float f = Mathf.Lerp(0.45f, 1f, n / 2f);
                    pts[2 - n] = bp + to * f;
                    dirs[2 - n] = to;
                }
                for (int i = 0; i < 3; i++) pts[i].y = target.Value.y;
                arrows.Show(pts, dirs);
                return;
            }
        }
        // Along the route, 10 / 17 / 24 m ahead (the arrows turn into the corners by themselves).
        for (; n < 3; n++)
        {
            route.PointAhead(bp, 24f - n * 7f, out pts[n], out dirs[n]);
            pts[n].y = bp.y - 0.4f;
        }
        arrows.Show(pts, dirs);

        // The examiner points into the turns, too.
        route.NextTurn(bp, out float dt, out int turn);
        if (turn != 0 && dt < 40f && dt > 6f && Time.time > nextHintAt && examiner != null)
        {
            nextHintAt = Time.time + 8f;
            examiner.Talk(1.5f);
            examiner.PointAt(bp + (bus.transform.forward * 25f + bus.transform.right * 15f * turn), 2.5f);
        }
    }

    // ---------------------------------------------------------------- stops

    bool NearTarget()
    {
        if (stopTarget == null) return false;
        stopTarget.Measure(bus.transform.TransformPoint(game.DoorLocal), out float along, out float across);
        return Mathf.Abs(along) < 14f && across < 7f;
    }

    // How well the bus stands at the stop. Returns false if it has to be done again.
    bool RateStop()
    {
        if (stopTarget == null) return true;
        stopTarget.Measure(bus.transform.TransformPoint(game.DoorLocal), out float along, out float across);
        along = Mathf.Abs(along);
        if (along < 1.2f && across < 2.3f) { Say(Loc.T("Perfekt! Die Tür steht genau am Schild.", "Perfect! The door is right at the sign.")); return true; }
        if (along < 3f && across < 3.4f) { Say(Loc.T("Gut. Nächstes Mal noch etwas genauer.", "Good. A bit more precise next time.")); return true; }
        if (across >= 3.4f)
            Say(Loc.T($"Zu weit vom Bordstein ({across:0.0} m). Türen zu und näher ran!", $"Too far from the kerb ({across:0.0} m). Close the doors and get closer!"));
        else
            Say(Loc.T($"Die Tür ist {along:0.0} m vom Schild weg. Türen zu und nochmal genau ans Schild!", $"The door is {along:0.0} m from the sign. Close the doors and line up with the sign again!"));
        return false;
    }

    void Say(string text)
    {
        feedback = text;
        feedbackUntil = Time.time + 5f;
        if (examiner != null) examiner.Talk(2f);
    }

    void Play(AudioClip clip)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, 0.6f, 0f);
    }

    int ConesHit => cones == null ? 0 : cones.Count(c => c != null && c.Knocked);

    void Finish()
    {
        if (arrows != null) arrows.Hide();
        Active = false;
        BusLights.Daylight = false;
        Progress.Data.tutorialDone = true;
        Progress.Save();
        if (menu != null) menu.AfterTutorial();
    }

    // ---------------------------------------------------------------- text

    string Instruction()
    {
        string K(GameAction a) => GameKeys.Tag(a);
        return step switch
        {
            Step.Welcome => Loc.T($"Guten Tag. Brandt, Ihr Prüfer. Bevor Sie die Nachtlinie 13 fahren, zeigen Sie mir hier auf dem Übungsplatz, dass Sie einen Bus fahren können.\nUmsehen mit der Maus. Los geht's: Gas geben mit {K(GameAction.Forward)}.",
                                  $"Good day. Brandt, your examiner. Before you drive night line 13, show me here on the practice ground that you can drive a bus.\nLook around with the mouse. Let's go: accelerate with {K(GameAction.Forward)}."),
            Step.Throttle => Loc.T($"Gas geben: {K(GameAction.Forward)} gedrückt halten. Fahren Sie über 20 km/h (höchstens 40).", $"Accelerate: hold {K(GameAction.Forward)}. Go faster than 20 km/h (40 at most)."),
            Step.Brake => Loc.T($"Und jetzt bremsen: {K(GameAction.Backward)}. Bis der Bus fast steht.", $"Now brake: {K(GameAction.Backward)}. Until the bus almost stands still."),
            Step.Slalom => Loc.T($"Lenken mit {K(GameAction.SteerLeft)} und {K(GameAction.SteerRight)}. Folgen Sie den grünen Pfeilen und fahren Sie durch die GRÜN leuchtenden Kegel-Tore. Tor {Mathf.Min(nextGate + 1, gates.Length)}/{gates.Length}",
                                 $"Steer with {K(GameAction.SteerLeft)} and {K(GameAction.SteerRight)}. Follow the green arrows and drive through the GREEN glowing cone gates. Gate {Mathf.Min(nextGate + 1, gates.Length)}/{gates.Length}"),
            Step.Lights => Loc.T($"Lenken mit {K(GameAction.SteerLeft)}/{K(GameAction.SteerRight)} - folgen Sie den grünen Pfeilen. Licht: {K(GameAction.Lights)} wechselt zwischen Abblend- und Fernlicht. Das Fernlicht leert einen Akku (Balken am Tacho) - ist er leer, geht ALLES Licht aus, bis er wieder voll ist. Zweimal umschalten.",
                                 $"Steer with {K(GameAction.SteerLeft)}/{K(GameAction.SteerRight)} - follow the green arrows. Lights: {K(GameAction.Lights)} switches between low and high beam. The high beam drains a battery (bar on the speedometer) - when it's empty ALL lights go out until it's full again. Switch twice."),
            Step.StopAtStop => Loc.T("Halten Sie an Haltestelle A (gelbes Feld, das Licht darüber). Die vordere Tür soll genau am H-Schild stehen, nah am Bordstein, gerade.",
                                     "Stop at bus stop A (yellow box, the light above it). The front door should be right at the H sign, close to the kerb, straight."),
            Step.OpenDoors => Loc.T($"Türen öffnen: {K(GameAction.Doors)}. Wie genau Sie halten, bringt Ihnen später Geld - oder kostet welches.", $"Open the doors: {K(GameAction.Doors)}. How precisely you stop will earn you money later - or cost you."),
            Step.CloseDoors => Loc.T($"Türen schließen: {K(GameAction.Doors)}. Mit offenen Türen fährt der Bus nicht.", $"Close the doors: {K(GameAction.Doors)}. The bus won't move with open doors."),
            Step.StandUp => Loc.T($"Im Stand können Sie aufstehen: {K(GameAction.Interact)}. Im Bus herumlaufen, mit Fahrgästen reden, an der Tür aussteigen.", $"When standing still you can get up: {K(GameAction.Interact)}. Walk around the bus, talk to passengers, get out at the door."),
            Step.SitDown => Loc.T($"Gehen Sie zurück zum Fahrersitz und setzen Sie sich: {K(GameAction.Interact)}.", $"Go back to the driver's seat and sit down: {K(GameAction.Interact)}."),
            Step.Timed => Loc.T("Letzte Aufgabe: Nachts haben Sie für jede Haltestelle ein Zeitlimit - zu spät heißt gefeuert. Fahren Sie zu Haltestelle B und öffnen Sie dort rechtzeitig die Türen.",
                                "Last task: at night you have a time limit for every stop - too late means fired. Drive to bus stop B and open the doors there in time."),
            _ => Loc.T($"Bestanden. Glückwunsch.\nIhre erste Schicht beginnt heute Nacht, 23:40 Uhr. Viel Glück. Sie werden es brauchen.",
                       $"Passed. Congratulations.\nYour first shift starts tonight, 11:40 PM. Good luck. You'll need it."),
        };
    }

    void OnGUI()
    {
        if (!Active || !started || GameUI.MenuOpen) return;
        float w = RetroGUI.VirtualWidth;
        var box = new Rect(w / 2 - 210, 250, 420, 62);
        RetroGUI.Panel(box, 4);
        string who = examiner != null ? Loc.T("PRÜFER ", "EXAMINER ") + examiner.displayName.ToUpperInvariant() : Loc.T("PRÜFER", "EXAMINER");
        RetroGUI.Label(new Rect(box.x + 10, box.y + 5, 260, 12), who, new Color(1f, 0.85f, 0.5f), true, true);
        RetroGUI.Label(new Rect(box.xMax - 150, box.y + 5, 140, 12), Loc.T($"Fahrprüfung {(int)step}/{(int)Step.Passed}", $"Driving test {(int)step}/{(int)Step.Passed}"), new Color(0.7f, 0.7f, 0.7f), false, true, TextAnchor.UpperRight);
        RetroGUI.Wrapped(new Rect(box.x + 10, box.y + 17, box.width - 20, box.height - 20), Instruction(), Color.white);
        if (step == Step.Timed)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(timedDeadline - Time.time));
            RetroGUI.ShadowLabel(new Rect(0, 30, w, 14), Loc.T($"Zeit: {left / 60}:{left % 60:00}", $"Time: {left / 60}:{left % 60:00}"), left < 10 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.4f));
        }
        if (feedback != null && Time.time < feedbackUntil)
            RetroGUI.ShadowLabel(new Rect(0, box.y - 18, w, 14), feedback, new Color(0.6f, 1f, 0.6f));
        else if (hint != null)
            RetroGUI.ShadowLabel(new Rect(0, box.y - 18, w, 14), "\"" + hint + "\"", new Color(1f, 0.9f, 0.6f));
        RetroGUI.Label(new Rect(box.x, box.yMax + 2, box.width, 10), Loc.T("Prüfung überspringen [RÜCKTASTE]", "Skip the test [BACKSPACE]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
    }
}
