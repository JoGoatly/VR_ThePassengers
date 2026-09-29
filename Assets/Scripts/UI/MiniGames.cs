using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Small repair / rescue games for the night events, drawn on top of the game:
///   RadioCode  - repeat the code the radio beeps (Simon)
///   Saw        - saw through a fallen tree: A and D in turns
///   Wires      - reconnect the engine cables by colour
///   Starter    - crank the starter when the needle is in the green
///   WheelBolts - circle the mouse around the wheel nuts (loosen, change wheel, tighten)
///   Fuses      - pull the blown fuses and put in the right ones
///   Wipe       - rub the hand prints off the windscreen
///   Cpr        - chest compressions in rhythm, then one long breath
/// Esc stops the game (the problem stays, try again).
/// </summary>
public class MiniGames : MonoBehaviour
{
    public static MiniGames Instance { get; private set; }

    public enum Kind { RadioCode, Saw, Wires, Starter, WheelBolts, Fuses, Wipe, Cpr }

    [Header("Sounds")]
    public AudioClip[] beeps;
    public AudioClip saw, ratchet, spark, success, fail, thump, squeak, scare, click;

    /// <summary>A minigame is on screen.</summary>
    public static bool Running => Instance != null && Instance.kind.HasValue;

    Kind? kind;
    string title;
    System.Action<bool> done;
    int level;
    SoundManager sound;
    string message;
    float messageUntil;
    float lockedUntil;

    const float PW = 400f, PH = 240f;
    Rect Panel => new Rect(RetroGUI.VirtualWidth * 0.5f - PW * 0.5f, 55f, PW, PH);

    static readonly Color Ink = new Color(0.9f, 0.9f, 0.85f);
    static readonly Color Hint = new Color(1f, 0.85f, 0.3f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.45f);
    static readonly Color Bad = new Color(1f, 0.35f, 0.3f);

    void Awake() => Instance = this;

    void Start() => sound = FindAnyObjectByType<SoundManager>();

    void OnDisable()
    {
        if (kind.HasValue) GameUI.MinigameOpen = false;
    }

    /// <summary>Start a minigame; done(true) when solved, done(false) when failed or stopped.</summary>
    public void Play(Kind k, string heading, System.Action<bool> onDone, int difficulty = 1)
    {
        kind = k;
        title = heading;
        done = onDone;
        level = Mathf.Clamp(difficulty, 1, 3);
        message = null;
        lockedUntil = 0f;
        GameUI.MinigameOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        switch (k)
        {
            case Kind.RadioCode: SetupRadio(); break;
            case Kind.Saw: SetupSaw(); break;
            case Kind.Wires: SetupWires(); break;
            case Kind.Starter: SetupStarter(); break;
            case Kind.WheelBolts: SetupBolts(); break;
            case Kind.Fuses: SetupFuses(); break;
            case Kind.Wipe: SetupWipe(); break;
            case Kind.Cpr: SetupCpr(); break;
        }
    }

    void Finish(bool ok)
    {
        var callback = done;
        kind = null;
        done = null;
        GameUI.MinigameOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Play(ok ? success : fail, 0.8f);
        callback?.Invoke(ok);
    }

    void Play(AudioClip clip, float volume = 0.8f)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, volume, 0f);
    }

    void Say(string text, float seconds = 1.5f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // ---------------------------------------------------------------- input helpers

    static Vector2 MouseV
    {
        get
        {
            var m = Mouse.current;
            if (m == null) return Vector2.zero;
            Vector2 p = m.position.ReadValue();
            return new Vector2(p.x, Screen.height - p.y) / RetroGUI.Scale;
        }
    }

    static bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    static bool Held => Mouse.current != null && Mouse.current.leftButton.isPressed;
    static bool SpacePressed => (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) || Clicked;
    static bool SpaceHeld => (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) || Held;

    void Update()
    {
        if (!kind.HasValue) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Finish(false); return; }
        if (Time.time < lockedUntil) return;
        switch (kind.Value)
        {
            case Kind.RadioCode: UpdateRadio(); break;
            case Kind.Saw: UpdateSaw(); break;
            case Kind.Wires: UpdateWires(); break;
            case Kind.Starter: UpdateStarter(); break;
            case Kind.WheelBolts: UpdateBolts(); break;
            case Kind.Fuses: UpdateFuses(); break;
            case Kind.Wipe: UpdateWipe(); break;
            case Kind.Cpr: UpdateCpr(); break;
        }
    }

    void OnGUI()
    {
        if (!kind.HasValue) return;
        GUI.depth = -360;
        var p = Panel;
        RetroGUI.Fill(new Rect(0, 0, RetroGUI.VirtualWidth, RetroGUI.VirtualHeight), new Color(0f, 0f, 0f, 0.45f));
        RetroGUI.Panel(new Rect(p.x - 3, p.y - 3, p.width + 6, p.height + 6), 1);
        RetroGUI.Label(new Rect(p.x + 8, p.y + 4, p.width - 16, 14), title, Hint, true);
        RetroGUI.Label(new Rect(p.x, p.yMax - 12, p.width - 6, 11), Loc.T("Abbrechen [ESC]", "Stop [ESC]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
        switch (kind.Value)
        {
            case Kind.RadioCode: DrawRadio(p); break;
            case Kind.Saw: DrawSaw(p); break;
            case Kind.Wires: DrawWires(p); break;
            case Kind.Starter: DrawStarter(p); break;
            case Kind.WheelBolts: DrawBolts(p); break;
            case Kind.Fuses: DrawFuses(p); break;
            case Kind.Wipe: DrawWipe(p); break;
            case Kind.Cpr: DrawCpr(p); break;
        }
        if (message != null && Time.time < messageUntil)
            RetroGUI.ShadowLabel(new Rect(p.x, p.y + 20, p.width, 14), message, Hint);
    }

    void Instruction(Rect p, string text) => RetroGUI.Label(new Rect(p.x + 8, p.yMax - 26, p.width - 16, 12), text, Ink, false, true);

    // ---------------------------------------------------------------- drawing helpers

    static Texture2D circle;

    static Texture2D Circle
    {
        get
        {
            if (circle != null) return circle;
            const int n = 64;
            circle = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                circle.SetPixel(x, y, dx * dx + dy * dy <= (n / 2f) * (n / 2f) ? Color.white : Color.clear);
            }
            circle.Apply();
            return circle;
        }
    }

    static void Disc(Vector2 centre, float radius, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(RetroGUI.R(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), Circle);
        GUI.color = old;
    }

    static void Line(Vector2 a, Vector2 b, Color c, float width = 3f)
    {
        float s = RetroGUI.Scale;
        Vector2 ra = a * s, rb = b * s;
        float length = Vector2.Distance(ra, rb);
        if (length < 0.5f) return;
        float angle = Mathf.Atan2(rb.y - ra.y, rb.x - ra.x) * Mathf.Rad2Deg;
        var matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, ra);
        GUI.DrawTexture(new Rect(ra.x, ra.y - width * s * 0.5f, length, width * s), RetroGUI.Tex(c));
        GUI.matrix = matrix;
    }

    // ---------------------------------------------------------------- radio signal (sine wave)

    // The radio receives a wave; turn the knobs until your wave lies on top of it.
    // Knobs: amplitude, frequency and (from night 3) phase.
    readonly float[] knob = new float[3], target = new float[3];
    int knobCount, dragKnob = -1;
    float dragStartY, dragStartValue, stableFor;
    static readonly string[] KnobNamesDe = { "AMPLITUDE", "FREQUENZ", "PHASE" };
    static readonly string[] KnobNamesEn = { "AMPLITUDE", "FREQUENCY", "PHASE" };

    void SetupRadio()
    {
        knobCount = level >= 2 ? 3 : 2;
        for (int i = 0; i < 3; i++)
        {
            target[i] = Random.Range(0.15f, 0.85f);
            do knob[i] = Random.value; while (Mathf.Abs(knob[i] - target[i]) < 0.25f);
        }
        if (knobCount < 3) knob[2] = target[2];
        dragKnob = -1;
        stableFor = 0f;
    }

    Rect Scope(Rect p) => new Rect(p.x + 20, p.y + 34, p.width - 40, 110);
    Vector2 Knob(Rect p, int i) => new Vector2(p.x + p.width / 2f + (i - (knobCount - 1) / 2f) * 110f, p.y + 180f);

    static float Wave(float[] v, float x, float time)
    {
        float amp = Mathf.Lerp(0.15f, 1f, v[0]);
        float freq = Mathf.Lerp(1f, 5f, v[1]);
        float phase = v[2] * Mathf.PI * 2f;
        return amp * Mathf.Sin(x * freq * Mathf.PI * 2f + phase + time);
    }

    float Match()
    {
        float err = 0f;
        for (int i = 0; i < knobCount; i++) err = Mathf.Max(err, Mathf.Abs(knob[i] - target[i]));
        return Mathf.Clamp01(1f - err / 0.35f);
    }

    void UpdateRadio()
    {
        var p = Panel;
        var m = MouseV;
        if (Clicked)
            for (int i = 0; i < knobCount; i++)
                if (Vector2.Distance(m, Knob(p, i)) < 26f) { dragKnob = i; dragStartY = m.y; dragStartValue = knob[i]; Play(click, 0.4f); }
        if (!Held) dragKnob = -1;
        if (dragKnob >= 0)
            knob[dragKnob] = Mathf.Clamp01(dragStartValue + (dragStartY - m.y) / 140f);   // drag up = turn right
        // Mouse wheel over a knob turns it too.
        var mouse = Mouse.current;
        float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(wheel) > 0.01f)
            for (int i = 0; i < knobCount; i++)
                if (Vector2.Distance(m, Knob(p, i)) < 30f) knob[i] = Mathf.Clamp01(knob[i] + Mathf.Sign(wheel) * 0.02f);

        // Close enough for a moment: the signal locks.
        float err = 0f;
        for (int i = 0; i < knobCount; i++) err = Mathf.Max(err, Mathf.Abs(knob[i] - target[i]));
        float tolerance = 0.05f - 0.01f * (level - 1);
        stableFor = err < tolerance ? stableFor + Time.deltaTime : 0f;
        if (stableFor > 0.02f && stableFor - Time.deltaTime <= 0.02f) Beep(2);
        if (stableFor >= 1.2f) Finish(true);
    }

    void Beep(int i)
    {
        if (beeps != null && beeps.Length > 0) Play(beeps[i % beeps.Length], 0.6f);
    }

    void DrawRadio(Rect p)
    {
        var scope = Scope(p);
        RetroGUI.Fill(scope, new Color(0.02f, 0.06f, 0.03f));
        for (int g = 1; g < 4; g++) RetroGUI.Fill(new Rect(scope.x, scope.y + scope.height * g / 4f, scope.width, 1), new Color(0.1f, 0.25f, 0.12f));
        for (int g = 1; g < 8; g++) RetroGUI.Fill(new Rect(scope.x + scope.width * g / 8f, scope.y, 1, scope.height), new Color(0.1f, 0.25f, 0.12f));
        float t = Time.time * 2f;
        const int samples = 90;
        Vector2 prevT = Vector2.zero, prevM = Vector2.zero;
        float noise = level >= 3 ? 0.06f : 0f;
        for (int k = 0; k <= samples; k++)
        {
            float x = k / (float)samples;
            float yt = Wave(target, x, t) + (noise > 0f ? (Mathf.PerlinNoise(x * 20f, t) - 0.5f) * noise * 2f : 0f);
            float ym = Wave(knob, x, t);
            Vector2 pt = new Vector2(scope.x + x * scope.width, scope.center.y - yt * scope.height * 0.42f);
            Vector2 pm = new Vector2(scope.x + x * scope.width, scope.center.y - ym * scope.height * 0.42f);
            if (k > 0)
            {
                Line(prevT, pt, new Color(0.3f, 1f, 0.4f, 0.9f), 2f);
                Line(prevM, pm, new Color(1f, 0.7f, 0.2f, 0.9f), 2f);
            }
            prevT = pt;
            prevM = pm;
        }
        int match = Mathf.RoundToInt(Match() * 100f);
        RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12),
            stableFor > 0f ? Loc.T("SIGNAL STABIL - halten...", "SIGNAL LOCKED - hold...") : Loc.T($"Übereinstimmung: {match}%", $"Match: {match}%"),
            stableFor > 0f ? Good : Ink);

        for (int i = 0; i < knobCount; i++)
        {
            Vector2 c = Knob(p, i);
            Disc(c, 22f, new Color(0.12f, 0.12f, 0.13f));
            Disc(c, 18f, dragKnob == i ? new Color(0.45f, 0.42f, 0.38f) : new Color(0.32f, 0.3f, 0.28f));
            float a = Mathf.Lerp(-135f, 135f, knob[i]) * Mathf.Deg2Rad;
            Line(c, c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * 17f, new Color(1f, 0.75f, 0.3f), 3f);
            RetroGUI.Label(new Rect(c.x - 50, c.y + 24, 100, 11), Loc.T(KnobNamesDe[i], KnobNamesEn[i]), Ink, false, true, TextAnchor.UpperCenter);
        }
        Instruction(p, Loc.T("Regler mit der Maus hoch/runter ziehen (oder Mausrad), bis die gelbe Welle auf der grünen liegt.",
                             "Drag the knobs up/down (or use the wheel) until the yellow wave lies on the green one."));
    }

    // ---------------------------------------------------------------- saw

    float sawProgress, sawOffset;
    int sawCut, lastSawKey;

    void SetupSaw()
    {
        sawProgress = 0f;
        sawCut = 0;
        lastSawKey = 0;
    }

    void UpdateSaw()
    {
        int key = GameKeys.Pressed(GameAction.SteerLeft) ? 1 : GameKeys.Pressed(GameAction.SteerRight) ? 2 : 0;
        if (key == 0) return;
        if (key == lastSawKey)
        {
            // Same key twice: the saw gets stuck.
            lockedUntil = Time.time + 0.5f;
            Play(click);
            Say(Loc.T("Die Säge klemmt!", "The saw is stuck!"), 0.8f);
            lastSawKey = 0;
            return;
        }
        lastSawKey = key;
        sawOffset = key == 1 ? -1f : 1f;
        sawProgress += 0.05f / (0.8f + 0.2f * level);
        Play(saw, 0.6f);
        if (sawProgress >= 1f)
        {
            sawCut++;
            sawProgress = 0f;
            if (sawCut >= 2) { Finish(true); return; }
            Play(ratchet);
            Say(Loc.T("Erster Schnitt durch! Jetzt der zweite.", "First cut done! Now the second one."), 2f);
        }
    }

    void DrawSaw(Rect p)
    {
        // The trunk from the side, with the cut going down.
        var trunk = new Rect(p.x + 40, p.y + 70, p.width - 80, 90);
        RetroGUI.Fill(trunk, new Color(0.3f, 0.2f, 0.12f));
        for (int i = 0; i < 6; i++) RetroGUI.Fill(new Rect(trunk.x, trunk.y + 8 + i * 14, trunk.width, 2), new Color(0.22f, 0.14f, 0.08f));
        float cutX = trunk.x + trunk.width * (sawCut == 0 ? 0.35f : 0.65f);
        if (sawCut >= 1) RetroGUI.Fill(new Rect(trunk.x + trunk.width * 0.35f - 1, trunk.y, 3, trunk.height), Color.black);
        RetroGUI.Fill(new Rect(cutX - 1, trunk.y, 3, trunk.height * sawProgress), Color.black);
        // The saw blade.
        float blade = Mathf.Lerp(sawOffset * 40f, 0f, 0f);
        sawOffset = Mathf.MoveTowards(sawOffset, 0f, Time.deltaTime * 3f);
        float by = trunk.y + trunk.height * sawProgress - 6;
        RetroGUI.Fill(new Rect(cutX - 70 + blade, by, 140, 6), new Color(0.7f, 0.72f, 0.75f));
        RetroGUI.Fill(new Rect(cutX + 70 + blade, by - 6, 26, 16), new Color(0.5f, 0.15f, 0.1f));
        RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), Loc.T($"Schnitt {sawCut + 1}/2  -  {Mathf.RoundToInt(sawProgress * 100)}%", $"Cut {sawCut + 1}/2  -  {Mathf.RoundToInt(sawProgress * 100)}%"), Ink);
        Instruction(p, Loc.T($"Abwechselnd {GameKeys.Tag(GameAction.SteerLeft)} und {GameKeys.Tag(GameAction.SteerRight)} drücken. Nie zweimal dieselbe Taste!",
                             $"Press {GameKeys.Tag(GameAction.SteerLeft)} and {GameKeys.Tag(GameAction.SteerRight)} in turns. Never the same key twice!"));
    }

    // ---------------------------------------------------------------- wires

    static readonly Color[] WireColors = { new Color(0.85f, 0.15f, 0.1f), new Color(0.2f, 0.4f, 0.95f), new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.8f, 0.25f) };
    readonly int[] wireLeft = new int[4], wireRight = new int[4];
    readonly bool[] wireDone = new bool[4];
    int wireSelected = -1;
    float wireFlash;

    void SetupWires()
    {
        Shuffle(wireLeft);
        Shuffle(wireRight);
        for (int i = 0; i < 4; i++) wireDone[i] = false;
        wireSelected = -1;
    }

    static void Shuffle(int[] a)
    {
        for (int i = 0; i < a.Length; i++) a[i] = i;
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    Vector2 WireEnd(Rect p, int row, bool left) => new Vector2(left ? p.x + 60 : p.xMax - 60, p.y + 55 + row * 38);

    void UpdateWires()
    {
        if (!Clicked) return;
        var p = Panel;
        var m = MouseV;
        for (int row = 0; row < 4; row++)
        {
            if (Vector2.Distance(m, WireEnd(p, row, true)) < 14f && !wireDone[wireLeft[row]])
            {
                wireSelected = wireLeft[row];
                Play(click, 0.5f);
                return;
            }
            if (Vector2.Distance(m, WireEnd(p, row, false)) < 14f && wireSelected >= 0)
            {
                if (wireRight[row] == wireSelected)
                {
                    wireDone[wireSelected] = true;
                    Play(ratchet);
                    wireSelected = -1;
                    if (System.Array.TrueForAll(wireDone, d => d)) Finish(true);
                }
                else
                {
                    Play(spark);
                    wireFlash = Time.time + 0.3f;
                    wireSelected = -1;
                    Say(Loc.T("Funken! Falsches Kabel.", "Sparks! Wrong cable."), 1f);
                }
                return;
            }
        }
    }

    void DrawWires(Rect p)
    {
        if (Time.time < wireFlash) RetroGUI.Fill(new Rect(p.x, p.y + 18, p.width, p.height - 40), new Color(1f, 0.9f, 0.5f, 0.25f));
        RetroGUI.Fill(new Rect(p.x + 150, p.y + 40, 100, 150), new Color(0.2f, 0.2f, 0.22f));   // engine block
        RetroGUI.Label(new Rect(p.x + 150, p.y + 108, 100, 12), Loc.T("MOTOR", "ENGINE"), new Color(0.5f, 0.5f, 0.5f), true, false, TextAnchor.MiddleCenter);
        for (int row = 0; row < 4; row++)
        {
            Vector2 l = WireEnd(p, row, true), r = WireEnd(p, row, false);
            RetroGUI.Fill(new Rect(p.x + 10, l.y - 3, l.x - p.x - 10, 6), WireColors[wireLeft[row]]);
            Disc(l, 8f, WireColors[wireLeft[row]] * (wireSelected == wireLeft[row] ? 1.3f : 1f));
            RetroGUI.Fill(new Rect(r.x, r.y - 3, p.xMax - 10 - r.x, 6), WireColors[wireRight[row]]);
            Disc(r, 8f, new Color(0.15f, 0.15f, 0.15f));
            Disc(r, 5f, WireColors[wireRight[row]]);
        }
        for (int c = 0; c < 4; c++)
        {
            if (!wireDone[c]) continue;
            int lr = System.Array.IndexOf(wireLeft, c), rr = System.Array.IndexOf(wireRight, c);
            Line(WireEnd(p, lr, true), WireEnd(p, rr, false), WireColors[c], 5f);
        }
        if (wireSelected >= 0)
            Line(WireEnd(p, System.Array.IndexOf(wireLeft, wireSelected), true), MouseV, WireColors[wireSelected], 5f);
        Instruction(p, Loc.T("Klicke ein Kabel links an, dann den Anschluss mit derselben Farbe rechts.",
                             "Click a cable on the left, then the socket of the same colour on the right."));
    }

    // ---------------------------------------------------------------- starter

    float needle, zoneStart, zoneWidth, needleSpeed;
    int starterHits;

    void SetupStarter()
    {
        starterHits = 0;
        needleSpeed = 0.7f + 0.2f * level;
        NewZone();
    }

    void NewZone()
    {
        zoneWidth = 0.17f - 0.03f * level;
        zoneStart = Random.Range(0.15f, 0.85f - zoneWidth);
    }

    void UpdateStarter()
    {
        needle = Mathf.PingPong(Time.time * needleSpeed, 1f);
        if (!SpacePressed) return;
        if (needle >= zoneStart && needle <= zoneStart + zoneWidth)
        {
            starterHits++;
            Play(thump, 1f);
            if (starterHits >= 3) { Finish(true); return; }
            Say(Loc.T("Der Motor zuckt...", "The engine twitches..."), 1f);
            NewZone();
        }
        else
        {
            starterHits = Mathf.Max(0, starterHits - 1);
            Play(fail, 0.6f);
            Say(Loc.T("Abgewürgt!", "Stalled!"), 0.8f);
        }
    }

    void DrawStarter(Rect p)
    {
        var bar = new Rect(p.x + 40, p.y + 100, p.width - 80, 26);
        RetroGUI.Fill(bar, new Color(0.15f, 0.15f, 0.15f));
        RetroGUI.Fill(new Rect(bar.x + bar.width * zoneStart, bar.y, bar.width * zoneWidth, bar.height), new Color(0.2f, 0.7f, 0.25f));
        RetroGUI.Fill(new Rect(bar.x + bar.width * needle - 2, bar.y - 8, 4, bar.height + 16), new Color(0.95f, 0.3f, 0.2f));
        for (int i = 0; i < 3; i++) Disc(new Vector2(p.x + p.width / 2 - 30 + i * 30, p.y + 160), 8f, i < starterHits ? Good : new Color(0.25f, 0.25f, 0.25f));
        RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), Loc.T("Anlasser: dreimal im richtigen Moment", "Starter: three times at the right moment"), Ink);
        Instruction(p, Loc.T("[LEERTASTE] oder Klick, wenn die Nadel im grünen Bereich ist.", "[SPACE] or click when the needle is in the green."));
    }

    // ---------------------------------------------------------------- wheel bolts

    static readonly int[] BoltOrder = { 0, 2, 4, 1, 3 };
    int boltStep, boltPhase;
    float boltTurned, lastBoltAngle, ratchetAt, phaseSwitchAt;
    bool boltTracking;

    void SetupBolts()
    {
        boltStep = 0;
        boltPhase = 0;
        boltTurned = 0f;
        boltTracking = false;
        phaseSwitchAt = -1f;
    }

    Vector2 WheelCentre(Rect p) => new Vector2(p.x + p.width / 2f, p.y + 118f);
    Vector2 Bolt(Rect p, int i)
    {
        float a = (-90f + i * 72f) * Mathf.Deg2Rad;
        return WheelCentre(p) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 42f;
    }

    void UpdateBolts()
    {
        if (phaseSwitchAt > 0f)
        {
            if (Time.time < phaseSwitchAt) return;
            phaseSwitchAt = -1f;
            boltPhase = 1;
            boltStep = 0;
            Say(Loc.T("Reserverad drauf. Jetzt festziehen!", "Spare wheel on. Now tighten!"), 2f);
        }
        var p = Panel;
        Vector2 bolt = Bolt(p, BoltOrder[boltStep]);
        Vector2 d = MouseV - bolt;
        if (!Held || d.magnitude > 60f || d.magnitude < 3f) { boltTracking = false; return; }
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // screen space, y down: clockwise = increasing
        if (boltTracking)
        {
            float delta = Mathf.DeltaAngle(lastBoltAngle, angle);
            float wanted = boltPhase == 0 ? -delta : delta;    // loosen anticlockwise, tighten clockwise
            if (wanted > 0f)
            {
                boltTurned += wanted;
                if (boltTurned - ratchetAt > 90f) { ratchetAt = boltTurned; Play(ratchet, 0.6f); }
            }
        }
        boltTracking = true;
        lastBoltAngle = angle;
        float needed = 540f + 180f * (level - 1);
        if (boltTurned >= needed)
        {
            boltTurned = 0f;
            ratchetAt = 0f;
            boltStep++;
            Play(click);
            if (boltStep >= BoltOrder.Length)
            {
                if (boltPhase == 0) { phaseSwitchAt = Time.time + 1.2f; Say(Loc.T("Rad runter... Reserverad drauf...", "Wheel off... spare wheel on..."), 1.2f); }
                else Finish(true);
            }
        }
    }

    void DrawBolts(Rect p)
    {
        Vector2 c = WheelCentre(p);
        Disc(c, 78f, new Color(0.08f, 0.08f, 0.08f));
        Disc(c, 58f, new Color(0.45f, 0.46f, 0.48f));
        Disc(c, 20f, new Color(0.3f, 0.3f, 0.32f));
        int current = boltStep < BoltOrder.Length ? BoltOrder[boltStep] : -1;
        for (int i = 0; i < 5; i++)
        {
            int orderIndex = System.Array.IndexOf(BoltOrder, i);
            bool doneBolt = orderIndex < boltStep;
            bool hidden = boltPhase == 0 && doneBolt;        // loosened and taken off
            if (phaseSwitchAt > 0f) hidden = true;
            if (hidden) { Disc(Bolt(p, i), 5f, new Color(0.2f, 0.2f, 0.2f)); continue; }
            Color col = i == current ? Hint : doneBolt ? Good : new Color(0.7f, 0.7f, 0.65f);
            Disc(Bolt(p, i), 9f, col);
        }
        if (current >= 0 && phaseSwitchAt < 0f)
        {
            // Progress ring around the current nut.
            float needed = 540f + 180f * (level - 1);
            int dots = Mathf.RoundToInt(16 * Mathf.Clamp01(boltTurned / needed));
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                Disc(Bolt(p, current) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17f, 2f, k < dots ? Good : new Color(0.3f, 0.3f, 0.3f));
            }
        }
        string what = boltPhase == 0 ? Loc.T("Radmuttern lösen (gegen den Uhrzeigersinn)", "Loosen the wheel nuts (anticlockwise)")
                                     : Loc.T("Radmuttern festziehen (im Uhrzeigersinn)", "Tighten the wheel nuts (clockwise)");
        RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), $"{what}  {Mathf.Min(boltStep, 5)}/5", Ink);
        Instruction(p, Loc.T("Maus gedrückt halten und um die gelbe Mutter kreisen.", "Hold the mouse button and circle around the yellow nut."));
    }

    // ---------------------------------------------------------------- fuses

    static readonly int[] Amps = { 10, 15, 20, 30 };
    static readonly Color[] AmpColors = { new Color(0.85f, 0.15f, 0.1f), new Color(0.2f, 0.4f, 0.95f), new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.8f, 0.25f) };
    readonly int[] fuseAmp = new int[6];
    readonly int[] fuseState = new int[6];   // 0 ok, 1 blown, 2 empty
    int fuseSelected = -1;

    void SetupFuses()
    {
        for (int i = 0; i < 6; i++) { fuseAmp[i] = Random.Range(0, Amps.Length); fuseState[i] = 0; }
        int blown = 2 + (level >= 3 ? 1 : 0);
        for (int n = 0; n < blown; n++)
        {
            int i;
            do i = Random.Range(0, 6); while (fuseState[i] != 0);
            fuseState[i] = 1;
        }
        fuseSelected = -1;
    }

    Rect FuseSlot(Rect p, int i) => new Rect(p.x + 52 + i * 50, p.y + 55, 30, 60);
    Rect FuseTray(Rect p, int i) => new Rect(p.x + 90 + i * 60, p.y + 150, 36, 40);

    void UpdateFuses()
    {
        if (!Clicked) return;
        var p = Panel;
        var m = MouseV;
        for (int i = 0; i < Amps.Length; i++)
            if (FuseTray(p, i).Contains(m)) { fuseSelected = i; Play(click, 0.5f); return; }
        for (int i = 0; i < 6; i++)
        {
            if (!FuseSlot(p, i).Contains(m)) continue;
            if (fuseState[i] == 1) { fuseState[i] = 2; Play(click); Say(Loc.T("Durchgebrannte Sicherung gezogen.", "Blown fuse pulled."), 1f); }
            else if (fuseState[i] == 2)
            {
                if (fuseSelected < 0) { Say(Loc.T("Erst unten eine Sicherung auswählen.", "Pick a fuse below first."), 1.2f); return; }
                if (fuseSelected == fuseAmp[i])
                {
                    fuseState[i] = 0;
                    Play(ratchet);
                    bool all = true;
                    for (int k = 0; k < 6; k++) all &= fuseState[k] == 0;
                    if (all) Finish(true);
                }
                else
                {
                    Play(spark);
                    lockedUntil = Time.time + 0.8f;
                    Say(Loc.T($"Falsch! Dieser Platz braucht {Amps[fuseAmp[i]]} A.", $"Wrong! This slot needs {Amps[fuseAmp[i]]} A."), 1.5f);
                }
            }
            return;
        }
    }

    void DrawFuses(Rect p)
    {
        RetroGUI.Fill(new Rect(p.x + 40, p.y + 36, p.width - 80, 95), new Color(0.18f, 0.18f, 0.2f));
        for (int i = 0; i < 6; i++)
        {
            var r = FuseSlot(p, i);
            RetroGUI.Label(new Rect(r.x - 6, r.y - 13, r.width + 12, 12), $"{Amps[fuseAmp[i]]}A", Ink, true, true, TextAnchor.UpperCenter);
            RetroGUI.Fill(r, new Color(0.05f, 0.05f, 0.05f));
            if (fuseState[i] == 0) RetroGUI.Fill(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), AmpColors[fuseAmp[i]]);
            else if (fuseState[i] == 1)
            {
                RetroGUI.Fill(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(0.2f, 0.16f, 0.12f));
                RetroGUI.Fill(new Rect(r.x + 8, r.y + 22, r.width - 16, 10), Color.black);
            }
        }
        RetroGUI.Label(new Rect(p.x + 8, p.y + 136, p.width - 16, 12), Loc.T("Ersatzsicherungen:", "Spare fuses:"), Ink, false, true);
        for (int i = 0; i < Amps.Length; i++)
        {
            var r = FuseTray(p, i);
            if (fuseSelected == i) RetroGUI.Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Hint);
            RetroGUI.Fill(r, AmpColors[i]);
            RetroGUI.Label(r, $"{Amps[i]}A", Color.black, true, false, TextAnchor.MiddleCenter);
        }
        Instruction(p, Loc.T("Schwarze Sicherungen anklicken (ziehen), Ersatz mit passender Stärke wählen und einsetzen.",
                             "Click black fuses (pull them), pick a spare with the right rating and put it in."));
    }

    // ---------------------------------------------------------------- wipe

    const int WipeW = 24, WipeH = 12;
    readonly float[] dirt = new float[WipeW * WipeH];
    float dirtTotal, squeakAt, faceUntil;
    bool faceShown;
    Vector2 lastWipe;

    Rect Glass(Rect p) => new Rect(p.x + 20, p.y + 34, p.width - 40, p.height - 70);

    void SetupWipe()
    {
        System.Array.Clear(dirt, 0, dirt.Length);
        // Hand prints: a palm and five fingers each.
        int hands = 5 + level;
        for (int h = 0; h < hands; h++)
        {
            int cx = Random.Range(2, WipeW - 2), cy = Random.Range(3, WipeH - 1);
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) Smear(cx + x, cy + y);
            for (int f = -2; f <= 2; f++) { Smear(cx + f, cy - 2); if (f != -2) Smear(cx + f, cy - 3); }
        }
        dirtTotal = 0f;
        foreach (var d in dirt) dirtTotal += d;
        faceShown = false;
        lastWipe = MouseV;
    }

    void Smear(int x, int y)
    {
        if (x < 0 || y < 0 || x >= WipeW || y >= WipeH) return;
        dirt[y * WipeW + x] = Mathf.Max(dirt[y * WipeW + x], Random.Range(0.7f, 1f));
    }

    void UpdateWipe()
    {
        var p = Panel;
        var g = Glass(p);
        var m = MouseV;
        float moved = Vector2.Distance(m, lastWipe);
        lastWipe = m;
        if (!Held || !g.Contains(m) || moved < 0.2f) return;
        float cw = g.width / WipeW, ch = g.height / WipeH;
        int mx = Mathf.FloorToInt((m.x - g.x) / cw), my = Mathf.FloorToInt((m.y - g.y) / ch);
        for (int y = my - 1; y <= my + 1; y++)
        for (int x = mx - 1; x <= mx + 1; x++)
        {
            if (x < 0 || y < 0 || x >= WipeW || y >= WipeH) continue;
            dirt[y * WipeW + x] = Mathf.Max(0f, dirt[y * WipeW + x] - moved * 0.012f / level);
        }
        if (Time.time > squeakAt) { squeakAt = Time.time + 0.35f; Play(squeak, 0.4f); }

        float left = 0f;
        foreach (var d in dirt) left += d;
        float clean = 1f - left / Mathf.Max(0.01f, dirtTotal);
        if (!faceShown && clean > 0.5f)
        {
            // Behind the glass: a face, looking in. Just for a moment.
            faceShown = true;
            faceUntil = Time.time + 1.1f;
            Play(scare, 1f);
        }
        if (clean >= 0.96f) Finish(true);
    }

    void DrawWipe(Rect p)
    {
        var g = Glass(p);
        RetroGUI.Fill(g, new Color(0.05f, 0.08f, 0.1f));
        if (Time.time < faceUntil)
        {
            Vector2 c = new Vector2(g.center.x + 40, g.center.y);
            Disc(c, 34f, new Color(0.75f, 0.72f, 0.68f));
            Disc(c + new Vector2(-12, -6), 6f, Color.black);
            Disc(c + new Vector2(12, -6), 6f, Color.black);
            RetroGUI.Fill(new Rect(c.x - 10, c.y + 14, 20, 4), new Color(0.2f, 0f, 0f));
        }
        float cw = g.width / WipeW, ch = g.height / WipeH;
        for (int y = 0; y < WipeH; y++)
        for (int x = 0; x < WipeW; x++)
        {
            float d = dirt[y * WipeW + x];
            if (d <= 0.02f) continue;
            RetroGUI.Fill(new Rect(g.x + x * cw, g.y + y * ch, cw + 0.5f, ch + 0.5f), new Color(0.35f, 0.05f, 0.04f, d * 0.95f));
        }
        float left = 0f;
        foreach (var d in dirt) left += d;
        int clean = Mathf.RoundToInt((1f - left / Mathf.Max(0.01f, dirtTotal)) * 100f);
        RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), Loc.T($"Sauber: {clean}%", $"Clean: {clean}%"), Ink);
        Instruction(p, Loc.T("Maus gedrückt halten und über die Abdrücke reiben.", "Hold the mouse button and rub over the prints."));
    }

    // ---------------------------------------------------------------- CPR

    const int Compressions = 12;
    const float BeatInterval = 0.62f, BeatWindow = 0.14f;
    float cprStart, breath;
    int cprJudged, cprGood, cprPhase, breathTries;
    readonly bool[] beatHit = new bool[Compressions];

    void SetupCpr()
    {
        cprStart = Time.time + 1.2f;
        cprJudged = 0;
        cprGood = 0;
        cprPhase = 0;
        breath = 0f;
        breathTries = 0;
        for (int i = 0; i < Compressions; i++) beatHit[i] = false;
    }

    float BeatTime(int i) => cprStart + i * BeatInterval;

    void UpdateCpr()
    {
        if (cprPhase == 0)
        {
            // Beats that went past unhit are misses.
            while (cprJudged < Compressions && Time.time > BeatTime(cprJudged) + BeatWindow)
            {
                if (!beatHit[cprJudged]) Say(Loc.T("Verpasst!", "Missed!"), 0.4f);
                cprJudged++;
            }
            if (SpacePressed)
            {
                int best = -1;
                for (int i = cprJudged; i < Compressions; i++)
                    if (!beatHit[i] && Mathf.Abs(Time.time - BeatTime(i)) <= BeatWindow) { best = i; break; }
                if (best >= 0) { beatHit[best] = true; cprGood++; Play(thump, 1f); }
                else { Play(click, 0.5f); Say(Loc.T("Zu früh!", "Too early!"), 0.4f); }
            }
            if (cprJudged >= Compressions)
            {
                if (cprGood < Compressions - 3 - (3 - level)) { Finish(false); return; }
                cprPhase = 1;
                Say(Loc.T("Jetzt beatmen!", "Now give a breath!"), 1.5f);
            }
            return;
        }
        // One long breath: hold, let go in the green.
        if (SpaceHeld) breath = Mathf.Min(1.1f, breath + Time.deltaTime / 1.8f);
        else if (breath > 0.05f)
        {
            if (breath >= 0.6f && breath <= 0.85f) { Finish(true); return; }
            breathTries++;
            Play(fail, 0.5f);
            Say(breath < 0.6f ? Loc.T("Zu kurz!", "Too short!") : Loc.T("Zu lang!", "Too long!"), 1f);
            breath = 0f;
            if (breathTries >= 3) Finish(false);
        }
    }

    void DrawCpr(Rect p)
    {
        if (cprPhase == 0)
        {
            var track = new Rect(p.x + 30, p.y + 95, p.width - 60, 30);
            RetroGUI.Fill(track, new Color(0.12f, 0.12f, 0.12f));
            float targetX = track.x + 50;
            RetroGUI.Fill(new Rect(targetX - 2, track.y - 6, 4, track.height + 12), Hint);
            for (int i = 0; i < Compressions; i++)
            {
                float dt = BeatTime(i) - Time.time;
                float x = targetX + dt * 160f;
                if (x < track.x - 10 || x > track.xMax) continue;
                Disc(new Vector2(x, track.center.y), 9f, beatHit[i] ? Good : i < cprJudged ? Bad : new Color(0.9f, 0.2f, 0.25f));
            }
            // Pulsing heart.
            float pulse = 1f + 0.15f * Mathf.Max(0f, Mathf.Cos((Time.time - cprStart) / BeatInterval * Mathf.PI * 2f));
            Disc(new Vector2(p.center.x, p.y + 165), 16f * pulse, new Color(0.7f, 0.1f, 0.12f));
            RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), Loc.T($"Herzdruckmassage  {cprGood}/{Compressions}", $"Chest compressions  {cprGood}/{Compressions}"), Ink);
            Instruction(p, Loc.T("[LEERTASTE], wenn ein Punkt die gelbe Linie erreicht.", "[SPACE] when a dot reaches the yellow line."));
        }
        else
        {
            var bar = new Rect(p.x + 60, p.y + 100, p.width - 120, 24);
            RetroGUI.Fill(bar, new Color(0.12f, 0.12f, 0.12f));
            RetroGUI.Fill(new Rect(bar.x + bar.width * 0.6f, bar.y, bar.width * 0.25f, bar.height), new Color(0.2f, 0.6f, 0.25f));
            RetroGUI.Fill(new Rect(bar.x, bar.y + 6, bar.width * Mathf.Min(1f, breath), bar.height - 12), new Color(0.7f, 0.85f, 1f));
            RetroGUI.Label(new Rect(p.x + 8, p.y + 20, p.width - 16, 12), Loc.T($"Beatmen  (Versuch {breathTries + 1}/3)", $"Breathe  (try {breathTries + 1}/3)"), Ink);
            Instruction(p, Loc.T("[LEERTASTE] halten und im grünen Bereich loslassen.", "Hold [SPACE] and let go in the green."));
        }
    }
}
