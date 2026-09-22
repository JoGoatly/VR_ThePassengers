using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Talking to the passenger at the door, all in one dialogue box:
///   T opens the talk menu, click a question with the mouse (Esc / T closes it).
///   The answer replaces the text in the box, F closes it.
/// Story passengers talk on their own; F shows their next line, and while they talk
/// the camera looks at their face.
/// Compare the answers with the register - a doppelganger has perfect papers
/// but gets facts about its own life wrong.
/// </summary>
public class DialogueView : MonoBehaviour
{
    public BoardingManager game;
    public DriverCamera driverCamera;

    /// <summary>Raised when the passenger answers (for a voice sound).</summary>
    public event System.Action<IdCard> Spoke;

    static readonly string[] Questions =
    {
        "Wie heißen Sie?",
        "Wann sind Sie geboren?",
        "Wo wohnen Sie?",
        "Was arbeiten Sie?",
        "Wohin fahren Sie?",
    };

    IdCard lastCard;
    float answerAt = -1f;
    string pendingAnswer;
    string question;          // last thing the driver asked
    string line;              // what the passenger says right now (null = box closed)
    bool lineIsIntro;         // the line is part of the story passenger's own talk
    readonly Queue<string> intro = new Queue<string>();
    float introStartAt;
    bool introStarted;

    bool MenuOpen
    {
        get => GameUI.DialogueOpen;
        set
        {
            if (GameUI.DialogueOpen == value) return;
            GameUI.DialogueOpen = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }
    }

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (driverCamera == null) driverCamera = FindAnyObjectByType<DriverCamera>();
    }

    void OnDisable() => GameUI.DialogueOpen = false;

    void Update()
    {
        var card = game != null ? game.PendingCard : null;
        if (card != lastCard)
        {
            pendingAnswer = null;
            question = null;
            line = null;
            lineIsIntro = false;
            intro.Clear();
            introStarted = false;
            MenuOpen = false;
            lastCard = card;
            // Story passengers start talking on their own.
            if (card != null && card.IntroLines != null)
                foreach (var l in card.IntroLines) intro.Enqueue(l);
            introStartAt = Time.time + 0.6f;
        }
        UpdateFocus(card);
        if (card == null) return;

        var kb = Keyboard.current;
        bool keys = kb != null && !GameUI.TerminalTyping;

        // First line comes by itself.
        if (!introStarted && intro.Count > 0 && Time.time >= introStartAt)
        {
            introStarted = true;
            Say(card, intro.Dequeue(), true);
            return;
        }

        // The answer comes after a short pause.
        if (pendingAnswer != null && Time.time >= answerAt)
        {
            Say(card, pendingAnswer, false);
            pendingAnswer = null;
        }
        if (!keys || pendingAnswer != null) return;

        if (MenuOpen)
        {
            if (kb.tKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) MenuOpen = false;
            return;
        }

        if (kb.fKey.wasPressedThisFrame && line != null)
        {
            // F: next line of the story passenger, or close the box.
            question = null;
            if (lineIsIntro && intro.Count > 0) Say(card, intro.Dequeue(), true);
            else { line = null; lineIsIntro = false; }
        }
        else if (kb.tKey.wasPressedThisFrame && !(lineIsIntro && intro.Count > 0))
        {
            line = null;
            lineIsIntro = false;
            question = null;
            MenuOpen = true;
        }
    }

    void Say(IdCard card, string text, bool fromIntro)
    {
        line = text;
        lineIsIntro = fromIntro;
        Spoke?.Invoke(card);
    }

    // While the passenger talks to you (or you talk to them), look at their face.
    void UpdateFocus(IdCard card)
    {
        if (driverCamera == null) return;
        Transform head = null;
        if (card != null && (lineIsIntro || MenuOpen))
        {
            var p = game.PendingPassenger;
            if (p != null) head = p.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head"));
        }
        driverCamera.focusTarget = head;
    }

    void Ask(IdCard card, int index)
    {
        MenuOpen = false;
        question = Questions[index];
        line = null;
        lineIsIntro = false;
        pendingAnswer = index switch
        {
            0 => card.SaidName,
            1 => card.SaidBirth,
            2 => card.SaidHome,
            3 => card.SaidJob,
            _ => card.SaidDestination,
        };
        // Doppelgangers take a moment too long.
        answerAt = Time.time + (card.Truth == Discrepancy.Doppelganger ? Random.Range(1.2f, 2.2f) : Random.Range(0.4f, 0.8f));
    }

    void OnGUI()
    {
        var card = game != null ? game.PendingCard : null;
        if (card == null) return;
        var hint = new Color(1f, 0.85f, 0.3f);
        var border = new Color(0.55f, 0.5f, 0.4f, 0.9f);
        var fill = new Color(0f, 0f, 0f, 0.72f);

        if (MenuOpen)
        {
            // Talk menu: click a question.
            var menu = new Rect(232, 196, 340, 84);
            RetroGUI.Frame(menu, fill, border);
            RetroGUI.Label(new Rect(menu.x + 8, menu.y + 4, menu.width - 16, 12), card.FirstName + " ansprechen:", new Color(1f, 0.85f, 0.55f), true, true);
            for (int i = 0; i < Questions.Length; i++)
            {
                if (RetroGUI.Button(new Rect(menu.x + 8, menu.y + 17 + i * 12.5f, menu.width - 16, 11.5f), Questions[i],
                        new Color(0.12f, 0.12f, 0.12f, 0.9f), new Color(0.9f, 0.9f, 0.9f)))
                    Ask(card, i);
            }
            RetroGUI.Label(new Rect(menu.x, menu.yMax - 11, menu.width - 6, 10), "schließen [T]", hint, false, true, TextAnchor.UpperRight);
            return;
        }

        if (line == null && pendingAnswer == null)
        {
            RetroGUI.ShadowLabel(new Rect(0, 284, RetroGUI.VirtualWidth, 14), "Ansprechen [T]", new Color(0.7f, 0.85f, 1f), false);
            return;
        }

        // One dialogue box, replaced with every line.
        var box = new Rect(232, 226, 340, 54);
        RetroGUI.Frame(box, fill, border);
        float y = box.y + 4;
        if (question != null)
        {
            RetroGUI.Label(new Rect(box.x + 8, y, box.width - 16, 12), "Du: " + question, new Color(0.7f, 0.7f, 0.7f), false, true);
            y += 11;
        }
        RetroGUI.Label(new Rect(box.x + 8, y, box.width - 16, 12), card.FirstName + ":", new Color(1f, 0.85f, 0.55f), true, true);
        string text = pendingAnswer != null ? "..." : line;
        RetroGUI.Wrapped(new Rect(box.x + 8, y + 10, box.width - 16, box.yMax - y - 12), text ?? "", Color.white);
        if (pendingAnswer == null)
            RetroGUI.Label(new Rect(box.x, box.yMax - 11, box.width - 6, 10), lineIsIntro && intro.Count > 0 ? "weiter [F]" : "schließen [F]",
                hint, false, true, TextAnchor.UpperRight);
    }
}
