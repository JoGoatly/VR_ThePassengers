using UnityEngine;

/// <summary>
/// The ID card of the passenger at the door, shown on the left of the screen
/// (the passenger is in the middle, the terminal on the right). E hides/shows it.
/// </summary>
public class IdCardView : MonoBehaviour
{
    public BoardingManager game;

    static readonly Color Paper = new Color(0.8f, 0.78f, 0.68f);
    static readonly Color PaperDark = new Color(0.55f, 0.56f, 0.48f);
    static readonly Color Ink = new Color(0.1f, 0.1f, 0.13f);
    static readonly Color InkLight = new Color(0.3f, 0.3f, 0.33f);
    static readonly Color Stripe = new Color(0.42f, 0.12f, 0.1f);

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
    }

    void OnGUI()
    {
        if (game == null) return;
        var card = game.PendingCard;
        if (card == null) return;

        if (GameUI.IdCardHidden)
        {
            RetroGUI.ShadowLabel(new Rect(10, 150, 200, 12), Loc.T("Ausweis einblenden ", "Show ID card ") + GameKeys.Tag(GameAction.Interact), Color.white, false, TextAnchor.UpperLeft);
            return;
        }

        if (card.HasTicket) DrawTicket(card);

        var r = new Rect(10, 95, 214, 138);
        // Slightly darkened by the night.
        RetroGUI.Frame(r, Paper, PaperDark, 2f);
        RetroGUI.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 15), Stripe);
        RetroGUI.Label(new Rect(r.x + 6, r.y + 4, r.width - 12, 12), Loc.T("LANDKREIS SCHWARZWALD - AUSWEIS", "BLACK FOREST DISTRICT - ID CARD"), new Color(1f, 0.9f, 0.78f), true, true);

        var photo = new Rect(r.x + 7, r.y + 22, 56, 68);
        RetroGUI.Frame(photo, new Color(0.45f, 0.47f, 0.5f), InkLight);
        if (game.PendingPortrait != null)
            GUI.DrawTexture(RetroGUI.R(photo.x + 1, photo.y + 1, photo.width - 2, photo.height - 2), game.PendingPortrait, ScaleMode.ScaleAndCrop);

        float x = photo.xMax + 7, y = r.y + 21;
        Row(x, ref y, Loc.T("NAME", "SURNAME"), card.LastName.ToUpperInvariant());
        Row(x, ref y, Loc.T("VORNAME", "GIVEN NAME"), card.FirstName);
        Row(x, ref y, Loc.T("GEBURTSDATUM", "DATE OF BIRTH"), card.BirthDate.ToString("dd.MM.yyyy"));
        Row(x, ref y, Loc.T("GÜLTIG BIS", "VALID UNTIL"), card.ExpiryDate.ToString("dd.MM.yyyy"));

        RetroGUI.Label(new Rect(r.x + 7, r.y + 94, 120, 10), Loc.T("WOHNBEZIRK", "DISTRICT"), InkLight, false, true);
        RetroGUI.Label(new Rect(r.x + 7, r.y + 102, 140, 12), card.District, Ink, true);
        RetroGUI.Fill(new Rect(r.x + 7, r.yMax - 22, r.width - 14, 1), PaperDark);
        RetroGUI.Label(new Rect(r.x + 7, r.yMax - 18, 60, 10), Loc.T("AUSWEIS-NR.", "ID NO."), InkLight, false, true);
        RetroGUI.Label(new Rect(r.x + 62, r.yMax - 19, 100, 12), card.IdNumber, Ink, true);
        RetroGUI.Label(new Rect(r.xMax - 30, r.yMax - 19, 22, 12), card.Gender == Gender.Male ? "M" : Loc.T("W", "F"), Ink, true, false, TextAnchor.UpperRight);

        RetroGUI.ShadowLabel(new Rect(r.x, r.yMax + 4, r.width, 12), GameKeys.Tag(GameAction.Interact) + Loc.T(" ausblenden", " hide"), new Color(0.75f, 0.75f, 0.75f), false, TextAnchor.UpperLeft);

        // ID scanner upgrade: a little device under the card that beeps at forged numbers and dates.
        if (Progress.Owns("scanner"))
        {
            bool forged = card.Truth == Discrepancy.WrongIdNumber || card.Truth == Discrepancy.WrongExpiry;
            var sr = new Rect(r.x, r.yMax + 18, r.width, 14);
            RetroGUI.Frame(sr, new Color(0.05f, 0.07f, 0.05f), new Color(0.3f, 0.35f, 0.3f));
            RetroGUI.Label(new Rect(sr.x + 5, sr.y + 2, sr.width - 10, 11),
                forged ? Loc.T("PRÜFGERÄT: FÄLSCHUNG ERKANNT", "SCANNER: FORGERY DETECTED") : Loc.T("PRÜFGERÄT: KEINE FÄLSCHUNG", "SCANNER: NO FORGERY"),
                forged ? new Color(1f, 0.35f, 0.3f) : new Color(0.5f, 1f, 0.6f), true, true);
        }
    }

    // The ticket above the ID card: night, direction, number and the stamp field.
    static void DrawTicket(IdCard card)
    {
        var t = new Rect(10, 24, 176, 66);
        var paper = new Color(0.78f, 0.8f, 0.66f);
        var ink = new Color(0.12f, 0.14f, 0.1f);
        var faint = new Color(0.35f, 0.38f, 0.3f);
        RetroGUI.Frame(t, paper, new Color(0.45f, 0.48f, 0.38f), 1f);
        RetroGUI.Fill(new Rect(t.x + 1, t.y + 1, t.width - 2, 11), new Color(0.2f, 0.35f, 0.22f));
        RetroGUI.Label(new Rect(t.x + 4, t.y + 2, t.width - 8, 10), Loc.T("FAHRSCHEIN - LINIE 13 NACHT", "TICKET - LINE 13 NIGHT"), new Color(0.9f, 1f, 0.9f), true, true);

        var night = card.TicketNight;
        string nightText = $"{night:dd.}/{night.AddDays(1):dd.MM.yyyy}";
        RetroGUI.Label(new Rect(t.x + 5, t.y + 14, 60, 10), Loc.T("GÜLTIG NACHT", "VALID NIGHT"), faint, false, true);
        RetroGUI.Label(new Rect(t.x + 5, t.y + 22, 110, 12), nightText, ink, true);
        RetroGUI.Label(new Rect(t.x + 5, t.y + 35, 60, 10), Loc.T("RICHTUNG", "DIRECTION"), faint, false, true);
        RetroGUI.Label(new Rect(t.x + 5, t.y + 43, 110, 12), card.TicketDirection, ink, true);
        RetroGUI.Label(new Rect(t.x + 5, t.y + 55, 110, 10), "NR. " + card.TicketNumber, faint, false, true);

        // Stamp field.
        var stamp = new Rect(t.xMax - 62, t.y + 16, 56, 44);
        RetroGUI.Frame(stamp, new Color(0.72f, 0.74f, 0.6f), faint, 1f);
        if (!string.IsNullOrEmpty(card.TicketStamp))
        {
            var red = new Color(0.75f, 0.1f, 0.08f);
            RetroGUI.Label(new Rect(stamp.x + 3, stamp.y + 8, stamp.width - 6, 10), Loc.T("ENTWERTET", "STAMPED"), red, true, true);
            RetroGUI.Label(new Rect(stamp.x + 3, stamp.y + 20, stamp.width - 6, 10), card.TicketStamp, red, false, true);
        }
        else
        {
            RetroGUI.Label(new Rect(stamp.x + 3, stamp.y + 16, stamp.width - 6, 10), Loc.T("entwerten", "stamp here"), faint, false, true, TextAnchor.UpperCenter);
        }
    }

    static void Row(float x, ref float y, string label, string value)
    {
        RetroGUI.Label(new Rect(x, y, 140, 10), label, InkLight, false, true);
        RetroGUI.Label(new Rect(x, y + 8, 140, 12), value, Ink, true);
        y += 18;
    }
}
