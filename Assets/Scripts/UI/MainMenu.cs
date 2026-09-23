using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Start of the game: language choice (flags) -> main menu -> intro newspaper with the
/// job ad -> the night shift begins. The game is paused (timeScale 0) until then.
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainMenu : MonoBehaviour
{
    enum Page { Language, Menu, Intro, Playing }

    [Tooltip("Skip everything and start driving right away (for testing)")]
    public bool skipInEditor;

    Page screen = Page.Language;
    float screenSince;
    GUIStyle big, title, text, small, button;
    float stylesForScale = -1f;

    static readonly Color Bg = new Color(0.02f, 0.02f, 0.03f);
    static readonly Color Paper = new Color(0.86f, 0.83f, 0.74f);
    static readonly Color Ink = new Color(0.12f, 0.11f, 0.1f);
    static readonly Color Faded = new Color(0.35f, 0.33f, 0.3f);
    static readonly Color Stamp = new Color(0.72f, 0.08f, 0.06f, 0.85f);

    void Awake()
    {
        if (skipInEditor && Application.isEditor)
        {
            screen = Page.Playing;
            return;
        }
        GameUI.MenuOpen = true;
        Time.timeScale = 0f;
    }

    void OnDestroy()
    {
        GameUI.MenuOpen = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (screen == Page.Playing) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // The intro can also be skipped with a key.
        var kb = Keyboard.current;
        if (screen == Page.Intro && kb != null && Time.unscaledTime - screenSince > 1f &&
            (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            StartGame();
    }

    void Show(Page s)
    {
        screen = s;
        screenSince = Time.unscaledTime;
    }

    void StartGame()
    {
        Show(Page.Playing);
        GameUI.MenuOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
        if (screen == Page.Playing) return;
        GUI.depth = -500;
        BuildStyles();
        float w = RetroGUI.VirtualWidth;
        RetroGUI.Fill(new Rect(0, 0, w, RetroGUI.VirtualHeight), Bg);

        switch (screen)
        {
            case Page.Language: DrawLanguage(w); break;
            case Page.Menu: DrawMenu(w); break;
            case Page.Intro: DrawIntro(w); break;
        }
    }

    void DrawLanguage(float w)
    {
        Label(new Rect(0, 70, w, 30), "THE PASSENGERS", title, new Color(0.85f, 0.8f, 0.7f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 110, w, 14), "Sprache wählen  /  Choose language", text, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);

        var de = new Rect(w / 2 - 150, 145, 120, 72);
        var en = new Rect(w / 2 + 30, 145, 120, 72);
        if (FlagButton(de, false, "DEUTSCH")) { Loc.English = false; Show(Page.Menu); }
        if (FlagButton(en, true, "ENGLISH")) { Loc.English = true; Show(Page.Menu); }
    }

    bool FlagButton(Rect r, bool usa, string caption)
    {
        bool hover = RetroGUI.R(r.x - 4, r.y - 4, r.width + 8, r.height + 26).Contains(Event.current.mousePosition);
        RetroGUI.Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), hover ? new Color(0.9f, 0.85f, 0.6f) : new Color(0.25f, 0.25f, 0.25f));
        if (usa) DrawUsaFlag(r); else DrawGermanFlag(r);
        Label(new Rect(r.x, r.yMax + 6, r.width, 14), caption, text, hover ? Color.white : new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);
        return GUI.Button(RetroGUI.R(r.x - 4, r.y - 4, r.width + 8, r.height + 26), GUIContent.none, GUIStyle.none);
    }

    static void DrawGermanFlag(Rect r)
    {
        float h = r.height / 3f;
        RetroGUI.Fill(new Rect(r.x, r.y, r.width, h), Color.black);
        RetroGUI.Fill(new Rect(r.x, r.y + h, r.width, h), new Color(0.87f, 0f, 0f));
        RetroGUI.Fill(new Rect(r.x, r.y + 2 * h, r.width, h), new Color(1f, 0.81f, 0f));
    }

    static void DrawUsaFlag(Rect r)
    {
        var red = new Color(0.7f, 0.13f, 0.2f);
        var blue = new Color(0.24f, 0.23f, 0.43f);
        float s = r.height / 13f;
        for (int i = 0; i < 13; i++)
            RetroGUI.Fill(new Rect(r.x, r.y + i * s, r.width, s + 0.05f), i % 2 == 0 ? red : Color.white);
        var canton = new Rect(r.x, r.y, r.width * 0.4f, s * 7f);
        RetroGUI.Fill(canton, blue);
        // Stars as small dots in alternating rows.
        for (int row = 0; row < 9; row++)
        {
            int count = row % 2 == 0 ? 6 : 5;
            float dx = canton.width / 6f, dy = canton.height / 9f;
            for (int col = 0; col < count; col++)
            {
                float x = canton.x + dx * (col + (row % 2 == 0 ? 0.5f : 1f));
                float y = canton.y + dy * (row + 0.5f);
                RetroGUI.Fill(new Rect(x - 0.6f, y - 0.6f, 1.2f, 1.2f), Color.white);
            }
        }
    }

    void DrawMenu(float w)
    {
        Label(new Rect(0, 60, w, 30), "THE PASSENGERS", title, new Color(0.85f, 0.8f, 0.7f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 92, w, 14), Loc.T("Nachtlinie 13", "Night Line 13"), text, new Color(0.6f, 0.2f, 0.15f), TextAnchor.MiddleCenter);

        float bx = w / 2 - 80, y = 140;
        if (MenuButton(new Rect(bx, y, 160, 22), Loc.T("SCHICHT BEGINNEN", "START SHIFT"))) Show(Page.Intro);
        if (MenuButton(new Rect(bx, y + 30, 160, 22), Loc.T("SPRACHE", "LANGUAGE"))) Show(Page.Language);
        if (MenuButton(new Rect(bx, y + 60, 160, 22), Loc.T("BEENDEN", "QUIT"))) Application.Quit();

        Label(new Rect(0, 300, w, 40), Loc.T(
            "WASD fahren  -  F Türen  -  L Licht  -  Maus umsehen, Mausrad Zoom  -  E Ausweis / Aussteigen  -  T Ansprechen  -  J / N Entscheiden",
            "WASD drive  -  F doors  -  L lights  -  Mouse look, wheel zoom  -  E ID card / get out  -  T talk  -  J / N decide"),
            small, new Color(0.45f, 0.45f, 0.45f), TextAnchor.UpperCenter);
    }

    bool MenuButton(Rect r, string caption)
    {
        bool hover = RetroGUI.R(r.x, r.y, r.width, r.height).Contains(Event.current.mousePosition);
        RetroGUI.Frame(r, hover ? new Color(0.25f, 0.06f, 0.05f) : new Color(0.08f, 0.08f, 0.08f), hover ? new Color(0.8f, 0.3f, 0.2f) : new Color(0.3f, 0.3f, 0.3f));
        Label(r, caption, button, hover ? Color.white : new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
        return GUI.Button(RetroGUI.R(r.x, r.y, r.width, r.height), GUIContent.none, GUIStyle.none);
    }

    // FNAF-style newspaper with the job ad that got you here.
    void DrawIntro(float w)
    {
        float t = Time.unscaledTime - screenSince;
        var page = new Rect(w / 2 - 190, 14, 380, 318);
        RetroGUI.Fill(new Rect(page.x + 4, page.y + 4, page.width, page.height), new Color(0f, 0f, 0f, 0.6f));
        RetroGUI.Fill(page, Paper);

        float x = page.x + 12, pw = page.width - 24;
        Label(new Rect(x, page.y + 8, pw, 26), Loc.T("SCHWARZWÄLDER BOTE", "BLACK FOREST HERALD"), big, Ink, TextAnchor.MiddleCenter);
        RetroGUI.Fill(new Rect(x, page.y + 36, pw, 1), Ink);
        Label(new Rect(x, page.y + 38, pw, 10), Loc.T("Freitag, 13. November 1998", "Friday, November 13, 1998"), small, Faded, TextAnchor.MiddleLeft);
        Label(new Rect(x, page.y + 38, pw, 10), Loc.T("Preis: 1,20 DM", "Price: 1.20 DM"), small, Faded, TextAnchor.MiddleRight);
        RetroGUI.Fill(new Rect(x, page.y + 49, pw, 1), Ink);

        // Left column: the article.
        float colW = pw * 0.56f;
        Label(new Rect(x, page.y + 54, colW, 30), Loc.T("Nachtbus-Fahrer weiterhin vermisst", "Night bus driver still missing"), title, Ink, TextAnchor.UpperLeft, true);
        Label(new Rect(x, page.y + 90, colW, 200), Loc.T(
            "Seit Dienstag fehlt von dem Fahrer der Nachtlinie 13 jede Spur. Sein Bus wurde am frühen Morgen an der Haltestelle " +
            "Waldfriedhof gefunden - Türen offen, Scheinwerfer an, Motor aus.\n\n" +
            "Es ist bereits der vierte Fahrer der Linie, der in diesem Jahr seinen Dienst nicht beendet. Die Verkehrsbetriebe " +
            "sprechen von \"persönlichen Gründen\". Die Polizei bittet Zeugen, sich zu melden.\n\n" +
            "Anwohner berichten von Gestalten am Straßenrand.",
            "Since Tuesday there has been no trace of the driver of night line 13. His bus was found early in the morning at the " +
            "Forest Cemetery stop - doors open, headlights on, engine off.\n\n" +
            "He is already the fourth driver of the line this year who did not finish his shift. The transport company speaks of " +
            "\"personal reasons\". The police are asking witnesses to come forward.\n\n" +
            "Residents report figures standing at the roadside."),
            small, Ink, TextAnchor.UpperLeft, true);

        // Right column: the help wanted ad.
        var ad = new Rect(x + colW + 10, page.y + 58, pw - colW - 10, 196);
        RetroGUI.Frame(ad, Paper, Ink, 1.5f);
        Label(new Rect(ad.x + 4, ad.y + 6, ad.width - 8, 24), Loc.T("FAHRER GESUCHT", "HELP WANTED"), big, Ink, TextAnchor.MiddleCenter);
        Label(new Rect(ad.x + 6, ad.y + 34, ad.width - 12, 158), Loc.T(
            "Verkehrsbetriebe Schwarzwald suchen ab SOFORT Busfahrer/in für die Nachtlinie 13.\n\n" +
            "Dienst: 23:40 - 06:00 Uhr\nKeine Erfahrung nötig.\nGute Bezahlung.\n\n" +
            "Aufgaben: Fahren, Fahrgäste kontrollieren, Anweisungen der Leitstelle befolgen.\n\nNicht aussteigen.",
            "Black Forest Transport is looking for a bus driver for night line 13, starting IMMEDIATELY.\n\n" +
            "Shift: 11:40 PM - 6:00 AM\nNo experience needed.\nGood pay.\n\n" +
            "Duties: drive, check passengers, follow the control centre's instructions.\n\nDo not leave the bus."),
            small, Ink, TextAnchor.UpperLeft, true);

        // Red stamp over the ad after a moment.
        if (t > 1.6f)
        {
            var m = GUI.matrix;
            var centre = RetroGUI.R(ad.center.x, ad.y + 150, 0, 0).position;
            GUIUtility.RotateAroundPivot(-14f, centre);
            var stamp = new Rect(ad.center.x - 55, ad.y + 136, 110, 28);
            RetroGUI.Fill(new Rect(stamp.x, stamp.y, stamp.width, 2), Stamp);
            RetroGUI.Fill(new Rect(stamp.x, stamp.yMax - 2, stamp.width, 2), Stamp);
            RetroGUI.Fill(new Rect(stamp.x, stamp.y, 2, stamp.height), Stamp);
            RetroGUI.Fill(new Rect(stamp.xMax - 2, stamp.y, 2, stamp.height), Stamp);
            Label(stamp, Loc.T("EINGESTELLT", "HIRED"), big, Stamp, TextAnchor.MiddleCenter);
            GUI.matrix = m;
        }

        Label(new Rect(x + colW + 10, page.y + 262, pw - colW - 10, 50), Loc.T(
            "Ihre erste Schicht: heute Nacht.\nMelden Sie sich um 23:40 im Bus.",
            "Your first shift: tonight.\nReport to the bus at 11:40 PM."), small, Faded, TextAnchor.UpperLeft, true);

        if (t > 2.2f)
        {
            bool blink = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            Label(new Rect(0, page.yMax + 6, w, 14), Loc.T("Klicken zum Fortfahren", "Click to continue"), text,
                blink ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
            if (Event.current.type == EventType.MouseDown) { StartGame(); Event.current.Use(); }
        }
    }

    // ------------------------------------------------------------------ helpers

    static void Label(Rect v, string s, GUIStyle style, Color color, TextAnchor anchor, bool wrap = false)
    {
        style.normal.textColor = color;
        style.alignment = anchor;
        style.wordWrap = wrap;
        GUI.Label(RetroGUI.R(v.x, v.y, v.width, v.height), s, style);
    }

    void BuildStyles()
    {
        float s = RetroGUI.Scale;
        if (Mathf.Approximately(s, stylesForScale) && big != null) return;
        stylesForScale = s;
        var baseStyle = new GUIStyle(GUI.skin.label) { clipping = TextClipping.Clip, richText = false };
        big = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold };
        title = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(14 * s), fontStyle = FontStyle.Bold };
        text = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(9 * s) };
        small = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(7.5f * s) };
        button = new GUIStyle(baseStyle) { fontSize = Mathf.RoundToInt(9 * s), fontStyle = FontStyle.Bold };
    }
}
