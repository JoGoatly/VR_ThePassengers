using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A small shop window (petrol station, home...): a list of things to buy with the mouse.
/// Esc or E closes it.
/// </summary>
public class ShopMenu : MonoBehaviour
{
    public class Item
    {
        public string name, description;
        public int price;
        public System.Func<bool> available;     // null = always
        public System.Func<string> buy;         // returns the toast text
    }

    static ShopMenu instance;
    public AudioClip buySound, errorSound;

    string title;
    List<Item> items;
    bool open;
    int openedFrame, hover = -1;
    string message;
    float messageUntil;

    void Awake() => instance = this;

    public static bool IsOpen => instance != null && instance.open;

    public static void Open(string title, List<Item> items)
    {
        if (instance == null) return;
        instance.title = title;
        instance.items = items;
        instance.open = true;
        instance.openedFrame = Time.frameCount;
        instance.message = null;
        GameUI.MinigameOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        open = false;
        GameUI.MinigameOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!open || Time.frameCount <= openedFrame + 1) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || GameKeys.Pressed(GameAction.Interact))) Close();
    }

    void Play(AudioClip clip)
    {
        var sm = FindAnyObjectByType<SoundManager>();
        if (sm != null && clip != null && Camera.main != null) sm.PlayWorld(clip, Camera.main.transform.position, 0.7f, 0f);
    }

    void OnGUI()
    {
        if (!open || items == null) return;
        GUI.depth = -340;
        float w = RetroGUI.VirtualWidth;
        var box = new Rect(w / 2 - 170, 50, 340, 40 + items.Count * 24 + 30);
        RetroGUI.Panel(box, 3);
        RetroGUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, 14), title, new Color(1f, 0.85f, 0.5f), true);
        RetroGUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, 14), $"{Progress.Money} €", new Color(0.7f, 1f, 0.7f), true, false, TextAnchor.UpperRight);
        hover = -1;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            bool avail = it.available == null || it.available();
            var r = new Rect(box.x + 10, box.y + 26 + i * 24, box.width - 20, 21);
            if (!avail) continue;
            bool affordable = Progress.Money >= it.price;
            if (RetroGUI.Button(r, "", affordable ? new Color(0.12f, 0.1f, 0.1f, 0.85f) : new Color(0.08f, 0.06f, 0.06f, 0.85f), Color.white))
            {
                if (!affordable) { message = Loc.T("Nicht genug Geld.", "Not enough money."); messageUntil = Time.time + 2f; Play(errorSound); }
                else
                {
                    Progress.AddMoney(-it.price);
                    message = it.buy != null ? it.buy() : it.name;
                    messageUntil = Time.time + 2.5f;
                    Progress.Save();
                    Play(buySound);
                }
            }
            RetroGUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 60, 11), it.name, affordable ? Color.white : new Color(0.5f, 0.5f, 0.5f), true, true);
            RetroGUI.Label(new Rect(r.x + 6, r.y + 10, r.width - 60, 11), it.description, new Color(0.7f, 0.7f, 0.65f), false, true);
            RetroGUI.Label(new Rect(r.xMax - 60, r.y + 4, 54, 12), $"{it.price} €", affordable ? new Color(1f, 0.85f, 0.4f) : new Color(0.5f, 0.4f, 0.3f), true, false, TextAnchor.UpperRight);
        }
        if (message != null && Time.time < messageUntil)
            RetroGUI.Label(new Rect(box.x + 10, box.yMax - 24, box.width - 20, 12), message, new Color(0.6f, 1f, 0.6f), false, true);
        RetroGUI.Label(new Rect(box.x, box.yMax - 12, box.width - 8, 10), Loc.T("Schließen [ESC]", "Close [ESC]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
    }

    /// <summary>What the petrol station sells.</summary>
    public static List<Item> PetrolStationItems()
    {
        return new List<Item>
        {
            new Item { name = Loc.T("Energy-Drink", "Energy drink"), description = Loc.T("Hält wach (+45%)", "Keeps you awake (+45%)"), price = 3,
                       buy = () => { Progress.Data.energyDrinks++; return Loc.T($"Energy-Drinks: {Progress.Data.energyDrinks}", $"Energy drinks: {Progress.Data.energyDrinks}"); } },
            new Item { name = Loc.T("Kaffee", "Coffee"), description = Loc.T("Sofort etwas wacher, heilt ein bisschen", "A bit more awake right away, heals a little"), price = 1,
                       buy = () => { ShiftRules.Instance?.WakeUp(0.15f); PlayerCombat.Instance?.Heal(10); return Loc.T("Heiß und bitter.", "Hot and bitter."); } },
            new Item { name = Loc.T("Sandwich", "Sandwich"), description = Loc.T("Heilt 35", "Heals 35"), price = 4,
                       buy = () => { PlayerCombat.Instance?.Heal(35); return Loc.T("Das Sandwich ist von gestern. Oder von 1994.", "The sandwich is from yesterday. Or from 1994."); } },
            new Item { name = Loc.T("Verbandskasten", "First aid kit"), description = Loc.T("Heilen mit [H]", "Heal with [H]"), price = 8,
                       buy = () => { Progress.AddMedkits(1); return Loc.T($"Verbandskästen: {Progress.Data.medkits}", $"First aid kits: {Progress.Data.medkits}"); } },
            new Item { name = Loc.T("Munition", "Ammunition"), description = Loc.T("6 Schuss", "6 rounds"), price = 6, available = () => Features.Has(Feature.Weapons),
                       buy = () => { Progress.AddAmmo(6); return Loc.T($"Munition: {Progress.Data.ammo}", $"Ammo: {Progress.Data.ammo}"); } },
            new Item { name = Loc.T("Rubbellos", "Scratch card"), description = Loc.T("Vielleicht ist heute dein Glückstag", "Maybe today is your lucky day"), price = 2,
                       buy = () =>
                       {
                           if (Random.value < 0.08f) { Progress.AddMoney(40); return Loc.T("GEWONNEN! +40 €", "YOU WIN! +40 €"); }
                           if (Random.value < 0.1f) return Loc.T("Unter dem Rubbelfeld steht: \"13\". Nur \"13\".", "Under the scratch field it says: \"13\". Just \"13\".");
                           return Loc.T("Niete.", "No luck.");
                       } },
        };
    }
}
