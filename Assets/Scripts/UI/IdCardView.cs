using UnityEngine;

/// <summary>
/// Shows the ID card of the passenger at the door: a small card in the corner,
/// and (E) a large card with the photo and the decision buttons.
/// </summary>
public class IdCardView : MonoBehaviour
{
    public BoardingManager game;

    static readonly Color Paper = new Color(0.86f, 0.84f, 0.74f);
    static readonly Color PaperDark = new Color(0.7f, 0.72f, 0.62f);
    static readonly Color Ink = new Color(0.12f, 0.12f, 0.16f);
    static readonly Color InkLight = new Color(0.35f, 0.35f, 0.38f);
    static readonly Color Stripe = new Color(0.55f, 0.18f, 0.16f);

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
    }

    void Update()
    {
        if (game == null || game.PendingCard == null) GameUI.IdCardOpen = false;
    }

    void OnGUI()
    {
        if (game == null || GameUI.ComputerOpen) return;
        var card = game.PendingCard;
        if (card == null) return;

        float vw = RetroGUI.VirtualWidth;
        if (!GameUI.IdCardOpen)
        {
            // Small card: the passenger is holding it up at the door.
            var small = new Rect(vw - 150, 250, 140, 88);
            DrawCard(small, card, 0.55f);
            RetroGUI.ShadowLabel(new Rect(small.x, small.y - 14, small.width, 12), "Ausweis ansehen [E]", Color.white, false);
            return;
        }

        RetroGUI.Fill(new Rect(0, 0, vw, RetroGUI.VirtualHeight), new Color(0, 0, 0, 0.5f));
        var big = new Rect((vw - 300) * 0.5f, 60, 300, 190);
        DrawCard(big, card, 1f);

        float by = big.yMax + 12;
        if (RetroGUI.Button(new Rect(big.x, by, 145, 22), "EINSTEIGEN LASSEN [J]", new Color(0.15f, 0.4f, 0.2f), Color.white))
            game.Decide(true);
        if (RetroGUI.Button(new Rect(big.xMax - 145, by, 145, 22), "ABWEISEN [N]", new Color(0.45f, 0.12f, 0.1f), Color.white))
            game.Decide(false);
        RetroGUI.ShadowLabel(new Rect(big.x, by + 28, big.width, 12), "Register prüfen: [Tab]     Schließen: [E]", new Color(0.8f, 0.8f, 0.8f), false);
    }

    void DrawCard(Rect r, IdCard card, float scale)
    {
        RetroGUI.Frame(r, Paper, PaperDark, 2f);
        RetroGUI.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 22 * scale), Stripe);
        if (scale >= 1f)
        {
            RetroGUI.Label(new Rect(r.x + 8, r.y + 5, r.width - 16, 12), "STADT RAVENSBRÜCK  -  PERSONALAUSWEIS", new Color(1f, 0.92f, 0.8f), true);
        }
        else
        {
            RetroGUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 10), "PERSONALAUSWEIS", new Color(1f, 0.92f, 0.8f), true, true);
        }

        // Photo.
        float pw = 70 * scale, ph = 82 * scale;
        var photo = new Rect(r.x + 8 * scale, r.y + 30 * scale, pw, ph);
        RetroGUI.Frame(photo, new Color(0.55f, 0.58f, 0.6f), InkLight);
        if (game.PendingPortrait != null)
            GUI.DrawTexture(RetroGUI.R(photo.x + 1, photo.y + 1, photo.width - 2, photo.height - 2), game.PendingPortrait, ScaleMode.ScaleAndCrop);

        float x = photo.xMax + 8 * scale;
        float y = r.y + 30 * scale;
        if (scale < 1f)
        {
            RetroGUI.Label(new Rect(x, y, r.xMax - x - 4, 10), card.LastName.ToUpperInvariant(), Ink, true, true);
            RetroGUI.Label(new Rect(x, y + 10, r.xMax - x - 4, 10), card.FirstName, Ink, false, true);
            return;
        }

        Row(x, ref y, "NAME", card.LastName.ToUpperInvariant());
        Row(x, ref y, "VORNAME", card.FirstName);
        Row(x, ref y, "GEBURTSDATUM", card.BirthDate.ToString("dd.MM.yyyy"));
        Row(x, ref y, "WOHNBEZIRK", card.District);
        Row(x, ref y, "GÜLTIG BIS", card.ExpiryDate.ToString("dd.MM.yyyy"));

        RetroGUI.Fill(new Rect(r.x + 8, r.yMax - 30, r.width - 16, 1), PaperDark);
        RetroGUI.Label(new Rect(r.x + 8, r.yMax - 24, 120, 12), "AUSWEIS-NR.", InkLight, false, true);
        RetroGUI.Label(new Rect(r.x + 80, r.yMax - 26, 200, 14), card.IdNumber, Ink, true);
        RetroGUI.Label(new Rect(r.xMax - 48, r.yMax - 26, 40, 14), card.Gender == Gender.Male ? "M" : "W", Ink, true, false, TextAnchor.UpperRight);
    }

    static void Row(float x, ref float y, string label, string value)
    {
        RetroGUI.Label(new Rect(x, y, 200, 10), label, InkLight, false, true);
        RetroGUI.Label(new Rect(x, y + 9, 200, 12), value, Ink, true);
        y += 23;
    }
}
