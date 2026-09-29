using System.Collections.Generic;

/// <summary>What the game has to offer grows night by night instead of everything at once.</summary>
public enum Feature
{
    Phone,        // work phone (Q): family, news, police
    Police,       // wanted persons, calling 110
    Shop,         // upgrades on the board computer
    SidePaths,    // dirt tracks to houses in the forest
    Fuel,         // the tank has to be filled at a petrol station
    Fatigue,      // staying awake with energy drinks
    Weapons,      // bat and pistol
    Tickets,      // tickets to check from night 3
    StopTimer,    // being fired when too late at a stop
}

public static class Features
{
    public static int UnlockNight(Feature f) => f switch
    {
        Feature.StopTimer => 1,
        Feature.Phone => 2,
        Feature.Police => 2,
        Feature.Shop => 2,
        Feature.Fuel => 2,
        Feature.Fatigue => 2,
        Feature.SidePaths => 3,
        Feature.Weapons => 3,
        Feature.Tickets => 3,
        _ => 1,
    };

    public static bool Has(Feature f) => Progress.Day >= UnlockNight(f);

    static string Name(Feature f) => f switch
    {
        Feature.Phone => Loc.T("Diensthandy (Q): Nachrichten, News, Notruf", "Work phone (Q): messages, news, emergency call"),
        Feature.Police => Loc.T("Fahndungsliste und Polizei", "Wanted list and police"),
        Feature.Shop => Loc.T("Shop im Bordcomputer", "Shop on the board computer"),
        Feature.Fuel => Loc.T("Tank: an Tankstellen nachfüllen", "Fuel tank: refill at petrol stations"),
        Feature.Fatigue => Loc.T("Müdigkeit: mit Energy-Drinks wach bleiben (X)", "Fatigue: stay awake with energy drinks (X)"),
        Feature.SidePaths => Loc.T("Feldwege zu Häusern im Wald", "Dirt tracks to houses in the forest"),
        Feature.Weapons => Loc.T("Waffen (Schläger, Pistole)", "Weapons (bat, pistol)"),
        Feature.Tickets => Loc.T("Fahrscheine prüfen", "Checking tickets"),
        _ => Loc.T("Zeitlimit bis zur nächsten Haltestelle", "Time limit to the next stop"),
    };

    /// <summary>Everything that is new tonight (for the dispatch mail).</summary>
    public static List<string> NewTonight()
    {
        var list = new List<string>();
        foreach (Feature f in System.Enum.GetValues(typeof(Feature)))
            if (UnlockNight(f) == Progress.Day && Progress.Day > 1) list.Add(Name(f));
        return list;
    }
}
