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
    }

    static void Row(float x, ref float y, string label, string value)
    {
        RetroGUI.Label(new Rect(x, y, 140, 10), label, InkLight, false, true);
        RetroGUI.Label(new Rect(x, y + 8, 140, 12), value, Ink, true);
        y += 18;
    }
}
