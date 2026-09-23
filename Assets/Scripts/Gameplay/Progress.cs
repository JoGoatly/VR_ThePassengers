using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything that survives from one night to the next (saved in PlayerPrefs).</summary>
[Serializable]
public class SaveData
{
    public int day = 1;
    public int money = 0;
    public List<string> owned = new List<string>();
    public int ammo = 0;
    public int medkits = 0;
    public List<int> notes = new List<int>();     // story notes found
    public List<int> drivers = new List<int>();   // missing drivers found
    public List<string> secrets = new List<string>(); // easter eggs found in the depot
}

/// <summary>Something that can be bought on the board computer.</summary>
public class ShopItem
{
    public string id;
    public string nameDe, nameEn, descDe, descEn;
    public int price;
    public bool consumable;
    public string requires;
    public string Name => Loc.T(nameDe, nameEn);
    public string Description => Loc.T(descDe, descEn);
}

/// <summary>
/// Night, money, upgrades, items and story progress. The shift statistics are only kept
/// for the current night.
/// </summary>
public static class Progress
{
    public const int LastDay = 7;
    const string Key = "save_v1";

    static SaveData data;
    public static SaveData Data => data ??= Load();

    public static int Day => Mathf.Clamp(Data.day, 1, LastDay);
    public static int Money => Data.money;
    public static bool HasSave => PlayerPrefs.HasKey(Key);

    // This night only.
    public static int ShiftEarned, ShiftFines, ShiftCorrect, ShiftWrong, ShiftFound;

    public static event Action Changed;

    static SaveData Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new SaveData();
        try { return JsonUtility.FromJson<SaveData>(json) ?? new SaveData(); }
        catch { return new SaveData(); }
    }

    public static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        data = null;
    }

    public static void NewGame()
    {
        data = new SaveData();
        Save();
        ResetShift();
    }

    public static void ResetShift()
    {
        ShiftEarned = ShiftFines = ShiftCorrect = ShiftWrong = ShiftFound = 0;
    }

    public static void AddMoney(int amount)
    {
        Data.money = Mathf.Max(0, Data.money + amount);
        Changed?.Invoke();
    }

    public static bool Owns(string id) => Data.owned.Contains(id);

    public static void AddAmmo(int n) { Data.ammo = Mathf.Max(0, Data.ammo + n); Changed?.Invoke(); }
    public static void AddMedkits(int n) { Data.medkits = Mathf.Max(0, Data.medkits + n); Changed?.Invoke(); }

    public static bool FoundNote(int id)
    {
        if (Data.notes.Contains(id)) return false;
        Data.notes.Add(id);
        Changed?.Invoke();
        return true;
    }

    /// <summary>An easter egg was discovered (true the first time).</summary>
    public static bool FoundSecret(string id)
    {
        Data.secrets ??= new List<string>();
        if (Data.secrets.Contains(id)) return false;
        Data.secrets.Add(id);
        Changed?.Invoke();
        return true;
    }

    public static int SecretsFound => Data.secrets?.Count ?? 0;

    public static bool FoundDriver(int id)
    {
        if (Data.drivers.Contains(id)) return false;
        Data.drivers.Add(id);
        Changed?.Invoke();
        return true;
    }

    // ---------------------------------------------------------------- shop

    public static readonly ShopItem[] Shop =
    {
        new ShopItem { id = "flashlight2", price = 60, nameDe = "Starke Taschenlampe", nameEn = "Strong flashlight",
            descDe = "Hellere Taschenlampe mit größerer Reichweite.", descEn = "Brighter flashlight with a longer reach." },
        new ShopItem { id = "medkit", price = 35, consumable = true, nameDe = "Verbandskasten", nameEn = "First aid kit",
            descDe = "Heilt 50 Lebenspunkte (Taste H).", descEn = "Heals 50 health (key H)." },
        new ShopItem { id = "bat", price = 80, nameDe = "Baseballschläger", nameEn = "Baseball bat",
            descDe = "Nahkampfwaffe. Taste 2, Linksklick schlägt.", descEn = "Melee weapon. Key 2, left click swings." },
        new ShopItem { id = "pistol", price = 220, nameDe = "Pistole (+6 Schuss)", nameEn = "Pistol (+6 rounds)",
            descDe = "Schusswaffe. Taste 3, Linksklick schießt.", descEn = "Firearm. Key 3, left click shoots." },
        new ShopItem { id = "ammo", price = 30, consumable = true, requires = "pistol", nameDe = "Munition (6 Schuss)", nameEn = "Ammo (6 rounds)",
            descDe = "Für die Pistole.", descEn = "For the pistol." },
        new ShopItem { id = "scanner", price = 150, nameDe = "Ausweis-Prüfgerät", nameEn = "ID scanner",
            descDe = "Erkennt gefälschte Ausweisnummern und Ablaufdaten.", descEn = "Detects forged ID numbers and expiry dates." },
        new ShopItem { id = "highbeam2", price = 90, nameDe = "Xenon-Fernlicht", nameEn = "Xenon high beam",
            descDe = "Das Fernlicht reicht deutlich weiter.", descEn = "The high beam reaches much further." },
    };

    public static bool CanBuy(ShopItem item)
    {
        if (Money < item.price) return false;
        if (!item.consumable && Owns(item.id)) return false;
        if (!string.IsNullOrEmpty(item.requires) && !Owns(item.requires)) return false;
        return true;
    }

    public static bool Buy(ShopItem item)
    {
        if (!CanBuy(item)) return false;
        Data.money -= item.price;
        switch (item.id)
        {
            case "medkit": Data.medkits++; break;
            case "ammo": Data.ammo += 6; break;
            case "pistol": Data.owned.Add(item.id); Data.ammo += 6; break;
            default: Data.owned.Add(item.id); break;
        }
        Save();
        Changed?.Invoke();
        return true;
    }
}
