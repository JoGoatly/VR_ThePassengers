using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The on-board computer as a real screen in the cockpit. Aim at it with the crosshair,
/// a mouse pointer appears on the screen, left click uses it. Apps: REGISTER (resident
/// search), POSTFACH (mails) and KONTROLLE (let the passenger in / turn them away).
/// Click the search field to type; while typing, driving keys are ignored (Esc/Enter ends typing).
/// </summary>
public class ComputerTerminal : MonoBehaviour
{
    public BoardingManager game;

    [Header("Monitor (bus space)")]
    public Vector3 monitorPosition = new Vector3(-0.33f, 1.4f, 4.1f);
    [Tooltip("Where the driver's eyes roughly are, the screen is turned towards it")]
    public Vector3 viewerPosition = new Vector3(-0.83f, 1.72f, 4.3f);
    public Vector2 screenSize = new Vector2(0.46f, 0.345f);
    public Material caseMaterial;
    [Tooltip("Unlit PSXLit material, its texture is replaced by the screen image")]
    public Material screenMaterial;
    public int resolutionX = 320, resolutionY = 240;
    public float maxUseDistance = 2.2f;

    public event System.Action Clicked, Typed, ErrorBeep;

    enum App { None, Register, Mail, Control }
    App app = App.None;

    PixelCanvas canvas;
    Transform screen;
    Camera cam;

    // Pointer state for this frame.
    Vector2Int pointer;
    bool hover, click;

    // Register.
    string query = "";
    bool typing, searched;
    bool allSelected;          // Ctrl+A
    float backspaceHeld;       // for key repeat
    List<Citizen> results = new List<Citizen>();
    Citizen selected;
    int resultScroll;

    // Mail.
    Mail openMail;
    int mailScroll, bodyScroll;

    float blink;

    static readonly Color32 Bg = new Color32(6, 14, 9, 255);
    static readonly Color32 Panel = new Color32(12, 30, 18, 255);
    static readonly Color32 Line = new Color32(40, 110, 60, 255);
    static readonly Color32 Text = new Color32(150, 255, 170, 255);
    static readonly Color32 Dim = new Color32(70, 140, 90, 255);
    static readonly Color32 Hi = new Color32(35, 95, 52, 255);
    static readonly Color32 White = new Color32(230, 255, 235, 255);
    static readonly Color32 Alert = new Color32(255, 90, 80, 255);
    static readonly Color32 Warn = new Color32(255, 200, 80, 255);
    static readonly Color32 Green = new Color32(30, 110, 45, 255);
    static readonly Color32 Red = new Color32(120, 25, 20, 255);

    const int TopBar = 12, TaskBar = 13;

    public bool IsHovered => hover;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        canvas = new PixelCanvas(resolutionX, resolutionY);
        BuildMonitor();
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDestroy()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        GameUI.TerminalTyping = false;
    }

    void BuildMonitor()
    {
        if (game == null || game.bus == null) return;
        Transform bus = game.bus.transform;
        var root = new GameObject("Board Computer").transform;
        root.SetParent(bus, false);
        root.localPosition = monitorPosition;
        root.localRotation = Quaternion.LookRotation(monitorPosition - viewerPosition);

        float w = screenSize.x, h = screenSize.y;
        var caseGo = MeshKit.Spawn("Case", root, MeshKit.Box(new Vector3(w + 0.06f, h + 0.06f, 0.24f), 0.5f), caseMaterial,
                                   root.position, root.rotation, false);
        caseGo.transform.localPosition = new Vector3(0f, -(h + 0.06f) * 0.5f, 0f);
        var arm = MeshKit.Spawn("Arm", root, MeshKit.Box(new Vector3(0.05f, 0.35f, 0.05f), 0.5f), caseMaterial, root.position, root.rotation, false);
        arm.transform.localPosition = new Vector3(0f, -h * 0.5f - 0.38f, 0.05f);

        // Screen quad with UVs 0..1, facing the driver (-Z).
        var mb = new MeshKit.Builder();
        float z = -0.121f;
        mb.Quad(new Vector3(-w / 2, -h / 2, z), new Vector3(-w / 2, h / 2, z), new Vector3(w / 2, h / 2, z), new Vector3(w / 2, -h / 2, z),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var mat = screenMaterial != null ? new Material(screenMaterial) : null;
        if (mat != null) mat.SetTexture("_MainTex", canvas.Texture);
        var screenGo = MeshKit.Spawn("Screen", root, mb.ToMesh("Screen"), mat, root.position, root.rotation, false);
        screen = screenGo.transform;

        // Faint green glow on the driver's face.
        var glow = new GameObject("Screen Glow").AddComponent<Light>();
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        glow.type = LightType.Point;
        glow.color = new Color(0.4f, 1f, 0.55f);
        glow.range = 1.4f;
        glow.intensity = 0.45f;
        glow.shadows = LightShadows.None;
    }

    // Crosshair in the middle of the view (hidden while the pointer is on the screen).
    void OnGUI()
    {
        if (hover || Event.current.type != EventType.Repaint) return;
        float s = Mathf.Max(2f, Mathf.Round(RetroGUI.Scale * 1.5f));
        var r = new Rect(Mathf.Round(Screen.width * 0.5f - s * 0.5f), Mathf.Round(Screen.height * 0.5f - s * 0.5f), s, s);
        GUI.DrawTexture(r, RetroGUI.Tex(new Color(1f, 1f, 1f, 0.55f)));
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (canvas == null || screen == null) return;
        if (cam == null) cam = Camera.main;
        blink += Time.deltaTime;

        UpdatePointer();
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        click = hover && mouse != null && mouse.leftButton.wasPressedThisFrame && !GameUI.DialogueOpen && !GameUI.MenuOpen;

        if (typing && kb != null)
        {
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.aKey.wasPressedThisFrame && query.Length > 0) allSelected = true;

            // Backspace / Delete: once on press, then repeating while held.
            bool erase = kb.backspaceKey.isPressed || kb.deleteKey.isPressed;
            if (erase)
            {
                bool first = backspaceHeld == 0f;
                backspaceHeld += Time.deltaTime;
                const float delay = 0.4f, interval = 0.045f;
                int repeatsBefore = backspaceHeld - Time.deltaTime < delay ? 0 : Mathf.FloorToInt((backspaceHeld - Time.deltaTime - delay) / interval) + 1;
                int repeatsNow = backspaceHeld < delay ? 0 : Mathf.FloorToInt((backspaceHeld - delay) / interval) + 1;
                int count = (first ? 1 : 0) + Mathf.Max(0, repeatsNow - repeatsBefore);
                for (int i = 0; i < count && query.Length > 0; i++)
                {
                    if (allSelected) { query = ""; allSelected = false; }
                    else query = query.Substring(0, query.Length - 1);
                    Typed?.Invoke();
                }
            }
            else backspaceHeld = 0f;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { typing = false; allSelected = false; RunSearch(); }
            if (kb.escapeKey.wasPressedThisFrame) { typing = false; allSelected = false; }
        }
        // Clicking anywhere else ends typing.
        if (typing && mouse != null && mouse.leftButton.wasPressedThisFrame && !hover) typing = false;

        Draw();
        canvas.Apply();
        GameUI.TerminalTyping = typing;
    }

    void UpdatePointer()
    {
        hover = false;
        if (cam == null) return;
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 o = screen.InverseTransformPoint(ray.origin);
        Vector3 d = screen.InverseTransformDirection(ray.direction);
        float z = -0.121f;
        if (Mathf.Abs(d.z) < 1e-5f) return;
        float t = (z - o.z) / d.z;
        if (t <= 0f) return;
        Vector3 hit = o + d * t;
        if (Vector3.Distance(screen.TransformPoint(hit), ray.origin) > maxUseDistance) return;
        float u = hit.x / screenSize.x + 0.5f, v = hit.y / screenSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;
        hover = true;
        pointer = new Vector2Int(Mathf.FloorToInt(u * canvas.Width), Mathf.FloorToInt((1f - v) * canvas.Height));
    }

    void OnTextInput(char c)
    {
        if (!typing || char.IsControl(c)) return;
        if (allSelected) { query = ""; allSelected = false; }
        if (query.Length >= 24) return;
        query += c;
        Typed?.Invoke();
    }

    bool Inside(int x, int y, int w, int h) =>
        hover && pointer.x >= x && pointer.x < x + w && pointer.y >= y && pointer.y < y + h;

    bool Hit(int x, int y, int w, int h)
    {
        if (!click || !Inside(x, y, w, h)) return false;
        click = false;
        Clicked?.Invoke();
        return true;
    }

    bool Button(int x, int y, int w, int h, string label, Color32 bg, Color32 fg)
    {
        bool over = Inside(x, y, w, h);
        canvas.Fill(x, y, w, h, over ? Lighten(bg) : bg);
        canvas.Frame(x, y, w, h, over ? White : Line);
        canvas.Text(x + (w - PixelCanvas.TextWidth(label)) / 2, y + (h - PixelFont.CellHeight) / 2 + 1, label, fg);
        return Hit(x, y, w, h);
    }

    static Color32 Lighten(Color32 c) => new Color32((byte)Mathf.Min(255, c.r + 30), (byte)Mathf.Min(255, c.g + 40), (byte)Mathf.Min(255, c.b + 30), 255);

    // ------------------------------------------------------------------ drawing

    void Draw()
    {
        int W = canvas.Width, H = canvas.Height;
        canvas.Clear(Bg);

        // Top bar.
        canvas.Fill(0, 0, W, TopBar, Panel);
        canvas.Text(3, 1, Loc.T("VBN-OS 1.3  NACHTLINIE 13", "VBN-OS 1.3  NIGHT LINE 13"), Dim);
        string clock = game != null ? game.ClockText : "--:--";
        canvas.Text(W - PixelCanvas.TextWidth(clock) - 3, 1, clock, Text);

        int top = TopBar + 1, bottom = H - TaskBar - 1;
        switch (app)
        {
            case App.None: DrawDesktop(top, bottom); break;
            case App.Register: Window(Loc.T("REGISTER - EINWOHNERMELDEAMT", "REGISTER - RESIDENTS OFFICE"), top, bottom); DrawRegister(top + 13, bottom); break;
            case App.Mail: Window(Loc.T("POSTFACH", "MAILBOX"), top, bottom); DrawMail(top + 13, bottom); break;
            case App.Control: Window(Loc.T("FAHRGASTKONTROLLE", "PASSENGER CHECK"), top, bottom); DrawControl(top + 13, bottom); break;
        }

        DrawTaskbar();
        if (hover) DrawPointer();
    }

    void Window(string title, int top, int bottom)
    {
        int W = canvas.Width;
        canvas.Fill(0, top, W, 12, Hi);
        canvas.Text(4, top + 1, title, White);
        if (Button(W - 14, top + 1, 12, 10, "x", Red, White)) { app = App.None; typing = false; }
        canvas.Frame(0, top, W, bottom - top, Line);
    }

    void DrawDesktop(int top, int bottom)
    {
        int unread = game != null ? game.Mail.UnreadCount : 0;
        bool someone = game != null && game.PendingCard != null;
        int y = top + 30;
        if (Icon(40, y, "REGISTER", 0, false)) Open(App.Register);
        if (Icon(135, y, unread > 0 ? Loc.T($"POST ({unread})", $"MAIL ({unread})") : Loc.T("POSTFACH", "MAILBOX"), 1, unread > 0)) Open(App.Mail);
        if (Icon(230, y, Loc.T("KONTROLLE", "CHECK"), 2, someone)) Open(App.Control);

        canvas.WrappedText(12, bottom - 50, canvas.Width - 24, 4,
            Loc.T("Leitstelle: Halten Sie nicht außerhalb der Haltestellen. Steigen Sie nicht aus. Lassen Sie niemanden ohne Kontrolle einsteigen.", "Dispatch: Do not stop outside the bus stops. Do not leave the bus. Do not let anyone on without a check."), Dim);
    }

    bool Icon(int x, int y, string label, int kind, bool attention)
    {
        bool over = Inside(x - 12, y - 4, 56, 70);
        if (over) canvas.Fill(x - 12, y - 4, 56, 70, Panel);
        Color32 c = attention && Mathf.Repeat(blink, 1f) < 0.5f ? Warn : Text;
        switch (kind)
        {
            case 0: // card with lines
                canvas.Frame(x, y, 32, 40, c);
                canvas.Fill(x + 4, y + 4, 10, 12, c);
                for (int i = 0; i < 4; i++) canvas.Fill(x + 4, y + 20 + i * 5, 24, 1, c);
                break;
            case 1: // envelope
                canvas.Frame(x, y + 8, 32, 24, c);
                canvas.Line(x, y + 8, x + 16, y + 22, c);
                canvas.Line(x + 31, y + 8, x + 16, y + 22, c);
                break;
            case 2: // person
                canvas.Frame(x + 10, y + 2, 12, 12, c);
                canvas.Fill(x + 6, y + 16, 20, 22, c);
                canvas.Fill(x + 10, y + 20, 12, 18, Bg);
                break;
        }
        canvas.Text(x + 16 - PixelCanvas.TextWidth(label) / 2, y + 48, label, c);
        return Hit(x - 12, y - 4, 56, 70);
    }

    void Open(App a)
    {
        app = a;
        typing = a == App.Register && string.IsNullOrEmpty(query);
    }

    void DrawTaskbar()
    {
        int W = canvas.Width, H = canvas.Height, y = H - TaskBar;
        canvas.Fill(0, y, W, TaskBar, Panel);
        int unread = game != null ? game.Mail.UnreadCount : 0;
        bool someone = game != null && game.PendingCard != null;
        if (Button(2, y + 1, 70, 11, "REGISTER", app == App.Register ? Hi : Panel, Text)) Open(App.Register);
        if (Button(74, y + 1, 70, 11, unread > 0 ? Loc.T($"POST ({unread})", $"MAIL ({unread})") : Loc.T("POSTFACH", "MAILBOX"), app == App.Mail ? Hi : (unread > 0 ? new Color32(70, 55, 10, 255) : Panel), Text)) Open(App.Mail);
        Color32 ctl = app == App.Control ? Hi : (someone && Mathf.Repeat(blink, 1f) < 0.5f ? new Color32(90, 70, 10, 255) : Panel);
        if (Button(146, y + 1, 76, 11, Loc.T("KONTROLLE", "CHECK"), ctl, Text)) Open(App.Control);
        if (game != null)
        {
            string score = Loc.T($"OK {game.Correct} F {game.Wrong}", $"OK {game.Correct} X {game.Wrong}");
            canvas.Text(W - PixelCanvas.TextWidth(score) - 3, y + 1, score, Dim);
        }
    }

    void DrawPointer()
    {
        int x = pointer.x, y = pointer.y;
        for (int i = 0; i < 9; i++)
        {
            canvas.Fill(x, y + i, Mathf.Max(1, i / 2 + 1), 1, White);
            canvas.Pixel(x + i / 2 + 1, y + i, Bg);
        }
        canvas.Fill(x + 2, y + 8, 1, 3, White);
    }

    // ------------------------------------------------------------------ register

    void DrawRegister(int top, int bottom)
    {
        int W = canvas.Width;
        canvas.Text(4, top + 3, "NAME:", Text);
        int fx = 38, fw = 170;
        canvas.Fill(fx, top + 1, fw, 13, new Color32(2, 6, 3, 255));
        canvas.Frame(fx, top + 1, fw, 13, typing ? White : Line);
        string visible = query.Length > 26 ? query.Substring(query.Length - 26) : query;
        if (typing && allSelected && visible.Length > 0)
        {
            // Selection highlight.
            canvas.Fill(fx + 2, top + 2, PixelCanvas.TextWidth(visible) + 2, 11, Line);
            canvas.Text(fx + 3, top + 2, visible, Bg);
        }
        else
        {
            string shown = visible + (typing && Mathf.Repeat(blink, 0.8f) < 0.4f ? "_" : "");
            canvas.Text(fx + 3, top + 2, shown, typing ? White : Text);
        }
        if (Hit(fx, top + 1, fw, 13)) typing = true;
        if (Button(fx + fw + 4, top + 1, 56, 13, Loc.T("SUCHEN", "SEARCH"), Green, White)) { typing = false; RunSearch(); }

        // Results.
        int ly = top + 18, lh = bottom - ly - 2, lw = 124;
        canvas.Frame(2, ly, lw, lh, Line);
        int rowH = 12, rows = (lh - 4) / rowH;
        if (!searched)
            canvas.WrappedText(6, ly + 4, lw - 8, 6, Loc.T("Suchfeld anklicken, Namen tippen, ENTER.", "Click the search field, type a name, ENTER."), Dim);
        else if (results.Count == 0)
            canvas.WrappedText(6, ly + 4, lw - 8, 4, Loc.T("KEIN EINTRAG GEFUNDEN", "NO ENTRY FOUND"), Alert);
        else
        {
            resultScroll = Mathf.Clamp(resultScroll, 0, Mathf.Max(0, results.Count - rows));
            for (int i = 0; i < rows && i + resultScroll < results.Count; i++)
            {
                var c = results[i + resultScroll];
                int ry = ly + 2 + i * rowH;
                bool sel = c == selected;
                if (sel || Inside(3, ry, lw - 12, rowH)) canvas.Fill(3, ry, lw - 12, rowH, sel ? Hi : Panel);
                canvas.Text(5, ry + 1, $"{c.LastName}, {c.FirstName}", sel ? White : Text, 18);
                if (Hit(3, ry, lw - 12, rowH)) selected = c;
            }
            if (results.Count > rows)
            {
                if (Button(lw - 9, ly + 1, 10, 11, "^", Panel, Text)) resultScroll--;
                if (Button(lw - 9, ly + lh - 12, 10, 11, "v", Panel, Text)) resultScroll++;
            }
        }

        // Record.
        int rx = lw + 6, rw = W - rx - 2;
        canvas.Frame(rx, ly, rw, lh, Line);
        if (selected == null)
        {
            canvas.Text(rx + 4, ly + 4, Loc.T("Eintrag wählen.", "Select an entry."), Dim);
            return;
        }
        int y = ly + 4;
        canvas.Text(rx + 4, y, Loc.T("AUSZUG MELDEREGISTER", "REGISTER EXTRACT"), Dim); y += 16;
        Field(rx + 4, ref y, Loc.T("NAME", "SURNAME"), selected.LastName.ToUpperInvariant());
        Field(rx + 4, ref y, Loc.T("VORNAME", "FIRST"), selected.FirstName);
        Field(rx + 4, ref y, Loc.T("GEBOREN", "BORN"), selected.BirthDate.ToString("dd.MM.yyyy"));
        Field(rx + 4, ref y, Loc.T("AUSWEIS", "ID NO."), selected.IdNumber);
        Field(rx + 4, ref y, Loc.T("GÜLTIG", "VALID"), selected.IdExpiry.ToString("dd.MM.yyyy"), selected.IdExpiry < CitizenRegistry.Today ? Alert : White);
        Field(rx + 4, ref y, Loc.T("BEZIRK", "DISTRICT"), selected.District);
        Field(rx + 4, ref y, Loc.T("BERUF", "JOB"), selected.Occupation);
        Color32 sc = selected.Status == "AKTIV" ? Text : selected.Status == "GESUCHT" ? Warn : Alert;
        canvas.Text(rx + 4, y, "STATUS", Dim);
        canvas.Text(rx + 58, y, StatusText(selected.Status), sc);
        y += 14;
        if (!string.IsNullOrEmpty(selected.Note))
            canvas.WrappedText(rx + 4, y, rw - 8, 6, selected.Note, sc);
    }

    static string StatusText(string status) => !Loc.English ? status :
        status == "AKTIV" ? "ACTIVE" : status == "GESUCHT" ? "WANTED" : status == "VERSTORBEN" ? "DECEASED" : status;

    void Field(int x, ref int y, string label, string value) => Field(x, ref y, label, value, White);

    void Field(int x, ref int y, string label, string value, Color32 color)
    {
        canvas.Text(x, y, label, Dim);
        canvas.Text(x + 54, y, value, color, 20);
        y += 13;
    }

    void RunSearch()
    {
        if (game == null || game.Registry == null) return;
        results = game.Registry.Search(query).ToList();
        searched = true;
        resultScroll = 0;
        selected = results.Count == 1 ? results[0] : null;
        if (results.Count == 0) ErrorBeep?.Invoke();
    }

    // ------------------------------------------------------------------ mail

    void DrawMail(int top, int bottom)
    {
        int W = canvas.Width;
        var mails = game.Mail.Mails;
        int lw = 112, ly = top + 2, lh = bottom - ly - 2, rowH = 22, rows = (lh - 4) / rowH;
        canvas.Frame(2, ly, lw, lh, Line);
        mailScroll = Mathf.Clamp(mailScroll, 0, Mathf.Max(0, mails.Count - rows));
        for (int i = 0; i < rows && i + mailScroll < mails.Count; i++)
        {
            var m = mails[i + mailScroll];
            int ry = ly + 2 + i * rowH;
            bool sel = m == openMail;
            if (sel || Inside(3, ry, lw - 12, rowH - 1)) canvas.Fill(3, ry, lw - 12, rowH - 1, sel ? Hi : Panel);
            canvas.Text(5, ry + 1, (m.Read ? "" : "*") + m.Subject, m.Read ? Text : Warn, 16);
            canvas.Text(5, ry + 11, m.From, Dim, 16);
            if (Hit(3, ry, lw - 12, rowH - 1)) { openMail = m; m.Read = true; bodyScroll = 0; }
        }
        if (mails.Count > rows)
        {
            if (Button(lw - 9, ly + 1, 10, 11, "^", Panel, Text)) mailScroll--;
            if (Button(lw - 9, ly + lh - 12, 10, 11, "v", Panel, Text)) mailScroll++;
        }

        int bx = lw + 6, bw = W - bx - 2;
        canvas.Frame(bx, ly, bw, lh, Line);
        if (openMail == null)
        {
            canvas.Text(bx + 4, ly + 4, Loc.T("Mail wählen.", "Select a mail."), Dim);
            return;
        }
        canvas.Text(bx + 4, ly + 3, openMail.From + "  " + openMail.Time, Dim, 32);
        canvas.WrappedText(bx + 4, ly + 15, bw - 8, 2, openMail.Subject, White);
        canvas.Fill(bx + 4, ly + 40, bw - 8, 1, Line);
        int lines = (lh - 48) / (PixelFont.CellHeight + 1);
        int total = canvas.WrappedText(bx + 4, ly + 44, bw - 16, lines, openMail.Body, Text, bodyScroll);
        if (total > lines)
        {
            if (Button(bx + bw - 12, ly + 42, 10, 11, "^", Panel, Text)) bodyScroll = Mathf.Max(0, bodyScroll - 3);
            if (Button(bx + bw - 12, ly + lh - 12, 10, 11, "v", Panel, Text)) bodyScroll = Mathf.Min(total - lines, bodyScroll + 3);
        }
    }

    // ------------------------------------------------------------------ control

    void DrawControl(int top, int bottom)
    {
        int W = canvas.Width;
        var card = game.PendingCard;
        if (card == null)
        {
            canvas.WrappedText(8, top + 8, W - 16, 6, Loc.T("Kein Fahrgast an der Tür.\n\nAn der Haltestelle anhalten und die Türen öffnen ", "No passenger at the door.\n\nStop at the bus stop and open the doors ") + "(" + GameKeys.Name(GameAction.Doors) + ").", Dim);
            return;
        }
        canvas.Text(8, top + 8, Loc.T("FAHRGAST AN DER TÜR:", "PASSENGER AT THE DOOR:"), Dim);
        canvas.Text(8, top + 22, card.FullName.ToUpperInvariant(), White);
        canvas.WrappedText(8, top + 42, W - 16, 4,
            Loc.T("Ausweis (links) mit dem REGISTER vergleichen: Name, Geburtsdatum, Ausweisnummer, Gültigkeit, Status. Ansprechen mit " + GameKeys.Name(GameAction.Talk) + " und die Antworten prüfen.", "Compare the ID (left) with the REGISTER: name, date of birth, ID number, validity, status. Talk with " + GameKeys.Name(GameAction.Talk) + " and check the answers."), Text);

        int by = bottom - 50;
        if (Button(12, by, 140, 34, Loc.T("EINLASSEN  ", "LET IN  ") + GameKeys.Tag(GameAction.LetIn), Green, White)) { game.Decide(true); app = App.None; }
        if (Button(W - 152, by, 140, 34, Loc.T("ABWEISEN  ", "TURN AWAY  ") + GameKeys.Tag(GameAction.TurnAway), Red, White)) { game.Decide(false); app = App.None; }
    }
}
