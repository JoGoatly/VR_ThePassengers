using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Start of the game: language choice (flags) -> main menu (settings: volume and key
/// bindings) -> intro newspaper with the job ad -> the night shift begins.
/// During the game Esc opens the pause menu. The game is paused (timeScale 0) while a menu is open.
/// </summary>
[DefaultExecutionOrder(-500)]
public class MainMenu : MonoBehaviour
{
    enum Page { Language, Menu, Settings, Keys, Intro, Playing, Paused, DayTitle, ShiftEnd, Ending, GameOver }

    [Tooltip("Skip everything and start driving right away (for testing)")]
    public bool skipInEditor;
    public AudioClip menuMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;

    static bool languageChosen;
    static bool showDayTitleOnLoad;   // set when the next night starts after a shift
    static bool showIntroOnLoad;      // set when the driving test is passed: newspaper, then night 1
    static bool playOnLoad;           // the driving test scene starts right away

    public const string MainScene = "SampleScene";
    public const string TutorialScene = "Tutorial";
    int lastWage;
    bool goodEnding;

    Page screen = Page.Language;
    Page settingsReturn = Page.Menu;
    float screenSince;
    GUIStyle big, title, text, small, button;
    float stylesForScale = -1f;
    AudioSource music;
    float musicLevel;          // 0..1 fade
    GameAction? rebinding;

    static readonly Color Bg = new Color(0.02f, 0.02f, 0.03f);
    static readonly Color Paper = new Color(0.86f, 0.83f, 0.74f);
    static readonly Color Ink = new Color(0.12f, 0.11f, 0.1f);
    static readonly Color Faded = new Color(0.35f, 0.33f, 0.3f);
    static readonly Color Stamp = new Color(0.72f, 0.08f, 0.06f, 0.85f);
    static readonly Color TitleCol = new Color(0.85f, 0.8f, 0.7f);
    static readonly Color Grey = new Color(0.6f, 0.6f, 0.6f);

    bool InMenu => screen != Page.Playing;

    void Awake()
    {
        GameSettings.Apply();
        var ost = Resources.Load<AudioClip>("Music/menu");
        if (ost != null) menuMusic = ost;
        music = gameObject.AddComponent<AudioSource>();
        music.clip = menuMusic;
        music.loop = true;
        music.spatialBlend = 0f;
        music.ignoreListenerPause = true;
        music.playOnAwake = false;
        music.volume = 0f;
        if (menuMusic != null) music.Play();

        if (playOnLoad || (skipInEditor && Application.isEditor))
        {
            playOnLoad = false;
            screen = Page.Playing;
            SetPaused(false);
            return;
        }
        Show(showIntroOnLoad ? Page.Intro : showDayTitleOnLoad ? Page.DayTitle : languageChosen ? Page.Menu : Page.Language);
        showDayTitleOnLoad = false;
        showIntroOnLoad = false;
        SetPaused(true);
    }

    void OnDestroy()
    {
        GameUI.MenuOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    static void SetPaused(bool paused)
    {
        GameUI.MenuOpen = paused;
        Time.timeScale = paused ? 0f : 1f;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    void Update()
    {
        // Menu music: full in the start menu, quieter in the pause menu, off while driving.
        float target = screen == Page.Playing ? 0f : settingsReturn == Page.Paused && screen != Page.Menu && screen != Page.Language && screen != Page.Intro ? 0.45f : 1f;
        musicLevel = Mathf.MoveTowards(musicLevel, target, Time.unscaledDeltaTime * (target > musicLevel ? 0.5f : 0.8f));
        if (music != null) music.volume = musicLevel * musicVolume * GameSettings.Music;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (screen == Page.Playing)
        {
            // Esc: pause (not while typing on the computer or choosing a question).
            if (kb.escapeKey.wasPressedThisFrame && !GameUI.TerminalTyping && !GameUI.DialogueOpen && !GameUI.NoteOpen && !GameUI.PhoneOpen && !GameUI.PcOpen && !GameUI.MinigameOpen && !GameUI.JustClosed)
            {
                settingsReturn = Page.Paused;
                Show(Page.Paused);
                SetPaused(true);
                AudioListener.pause = true;
            }
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (rebinding.HasValue)
        {
            if (kb.escapeKey.wasPressedThisFrame) { rebinding = null; return; }
            foreach (var key in kb.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame) continue;
                GameKeys.Set(rebinding.Value, key.keyCode);
                rebinding = null;
                break;
            }
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            switch (screen)
            {
                case Page.Paused: Resume(); break;
                case Page.Settings: Show(settingsReturn); break;
                case Page.Keys: Show(Page.Settings); break;
                case Page.Intro: if (Time.unscaledTime - screenSince > 1f) StartGame(); break;
            }
            return;
        }

        // The intro and the night title can also be skipped with a key.
        if ((screen == Page.Intro || screen == Page.DayTitle) && Time.unscaledTime - screenSince > 1f &&
            (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            StartGame();
    }

    void Show(Page s)
    {
        screen = s;
        screenSince = Time.unscaledTime;
    }

    void StartGame()
    {
        // Night 1 without the driving test: the test (own scene) comes first.
        if (Progress.Day == 1 && !Progress.Data.tutorialDone && SceneManager.GetActiveScene().name != TutorialScene)
        {
            playOnLoad = true;
            LoadScene(TutorialScene);
            return;
        }
        Show(Page.Playing);
        SetPaused(false);
    }

    void Resume()
    {
        Show(Page.Playing);
        SetPaused(false);
        AudioListener.pause = false;
    }

    /// <summary>The shift is over: show what was earned, then the next night.</summary>
    public void ShowShiftEnd(int wage)
    {
        lastWage = wage;
        settingsReturn = Page.Menu;
        Show(Page.ShiftEnd);
        SetPaused(true);
        AudioListener.pause = true;
    }

    public void ShowEnding(bool good)
    {
        goodEnding = good;
        settingsReturn = Page.Menu;
        Show(Page.Ending);
        SetPaused(true);
        AudioListener.pause = true;
    }

    string gameOverTitle, gameOverText;
    AudioSource gameOverMusic;

    /// <summary>Game over (fired, fell asleep...): the night can be played again.</summary>
    public void ShowGameOver(string title, string text)
    {
        if (screen == Page.GameOver) return;
        gameOverTitle = title;
        gameOverText = text;
        settingsReturn = Page.Menu;
        Show(Page.GameOver);
        SetPaused(true);
        AudioListener.pause = true;
        var clip = Resources.Load<AudioClip>("Music/gameover");
        if (clip != null)
        {
            gameOverMusic = new GameObject("Game Over Music").AddComponent<AudioSource>();
            gameOverMusic.clip = clip;
            gameOverMusic.loop = true;
            gameOverMusic.ignoreListenerPause = true;
            gameOverMusic.volume = 0.6f * GameSettings.Music;
            gameOverMusic.Play();
        }
    }

    void DrawGameOver(float w)
    {
        float t = Time.unscaledTime - screenSince;
        float a = Mathf.Clamp01(t / 1.5f);
        RetroGUI.Fill(new Rect(0, 0, w, RetroGUI.VirtualHeight), new Color(0f, 0f, 0f, 0.85f * a));
        Label(new Rect(0, 60, w, 34), gameOverTitle, big, new Color(0.8f, 0.08f, 0.05f, a), TextAnchor.MiddleCenter);
        Label(new Rect(w / 2 - 200, 110, 400, 120), gameOverText, text, new Color(0.8f, 0.78f, 0.72f, a), TextAnchor.UpperCenter, true);
        if (t < 2f) return;
        if (MenuButton(new Rect(w / 2 - 80, 250, 160, 22), Loc.T("NACHT WIEDERHOLEN", "RETRY THE NIGHT"))) NextNight();
        if (MenuButton(new Rect(w / 2 - 80, 278, 160, 22), Loc.T("HAUPTMENÜ", "MAIN MENU"))) BackToMainMenu();
    }

    /// <summary>The driving test is passed: on to the first night.</summary>
    public void AfterTutorial()
    {
        showIntroOnLoad = true;
        BackToMainMenu();
    }

    void NextNight()
    {
        showDayTitleOnLoad = true;
        BackToMainMenu();
    }

    void BackToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameUI.MenuOpen = false;
        GameUI.DialogueOpen = false;
        GameUI.TerminalTyping = false;
        LoadScene(MainScene);
    }

    static void LoadScene(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameUI.MenuOpen = false;
        GameUI.InBus = false;
        GameUI.PlayerOutside = false;
        SceneManager.LoadScene(scene);
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
        if (screen == Page.Playing) return;
        GUI.depth = -500;
        BuildStyles();
        float w = RetroGUI.VirtualWidth;
        // The pause menu shows the (frozen) game dimmed behind it.
        bool overGame = settingsReturn == Page.Paused && (screen == Page.Paused || screen == Page.Settings || screen == Page.Keys);
        RetroGUI.Fill(new Rect(0, 0, w, RetroGUI.VirtualHeight), overGame ? new Color(0f, 0f, 0f, 0.78f) : Bg);

        switch (screen)
        {
            case Page.Language: DrawLanguage(w); break;
            case Page.Menu: DrawMenu(w); break;
            case Page.Settings: DrawSettings(w); break;
            case Page.Keys: DrawKeys(w); break;
            case Page.Intro: DrawIntro(w); break;
            case Page.Paused: DrawPause(w); break;
            case Page.DayTitle: DrawDayTitle(w); break;
            case Page.ShiftEnd: DrawShiftEnd(w); break;
            case Page.Ending: DrawEnding(w); break;
            case Page.GameOver: DrawGameOver(w); break;
        }
    }

    void DrawLanguage(float w)
    {
        Label(new Rect(0, 70, w, 30), "THE PASSENGERS", title, TitleCol, TextAnchor.MiddleCenter);
        Label(new Rect(0, 110, w, 14), "Sprache wählen  /  Choose language", text, Grey, TextAnchor.MiddleCenter);

        var de = new Rect(w / 2 - 150, 145, 120, 72);
        var en = new Rect(w / 2 + 30, 145, 120, 72);
        if (FlagButton(de, false, "DEUTSCH")) { Loc.English = false; languageChosen = true; Show(Page.Menu); }
        if (FlagButton(en, true, "ENGLISH")) { Loc.English = true; languageChosen = true; Show(Page.Menu); }
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
        Label(new Rect(0, 60, w, 30), "THE PASSENGERS", title, TitleCol, TextAnchor.MiddleCenter);
        Label(new Rect(0, 92, w, 14), Loc.T("Nachtlinie 13", "Night Line 13"), text, new Color(0.6f, 0.2f, 0.15f), TextAnchor.MiddleCenter);

        float bx = w / 2 - 80, y = 124;
        if (Progress.HasSave && Progress.Data.day > 1)
        {
            if (MenuButton(new Rect(bx, y, 160, 22), Loc.T($"WEITER (NACHT {Progress.Day})", $"CONTINUE (NIGHT {Progress.Day})"))) Show(Page.DayTitle);
            y += 30;
        }
        if (MenuButton(new Rect(bx, y, 160, 22), Loc.T("NEUES SPIEL", "NEW GAME"))) { Progress.NewGame(); StartGame(); }   // the driving test first, the newspaper after it
        if (MenuButton(new Rect(bx, y + 30, 160, 22), Loc.T("EINSTELLUNGEN", "SETTINGS"))) { settingsReturn = Page.Menu; Show(Page.Settings); }
        if (MenuButton(new Rect(bx, y + 60, 160, 22), Loc.T("SPRACHE", "LANGUAGE"))) Show(Page.Language);
        if (MenuButton(new Rect(bx, y + 90, 160, 22), Loc.T("BEENDEN", "QUIT"))) Application.Quit();
    }

    void DrawPause(float w)
    {
        Label(new Rect(0, 70, w, 30), Loc.T("PAUSE", "PAUSED"), title, TitleCol, TextAnchor.MiddleCenter);
        float bx = w / 2 - 80, y = 120;
        if (MenuButton(new Rect(bx, y, 160, 22), Loc.T("WEITER", "RESUME"))) Resume();
        if (MenuButton(new Rect(bx, y + 30, 160, 22), Loc.T("EINSTELLUNGEN", "SETTINGS"))) { settingsReturn = Page.Paused; Show(Page.Settings); }
        if (MenuButton(new Rect(bx, y + 60, 160, 22), Loc.T("HAUPTMENÜ", "MAIN MENU"))) BackToMainMenu();
        if (MenuButton(new Rect(bx, y + 90, 160, 22), Loc.T("BEENDEN", "QUIT"))) Application.Quit();
    }

    void DrawSettings(float w)
    {
        Label(new Rect(0, 50, w, 30), Loc.T("EINSTELLUNGEN", "SETTINGS"), title, TitleCol, TextAnchor.MiddleCenter);
        float x = w / 2 - 140, y = 100;
        GameSettings.Master = Slider(new Rect(x, y, 280, 20), Loc.T("Gesamtlautstärke", "Master volume"), GameSettings.Master);
        GameSettings.Music = Slider(new Rect(x, y + 30, 280, 20), Loc.T("Musik & Radio", "Music & radio"), GameSettings.Music);
        GameSettings.Effects = Slider(new Rect(x, y + 60, 280, 20), Loc.T("Effekte", "Effects"), GameSettings.Effects);

        float bx = w / 2 - 80;
        if (MenuButton(new Rect(bx, y + 105, 160, 22), Loc.T("TASTENBELEGUNG", "KEY BINDINGS"))) Show(Page.Keys);
        if (MenuButton(new Rect(bx, y + 135, 160, 22), Loc.T("ZURÜCK", "BACK"))) { PlayerPrefs.Save(); Show(settingsReturn); }
    }

    float Slider(Rect r, string caption, float value)
    {
        Label(new Rect(r.x, r.y, 120, r.height), caption, text, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
        var bar = new Rect(r.x + 130, r.y + r.height / 2 - 3, r.width - 170, 6);
        RetroGUI.Fill(bar, new Color(0.15f, 0.15f, 0.15f));
        RetroGUI.Fill(new Rect(bar.x, bar.y, bar.width * value, bar.height), new Color(0.7f, 0.25f, 0.18f));
        RetroGUI.Fill(new Rect(bar.x + bar.width * value - 2, bar.y - 4, 4, bar.height + 8), new Color(0.9f, 0.85f, 0.75f));
        Label(new Rect(bar.xMax + 6, r.y, 40, r.height), Mathf.RoundToInt(value * 100) + "%", text, Grey, TextAnchor.MiddleLeft);

        // Click or drag on the bar.
        var e = Event.current;
        var hit = RetroGUI.R(bar.x - 4, r.y, bar.width + 8, r.height);
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition))
        {
            var screenBar = RetroGUI.R(bar.x, bar.y, bar.width, bar.height);
            value = Mathf.Clamp01((e.mousePosition.x - screenBar.x) / screenBar.width);
            e.Use();
        }
        return value;
    }

    void DrawKeys(float w)
    {
        Label(new Rect(0, 24, w, 24), Loc.T("TASTENBELEGUNG", "KEY BINDINGS"), title, TitleCol, TextAnchor.MiddleCenter);
        float x = w / 2 - 150, y = 56;
        var actions = GameKeys.All.ToList();
        foreach (var a in actions)
        {
            // Doors and "next dialogue line" may share a key on purpose.
            bool clash = actions.Any(o => o != a && GameKeys.Get(o) == GameKeys.Get(a) &&
                !((a == GameAction.Doors && o == GameAction.Continue) || (a == GameAction.Continue && o == GameAction.Doors)));
            Label(new Rect(x, y, 190, 14), GameKeys.Label(a), text, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
            string caption = rebinding == a ? Loc.T("Taste drücken...", "Press a key...") : GameKeys.Name(a);
            var r = new Rect(x + 200, y, 100, 13);
            bool hover = RetroGUI.R(r.x, r.y, r.width, r.height).Contains(Event.current.mousePosition);
            RetroGUI.Frame(r, rebinding == a ? new Color(0.35f, 0.1f, 0.06f) : hover ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.1f, 0.1f, 0.1f),
                clash ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.35f, 0.35f, 0.35f));
            Label(r, caption, small, clash ? new Color(1f, 0.5f, 0.4f) : Color.white, TextAnchor.MiddleCenter);
            if (!rebinding.HasValue && GUI.Button(RetroGUI.R(r.x, r.y, r.width, r.height), GUIContent.none, GUIStyle.none)) rebinding = a;
            y += 15;
        }
        Label(new Rect(0, y + 2, w, 12), Loc.T("Klicken und neue Taste drücken  -  Esc bricht ab", "Click and press a new key  -  Esc cancels"),
            small, Grey, TextAnchor.MiddleCenter);
        float bx = w / 2 - 165;
        if (MenuButton(new Rect(bx, y + 18, 160, 20), Loc.T("STANDARD", "DEFAULTS"))) { rebinding = null; GameKeys.ResetAll(); }
        if (MenuButton(new Rect(bx + 170, y + 18, 160, 20), Loc.T("ZURÜCK", "BACK"))) { rebinding = null; Show(Page.Settings); }
    }

    // "NIGHT 3" title card before a night.
    void DrawDayTitle(float w)
    {
        float t = Time.unscaledTime - screenSince;
        float a = Mathf.Clamp01(t / 1.2f);
        Label(new Rect(0, 110, w, 40), Loc.T($"NACHT {Progress.Day}", $"NIGHT {Progress.Day}"), big, new Color(0.75f, 0.1f, 0.08f, a), TextAnchor.MiddleCenter);
        Label(new Rect(w / 2 - 200, 160, 400, 60), Story.DayIntro(Progress.Day), text, new Color(0.75f, 0.72f, 0.65f, a), TextAnchor.UpperCenter, true);
        Label(new Rect(0, 230, w, 14), Loc.T($"{DayManager.QuotaFor(Progress.Day)} Fahrgäste  -  Konto: {Progress.Money} €",
            $"{DayManager.QuotaFor(Progress.Day)} passengers  -  account: {Progress.Money} €"), small, Grey, TextAnchor.MiddleCenter);
        if (t > 1.5f)
        {
            bool blink = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            Label(new Rect(0, 300, w, 14), Loc.T("Klicken zum Starten", "Click to start"), text, blink ? new Color(0.8f, 0.8f, 0.8f) : Grey, TextAnchor.MiddleCenter);
            if (Event.current.type == EventType.MouseDown) { StartGame(); Event.current.Use(); }
        }
    }

    void DrawShiftEnd(float w)
    {
        var page = new Rect(w / 2 - 150, 40, 300, 250);
        RetroGUI.Fill(page, Paper);
        float x = page.x + 16, y = page.y + 12, cw = page.width - 32;
        int night = Progress.Day - 1;
        Label(new Rect(x, y, cw, 24), Loc.T($"NACHT {night} BEENDET", $"NIGHT {night} COMPLETE"), title, Ink, TextAnchor.MiddleCenter);
        RetroGUI.Fill(new Rect(x, y + 28, cw, 1), Ink);
        y += 38;
        void Row(string label, string value)
        {
            Label(new Rect(x, y, cw, 14), label, text, Ink, TextAnchor.MiddleLeft);
            Label(new Rect(x, y, cw, 14), value, text, Ink, TextAnchor.MiddleRight);
            y += 17;
        }
        Row(Loc.T("Richtige Entscheidungen", "Correct decisions"), $"{Progress.ShiftCorrect}   +{Progress.ShiftEarned} €");
        Row(Loc.T("Fehler", "Mistakes"), $"{Progress.ShiftWrong}   -{Progress.ShiftFines} €");
        Row(Loc.T("Grundlohn", "Base wage"), $"+{lastWage} €");
        if (Progress.ShiftFound > 0) Row(Loc.T("Gefunden", "Found"), $"+{Progress.ShiftFound} €");
        RetroGUI.Fill(new Rect(x, y + 2, cw, 1), Ink);
        y += 8;
        Row(Loc.T("Kontostand", "Balance"), $"{Progress.Money} €");
        Row(Loc.T("Vermisste Fahrer gefunden", "Missing drivers found"), $"{Progress.Data.drivers.Count} / {Story.Drivers.Length}");

        float bx = w / 2 - 80;
        if (MenuButton(new Rect(bx, page.yMax + 12, 160, 22), Loc.T("NÄCHSTE NACHT", "NEXT NIGHT"))) NextNight();
        if (MenuButton(new Rect(bx, page.yMax + 40, 160, 22), Loc.T("HAUPTMENÜ", "MAIN MENU"))) BackToMainMenu();
    }

    void DrawEnding(float w)
    {
        float t = Time.unscaledTime - screenSince;
        float a = Mathf.Clamp01(t / 2f);
        Label(new Rect(0, 40, w, 30), goodEnding ? Loc.T("ENDE", "THE END") : Loc.T("ENDE?", "THE END?"), big,
            new Color(goodEnding ? 0.8f : 0.75f, goodEnding ? 0.75f : 0.1f, goodEnding ? 0.6f : 0.08f, a), TextAnchor.MiddleCenter);
        Label(new Rect(w / 2 - 210, 85, 420, 200), Story.Ending(goodEnding), text, new Color(0.8f, 0.78f, 0.72f, a), TextAnchor.UpperLeft, true);
        if (t > 3f && MenuButton(new Rect(w / 2 - 80, 300, 160, 22), Loc.T("HAUPTMENÜ", "MAIN MENU"))) BackToMainMenu();
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
        Label(new Rect(ad.x + 2, ad.y + 6, ad.width - 4, 24), Loc.T("FAHRER GESUCHT", "HELP WANTED"), title, Ink, TextAnchor.MiddleCenter);
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
            var stamp = new Rect(ad.center.x - 64, ad.y + 136, 128, 28);
            RetroGUI.Fill(new Rect(stamp.x, stamp.y, stamp.width, 2), Stamp);
            RetroGUI.Fill(new Rect(stamp.x, stamp.yMax - 2, stamp.width, 2), Stamp);
            RetroGUI.Fill(new Rect(stamp.x, stamp.y, 2, stamp.height), Stamp);
            RetroGUI.Fill(new Rect(stamp.xMax - 2, stamp.y, 2, stamp.height), Stamp);
            Label(stamp, Loc.T("EINGESTELLT", "HIRED"), title, Stamp, TextAnchor.MiddleCenter);
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
        // Same colour in every state, so text does not light up under the mouse.
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = color;
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
