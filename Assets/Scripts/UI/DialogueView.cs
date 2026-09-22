using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Talking to the passenger at the door: keys 1-5 ask a question, the answer appears in one
/// dialogue box that is replaced with every new line. Story passengers talk on their own;
/// F shows their next line, and while they talk the camera looks at their face.
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
    string line;              // what the passenger says right now
    readonly Queue<string> intro = new Queue<string>();
    float introStartAt;
    bool introStarted;
    float focusUntil;

    bool Talking => introStarted && intro.Count > 0;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (driverCamera == null) driverCamera = FindAnyObjectByType<DriverCamera>();
    }

    void Update()
    {
        var card = game != null ? game.PendingCard : null;
        if (card != lastCard)
        {
            pendingAnswer = null;
            question = null;
            line = null;
            intro.Clear();
            introStarted = false;
            focusUntil = -1f;
            lastCard = card;
            // Story passengers start talking on their own.
            if (card != null && card.IntroLines != null)
                foreach (var l in card.IntroLines) intro.Enqueue(l);
            introStartAt = Time.time + 0.6f;
        }
        UpdateFocus(card);
        if (card == null) return;

        var kb = Keyboard.current;

        // First line comes by itself, the rest with F.
        if (!introStarted && intro.Count > 0 && Time.time >= introStartAt)
        {
            introStarted = true;
            Say(card, intro.Dequeue());
        }
        else if (introStarted && intro.Count > 0 && pendingAnswer == null && kb != null && !GameUI.TerminalTyping && kb.fKey.wasPressedThisFrame)
        {
            question = null;
            Say(card, intro.Dequeue());
        }

        // The answer comes after a short pause.
        if (pendingAnswer != null && Time.time >= answerAt)
        {
            Say(card, pendingAnswer);
            pendingAnswer = null;
        }

        if (kb == null || GameUI.TerminalTyping || pendingAnswer != null) return;
        for (int i = 0; i < Questions.Length; i++)
        {
            if (!KeyPressed(kb, i)) continue;
            Ask(card, i);
            break;
        }
    }

    void Say(IdCard card, string text)
    {
        line = text;
        if (introStarted && intro.Count == 0 && card.IntroLines != null && focusUntil < 0f) focusUntil = Time.time + 3f;
        Spoke?.Invoke(card);
    }

    // While a story passenger is still talking, look at their face.
    void UpdateFocus(IdCard card)
    {
        if (driverCamera == null) return;
        Transform head = null;
        if (card != null && (Talking || Time.time < focusUntil))
        {
            var p = game.PendingPassenger;
            if (p != null) head = p.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head"));
        }
        driverCamera.focusTarget = head;
    }


    static bool KeyPressed(Keyboard kb, int i)
    {
        switch (i)
        {
            case 0: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case 1: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case 2: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case 3: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            default: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
        }
    }

    void Ask(IdCard card, int index)
    {
        question = Questions[index];
        line = null;
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
        float w = RetroGUI.VirtualWidth;

        // Question menu.
        string menu = "";
        for (int i = 0; i < Questions.Length; i++) menu += $"[{i + 1}] {Questions[i]}   ";
        RetroGUI.ShadowLabel(new Rect(0, 284, w, 14), menu, new Color(0.7f, 0.85f, 1f), false);

        // One dialogue box, replaced with every line.
        if (question == null && line == null && pendingAnswer == null) return;
        var box = new Rect(232, 226, 340, 54);
        RetroGUI.Frame(box, new Color(0f, 0f, 0f, 0.72f), new Color(0.55f, 0.5f, 0.4f, 0.9f));
        float y = box.y + 4;
        if (question != null)
        {
            RetroGUI.Label(new Rect(box.x + 8, y, box.width - 16, 12), "Du: " + question, new Color(0.7f, 0.7f, 0.7f), false, true);
            y += 11;
        }
        RetroGUI.Label(new Rect(box.x + 8, y, box.width - 16, 12), card.FirstName + ":", new Color(1f, 0.85f, 0.55f), true, true);
        string text = pendingAnswer != null ? "..." : line;
        RetroGUI.Wrapped(new Rect(box.x + 8, y + 10, box.width - 16, box.yMax - y - 12), text ?? "", Color.white);
        if (introStarted && intro.Count > 0 && pendingAnswer == null)
            RetroGUI.Label(new Rect(box.x, box.yMax - 11, box.width - 6, 10), "weiter [F]", new Color(1f, 0.85f, 0.3f), false, true, TextAnchor.UpperRight);
    }
}
