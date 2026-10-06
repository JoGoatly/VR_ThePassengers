using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The driving test before the first night, in daylight on its own map: a fenced practice
/// ground (TrainingYard) with a painted circuit, a cone slalom and two bus stops - no forest.
/// An examiner explains the controls step by step - driving, braking, the slalom, lights,
/// stopping precisely at a bus stop, the doors, getting up in the bus - and finally a stop
/// against the clock. Passed = the newspaper intro, then the first night.
/// </summary>
[DefaultExecutionOrder(-100)]   // decides before the night systems run
public class Tutorial : MonoBehaviour
{
    /// <summary>The driving test is running (night systems wait).</summary>
    public static bool Active { get; private set; }

    public BusController bus;
    public BoardingManager game;
    public MainMenu menu;
    public AudioClip stepSound, passSound;

    [Tooltip("Where the practice ground is built (far away from the forest)")]
    public Vector3 yardPosition = new Vector3(0f, -1000f, 0f);
    public float timedLimit = 80f;

    enum Step { Welcome, Throttle, Brake, Steer, Lights, StopAtStop, OpenDoors, CloseDoors, StandUp, SitDown, Timed, Passed }

    Step step;
    float stepStart, topSpeed, timedDeadline;
    int lightPresses;
    TrainingYard yard;
    TrainingYard.Stop stopTarget;
    string feedback;
    float feedbackUntil;
    ForestRoad road;
    BusLights lights;
    SoundManager sound;
    bool started;
    AudioSource music;

    bool decided;

    void Awake()
    {
        Active = false;
        BusLights.Daylight = false;
    }

    void OnDestroy()
    {
        Active = false;
        BusLights.Daylight = false;
    }

    // Decided when the menu closes (a new game resets the save first).
    void Begin()
    {
        decided = true;
        Active = !Progress.Data.tutorialDone && Progress.Day == 1;
        BusLights.Daylight = Active;
        if (!Active) { enabled = false; return; }
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (menu == null) menu = FindAnyObjectByType<MainMenu>();
        road = FindAnyObjectByType<ForestRoad>();
        lights = bus != null ? bus.GetComponent<BusLights>() : null;
        sound = FindAnyObjectByType<SoundManager>();
        if (lights != null) lights.Switched += _ => lightPresses++;

        // Daylight.
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.intensity = 1.4f; l.color = new Color(1f, 0.95f, 0.86f); }
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
        var stars = FindAnyObjectByType<StarSky>();
        if (stars != null) stars.gameObject.SetActive(false);

        // Own map: the practice ground instead of the forest.
        if (road != null)
            foreach (Transform child in road.transform) child.gameObject.SetActive(false);
        yard = TrainingYard.Build(road, bus, yardPosition);
        if (bus != null) bus.ResetTo(yard.StartPosition + Vector3.up * bus.GroundOffset, yard.StartRotation);
    }

    void Update()
    {
        if (!decided && !GameUI.MenuOpen) Begin();
        if (!Active || GameUI.MenuOpen || bus == null) return;
        if (!started)
        {
            started = true;
            game.ServingDone = true;    // nobody waits at the stops today
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
        topSpeed = Mathf.Max(topSpeed, speed);
        float since = Time.time - stepStart;
        switch (step)
        {
            case Step.Welcome:
                if (since > 6f || GameKeys.Pressed(GameAction.Continue)) Next(Step.Throttle);
                break;
            case Step.Throttle:
                if (speed > 20f) Next(Step.Brake);
                break;
            case Step.Brake:
                if (speed < 3f) Next(Step.Steer);
                break;
            case Step.Steer:
                if (yard.SlalomDone)
                {
                    if (yard.MissedGates > 1)
                    {
                        Say(Loc.T($"{yard.MissedGates} Tore verpasst. Noch eine Runde - nochmal durch den Slalom!", $"{yard.MissedGates} gates missed. One more lap - the slalom again!"));
                        yard.ResetSlalom();
                    }
                    else
                    {
                        Say(yard.MissedGates == 0 ? Loc.T("Sauber durch den Slalom!", "Clean through the slalom!") : Loc.T("Ein Tor verpasst - geht noch.", "One gate missed - acceptable."));
                        yard.TrackGates = false;
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
                if (since > 6f) Finish();
                break;
        }
    }

    void Next(Step s)
    {
        step = s;
        stepStart = Time.time;
        if (s == Step.Steer && yard != null) { yard.ResetSlalom(); yard.TrackGates = true; }
        if (s == Step.Lights) lightPresses = 0;
        if (s == Step.StopAtStop && yard != null) stopTarget = yard.StopA;
        if (s == Step.Timed && yard != null) { stopTarget = yard.StopB; timedDeadline = Time.time + timedLimit; }
        Play(s == Step.Passed ? passSound : stepSound);
    }

    // The bus stands at the target stop (roughly - how well is rated when the doors open).
    bool NearTarget()
    {
        if (stopTarget == null) return false;
        TrainingYard.Measure(stopTarget, bus.transform.TransformPoint(game.DoorLocal), out float along, out float across);
        return Mathf.Abs(along) < 14f && across < 7f;
    }

    // How well the bus stands at the stop. Returns false if it has to be done again.
    bool RateStop()
    {
        if (stopTarget == null) return true;
        TrainingYard.Measure(stopTarget, bus.transform.TransformPoint(game.DoorLocal), out float along, out float across);
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
    }

    void Play(AudioClip clip)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, 0.6f, 0f);
    }

    void Finish()
    {
        Active = false;
        BusLights.Daylight = false;
        Progress.Data.tutorialDone = true;
        Progress.Save();
        if (menu != null) menu.AfterTutorial();
    }

    string Instruction()
    {
        string K(GameAction a) => GameKeys.Tag(a);
        return step switch
        {
            Step.Welcome => Loc.T($"Guten Tag. Ich bin Ihr Prüfer. Bevor Sie die Nachtlinie 13 fahren, zeigen Sie mir, dass Sie einen Bus fahren können.\nUmsehen mit der Maus. Weiter {K(GameAction.Continue)}",
                                  $"Good day. I'm your examiner. Before you drive night line 13, show me that you can drive a bus.\nLook around with the mouse. Continue {K(GameAction.Continue)}"),
            Step.Throttle => Loc.T($"Gas geben: {K(GameAction.Forward)} gedrückt halten. Fahren Sie über 20 km/h.", $"Accelerate: hold {K(GameAction.Forward)}. Go faster than 20 km/h."),
            Step.Brake => Loc.T($"Und jetzt bremsen: {K(GameAction.Backward)}. Bis der Bus fast steht.", $"Now brake: {K(GameAction.Backward)}. Until the bus almost stands still."),
            Step.Steer => Loc.T($"Lenken mit {K(GameAction.SteerLeft)} und {K(GameAction.SteerRight)}. Folgen Sie der Spur um die Kurve und fahren Sie Slalom durch die GRÜNEN Kegel-Tore. Tor {Mathf.Min(yard.NextGate + 1, yard.GateCount)}/{yard.GateCount}",
                                $"Steer with {K(GameAction.SteerLeft)} and {K(GameAction.SteerRight)}. Follow the lane round the bend and slalom through the GREEN cone gates. Gate {Mathf.Min(yard.NextGate + 1, yard.GateCount)}/{yard.GateCount}"),
            Step.Lights => Loc.T($"Licht: {K(GameAction.Lights)} wechselt zwischen Abblend- und Fernlicht. Das Fernlicht leert einen Akku (Balken am Tacho) - ist er leer, geht ALLES Licht aus, bis er wieder voll ist. Zweimal umschalten.",
                                 $"Lights: {K(GameAction.Lights)} switches between low and high beam. The high beam drains a battery (bar on the speedometer) - when it's empty ALL lights go out until it's full again. Switch twice."),
            Step.StopAtStop => Loc.T("Halten Sie an Haltestelle A (gelbes Feld). Die vordere Tür soll genau am H-Schild stehen, nah am Bordstein, gerade.",
                                     "Stop at bus stop A (yellow box). The front door should be right at the H sign, close to the kerb, straight."),
            Step.OpenDoors => Loc.T($"Türen öffnen: {K(GameAction.Doors)}. Wie genau Sie halten, bringt Ihnen später Geld - oder kostet welches.", $"Open the doors: {K(GameAction.Doors)}. How precisely you stop will earn you money later - or cost you."),
            Step.CloseDoors => Loc.T($"Türen schließen: {K(GameAction.Doors)}. Mit offenen Türen fährt der Bus nicht.", $"Close the doors: {K(GameAction.Doors)}. The bus won't move with open doors."),
            Step.StandUp => Loc.T($"Im Stand können Sie aufstehen: {K(GameAction.Interact)}. Im Bus herumlaufen, mit Fahrgästen reden, an der Tür aussteigen.", $"When standing still you can get up: {K(GameAction.Interact)}. Walk around the bus, talk to passengers, get out at the door."),
            Step.SitDown => Loc.T($"Gehen Sie zurück zum Fahrersitz und setzen Sie sich: {K(GameAction.Interact)}.", $"Go back to the driver's seat and sit down: {K(GameAction.Interact)}."),
            Step.Timed => Loc.T("Letzte Aufgabe: Nachts haben Sie für jede Haltestelle ein Zeitlimit - zu spät heißt gefeuert. Fahren Sie eine Runde zu Haltestelle B und öffnen Sie dort rechtzeitig die Türen.",
                                "Last task: at night you have a time limit for every stop - too late means fired. Drive a lap to bus stop B and open the doors there in time."),
            _ => Loc.T($"Bestanden. Glückwunsch. Umgefahrene Kegel: {yard.ConesHit}.\nIhre erste Schicht beginnt heute Nacht, 23:40 Uhr. Viel Glück. Sie werden es brauchen.",
                       $"Passed. Congratulations. Cones knocked over: {yard.ConesHit}.\nYour first shift starts tonight, 11:40 PM. Good luck. You'll need it."),
        };
    }

    void OnGUI()
    {
        if (!Active || !started || GameUI.MenuOpen) return;
        float w = RetroGUI.VirtualWidth;
        var box = new Rect(w / 2 - 210, 250, 420, 62);
        RetroGUI.Panel(box, 4);
        RetroGUI.Label(new Rect(box.x + 10, box.y + 5, 200, 12), Loc.T("PRÜFER", "EXAMINER"), new Color(1f, 0.85f, 0.5f), true, true);
        RetroGUI.Label(new Rect(box.xMax - 150, box.y + 5, 140, 12), Loc.T($"Fahrprüfung {(int)step}/{(int)Step.Passed}", $"Driving test {(int)step}/{(int)Step.Passed}"), new Color(0.7f, 0.7f, 0.7f), false, true, TextAnchor.UpperRight);
        RetroGUI.Wrapped(new Rect(box.x + 10, box.y + 17, box.width - 20, box.height - 20), Instruction(), Color.white);
        if (step == Step.Timed)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(timedDeadline - Time.time));
            RetroGUI.ShadowLabel(new Rect(0, 30, w, 14), Loc.T($"Zeit: {left / 60}:{left % 60:00}", $"Time: {left / 60}:{left % 60:00}"), left < 10 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.4f));
        }
        if (feedback != null && Time.time < feedbackUntil)
            RetroGUI.ShadowLabel(new Rect(0, box.y - 18, w, 14), feedback, new Color(0.6f, 1f, 0.6f));
        RetroGUI.Label(new Rect(box.x, box.yMax + 2, box.width, 10), Loc.T("Prüfung überspringen [RÜCKTASTE]", "Skip the test [BACKSPACE]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
    }
}
