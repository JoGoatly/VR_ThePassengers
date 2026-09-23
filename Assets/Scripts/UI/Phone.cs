using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The work phone (key Q): SMS with the family (pick a reply), a WAP news page that reports
/// on the anomalies and later on what happens on line 13, and calls - 110 sends the police.
/// Opens in the corner of the screen with a free mouse cursor; driving keys keep working.
/// </summary>
public class Phone : MonoBehaviour
{
    public BoardingManager game;
    public PoliceDispatch police;
    public SoundManager sound;
    public AudioClip smsSound, ringSound;

    enum Page { Home, Chats, Chat, News, Article, Contacts, Calling }

    class Message { public bool mine; public string text; }

    Page page = Page.Home;
    bool open;
    string chatWith;
    int article;
    readonly Dictionary<string, List<Message>> chats = new Dictionary<string, List<Message>>();
    readonly Dictionary<string, int> unread = new Dictionary<string, int>();
    readonly Dictionary<string, PhoneContent.Sms> awaitingReply = new Dictionary<string, PhoneContent.Sms>();
    readonly HashSet<int> readArticles = new HashSet<int>();
    readonly List<(float at, System.Action action)> scheduled = new List<(float, System.Action)>();
    string calling, callResult;
    float callStarted;
    AudioSource ring;

    static readonly Color Body = new Color(0.12f, 0.13f, 0.14f);
    static readonly Color Lcd = new Color(0.55f, 0.64f, 0.45f);
    static readonly Color LcdDark = new Color(0.45f, 0.53f, 0.36f);
    static readonly Color Ink = new Color(0.1f, 0.14f, 0.08f);
    static readonly Color InkDim = new Color(0.25f, 0.32f, 0.2f);

    int UnreadSms => unread.Values.Sum();
    int UnreadNews => PhoneContent.News.Count(a => a.night <= Progress.Day && !readArticles.Contains(System.Array.IndexOf(PhoneContent.News, a)));

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (police == null) police = FindAnyObjectByType<PoliceDispatch>();
        if (sound == null) sound = FindAnyObjectByType<SoundManager>();
        foreach (var sms in PhoneContent.ForNight(Progress.Day))
        {
            var m = sms;
            scheduled.Add((Time.time + m.delay, () => Receive(m.from, Loc.T(m.de, m.en), m)));
        }
        // The news of the night arrive a little after the start.
        scheduled.Add((Time.time + 25f, () => Beep()));
        ring = gameObject.AddComponent<AudioSource>();
        ring.playOnAwake = false;
        ring.loop = true;
        ring.spatialBlend = 0f;
    }

    void OnDisable() => GameUI.PhoneOpen = false;

    void Receive(string from, string text, PhoneContent.Sms withReplies)
    {
        Chat(from).Add(new Message { mine = false, text = text });
        if (withReplies != null) awaitingReply[from] = withReplies;
        if (!(open && page == Page.Chat && chatWith == from)) unread[from] = (unread.TryGetValue(from, out int n) ? n : 0) + 1;
        Beep();
    }

    void Beep()
    {
        if (sound != null && smsSound != null && Camera.main != null)
            sound.PlayWorld(smsSound, Camera.main.transform.position, 0.8f, 0f);
    }

    List<Message> Chat(string id)
    {
        if (!chats.TryGetValue(id, out var list)) chats[id] = list = new List<Message>();
        return list;
    }

    void Update()
    {
        for (int i = scheduled.Count - 1; i >= 0; i--)
        {
            if (Time.time < scheduled[i].at) continue;
            var a = scheduled[i].action;
            scheduled.RemoveAt(i);
            a?.Invoke();
        }
        if (ring != null) ring.volume = 0.5f * GameSettings.Effects;

        var kb = Keyboard.current;
        if (kb == null || GameUI.MenuOpen || GameUI.TerminalTyping || GameUI.NoteOpen) return;
        if (GameKeys.Pressed(GameAction.Phone)) SetOpen(!open);
        else if (open && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
    }

    void SetOpen(bool value)
    {
        open = value;
        GameUI.PhoneOpen = value;
        Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value;
        if (!value && ring != null) ring.Stop();
    }

    // ---------------------------------------------------------------- drawing

    void OnGUI()
    {
        if (GameUI.MenuOpen) return;
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        if (!open)
        {
            int n = UnreadSms + UnreadNews;
            if (n > 0 && Mathf.Repeat(Time.time, 1.2f) < 0.8f)
                RetroGUI.ShadowLabel(new Rect(w - 160, h - 18, 150, 12), Loc.T($"HANDY ({n}) ", $"PHONE ({n}) ") + GameKeys.Tag(GameAction.Phone),
                    new Color(0.6f, 1f, 0.6f), true, TextAnchor.UpperRight);
            return;
        }

        GUI.depth = -200;
        var phone = new Rect(w - 158, 96, 148, 250);
        RetroGUI.Frame(phone, Body, new Color(0.3f, 0.32f, 0.34f), 2f);
        RetroGUI.Label(new Rect(phone.x, phone.y + 4, phone.width, 10), "NOKIO 5110", new Color(0.5f, 0.52f, 0.55f), true, true, TextAnchor.UpperCenter);
        var screen = new Rect(phone.x + 10, phone.y + 18, phone.width - 20, 168);
        RetroGUI.Fill(screen, Lcd);

        // Status line.
        RetroGUI.Label(new Rect(screen.x + 3, screen.y + 1, 60, 10), game != null ? game.ClockText : "", Ink, true, true);
        RetroGUI.Label(new Rect(screen.x, screen.y + 1, screen.width - 3, 10), "D1  |||", InkDim, false, true, TextAnchor.UpperRight);
        RetroGUI.Fill(new Rect(screen.x + 2, screen.y + 12, screen.width - 4, 1), InkDim);
        var content = new Rect(screen.x + 3, screen.y + 15, screen.width - 6, screen.height - 18);

        switch (page)
        {
            case Page.Home: DrawHome(content); break;
            case Page.Chats: DrawChats(content); break;
            case Page.Chat: DrawChat(content); break;
            case Page.News: DrawNews(content); break;
            case Page.Article: DrawArticle(content); break;
            case Page.Contacts: DrawContacts(content); break;
            case Page.Calling: DrawCalling(content); break;
        }

        // Keys under the screen.
        var back = new Rect(phone.x + 10, screen.yMax + 8, 60, 16);
        var close = new Rect(phone.xMax - 70, screen.yMax + 8, 60, 16);
        if (Key(back, Loc.T("ZURÜCK", "BACK"))) GoBack();
        if (Key(close, Loc.T("ZU ", "CLOSE ") + GameKeys.Tag(GameAction.Phone))) SetOpen(false);
        RetroGUI.Label(new Rect(phone.x, phone.yMax - 26, phone.width, 10), "1 2 3   4 5 6   7 8 9", new Color(0.4f, 0.42f, 0.45f), false, true, TextAnchor.UpperCenter);
    }

    void GoBack()
    {
        if (page == Page.Calling && ring != null) ring.Stop();
        page = page switch
        {
            Page.Chat => Page.Chats,
            Page.Article => Page.News,
            Page.Calling => Page.Contacts,
            _ => Page.Home,
        };
    }

    bool Key(Rect r, string text)
    {
        bool hover = RetroGUI.R(r.x, r.y, r.width, r.height).Contains(Event.current.mousePosition);
        RetroGUI.Frame(r, hover ? new Color(0.3f, 0.32f, 0.35f) : new Color(0.2f, 0.21f, 0.23f), new Color(0.4f, 0.42f, 0.45f));
        RetroGUI.Label(r, text, new Color(0.85f, 0.87f, 0.9f), true, true, TextAnchor.MiddleCenter);
        return GUI.Button(RetroGUI.R(r.x, r.y, r.width, r.height), GUIContent.none, GUIStyle.none);
    }

    // A selectable line on the LCD.
    bool Line(Rect r, string text, bool highlight = false)
    {
        bool hover = RetroGUI.R(r.x, r.y, r.width, r.height).Contains(Event.current.mousePosition);
        if (hover || highlight) RetroGUI.Fill(r, hover ? Ink : LcdDark);
        RetroGUI.Label(new Rect(r.x + 2, r.y, r.width - 4, r.height), text, hover ? Lcd : Ink, true, true);
        return GUI.Button(RetroGUI.R(r.x, r.y, r.width, r.height), GUIContent.none, GUIStyle.none);
    }

    void DrawHome(Rect c)
    {
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), Loc.T("MENÜ", "MENU"), InkDim, false, true);
        int sms = UnreadSms, news = UnreadNews;
        if (Line(new Rect(c.x, c.y + 14, c.width, 14), Loc.T("Nachrichten", "Messages") + (sms > 0 ? $" ({sms})" : ""), sms > 0)) page = Page.Chats;
        if (Line(new Rect(c.x, c.y + 30, c.width, 14), Loc.T("WAP: Nachrichten", "WAP: News") + (news > 0 ? $" ({news})" : ""), news > 0)) page = Page.News;
        if (Line(new Rect(c.x, c.y + 46, c.width, 14), Loc.T("Telefon", "Call"))) page = Page.Contacts;
    }

    void DrawChats(Rect c)
    {
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), Loc.T("NACHRICHTEN", "MESSAGES"), InkDim, false, true);
        float y = c.y + 14;
        if (chats.Count == 0) RetroGUI.Label(new Rect(c.x, y, c.width, 10), Loc.T("Keine Nachrichten.", "No messages."), InkDim, false, true);
        foreach (var id in chats.Keys.ToList())
        {
            int n = unread.TryGetValue(id, out int u) ? u : 0;
            if (Line(new Rect(c.x, y, c.width, 14), PhoneContent.ContactName(id) + (n > 0 ? $" ({n})" : ""), n > 0))
            {
                chatWith = id;
                unread[id] = 0;
                page = Page.Chat;
            }
            y += 16;
        }
    }

    void DrawChat(Rect c)
    {
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), PhoneContent.ContactName(chatWith), InkDim, false, true);
        unread[chatWith] = 0;
        var list = Chat(chatWith);
        awaitingReply.TryGetValue(chatWith, out var pending);

        // Replies at the bottom, messages above them (newest at the bottom).
        float bottom = c.yMax;
        if (pending != null)
        {
            bottom -= 34;
            for (int i = 0; i < 2; i++)
            {
                string reply = Loc.T(pending.replyDe[i], pending.replyEn[i]);
                if (Line(new Rect(c.x, bottom + i * 17, c.width, 16), "> " + reply))
                {
                    list.Add(new Message { mine = true, text = reply });
                    awaitingReply.Remove(chatWith);
                    string from = chatWith, answer = Loc.T(pending.answerDe[i], pending.answerEn[i]);
                    scheduled.Add((Time.time + Random.Range(6f, 14f), () => Receive(from, answer, null)));
                    break;
                }
            }
        }

        float y = bottom - 2;
        const int charsPerLine = 22;
        for (int i = list.Count - 1; i >= 0 && y > c.y + 14; i--)
        {
            var m = list[i];
            int lines = Mathf.Max(1, Mathf.CeilToInt(m.text.Length / (float)charsPerLine) + 1);
            float hgt = lines * 8.5f + 3;
            y -= hgt;
            if (y < c.y + 12) break;
            var r = new Rect(m.mine ? c.x + 14 : c.x, y, c.width - 14, hgt - 2);
            RetroGUI.Fill(r, m.mine ? LcdDark : new Color(0.62f, 0.7f, 0.52f));
            GUI.Label(RetroGUI.R(r.x + 2, r.y + 1, r.width - 4, r.height), m.text, SmallWrap(m.mine ? Ink : Ink));
        }
    }

    GUIStyle smallWrap;
    float smallWrapScale;
    GUIStyle SmallWrap(Color c)
    {
        if (smallWrap == null || !Mathf.Approximately(smallWrapScale, RetroGUI.Scale))
        {
            smallWrapScale = RetroGUI.Scale;
            smallWrap = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(7f * RetroGUI.Scale), wordWrap = true, clipping = TextClipping.Clip };
            smallWrap.padding = new RectOffset(0, 0, 0, 0);
            smallWrap.margin = new RectOffset(0, 0, 0, 0);
        }
        smallWrap.normal.textColor = smallWrap.hover.textColor = c;
        return smallWrap;
    }

    void DrawNews(Rect c)
    {
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), Loc.T("SCHWARZWALD-INFO (WAP)", "BLACK FOREST INFO (WAP)"), InkDim, false, true);
        float y = c.y + 13;
        // Newest first.
        for (int i = PhoneContent.News.Length - 1; i >= 0 && y < c.yMax - 12; i--)
        {
            var a = PhoneContent.News[i];
            if (a.night > Progress.Day) continue;
            bool isNew = !readArticles.Contains(i);
            if (Line(new Rect(c.x, y, c.width, 24), (isNew ? "* " : "") + Loc.T(a.titleDe, a.titleEn), isNew))
            {
                article = i;
                readArticles.Add(i);
                page = Page.Article;
            }
            y += 26;
        }
    }

    void DrawArticle(Rect c)
    {
        var a = PhoneContent.News[article];
        GUI.Label(RetroGUI.R(c.x, c.y, c.width, 30), Loc.T(a.titleDe, a.titleEn), SmallWrap(Ink));
        RetroGUI.Fill(new Rect(c.x, c.y + 30, c.width, 1), InkDim);
        GUI.Label(RetroGUI.R(c.x, c.y + 34, c.width, c.height - 34), Loc.T(a.textDe, a.textEn), SmallWrap(Ink));
    }

    void DrawContacts(Rect c)
    {
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), Loc.T("TELEFONBUCH", "CONTACTS"), InkDim, false, true);
        string[] ids = { "110", PhoneContent.Anna, PhoneContent.Mia, "dispatch" };
        float y = c.y + 14;
        foreach (var id in ids)
        {
            string name = id == "110" ? Loc.T("Polizei 110", "Police 110") : id == "dispatch" ? Loc.T("Leitstelle", "Dispatch") : PhoneContent.ContactName(id);
            if (Line(new Rect(c.x, y, c.width, 14), name, id == "110" && game != null && game.PendingCard != null && !game.PoliceOnTheWay))
                StartCall(id);
            y += 16;
        }
    }

    void StartCall(string id)
    {
        calling = id;
        callResult = null;
        callStarted = Time.time;
        page = Page.Calling;
        if (ring != null && ringSound != null) { ring.clip = ringSound; ring.Play(); }
    }

    void DrawCalling(Rect c)
    {
        string name = calling == "110" ? Loc.T("Polizei 110", "Police 110") : calling == "dispatch" ? Loc.T("Leitstelle", "Dispatch") : PhoneContent.ContactName(calling);
        RetroGUI.Label(new Rect(c.x, c.y, c.width, 10), Loc.T("ANRUF", "CALLING"), InkDim, false, true);
        RetroGUI.Label(new Rect(c.x, c.y + 14, c.width, 12), name, Ink, true);

        float t = Time.time - callStarted;
        if (callResult == null && t < 2.5f)
        {
            RetroGUI.Label(new Rect(c.x, c.y + 32, c.width, 10), Loc.T("Verbinde", "Connecting") + new string('.', 1 + (int)(t * 2) % 3), InkDim, false, true);
            return;
        }
        if (callResult == null)
        {
            if (ring != null) ring.Stop();
            callResult = CallAnswer(calling);
        }
        GUI.Label(RetroGUI.R(c.x, c.y + 32, c.width, c.height - 32), callResult, SmallWrap(Ink));
    }

    string CallAnswer(string id)
    {
        int night = Progress.Day;
        switch (id)
        {
            case "110":
                return police != null ? police.Call() : Loc.T("Kein Netz.", "No signal.");
            case "dispatch":
                return night >= 6 ? Loc.T("Es klingelt. Und klingelt. Dann atmet jemand in den Hörer.", "It rings. And rings. Then someone breathes into the phone.")
                                  : Loc.T("\"Leitstelle. Fahren Sie weiter.\" - aufgelegt.", "\"Dispatch. Keep driving.\" - hung up.");
            case PhoneContent.Anna:
                return night >= 6 ? Loc.T("\"Kein Anschluss unter dieser Nummer.\"", "\"The number you have dialled is not available.\"")
                                  : Loc.T("\"Hallo? ... Ich hör dich kaum, das Netz ist so schlecht. Komm gut heim.\"", "\"Hello? ... I can barely hear you, the signal is so bad. Get home safe.\"");
            case PhoneContent.Mia:
                return night >= 5 ? Loc.T("Nur Rauschen. Dann ganz leise: \"papa, hinter dir.\"", "Only static. Then very quietly: \"daddy, behind you.\"")
                                  : Loc.T("Mailbox: \"Hier ist Mia! Sprich nach dem Piep!\"", "Voicemail: \"This is Mia! Talk after the beep!\"");
        }
        return "";
    }
}
