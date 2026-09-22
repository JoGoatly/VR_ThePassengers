using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On-board computer: a monitor next to the steering wheel, and (Tab) a full screen
/// terminal with the resident register search and the mail inbox.
/// </summary>
public class ComputerTerminal : MonoBehaviour
{
    public BoardingManager game;

    [Header("Monitor in the cockpit (bus space)")]
    public Vector3 monitorPosition = new Vector3(1.12f, 1.22f, 4.45f);
    public Material caseMaterial;
    public Material screenMaterial;

    enum Tab { Register, Mail }
    Tab tab = Tab.Register;
    string query = "";
    List<Citizen> results = new List<Citizen>();
    Citizen selected;
    Mail openMail;
    bool focusSearch;

    static readonly Color Bg = new Color(0.02f, 0.07f, 0.04f);
    static readonly Color Panel = new Color(0.04f, 0.12f, 0.07f);
    static readonly Color Line = new Color(0.2f, 0.55f, 0.3f);
    static readonly Color Text = new Color(0.62f, 1f, 0.7f);
    static readonly Color Dim = new Color(0.35f, 0.65f, 0.42f);
    static readonly Color Alert = new Color(1f, 0.35f, 0.3f);
    static readonly Color Warn = new Color(1f, 0.8f, 0.3f);

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        BuildMonitor();
    }

    void BuildMonitor()
    {
        if (game == null || game.bus == null) return;
        Transform bus = game.bus.transform;
        var root = new GameObject("Board Computer").transform;
        root.SetParent(bus, false);
        root.localPosition = monitorPosition;

        // Face the driver (roughly towards the seat on the left of the monitor).
        Vector3 toDriver = new Vector3(-0.6f, 0.25f, -0.35f);
        root.localRotation = Quaternion.LookRotation(-toDriver.normalized);

        var caseMesh = MeshKit.Box(new Vector3(0.34f, 0.26f, 0.22f), 0.34f);
        var screenMesh = MeshKit.Box(new Vector3(0.28f, 0.2f, 0.01f), 0.28f);
        var c = MeshKit.Spawn("Case", root, caseMesh, caseMaterial, root.position, root.rotation, false);
        c.transform.localPosition = Vector3.zero;
        var s = MeshKit.Spawn("Screen", root, screenMesh, screenMaterial, root.position, root.rotation, false);
        s.transform.localPosition = new Vector3(0f, 0.03f, -0.115f);
        var foot = MeshKit.Spawn("Stand", root, MeshKit.Box(new Vector3(0.08f, 0.1f, 0.08f), 0.1f), caseMaterial, root.position, root.rotation, false);
        foot.transform.localPosition = new Vector3(0f, -0.1f, 0f);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            GameUI.ComputerOpen = !GameUI.ComputerOpen;
            if (GameUI.ComputerOpen) { GameUI.IdCardOpen = false; focusSearch = tab == Tab.Register; }
        }
        if (GameUI.ComputerOpen && kb.escapeKey.wasPressedThisFrame) GameUI.ComputerOpen = false;
    }

    void OnGUI()
    {
        if (!GameUI.ComputerOpen || game == null) return;

        // Enter in the search field.
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() == "search")
        {
            RunSearch();
            Event.current.Use();
        }

        float vw = RetroGUI.VirtualWidth;
        float w = 560f, h = 300f;
        float x = (vw - w) * 0.5f, y = 20f;

        RetroGUI.Fill(new Rect(0, 0, vw, RetroGUI.VirtualHeight), new Color(0, 0, 0, 0.55f));
        RetroGUI.Frame(new Rect(x - 6, y - 6, w + 12, h + 12), new Color(0.72f, 0.69f, 0.6f), new Color(0.35f, 0.33f, 0.28f), 2f);
        RetroGUI.Frame(new Rect(x, y, w, h), Bg, Line);

        // Title bar.
        RetroGUI.Fill(new Rect(x + 1, y + 1, w - 2, 16), Panel);
        RetroGUI.Label(new Rect(x + 6, y + 4, 300, 12), "VBN BORDRECHNER 2.1  -  NACHTLINIE 13", Text, true);
        RetroGUI.Label(new Rect(x + w - 250, y + 4, 244, 12),
            $"{CitizenRegistry.Today:dd.MM.yyyy}  {game.ClockText}   OK {game.Correct}  FEHLER {game.Wrong}", Dim, false, false, TextAnchor.UpperRight);

        // Tabs.
        int unread = game.Mail.UnreadCount;
        if (TabButton(new Rect(x + 6, y + 22, 90, 16), "REGISTER", tab == Tab.Register)) { tab = Tab.Register; focusSearch = true; }
        if (TabButton(new Rect(x + 100, y + 22, 110, 16), unread > 0 ? $"POSTFACH ({unread})" : "POSTFACH", tab == Tab.Mail)) tab = Tab.Mail;
        RetroGUI.Label(new Rect(x + w - 200, y + 25, 194, 12), "[Tab] schließen", Dim, false, true, TextAnchor.UpperRight);

        var content = new Rect(x + 6, y + 44, w - 12, h - 50);
        if (tab == Tab.Register) DrawRegister(content);
        else DrawMail(content);

        // Decision buttons are available here too while someone waits at the door.
        if (game.PendingCard != null)
        {
            var card = game.PendingCard;
            float by = y + h + 10;
            RetroGUI.Frame(new Rect(x, by, w, 26), Panel, Line);
            RetroGUI.Label(new Rect(x + 8, by + 8, 250, 12), "An der Tür: " + card.FullName, Warn, true);
            if (RetroGUI.Button(new Rect(x + w - 230, by + 5, 110, 16), "EINSTEIGEN [J]", new Color(0.15f, 0.4f, 0.2f), Color.white))
            { GameUI.ComputerOpen = false; game.Decide(true); }
            if (RetroGUI.Button(new Rect(x + w - 115, by + 5, 108, 16), "ABWEISEN [N]", new Color(0.45f, 0.12f, 0.1f), Color.white))
            { GameUI.ComputerOpen = false; game.Decide(false); }
        }
    }

    bool TabButton(Rect r, string text, bool active)
    {
        return RetroGUI.Button(r, text, active ? new Color(0.2f, 0.5f, 0.28f) : Panel, active ? Color.white : Dim);
    }

    void DrawRegister(Rect r)
    {
        RetroGUI.Label(new Rect(r.x, r.y + 3, 60, 12), "NAME:", Text, true);
        query = RetroGUI.TextField(new Rect(r.x + 42, r.y, 220, 16), query, "search");
        if (focusSearch) { GUI.FocusControl("search"); focusSearch = false; }
        if (RetroGUI.Button(new Rect(r.x + 268, r.y, 70, 16), "SUCHEN")) RunSearch();
        RetroGUI.Label(new Rect(r.x + 346, r.y + 3, 200, 12), "Vor- und/oder Nachname", Dim, false, true);

        // Result list.
        var list = new Rect(r.x, r.y + 24, 200, r.height - 24);
        RetroGUI.Frame(list, Panel, Line);
        if (results.Count == 0)
        {
            RetroGUI.Label(new Rect(list.x + 6, list.y + 6, list.width - 12, 12),
                string.IsNullOrEmpty(query) ? "Namen eingeben..." : "KEIN EINTRAG GEFUNDEN", string.IsNullOrEmpty(query) ? Dim : Alert, true);
        }
        else
        {
            float rowH = 14f;
            for (int i = 0; i < results.Count && i < 16; i++)
            {
                var c = results[i];
                var row = new Rect(list.x + 3, list.y + 3 + i * rowH, list.width - 6, rowH - 1);
                bool isSel = c == selected;
                if (RetroGUI.Button(row, "", isSel ? new Color(0.2f, 0.45f, 0.26f) : Panel, Text)) selected = c;
                RetroGUI.Label(new Rect(row.x + 4, row.y + 2, row.width - 8, 12), $"{c.LastName}, {c.FirstName}", isSel ? Color.white : Text);
            }
            if (results.Count > 16)
                RetroGUI.Label(new Rect(list.x + 6, list.yMax - 14, list.width - 12, 12), $"... {results.Count - 16} weitere, genauer suchen", Dim, false, true);
        }

        // Record.
        var rec = new Rect(r.x + 206, r.y + 24, r.width - 206, r.height - 24);
        RetroGUI.Frame(rec, Panel, Line);
        if (selected == null)
        {
            RetroGUI.Label(new Rect(rec.x + 8, rec.y + 8, rec.width - 16, 12), "Eintrag auswählen.", Dim);
            return;
        }

        float ly = rec.y + 8;
        RetroGUI.Header(new Rect(rec.x + 8, ly, rec.width - 16, 16), "EINWOHNERREGISTER - AUSZUG", Text);
        ly += 22;
        Field(rec.x + 8, ref ly, "NAME", selected.LastName.ToUpperInvariant() + ", " + selected.FirstName.ToUpperInvariant());
        Field(rec.x + 8, ref ly, "GEBOREN", selected.BirthDate.ToString("dd.MM.yyyy"));
        Field(rec.x + 8, ref ly, "AUSWEIS-NR.", selected.IdNumber);
        Field(rec.x + 8, ref ly, "GESCHLECHT", selected.Gender == Gender.Male ? "M" : "W");
        Field(rec.x + 8, ref ly, "WOHNBEZIRK", selected.District);

        Color statusColor = selected.Status == "AKTIV" ? Text : selected.Status == "GESUCHT" ? Warn : Alert;
        RetroGUI.Label(new Rect(rec.x + 8, ly, 90, 12), "STATUS", Dim);
        RetroGUI.Label(new Rect(rec.x + 100, ly, 200, 12), selected.Status, statusColor, true);
        ly += 18;
        if (!string.IsNullOrEmpty(selected.Note))
            RetroGUI.Wrapped(new Rect(rec.x + 8, ly, rec.width - 16, 60), selected.Note, statusColor);
    }

    void Field(float x, ref float y, string label, string value)
    {
        RetroGUI.Label(new Rect(x, y, 90, 12), label, Dim);
        RetroGUI.Label(new Rect(x + 92, y, 240, 12), value, Text, true);
        y += 16;
    }

    void RunSearch()
    {
        results = game.Registry.Search(query).ToList();
        selected = results.Count == 1 ? results[0] : null;
    }

    void DrawMail(Rect r)
    {
        var mails = game.Mail.Mails;
        var list = new Rect(r.x, r.y, 210, r.height);
        RetroGUI.Frame(list, Panel, Line);
        float rowH = 26f;
        for (int i = 0; i < mails.Count && i < 9; i++)
        {
            var m = mails[i];
            var row = new Rect(list.x + 3, list.y + 3 + i * rowH, list.width - 6, rowH - 2);
            bool isSel = m == openMail;
            if (RetroGUI.Button(row, "", isSel ? new Color(0.2f, 0.45f, 0.26f) : Panel, Text)) { openMail = m; m.Read = true; }
            RetroGUI.Label(new Rect(row.x + 4, row.y + 2, row.width - 40, 12), (m.Read ? "" : "* ") + m.Subject, m.Read ? Text : Color.white, !m.Read);
            RetroGUI.Label(new Rect(row.x + 4, row.y + 13, row.width - 8, 10), m.From, Dim, false, true);
            RetroGUI.Label(new Rect(row.xMax - 40, row.y + 13, 36, 10), m.Time, Dim, false, true, TextAnchor.UpperRight);
        }

        var body = new Rect(r.x + 216, r.y, r.width - 216, r.height);
        RetroGUI.Frame(body, Panel, Line);
        if (openMail == null)
        {
            RetroGUI.Label(new Rect(body.x + 8, body.y + 8, body.width - 16, 12), "Mail auswählen.", Dim);
            return;
        }
        RetroGUI.Label(new Rect(body.x + 8, body.y + 8, body.width - 16, 12), "VON: " + openMail.From + "   " + openMail.Time, Dim);
        RetroGUI.Header(new Rect(body.x + 8, body.y + 22, body.width - 16, 16), openMail.Subject, Color.white);
        RetroGUI.Fill(new Rect(body.x + 8, body.y + 40, body.width - 16, 1), Line);
        RetroGUI.Wrapped(new Rect(body.x + 8, body.y + 46, body.width - 16, body.height - 52), openMail.Body, Text);
    }
}
