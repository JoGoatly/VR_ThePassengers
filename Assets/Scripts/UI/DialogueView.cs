using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Talking to the passenger at the door: keys 1-5 ask a question, the answer appears as a
/// subtitle. Compare the answers with the register - a doppelganger has perfect papers
/// but gets facts about its own life wrong.
/// </summary>
public class DialogueView : MonoBehaviour
{
    public BoardingManager game;

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

    readonly List<(string who, string text)> log = new List<(string, string)>();
    IdCard lastCard;
    float answerAt = -1f;
    string pendingAnswer;
    readonly Queue<string> intro = new Queue<string>();
    float nextIntroAt;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
    }

    void Update()
    {
        var card = game != null ? game.PendingCard : null;
        if (card != lastCard)
        {
            log.Clear();
            pendingAnswer = null;
            intro.Clear();
            lastCard = card;
            // Story passengers start talking on their own.
            if (card != null && card.IntroLines != null)
                foreach (var line in card.IntroLines) intro.Enqueue(line);
            nextIntroAt = Time.time + 0.6f;
        }
        if (card == null) return;

        if (intro.Count > 0 && pendingAnswer == null && Time.time >= nextIntroAt)
        {
            string line = intro.Dequeue();
            log.Add((card.FirstName, line));
            Spoke?.Invoke(card);
            nextIntroAt = Time.time + 1.6f + line.Length * 0.05f;   // time to read it
        }

        // The answer comes after a short pause.
        if (pendingAnswer != null && Time.time >= answerAt)
        {
            log.Add((card.FirstName, pendingAnswer));
            pendingAnswer = null;
            Spoke?.Invoke(card);
            nextIntroAt = Mathf.Max(nextIntroAt, Time.time + 2f);
        }

        var kb = Keyboard.current;
        if (kb == null || GameUI.TerminalTyping || pendingAnswer != null) return;
        for (int i = 0; i < Questions.Length; i++)
        {
            if (!KeyPressed(kb, i)) continue;
            Ask(card, i);
            break;
        }
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

    void Ask(IdCard card, int question)
    {
        log.Add(("Du", Questions[question]));
        pendingAnswer = question switch
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

        // Last lines of the conversation.
        int first = Mathf.Max(0, log.Count - 4);
        float y = 222;
        for (int i = first; i < log.Count; i++)
        {
            var (who, text) = log[i];
            Color c = who == "Du" ? new Color(0.75f, 0.75f, 0.75f) : Color.white;
            RetroGUI.ShadowLabel(new Rect(0, y, w, 14), $"{who}: \"{text}\"", c, who != "Du");
            y += 14;
        }
        if (pendingAnswer != null)
            RetroGUI.ShadowLabel(new Rect(0, y, w, 14), "...", Color.white, false);
    }
}
