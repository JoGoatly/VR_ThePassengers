using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The driving test before the first night, in daylight: an examiner explains the controls
/// step by step - driving, braking, steering, lights, stopping precisely at a bus stop,
/// the doors, getting up in the bus - and finally a stop against the clock.
/// No passengers, no scares. Passed = the first night begins.
/// </summary>
public class Tutorial : MonoBehaviour
{
    /// <summary>The driving test is running (night systems wait).</summary>
    public static bool Active { get; private set; }

    public BusController bus;
    public BoardingManager game;
    public MainMenu menu;
    public AudioClip stepSound, passSound;

    enum Step { Welcome, Throttle, Brake, Steer, Lights, StopAtStop, OpenDoors, CloseDoors, StandUp, SitDown, Timed, Passed }

    Step step;
    float stepStart, topSpeed, startArc, timedDeadline;
    int lightPresses;
    BusStop stopTarget;
    string feedback;
    float feedbackUntil;
    ForestRoad road;
    BusLights lights;
    SoundManager sound;
    bool started;
    AudioSource music;

    void Awake()
    {
        Active = !Progress.Data.tutorialDone && Progress.Day == 1;
        BusLights.Daylight = Active;
    }

    void OnDestroy()
    {
        Active = false;
        BusLights.Daylight = false;
    }

    void Start()
    {
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
    }

    void Update()
    {
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
                if (road.BusArcLength - startArc > 160f) Next(Step.Lights);
                break;
            case Step.Lights:
                if (lightPresses >= 2) Next(Step.StopAtStop);
                break;
            case Step.StopAtStop:
            {
                var next = game.NextStop(out float dist);
                if (next != null && next != stopTarget) stopTarget = next;
                if (stopTarget != null && Mathf.Abs(bus.Speed) < 0.3f && Mathf.Abs(dist) < 22f) Next(Step.OpenDoors);
                break;
            }
            case Step.OpenDoors:
                if (Mathf.Abs(bus.Speed) > 1f) { Say(Loc.T("Erst die Türen öffnen, dann weiterfahren!", "Open the doors first, then drive on!")); Next(Step.StopAtStop); break; }
                if (bus.DoorsFullyOpen) { if (RateStop()) Next(Step.CloseDoors); else Next(Step.StopAtStop); }
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
            {
                var next = game.NextStop(out float dist);
                if (stopTarget == null || (next != null && next != stopTarget && since < 1f)) { stopTarget = next; timedDeadline = Time.time + Mathf.Max(30f, dist / 10f + 15f); }
                if (stopTarget != null && next == stopTarget && Mathf.Abs(bus.Speed) < 0.3f && Mathf.Abs(dist) < 22f && bus.DoorsFullyOpen)
                {
                    RateStop();
                    Next(Step.Passed);
                }
                else if (next != stopTarget && since > 2f)
                {
                    Say(Loc.T("Vorbeigefahren! Die nächste Haltestelle, neue Zeit.", "Drove past! The next stop, new time."));
                    stopTarget = next;
                    timedDeadline = Time.time + Mathf.Max(30f, dist / 10f + 15f);
                }
                else if (Time.time > timedDeadline)
                {
                    Say(Loc.T("Zu langsam! Noch einmal - nächste Haltestelle.", "Too slow! Once more - next stop."));
                    stopTarget = null;
                    stepStart = Time.time;
                }
                break;
            }
            case Step.Passed:
                if (since > 6f) Finish();
                break;
        }
    }

    void Next(Step s)
    {
        step = s;
        stepStart = Time.time;
        if (s == Step.Steer && road != null) startArc = road.BusArcLength;
        if (s == Step.Lights) lightPresses = 0;
        if (s == Step.StopAtStop || s == Step.Timed) stopTarget = null;
        Play(s == Step.Passed ? passSound : stepSound);
    }

    // How well the bus stands at the stop. Returns false if it has to be done again.
    bool RateStop()
    {
        if (stopTarget == null) return true;
        Vector3 door = bus.transform.TransformPoint(game.DoorLocal);
        Vector3 d = stopTarget.waitPoint.position - door;
        d.y = 0f;
        float along = Mathf.Abs(Vector3.Dot(d, stopTarget.roadDirection));
        float across = Vector3.ProjectOnPlane(d, stopTarget.roadDirection).magnitude;
        if (along < 1.2f && across < 2.9f) { Say(Loc.T("Perfekt! Die Tür steht genau am Schild.", "Perfect! The door is right at the sign.")); return true; }
        if (along < 3f && across < 3.8f) { Say(Loc.T("Gut. Nächstes Mal noch etwas genauer.", "Good. A bit more precise next time.")); return true; }
        Say(Loc.T($"Zu weit weg ({along:0.0} m). Nochmal an der nächsten Haltestelle - Tür ans Schild!", $"Too far away ({along:0.0} m). Again at the next stop - door at the sign!"));
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
            Step.Steer => Loc.T($"Lenken mit {K(GameAction.SteerLeft)} und {K(GameAction.SteerRight)}. Bleiben Sie auf der rechten Spur - fahren Sie 160 m.", $"Steer with {K(GameAction.SteerLeft)} and {K(GameAction.SteerRight)}. Stay in the right lane - drive 160 m."),
            Step.Lights => Loc.T($"Licht: {K(GameAction.Lights)} wechselt zwischen Abblend- und Fernlicht. Das Fernlicht leert einen Akku (Balken am Tacho) - ist er leer, geht ALLES Licht aus, bis er wieder voll ist. Zweimal umschalten.",
                                 $"Lights: {K(GameAction.Lights)} switches between low and high beam. The high beam drains a battery (bar on the speedometer) - when it's empty ALL lights go out until it's full again. Switch twice."),
            Step.StopAtStop => Loc.T("Halten Sie an der nächsten Haltestelle. Die vordere Tür soll genau am Haltestellenschild stehen, nah am Bordstein, gerade.",
                                     "Stop at the next bus stop. The front door should be right at the bus stop sign, close to the kerb, straight."),
            Step.OpenDoors => Loc.T($"Türen öffnen: {K(GameAction.Doors)}. Wie genau Sie halten, bringt Ihnen später Geld - oder kostet welches.", $"Open the doors: {K(GameAction.Doors)}. How precisely you stop will earn you money later - or cost you."),
            Step.CloseDoors => Loc.T($"Türen schließen: {K(GameAction.Doors)}. Mit offenen Türen fährt der Bus nicht.", $"Close the doors: {K(GameAction.Doors)}. The bus won't move with open doors."),
            Step.StandUp => Loc.T($"Im Stand können Sie aufstehen: {K(GameAction.Interact)}. Im Bus herumlaufen, mit Fahrgästen reden, an der Tür aussteigen.", $"When standing still you can get up: {K(GameAction.Interact)}. Walk around the bus, talk to passengers, get out at the door."),
            Step.SitDown => Loc.T($"Gehen Sie zurück zum Fahrersitz und setzen Sie sich: {K(GameAction.Interact)}.", $"Go back to the driver's seat and sit down: {K(GameAction.Interact)}."),
            Step.Timed => Loc.T("Letzte Aufgabe: Nachts haben Sie für jede Haltestelle ein Zeitlimit - zu spät heißt gefeuert. Erreichen Sie die nächste Haltestelle rechtzeitig und öffnen Sie die Türen.",
                                "Last task: at night you have a time limit for every stop - too late means fired. Reach the next stop in time and open the doors."),
            _ => Loc.T("Bestanden. Glückwunsch. Ihre erste Schicht beginnt heute Nacht, 23:40 Uhr.\nViel Glück. Sie werden es brauchen.",
                       "Passed. Congratulations. Your first shift starts tonight, 11:40 PM.\nGood luck. You'll need it."),
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
        if (step == Step.Timed && stopTarget != null)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(timedDeadline - Time.time));
            RetroGUI.ShadowLabel(new Rect(0, 30, w, 14), Loc.T($"Zeit: {left / 60}:{left % 60:00}", $"Time: {left / 60}:{left % 60:00}"), left < 10 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.4f));
        }
        if (feedback != null && Time.time < feedbackUntil)
            RetroGUI.ShadowLabel(new Rect(0, box.y - 18, w, 14), feedback, new Color(0.6f, 1f, 0.6f));
        RetroGUI.Label(new Rect(box.x, box.yMax + 2, box.width, 10), Loc.T("Prüfung überspringen [RÜCKTASTE]", "Skip the test [BACKSPACE]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
    }
}
