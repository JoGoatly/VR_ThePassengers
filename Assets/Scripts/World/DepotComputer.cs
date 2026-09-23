using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The old service PC in the depot office: mails, personnel files, tonight's log book,
/// the security cameras - and clocking out, which ends the night.
/// </summary>
public class DepotComputer : Interactable
{
    public AudioClip clickSound;

    enum Page { Mail, Staff, Log, Cameras, ClockOut }
    Page page = Page.Mail;
    bool open;
    int openedFrame, mailIndex = -1;
    float noiseSeed;

    public override string Prompt => Loc.T("Dienst-PC benutzen", "Use the service PC");

    public override void Use()
    {
        open = true;
        openedFrame = Time.frameCount;
        page = Page.Mail;
        mailIndex = -1;
        GameUI.PcOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Click();
    }

    void Close()
    {
        open = false;
        GameUI.PcOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (open) Close();
    }

    void Click()
    {
        var sm = FindAnyObjectByType<SoundManager>();
        if (sm != null && clickSound != null) sm.PlayWorld(clickSound, transform.position, 0.6f, 0f);
    }

    void Update()
    {
        if (!open || Time.frameCount <= openedFrame + 1) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || GameKeys.Pressed(GameAction.Interact))) Close();
        if (!GameUI.PlayerOutside) Close();
    }

    // ---------------------------------------------------------------- content

    struct Mail { public string from, subject, body; }

    Mail[] Mails()
    {
        int day = Progress.Day;
        var list = new System.Collections.Generic.List<Mail>
        {
            new Mail
            {
                from = Loc.T("Leitstelle", "Dispatch"),
                subject = Loc.T("Dienstanweisung Betriebshof", "Depot instructions"),
                body = Loc.T("Nach jeder Schicht: Bus abstellen, am Dienst-PC ausstempeln.\nDas Archiv ist nicht zu betreten.\nDer Wagen im hinteren Hof ist nicht zu betreten.\nFragen Sie nicht nach Horst.\n\nLeitstelle",
                             "After every shift: park the bus, clock out at the service PC.\nDo not enter the archive.\nDo not enter the vehicle in the back yard.\nDo not ask about Horst.\n\nDispatch"),
            },
            new Mail
            {
                from = "Horst",
                subject = Loc.T("kaffeemaschine", "coffee machine"),
                body = Loc.T("wer auch immer das liest: die kaffeemaschine NICHT nach mitternacht benutzen.\nsie macht dann keinen kaffee mehr.\n\nh.",
                             "whoever reads this: do NOT use the coffee machine after midnight.\nit doesn't make coffee anymore then.\n\nh."),
            },
        };
        if (day >= 2)
            list.Add(new Mail
            {
                from = Loc.T("Personalabteilung", "Human resources"),
                subject = Loc.T("Ihr Spind", "Your locker"),
                body = Loc.T("Ihr Spind wurde vorbereitet. Ihr Name steht bereits drauf.\nWir haben ihn schon 1994 draufgeschrieben.",
                             "Your locker has been prepared. Your name is already on it.\nWe wrote it on in 1994."),
            });
        if (day >= 4)
            list.Add(new Mail
            {
                from = "J. Keller",
                subject = Loc.T("(kein Betreff)", "(no subject)"),
                body = Loc.T("Wenn du das liest, lebst du noch. Gut.\nZähl die Spinde. Es sind sechs. Es waren mal fünf.\nDer sechste ist für dich.\nFahr in der letzten Nacht am Depot vorbei. Nicht anhalten.",
                             "If you're reading this, you're still alive. Good.\nCount the lockers. There are six. There used to be five.\nThe sixth is for you.\nOn the last night drive past the depot. Don't stop."),
            });
        if (day >= 6)
            list.Add(new Mail
            {
                from = "???",
                subject = "13",
                body = Loc.T("wir sehen dich auf kamera 3", "we see you on camera 3"),
            });
        return list.ToArray();
    }

    string StaffText()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Story.Drivers.Length; i++)
        {
            var d = Story.Drivers[i];
            string state = Progress.Data.drivers.Contains(i) ? Loc.T("GEFUNDEN (TOT)", "FOUND (DEAD)") : Loc.T("VERMISST", "MISSING");
            sb.AppendLine($"{d.name,-14} {d.badge.Split('-')[0].Trim(),-9} {state}");
        }
        sb.AppendLine(Loc.T("Horst Lang     VBN 0666  KRANK (SEIT 1994)", "Horst Lang     VBN 0666  SICK (SINCE 1994)"));
        sb.AppendLine(Loc.T("SIE            VBN 1313  AKTIV", "YOU            VBN 1313  ACTIVE"));
        sb.AppendLine();
        sb.Append(Loc.T($"Vermisste Fahrer gefunden: {Progress.Data.drivers.Count} / {Story.Drivers.Length}",
                        $"Missing drivers found: {Progress.Data.drivers.Count} / {Story.Drivers.Length}"));
        return sb.ToString();
    }

    string LogText()
    {
        return Loc.T(
            $"FAHRTENBUCH - NACHT {Progress.Day} / {Progress.LastDay}\n\n" +
            $"Richtige Entscheidungen:  {Progress.ShiftCorrect}\n" +
            $"Fehler:                   {Progress.ShiftWrong}\n" +
            $"Verdient:                 {Progress.ShiftEarned} €\n" +
            $"Strafen:                  {Progress.ShiftFines} €\n" +
            $"Gefunden:                 {Progress.ShiftFound} €\n\n" +
            $"Kontostand:               {Progress.Money} €\n" +
            $"Geheimnisse im Depot:     {Progress.SecretsFound} / {Depot.SecretCount}\n" +
            $"Zettel gefunden:          {Progress.Data.notes.Count} / {Story.Notes.Length}",
            $"LOG BOOK - NIGHT {Progress.Day} / {Progress.LastDay}\n\n" +
            $"Correct decisions:        {Progress.ShiftCorrect}\n" +
            $"Mistakes:                 {Progress.ShiftWrong}\n" +
            $"Earned:                   {Progress.ShiftEarned} €\n" +
            $"Fines:                    {Progress.ShiftFines} €\n" +
            $"Found:                    {Progress.ShiftFound} €\n\n" +
            $"Balance:                  {Progress.Money} €\n" +
            $"Depot secrets:            {Progress.SecretsFound} / {Depot.SecretCount}\n" +
            $"Notes found:              {Progress.Data.notes.Count} / {Story.Notes.Length}");
    }

    // ---------------------------------------------------------------- drawing

    void OnGUI()
    {
        if (!open) return;
        GUI.depth = -350;
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.75f));

        var green = new Color(0.55f, 1f, 0.6f);
        var dim = new Color(0.3f, 0.6f, 0.35f);
        var screen = new Rect(w / 2 - 230, 30, 460, 290);
        RetroGUI.Frame(new Rect(screen.x - 8, screen.y - 8, screen.width + 16, screen.height + 16), new Color(0.72f, 0.7f, 0.62f), new Color(0.3f, 0.29f, 0.25f), 2f);
        RetroGUI.Fill(screen, new Color(0.02f, 0.07f, 0.03f));
        // Scanlines.
        for (float y = screen.y; y < screen.yMax; y += 3f)
            RetroGUI.Fill(new Rect(screen.x, y, screen.width, 1f), new Color(0f, 0f, 0f, 0.25f));

        RetroGUI.Label(new Rect(screen.x + 8, screen.y + 5, 300, 12), Loc.T("BETRIEBSHOF LINIE 13 - DIENST-PC", "LINE 13 DEPOT - SERVICE PC"), green, true);
        var game = FindAnyObjectByType<BoardingManager>();
        RetroGUI.Label(new Rect(screen.xMax - 80, screen.y + 5, 72, 12), game != null ? game.ClockText : "--:--", dim, false, false, TextAnchor.UpperRight);
        RetroGUI.Fill(new Rect(screen.x + 6, screen.y + 19, screen.width - 12, 1), dim);

        // Menu.
        string[] labels =
        {
            Loc.T("POST", "MAIL"), Loc.T("PERSONAL", "STAFF"), Loc.T("FAHRTENBUCH", "LOG BOOK"),
            Loc.T("KAMERAS", "CAMERAS"), Loc.T("AUSSTEMPELN", "CLOCK OUT"),
        };
        for (int i = 0; i < labels.Length; i++)
        {
            bool active = (int)page == i;
            var bg = active ? new Color(0.2f, 0.45f, 0.25f) : new Color(0.06f, 0.16f, 0.08f);
            if (RetroGUI.Button(new Rect(screen.x + 8, screen.y + 26 + i * 22, 96, 18), labels[i], bg, green))
            {
                page = (Page)i;
                mailIndex = -1;
                Click();
            }
        }
        if (RetroGUI.Button(new Rect(screen.x + 8, screen.yMax - 24, 96, 18), Loc.T("SCHLIESSEN", "CLOSE"), new Color(0.2f, 0.08f, 0.06f), new Color(1f, 0.75f, 0.7f)))
        {
            Click();
            Close();
            return;
        }

        var content = new Rect(screen.x + 114, screen.y + 26, screen.width - 122, screen.height - 34);
        switch (page)
        {
            case Page.Mail: DrawMail(content, green, dim); break;
            case Page.Staff: RetroGUI.Wrapped(content, StaffText(), green); break;
            case Page.Log: RetroGUI.Wrapped(content, LogText(), green); break;
            case Page.Cameras: DrawCameras(content, green, dim); break;
            case Page.ClockOut: DrawClockOut(content, green, dim); break;
        }
    }

    void DrawMail(Rect r, Color green, Color dim)
    {
        var mails = Mails();
        if (mailIndex >= 0 && mailIndex < mails.Length)
        {
            var m = mails[mailIndex];
            RetroGUI.Label(new Rect(r.x, r.y, r.width, 12), Loc.T("Von: ", "From: ") + m.from, dim);
            RetroGUI.Label(new Rect(r.x, r.y + 12, r.width, 12), m.subject, green, true);
            RetroGUI.Wrapped(new Rect(r.x, r.y + 30, r.width, r.height - 56), m.body, green);
            if (RetroGUI.Button(new Rect(r.x, r.yMax - 20, 70, 18), Loc.T("ZURÜCK", "BACK"))) { mailIndex = -1; Click(); }
            return;
        }
        for (int i = 0; i < mails.Length; i++)
        {
            if (RetroGUI.Button(new Rect(r.x, r.y + i * 22, r.width, 18), $"{mails[i].from}:  {mails[i].subject}", new Color(0.04f, 0.12f, 0.06f), green))
            {
                mailIndex = i;
                Click();
            }
        }
    }

    void DrawCameras(Rect r, Color green, Color dim)
    {
        // Four noisy camera pictures; camera 3 (archive) shows someone from night 3 on.
        string[] names = { Loc.T("CAM 1 HALLE", "CAM 1 GARAGE"), Loc.T("CAM 2 HOF", "CAM 2 YARD"), Loc.T("CAM 3 ARCHIV", "CAM 3 ARCHIVE"), Loc.T("CAM 4 BÜRO", "CAM 4 OFFICE") };
        float cw = (r.width - 6) / 2f, ch = (r.height - 30) / 2f;
        noiseSeed += Time.unscaledDeltaTime;
        var rand = new System.Random((int)(noiseSeed * 12f));
        for (int i = 0; i < 4; i++)
        {
            var cell = new Rect(r.x + (i % 2) * (cw + 6), r.y + (i / 2) * (ch + 6), cw, ch);
            RetroGUI.Fill(cell, new Color(0.04f, 0.06f, 0.05f));
            for (int n = 0; n < 40; n++)
            {
                float x = cell.x + (float)rand.NextDouble() * (cell.width - 3), y = cell.y + (float)rand.NextDouble() * (cell.height - 2);
                float g = 0.1f + (float)rand.NextDouble() * 0.25f;
                RetroGUI.Fill(new Rect(x, y, 3, 1), new Color(g, g + 0.05f, g));
            }
            if (i == 2 && Progress.Day >= 3)
            {
                // A figure standing still in the archive.
                float fx = cell.x + cell.width * 0.62f, fy = cell.y + cell.height * 0.3f;
                RetroGUI.Fill(new Rect(fx, fy, 6, 6), new Color(0.35f, 0.38f, 0.35f));
                RetroGUI.Fill(new Rect(fx - 2, fy + 6, 10, 22), new Color(0.3f, 0.33f, 0.3f));
            }
            if (i == 3)
            {
                // Someone at the desk, seen from behind: you.
                float fx = cell.x + cell.width * 0.45f, fy = cell.y + cell.height * 0.35f;
                RetroGUI.Fill(new Rect(fx, fy, 7, 7), new Color(0.4f, 0.42f, 0.4f));
                RetroGUI.Fill(new Rect(fx - 3, fy + 7, 13, 18), new Color(0.35f, 0.37f, 0.35f));
            }
            RetroGUI.Label(new Rect(cell.x + 3, cell.y + 2, cell.width, 12), names[i], green, false, true);
            if (Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f) RetroGUI.Fill(new Rect(cell.xMax - 8, cell.y + 4, 4, 4), new Color(0.9f, 0.1f, 0.1f));
        }
        string status = Progress.Day >= 3 ? Loc.T("CAM 3: BEWEGUNG ERKANNT", "CAM 3: MOTION DETECTED") : Loc.T("KEINE BEWEGUNG", "NO MOTION");
        RetroGUI.Label(new Rect(r.x, r.yMax - 14, r.width, 12), status, Progress.Day >= 3 ? new Color(1f, 0.4f, 0.35f) : dim);
    }

    void DrawClockOut(Rect r, Color green, Color dim)
    {
        var days = FindAnyObjectByType<DayManager>();
        bool last = Progress.Day == Progress.LastDay;
        string text = Loc.T(
            $"Schicht beenden und nach Hause fahren.\n\nNacht {Progress.Day} von {Progress.LastDay}.\nDer Lohn wird beim Ausstempeln überwiesen.",
            $"End the shift and go home.\n\nNight {Progress.Day} of {Progress.LastDay}.\nYour wage is paid when you clock out.");
        if (last)
            text += Loc.T("\n\nJemand hat mit Kuli auf den Monitorrand geschrieben:\n\"NICHT AUSSTEMPELN. WEITERFAHREN.\"",
                          "\n\nSomeone wrote on the monitor frame in ballpoint:\n\"DON'T CLOCK OUT. KEEP DRIVING.\"");
        RetroGUI.Wrapped(new Rect(r.x, r.y, r.width, 120), text, green);
        if (days == null || !days.AtDepot) return;
        if (RetroGUI.Button(new Rect(r.x, r.y + 130, 160, 24), Loc.T("SCHICHT BEENDEN", "END SHIFT"), new Color(0.35f, 0.12f, 0.08f), new Color(1f, 0.85f, 0.7f)))
        {
            Click();
            Close();
            days.ClockOut();
        }
    }
}
