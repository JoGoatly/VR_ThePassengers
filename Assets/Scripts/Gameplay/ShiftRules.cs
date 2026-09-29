using UnityEngine;

/// <summary>
/// The rules of the job while driving:
///   - a time limit to reach the next stop (too late = fired),
///   - how well the bus stopped at a stop (pay bonus or deduction),
///   - fatigue: stay awake with energy drinks (X) or fall asleep at the wheel,
///   - fuel: the tank empties while driving and has to be filled at petrol stations.
/// </summary>
public class ShiftRules : MonoBehaviour
{
    public static ShiftRules Instance { get; private set; }

    public BoardingManager game;
    public BusController bus;
    public MainMenu menu;

    [Header("Stop timer")]
    [Tooltip("Speed the time limit expects (m/s); night 1 is more generous")]
    public float expectedSpeed = 11f;
    public float graceSeconds = 30f;

    [Header("Fatigue")]
    [Tooltip("Seconds from wide awake to asleep")]
    public float awakeSeconds = 320f;
    public float drinkBoost = 0.45f;
    public AudioClip drinkSound, heartbeat, warningBeep;

    [Header("Fuel")]
    [Tooltip("Metres on a full tank")]
    public float tankRange = 4200f;

    /// <summary>Seconds left to reach the next stop (negative = no limit right now).</summary>
    public float TimeLeft { get; private set; } = -1f;
    /// <summary>1 = wide awake, 0 = asleep.</summary>
    public float Alertness { get; private set; } = 1f;
    public float Fuel => Progress.Data.fuel;

    BusStop target;
    float deadline;
    BusStop lastRated;
    float nextBeep, lastArc = float.NaN, blinkPhase, driftTarget, nextDriftAt;
    bool over;
    SoundManager sound;
    ForestRoad road;

    void Awake() => Instance = this;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (menu == null) menu = FindAnyObjectByType<MainMenu>();
        road = FindAnyObjectByType<ForestRoad>();
        sound = FindAnyObjectByType<SoundManager>();
        if (!Features.Has(Feature.Fuel)) Progress.Data.fuel = 1f;
    }

    bool Running => !over && game != null && bus != null && !GameUI.MenuOpen && !Tutorial.Active && !GameUI.AtHome;

    void Update()
    {
        if (!Running) return;
        float dt = Time.deltaTime;
        UpdateStopTimer(dt);
        RateStopping();
        UpdateFatigue(dt);
        UpdateFuel();
    }

    void Fail(string title, string text)
    {
        if (over) return;
        over = true;
        if (menu != null) menu.ShowGameOver(title, text);
    }

    void Play(AudioClip clip, float volume = 0.8f)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, volume, 0f);
    }

    // ---------------------------------------------------------------- stop timer

    void UpdateStopTimer(float dt)
    {
        if (!Features.Has(Feature.StopTimer)) { TimeLeft = -1f; return; }
        var next = game.NextStop(out float dist);
        if (next == null) { TimeLeft = -1f; target = null; return; }
        if (next != target)
        {
            // A new stop ahead: time for the distance at a sensible speed, plus some grace.
            target = next;
            float speed = expectedSpeed + Mathf.Min(3f, (Progress.Day - 1) * 0.6f);
            float grace = graceSeconds + (Progress.Day == 1 ? 20f : 0f);
            deadline = Time.time + Mathf.Max(0f, dist) / speed + grace;
        }
        // The clock stops while you can't drive on: at a stop, passengers at the door,
        // walking around, breakdowns, repair games.
        bool paused = game.CurrentPhase != BoardingManager.Phase.Driving || GameUI.PlayerOutside || GameUI.InBus ||
                      GameUI.MinigameOpen || GameUI.AnyOpen || (NightEvents.Instance != null && NightEvents.Instance.Blocking) ||
                      (Mathf.Abs(bus.Speed) < 0.3f && dist < 25f) || bus.DoorsFullyOpen;
        if (paused) deadline += dt;
        TimeLeft = deadline - Time.time;
        if (TimeLeft < 10f && Time.time > nextBeep)
        {
            nextBeep = Time.time + 1f;
            Play(warningBeep, 0.5f);
        }
        if (TimeLeft <= 0f)
            Fail(Loc.T("DU WURDEST GEFEUERT", "YOU'RE FIRED"),
                 Loc.T($"Du bist viel zu spät an der Haltestelle \"{target.stopName}\" angekommen.\nDie Leitstelle hat dich noch in der Nacht entlassen. Die Linie 13 braucht einen Fahrer, der pünktlich ist.",
                       $"You arrived far too late at the stop \"{target.stopName}\".\nDispatch fired you that same night. Line 13 needs a driver who is on time."));
    }

    // ---------------------------------------------------------------- stopping at a stop

    // When the doors open at a stop: how close is the door to the waiting point, how straight
    // is the bus, how far from the kerb?
    void RateStopping()
    {
        if (!bus.DoorsFullyOpen || Mathf.Abs(bus.Speed) > 0.3f) return;
        var stop = game.NextStop(out float dist);
        if (stop == null || stop == lastRated || Mathf.Abs(dist) > 20f) return;
        lastRated = stop;

        Vector3 door = bus.transform.TransformPoint(game.DoorLocal);
        Vector3 d = stop.waitPoint.position - door;
        d.y = 0f;
        float along = Mathf.Abs(Vector3.Dot(d, stop.roadDirection));
        float across = Vector3.ProjectOnPlane(d, stop.roadDirection).magnitude;
        float angle = Vector3.Angle(Vector3.ProjectOnPlane(bus.transform.forward, Vector3.up), Vector3.ProjectOnPlane(stop.roadDirection, Vector3.up));

        int bonus;
        string verdict;
        if (along < 1.2f && across < 2.9f && angle < 6f) { bonus = 6; verdict = Loc.T("Perfekt gehalten!", "Perfect stop!"); }
        else if (along < 3f && across < 3.8f && angle < 12f) { bonus = 2; verdict = Loc.T("Gut gehalten.", "Good stop."); }
        else if (along < game.stopTolerance + 2f) { bonus = -4; verdict = Loc.T("Schlecht gehalten - die Leute mussten laufen.", "Bad stop - people had to walk."); }
        else return;

        Progress.AddMoney(bonus);
        if (bonus > 0) Progress.ShiftEarned += bonus; else Progress.ShiftFines += -bonus;
        Progress.Save();
        game.ShowToast($"{verdict}  {(bonus > 0 ? "+" : "")}{bonus} €");
    }

    // ---------------------------------------------------------------- fatigue

    void UpdateFatigue(float dt)
    {
        if (!Features.Has(Feature.Fatigue)) { Alertness = 1f; return; }
        bool resting = GameUI.PlayerOutside || GameUI.InBus;   // moving around keeps you a little fresher
        Alertness = Mathf.Max(0f, Alertness - dt / awakeSeconds * (resting ? 0.4f : 1f) * (1f + 0.6f * DayManager.Dread));

        bool canDrink = !GameUI.MenuOpen && !GameUI.MinigameOpen && !GameUI.NoteOpen && !GameUI.PcOpen && !GameUI.TerminalTyping;
        if (canDrink && GameKeys.Pressed(GameAction.Drink))
        {
            if (Progress.Data.energyDrinks > 0)
            {
                Progress.Data.energyDrinks--;
                Progress.Save();
                Alertness = Mathf.Min(1f, Alertness + drinkBoost);
                Play(drinkSound, 0.9f);
                game.ShowToast(Loc.T($"Energy-Drink. Hellwach.  (noch {Progress.Data.energyDrinks})", $"Energy drink. Wide awake.  ({Progress.Data.energyDrinks} left)"));
            }
            else game.ShowToast(Loc.T("Keine Energy-Drinks mehr!", "No energy drinks left!"));
        }

        // Very tired: the bus drifts, the heart pounds.
        if (Alertness < 0.25f && !GameUI.PlayerOutside && !GameUI.InBus)
        {
            if (Time.time > nextDriftAt) { nextDriftAt = Time.time + Random.Range(1.5f, 4f); driftTarget = Random.Range(-0.18f, 0.18f) * (1f - Alertness / 0.25f); }
            if (NightEvents.Instance == null || !NightEvents.Instance.Blocking) bus.steerPull = Mathf.MoveTowards(bus.steerPull, driftTarget, dt * 0.3f);
            if (Time.time > nextBeep && heartbeat != null) { nextBeep = Time.time + 1.1f; Play(heartbeat, 0.5f); }
        }
        if (Alertness <= 0f)
            Fail(Loc.T("EINGESCHLAFEN", "FELL ASLEEP"),
                 Loc.T("Deine Augen fallen zu. Nur für einen Moment.\nAls du sie wieder öffnest, steht der Bus im Wald. Alle Sitze sind leer. Auch deiner.",
                       "Your eyes close. Just for a moment.\nWhen you open them again, the bus stands in the forest. Every seat is empty. So is yours."));
    }

    // ---------------------------------------------------------------- fuel

    void UpdateFuel()
    {
        if (!Features.Has(Feature.Fuel) || road == null) return;
        float s = road.BusArcLength;
        if (!float.IsNaN(lastArc) && s > lastArc && s - lastArc < 30f)
        {
            Progress.Data.fuel = Mathf.Max(0f, Progress.Data.fuel - (s - lastArc) / tankRange);
            if (Progress.Data.fuel <= 0f)
                Fail(Loc.T("TANK LEER", "OUT OF FUEL"),
                     Loc.T("Der Motor stottert und geht aus. Mitten im Wald.\nDie Leitstelle schickt einen Abschleppwagen - und deine Kündigung. Tanken gehört zum Job.",
                           "The engine sputters and dies. In the middle of the forest.\nDispatch sends a tow truck - and your notice. Refuelling is part of the job."));
        }
        lastArc = s;
    }

    /// <summary>Coffee and the like.</summary>
    public void WakeUp(float amount) => Alertness = Mathf.Min(1f, Alertness + amount);

    /// <summary>Fill up at a pump (0..1 of a tank). Returns what was filled.</summary>
    public static float Refuel(float amount)
    {
        float before = Progress.Data.fuel;
        Progress.Data.fuel = Mathf.Min(1f, before + amount);
        return Progress.Data.fuel - before;
    }

    // ---------------------------------------------------------------- HUD

    void OnGUI()
    {
        if (!Running) return;
        // Heavy eyelids when tired.
        if (Alertness < 0.3f)
        {
            float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
            blinkPhase += Time.deltaTime * (1.2f + (0.3f - Alertness) * 6f);
            float close = Mathf.Clamp01((0.3f - Alertness) / 0.3f) * (0.35f + 0.65f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(blinkPhase)), 6f));
            RetroGUI.Fill(new Rect(0, 0, w, h * 0.5f * close), Color.black);
            RetroGUI.Fill(new Rect(0, h - h * 0.5f * close, w, h * 0.5f * close), Color.black);
            if (Mathf.Repeat(Time.time, 2f) < 1.4f)
                RetroGUI.ShadowLabel(new Rect(0, h * 0.5f - 6, w, 14),
                    Loc.T($"Du schläfst ein... Energy-Drink {GameKeys.Tag(GameAction.Drink)}  ({Progress.Data.energyDrinks})", $"You're falling asleep... energy drink {GameKeys.Tag(GameAction.Drink)}  ({Progress.Data.energyDrinks})"),
                    new Color(1f, 0.5f, 0.35f));
        }
    }
}
