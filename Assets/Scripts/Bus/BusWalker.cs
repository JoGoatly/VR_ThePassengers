using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Walking around inside the standing bus. E in the driver's seat stands up (or uses the
/// radio when a radio check is on). In the aisle: talk to seated passengers, use things
/// (fuse box, first aid), get out through the front door, or sit back down to drive.
/// The player moves in bus space, so the bus is always the floor.
/// </summary>
public class BusWalker : MonoBehaviour
{
    public BusController bus;
    public BoardingManager game;
    public PlayerOnFoot onFoot;

    public float walkSpeed = 1.6f;
    public float eyeHeight = 1.62f;
    public float mouseSensitivity = 0.12f;
    public AudioClip sitSound;

    DriverCamera driverCamera;
    DriverBody driver;
    Transform cam, camParent, pivot;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    float yaw, pitch, stepDistance, bobPhase;
    BusInterior inside;
    SoundManager sound;

    // Talking to a seated passenger.
    Passenger talkTo;
    bool talkMenu;
    string talkQuestion, talkAnswer;
    float answerAt;

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (onFoot == null) onFoot = FindAnyObjectByType<PlayerOnFoot>();
        driverCamera = FindAnyObjectByType<DriverCamera>();
        driver = FindAnyObjectByType<DriverBody>();
        sound = FindAnyObjectByType<SoundManager>();
        GameUI.InBus = false;
    }

    void OnDestroy() => GameUI.InBus = false;

    bool EPressed => GameKeys.Pressed(GameAction.Interact) && !GameUI.JustClosed && !GameUI.TerminalTyping && !GameUI.DialogueOpen;

    void Update()
    {
        if (bus == null || game == null || GameUI.MenuOpen || GameUI.NoteOpen || GameUI.PcOpen) return;
        if (MiniGames.Running) return;

        if (!GameUI.InBus)
        {
            if (GameUI.PlayerOutside || GameUI.PhoneOpen || !EPressed) return;
            bool standing = Mathf.Abs(bus.Speed) < 0.3f && game.CurrentPhase == BoardingManager.Phase.Driving;
            if (!standing) return;
            var events = NightEvents.Instance;
            if (events != null && events.SeatPrompt != null) events.UseSeatAction();
            else StandUp();
            return;
        }

        if (talkMenu || talkAnswer != null) { UpdateTalk(); return; }
        Walk();

        if (!EPressed) return;
        var usable = Interactable.Nearest(pivot.position, pivot.forward);
        var rider = RiderInFront();
        if (usable != null) usable.Use();
        else if (rider != null) OpenTalk(rider);
        else if (inside.AtDriverSeat(pivot.localPosition)) SitDown();
        else if (inside.AtFrontDoor(pivot.localPosition)) { SitDown(); if (onFoot != null) onFoot.GetOut(); }
    }

    // ---------------------------------------------------------------- stand up / sit down

    void StandUp()
    {
        inside = BusInterior.Of(bus.transform);
        if (inside == null || !inside.Valid || driverCamera == null)
        {
            // Without the interior: straight out of the door like before.
            if (onFoot != null) onFoot.GetOut();
            return;
        }
        cam = driverCamera.transform;
        camParent = cam.parent;
        camLocalPos = cam.localPosition;
        camLocalRot = cam.localRotation;
        driverCamera.enabled = false;
        if (driver != null) driver.gameObject.SetActive(false);

        pivot = new GameObject("Player (in bus)").transform;
        pivot.SetParent(bus.transform, false);
        pivot.localPosition = inside.StandUpSpot;
        Vector3 back = -inside.Forward;
        yaw = Mathf.Atan2(back.x, back.z) * Mathf.Rad2Deg;   // looking down the aisle
        pitch = 0f;
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        cam.SetParent(pivot, false);
        cam.localPosition = new Vector3(0f, eyeHeight, 0f);
        cam.localRotation = Quaternion.identity;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameUI.InBus = true;
        Play(sitSound);
    }

    void SitDown()
    {
        if (!GameUI.InBus) return;
        CloseTalk();
        cam.SetParent(camParent, false);
        cam.localPosition = camLocalPos;
        cam.localRotation = camLocalRot;
        driverCamera.enabled = true;
        if (driver != null) driver.gameObject.SetActive(true);
        if (pivot != null) Destroy(pivot.gameObject);
        pivot = null;
        GameUI.InBus = false;
        Play(sitSound);
    }

    // ---------------------------------------------------------------- walking

    void Walk()
    {
        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !GameUI.PhoneOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Vector2 input = Vector2.zero;
        if (GameKeys.Held(GameAction.Forward)) input.y += 1f;
        if (GameKeys.Held(GameAction.Backward)) input.y -= 1f;
        if (GameKeys.Held(GameAction.SteerRight)) input.x += 1f;
        if (GameKeys.Held(GameAction.SteerLeft)) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = pivot.localRotation * new Vector3(input.x, 0f, input.y) * walkSpeed * Time.deltaTime;
        Vector3 before = pivot.localPosition;
        pivot.localPosition = inside.ClampWalk(before + move);
        float moved = Vector3.Distance(before, pivot.localPosition);

        bobPhase += moved * 5f;
        cam.localPosition = new Vector3(0f, eyeHeight - Mathf.Abs(Mathf.Sin(bobPhase)) * 0.03f, 0f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        stepDistance += moved;
        if (stepDistance > 0.7f)
        {
            stepDistance = 0f;
            Passenger.RaiseStep(pivot.position);
        }
    }

    // A seated passenger right in front of us.
    Passenger RiderInFront()
    {
        Passenger best = null;
        float bestScore = float.MaxValue;
        foreach (var r in game.Riders)
        {
            if (r == null || !r.Sitting) continue;
            Vector3 d = r.transform.position - pivot.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > 1.5f) continue;
            float facing = dist > 0.01f ? Vector3.Dot(d / dist, cam.forward) : 1f;
            if (facing < 0.3f) continue;
            float score = dist - facing;
            if (score < bestScore) { bestScore = score; best = r; }
        }
        return best;
    }

    // ---------------------------------------------------------------- talking

    static string[] TalkQuestions => new[]
    {
        Loc.T("Alles in Ordnung bei Ihnen?", "Are you alright?"),
        Loc.T("Wohin fahren Sie?", "Where are you going?"),
        Loc.T("Kannten Sie meinen Vorgänger?", "Did you know the driver before me?"),
        Loc.T("Haben Sie heute Nacht etwas Seltsames gesehen?", "Have you seen anything strange tonight?"),
    };

    void OpenTalk(Passenger p)
    {
        talkTo = p;
        talkMenu = true;
        talkAnswer = null;
        talkQuestion = null;
        GameUI.MinigameOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseTalk()
    {
        bool wasOpen = talkMenu || talkAnswer != null;
        talkMenu = false;
        talkAnswer = null;
        talkTo = null;
        if (!wasOpen) return;
        GameUI.MinigameOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UpdateTalk()
    {
        var kb = Keyboard.current;
        if (talkTo == null) { CloseTalk(); return; }
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { CloseTalk(); return; }
        if (talkAnswer != null && Time.time >= answerAt &&
            (GameKeys.Pressed(GameAction.Continue) || GameKeys.Pressed(GameAction.Interact)))
        {
            // Back to the questions.
            talkAnswer = null;
            talkMenu = true;
        }
    }

    void Ask(int index)
    {
        talkQuestion = TalkQuestions[index];
        talkAnswer = Answer(talkTo, index);
        answerAt = Time.time + 0.5f;
        talkMenu = false;
        if (talkTo != null && sound != null && talkTo.Card != null)
        {
            var voices = talkTo.Card.Gender == Gender.Male ? sound.maleVoices : sound.femaleVoices;
            if (voices != null && voices.Length > 0) sound.PlayWorld(voices[Random.Range(0, voices.Length)], talkTo.transform.position, 0.8f, 1f);
        }
    }

    // What a passenger says; the ones that shouldn't be here say strange things.
    static string Answer(Passenger p, int q)
    {
        var card = p != null ? p.Card : null;
        if (card == null) return "...";
        bool wrong = card.Truth == Discrepancy.Deceased || card.Truth == Discrepancy.Doppelganger || card.Truth == Discrepancy.Duplicate;
        int pick = Mathf.Abs((card.FullName + q).GetHashCode());
        string[] pool;
        switch (q)
        {
            case 0:
                pool = wrong
                    ? new[] { Loc.T("Mir ist so kalt. Schon seit Jahren.", "I'm so cold. For years now."),
                              Loc.T("Alles ist in Ordnung. Alles ist in Ordnung. Alles ist in Ordnung.", "Everything is fine. Everything is fine. Everything is fine."),
                              Loc.T("Ich weiß nicht mehr, wie ich eingestiegen bin.", "I don't remember getting on.") }
                    : new[] { Loc.T("Geht so. Lange Schicht gehabt.", "So-so. Long shift."),
                              Loc.T("Ja, danke. Ich will nur nach Hause.", "Yes, thanks. I just want to get home."),
                              Loc.T("Mir ist ein bisschen übel. Sie fahren ganz schön schnell.", "I feel a bit sick. You drive quite fast."),
                              Loc.T("Solange Sie nicht halten, wo keine Haltestelle ist...", "As long as you don't stop where there's no stop...") };
                break;
            case 1:
                if (!wrong && !string.IsNullOrEmpty(card.SaidDestination)) return card.SaidDestination;
                pool = new[] { Loc.T("Nach Hause. Zum Waldfriedhof.", "Home. To the Waldfriedhof."),
                               Loc.T("Dahin, wo Sie auch hinfahren.", "Where you are going, too."),
                               Loc.T("Bis zur Endstation. Wie alle.", "To the last stop. Like everyone.") };
                break;
            case 2:
                pool = wrong
                    ? new[] { Loc.T("Er sitzt doch hinten. Sehen Sie ihn nicht?", "He's sitting at the back. Can't you see him?"),
                              Loc.T("Er hat mich auch gefragt, ob ich ihn kenne.", "He asked me if I knew him, too."),
                              Loc.T("Karl? Karl ist nie ausgestiegen.", "Karl? Karl never got off.") }
                    : new[] { Loc.T("Den Karl? Netter Mann. Hat immer gepfiffen.", "Karl? Nice man. Always whistling."),
                              Loc.T("Der hat nie angehalten, wenn es geregnet hat.", "He never stopped when it rained."),
                              Loc.T("Nein. Aber die Leute reden viel über die Fahrer der 13.", "No. But people talk a lot about the drivers of the 13."),
                              Loc.T("Der letzte hat irgendwann nur noch auf den Wald gestarrt.", "The last one just stared at the forest in the end.") };
                break;
            default:
                pool = wrong
                    ? new[] { Loc.T("Nur Sie.", "Only you."),
                              Loc.T("Den Mann auf dem Dach. Er hält sich gut fest.", "The man on the roof. He holds on tight."),
                              Loc.T("Ihre Haltestelle kommt bald. Die letzte.", "Your stop is coming soon. The last one.") }
                    : new[] { Loc.T("Am Waldrand stand jemand. Ohne Gesicht.", "Someone stood at the edge of the forest. Without a face."),
                              Loc.T("Nein. Und ich will auch nichts sehen.", "No. And I don't want to see anything."),
                              Loc.T("Das Radio hat vorhin meinen Namen gesagt.", "The radio said my name earlier."),
                              Loc.T("Der Mann, der vorhin eingestiegen ist... der sitzt nicht mehr da.", "The man who got on earlier... he's not sitting there anymore.") };
                break;
        }
        return pool[pick % pool.Length];
    }

    // ---------------------------------------------------------------- HUD

    void OnGUI()
    {
        if (!GameUI.InBus || GameUI.MenuOpen || MiniGames.Running) return;
        float w = RetroGUI.VirtualWidth;
        var border = new Color(0.55f, 0.5f, 0.4f, 0.9f);
        var fill = new Color(0f, 0f, 0f, 0.72f);
        var hint = new Color(1f, 0.85f, 0.3f);

        if (talkMenu && talkTo != null)
        {
            var menu = new Rect(w / 2 - 170, 200, 340, 72);
            RetroGUI.Panel(menu, 1);
            string name = talkTo.Card != null ? talkTo.Card.FirstName : "?";
            RetroGUI.Label(new Rect(menu.x + 8, menu.y + 4, menu.width - 16, 12), Loc.T(name + " ansprechen:", "Talk to " + name + ":"), new Color(1f, 0.85f, 0.55f), true, true);
            var qs = TalkQuestions;
            for (int i = 0; i < qs.Length; i++)
                if (RetroGUI.Button(new Rect(menu.x + 8, menu.y + 17 + i * 12.5f, menu.width - 16, 11.5f), qs[i], new Color(0.12f, 0.12f, 0.12f, 0.9f), new Color(0.9f, 0.9f, 0.9f)))
                    Ask(i);
            RetroGUI.Label(new Rect(menu.x, menu.yMax - 11, menu.width - 6, 10), Loc.T("schließen [ESC]", "close [ESC]"), hint, false, true, TextAnchor.UpperRight);
            return;
        }
        if (talkAnswer != null && talkTo != null)
        {
            var box = new Rect(w / 2 - 170, 226, 340, 54);
            RetroGUI.Panel(box, 1);
            RetroGUI.Label(new Rect(box.x + 8, box.y + 4, box.width - 16, 12), Loc.T("Du: ", "You: ") + talkQuestion, new Color(0.7f, 0.7f, 0.7f), false, true);
            RetroGUI.Label(new Rect(box.x + 8, box.y + 15, box.width - 16, 12), (talkTo.Card != null ? talkTo.Card.FirstName : "?") + ":", new Color(1f, 0.85f, 0.55f), true, true);
            RetroGUI.Wrapped(new Rect(box.x + 8, box.y + 25, box.width - 16, 26), Time.time < answerAt ? "..." : talkAnswer, Color.white);
            RetroGUI.Label(new Rect(box.x, box.yMax - 11, box.width - 6, 10), Loc.T("weiter ", "next ") + GameKeys.Tag(GameAction.Continue), hint, false, true, TextAnchor.UpperRight);
            return;
        }

        // What E does here.
        string prompt = null;
        var usable = pivot != null ? Interactable.Nearest(pivot.position, pivot.forward) : null;
        var rider = pivot != null ? RiderInFront() : null;
        if (usable != null) prompt = usable.Prompt;
        else if (rider != null) prompt = Loc.T("Reden mit ", "Talk to ") + (rider.Card != null ? rider.Card.FirstName : "?");
        else if (pivot != null && inside.AtDriverSeat(pivot.localPosition)) prompt = Loc.T("Hinsetzen und weiterfahren", "Sit down and drive on");
        else if (pivot != null && inside.AtFrontDoor(pivot.localPosition)) prompt = Loc.T("Aussteigen", "Get out");
        if (prompt != null) RetroGUI.ShadowLabel(new Rect(0, 318, w, 14), prompt + "  " + GameKeys.Tag(GameAction.Interact), hint);
        else RetroGUI.ShadowLabel(new Rect(0, 334, w, 14), Loc.T("Im Bus  -  zum Fahrersitz gehen, um weiterzufahren", "In the bus  -  go to the driver's seat to drive on"), new Color(0.8f, 0.8f, 0.8f), false);
    }

    void Play(AudioClip clip)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, 0.6f, 0f);
    }
}
