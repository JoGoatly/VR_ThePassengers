using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What is wrong with an ID card (None = passenger may board).</summary>
public enum Discrepancy
{
    None,
    Expired,          // card expired
    WrongBirthDate,   // birth date on the card differs from the register
    WrongIdNumber,    // ID number on the card differs from the register
    NotRegistered,    // name not in the register (often a slightly misspelled real name)
    Wanted,           // register status GESUCHT
    Deceased,         // register status VERSTORBEN
    WrongExpiry,      // expiry date on the card differs from the register (forged)
    Doppelganger,     // papers perfect, but the person gets facts about themselves wrong when asked
}

public enum Gender { Male, Female }

public class Citizen
{
    public string FirstName;
    public string LastName;
    public Gender Gender;
    public DateTime BirthDate;
    public string IdNumber;
    public string District;
    public string Status;     // AKTIV, GESUCHT, VERSTORBEN
    public string Note;
    public string Occupation;
    public DateTime IdExpiry;

    public string FullName => FirstName + " " + LastName;
}

public class IdCard
{
    public string FirstName;
    public string LastName;
    public Gender Gender;
    public DateTime BirthDate;
    public string IdNumber;
    public string District;
    public DateTime ExpiryDate;
    public Discrepancy Truth;   // hidden from the player

    // What the person says when asked (the driver can compare it with the register).
    public string SaidName, SaidBirth, SaidHome, SaidJob, SaidDestination;

    public string FullName => FirstName + " " + LastName;
}

/// <summary>
/// The city's resident register the driver checks IDs against, plus the generator
/// for passenger ID cards. Everything is seeded, so a run is reproducible.
/// </summary>
public class CitizenRegistry
{
    public static readonly DateTime Today = new DateTime(1998, 11, 13);

    static readonly string[] MaleNames =
    {
        "Thomas", "Michael", "Andreas", "Stefan", "Frank", "Jürgen", "Klaus", "Uwe", "Ralf", "Dieter",
        "Markus", "Holger", "Bernd", "Werner", "Karl", "Heinz", "Jens", "Sven", "Dirk", "Günter", "Horst", "Rainer",
    };
    static readonly string[] FemaleNames =
    {
        "Sabine", "Petra", "Monika", "Andrea", "Claudia", "Susanne", "Birgit", "Karin", "Ute", "Heike",
        "Anja", "Silke", "Renate", "Ingrid", "Gisela", "Martina", "Kerstin", "Doris", "Ursula", "Elke", "Brigitte", "Nicole",
    };
    static readonly string[] LastNames =
    {
        "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann",
        "Koch", "Richter", "Klein", "Wolf", "Schröder", "Neumann", "Schwarz", "Zimmermann", "Braun", "Krüger",
        "Hofmann", "Hartmann", "Lange", "Werner", "Krause", "Lehmann", "Köhler", "Maier", "Huber", "Kaiser",
        "Fuchs", "Peters", "Lang", "Scholz", "Möller", "Weiß", "Jung", "Hahn", "Vogel", "Friedrich", "Keller", "Brandt",
    };
    // (male, female) job titles
    static readonly (string m, string f)[] Jobs =
    {
        ("Förster", "Försterin"), ("Bäcker", "Bäckerin"), ("Krankenpfleger", "Krankenschwester"), ("Lehrer", "Lehrerin"),
        ("Schreiner", "Schreinerin"), ("Metzger", "Metzgerin"), ("Kellner", "Kellnerin"), ("Buchhalter", "Buchhalterin"),
        ("Postbote", "Postbotin"), ("Pfarrer", "Pastorin"), ("Mechaniker", "Mechanikerin"), ("Nachtwächter", "Nachtwächterin"),
        ("Bestatter", "Bestatterin"), ("Landwirt", "Landwirtin"), ("Verkäufer", "Verkäuferin"), ("Elektriker", "Elektrikerin"),
        ("Arzt", "Ärztin"), ("Holzfäller", "Friseurin"), ("Student", "Studentin"), ("Fernfahrer", "Schneiderin"),
    };
    static readonly string[] Months =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember",
    };
    public static readonly string[] Destinations =
    {
        "Waldfriedhof", "Forsthaus Eichgrund", "Alte Sägemühle", "Am Moor", "Schwarzer Weiher", "Köhlerhütte",
        "Wolfsschlucht", "Kreuzweg", "Hünengrab", "Birkenhain", "Steinbruch", "Endstation",
    };

    static readonly string[] Districts =
    {
        "Altstadt", "Nordviertel", "Mühlenfeld", "Am Wasserturm", "Südhang", "Lindenau", "Bahnhofsviertel",
    };
    static readonly string[] WantedNotes =
    {
        "Fahndung wegen Körperverletzung. Nicht befördern, Leitstelle informieren.",
        "Flüchtig. Zuletzt gesehen am Bahnhof Nord.",
        "Mehrfacher Schwarzfahrer, Beförderungsverbot bis 1999.",
    };
    static readonly string[] DeceasedNotes =
    {
        "Verstorben am 02.03.1996. Grab: Friedhof Friedhofstraße, Reihe 4.",
        "Verstorben 1997 (Verkehrsunfall, Linie 7).",
        "Verstorben am 31.10.1995. Todesursache unbekannt.",
        "Verstorben 1994. Leiche nie gefunden.",
    };

    public readonly List<Citizen> Citizens = new List<Citizen>();
    readonly Random rng;

    public CitizenRegistry(int seed, int size = 90)
    {
        rng = new Random(seed);
        var used = new HashSet<string>();
        while (Citizens.Count < size)
        {
            var c = NewCitizen();
            if (!used.Add(c.FullName)) continue;
            Citizens.Add(c);
        }
    }

    Citizen NewCitizen()
    {
        var gender = rng.NextDouble() < 0.5 ? Gender.Male : Gender.Female;
        var c = new Citizen
        {
            Gender = gender,
            FirstName = Pick(gender == Gender.Male ? MaleNames : FemaleNames),
            LastName = Pick(LastNames),
            BirthDate = Today.AddDays(-rng.Next(18 * 365, 80 * 365)),
            IdNumber = NewIdNumber(),
            District = Pick(Districts),
            Status = "AKTIV",
            Note = "",
        };
        c.Occupation = JobFor(gender, c.BirthDate);
        c.IdExpiry = rng.NextDouble() < 0.85 ? Today.AddDays(rng.Next(60, 8 * 365)) : Today.AddDays(-rng.Next(3, 900));
        double r = rng.NextDouble();
        if (r < 0.07) { c.Status = "VERSTORBEN"; c.Note = Pick(DeceasedNotes); }
        else if (r < 0.12) { c.Status = "GESUCHT"; c.Note = Pick(WantedNotes); }
        return c;
    }

    string NewIdNumber()
    {
        const string letters = "CFGHJKLMNPRTVWXYZ";
        return letters[rng.Next(letters.Length)] + rng.Next(1000000, 9999999).ToString();
    }

    T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

    public IEnumerable<Citizen> Search(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();
        if (query.Length == 0) return Enumerable.Empty<Citizen>();
        var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return Citizens
            .Where(c => words.All(w => c.FullName.ToLowerInvariant().Contains(w)))
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName);
    }

    /// <summary>
    /// Creates the ID card of the next passenger. allowed = discrepancies the player
    /// already knows the rules for (from the mails).
    /// </summary>
    public IdCard CreatePassengerCard(ICollection<Discrepancy> allowed)
    {
        Discrepancy kind = Discrepancy.None;
        if (rng.NextDouble() > 0.55 && allowed.Count > 0)
        {
            var options = allowed.Where(d => d != Discrepancy.None).ToList();
            if (options.Count > 0) kind = options[rng.Next(options.Count)];
        }

        Citizen source = kind switch
        {
            Discrepancy.Wanted => Citizens.Where(c => c.Status == "GESUCHT").OrderBy(_ => rng.Next()).FirstOrDefault(),
            Discrepancy.Deceased => Citizens.Where(c => c.Status == "VERSTORBEN").OrderBy(_ => rng.Next()).FirstOrDefault(),
            Discrepancy.Expired => Citizens.Where(c => c.Status == "AKTIV" && c.IdExpiry < Today).OrderBy(_ => rng.Next()).FirstOrDefault(),
            _ => Citizens.Where(c => c.Status == "AKTIV" && c.IdExpiry >= Today).OrderBy(_ => rng.Next()).First(),
        };
        if (source == null) { kind = Discrepancy.None; source = Citizens.First(c => c.Status == "AKTIV" && c.IdExpiry >= Today); }

        var card = new IdCard
        {
            FirstName = source.FirstName,
            LastName = source.LastName,
            Gender = source.Gender,
            BirthDate = source.BirthDate,
            IdNumber = source.IdNumber,
            District = source.District,
            ExpiryDate = source.IdExpiry,
            Truth = kind,
        };
        if (kind != Discrepancy.Expired && card.ExpiryDate < Today) card.ExpiryDate = Today.AddDays(rng.Next(60, 900));

        switch (kind)
        {
            case Discrepancy.Expired:
                break;   // card and register agree: both expired
            case Discrepancy.WrongExpiry:
                card.ExpiryDate = source.IdExpiry.AddYears(rng.Next(1, 4)).AddDays(rng.Next(-20, 20));
                break;
            case Discrepancy.WrongBirthDate:
                card.BirthDate = rng.NextDouble() < 0.5
                    ? source.BirthDate.AddYears(rng.NextDouble() < 0.5 ? -1 : 1)
                    : source.BirthDate.AddDays(rng.NextDouble() < 0.5 ? -rng.Next(1, 40) : rng.Next(1, 40));
                break;
            case Discrepancy.WrongIdNumber:
                card.IdNumber = MutateIdNumber(source.IdNumber);
                break;
            case Discrepancy.NotRegistered:
                MakeUnregisteredName(card);
                break;
        }
        WriteAnswers(card, source);
        return card;
    }

    // ------------------------------------------------------------------ talking

    string JobFor(Gender g, DateTime birth)
    {
        int age = Today.Year - birth.Year;
        if (age >= 66) return g == Gender.Male ? "Rentner" : "Rentnerin";
        var j = Pick(Jobs);
        return g == Gender.Male ? j.m : j.f;
    }

    public static string SpokenDate(DateTime d) => $"{d.Day}. {Months[d.Month - 1]} {d.Year}";

    static string HomePhrase(string district) =>
        district.StartsWith("Am ") ? district : "In " + district;

    void WriteAnswers(IdCard card, Citizen source)
    {
        string job = source.Occupation;
        string home = source.District;
        DateTime birth = card.BirthDate;

        if (card.Truth == Discrepancy.Doppelganger)
        {
            // Everything on paper is right - but it doesn't know its own life.
            switch (rng.Next(3))
            {
                case 0: birth = birth.AddYears(rng.NextDouble() < 0.5 ? -rng.Next(1, 6) : rng.Next(1, 6)).AddDays(rng.Next(-60, 60)); break;
                case 1: do home = Pick(Districts); while (home == source.District); break;
                default: do { var j = Pick(Jobs); job = source.Gender == Gender.Male ? j.m : j.f; } while (job == source.Occupation); break;
            }
        }

        card.SaidName = card.FullName + ".";
        card.SaidBirth = "Am " + SpokenDate(birth) + ".";
        card.SaidHome = HomePhrase(home) + ".";
        card.SaidJob = "Ich bin " + job + ".";
        card.SaidDestination = "Zur Haltestelle " + Pick(Destinations) + ".";

        switch (card.Truth)
        {
            case Discrepancy.Doppelganger:
                if (rng.NextDouble() < 0.5) card.SaidJob = "Ich... bin... " + job + ". Ja. " + job + ".";
                if (rng.NextDouble() < 0.4) card.SaidDestination = "Dahin, wo Sie auch hinfahren.";
                break;
            case Discrepancy.Deceased:
                card.SaidHome = rng.NextDouble() < 0.5 ? "Am Waldfriedhof. Reihe vier." : HomePhrase(home) + ". Früher.";
                card.SaidDestination = "Nach Hause. Endlich nach Hause.";
                break;
            case Discrepancy.Wanted:
                if (rng.NextDouble() < 0.5) card.SaidDestination = "Weg. Einfach nur weg hier.";
                break;
        }
    }

    string MutateIdNumber(string id)
    {
        var chars = id.ToCharArray();
        int changes = rng.Next(1, 3);
        for (int i = 0; i < changes; i++)
        {
            int pos = rng.Next(1, chars.Length);
            char old = chars[pos];
            while (chars[pos] == old) chars[pos] = (char)('0' + rng.Next(10));
        }
        return new string(chars);
    }

    // A real name with one letter changed (Meyer -> Meier), or a completely unknown name.
    void MakeUnregisteredName(IdCard card)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string last = card.LastName;
            if (rng.NextDouble() < 0.7 && last.Length > 3)
            {
                const string swaps = "aeioulnrst";
                var chars = last.ToCharArray();
                int pos = rng.Next(1, chars.Length);
                chars[pos] = swaps[rng.Next(swaps.Length)];
                last = new string(chars);
            }
            else
            {
                last = Pick(LastNames);
            }

            if (Citizens.All(c => !(c.LastName == last && c.FirstName == card.FirstName)))
            {
                card.LastName = last;
                return;
            }
        }
        card.LastName = card.LastName + "-" + Pick(LastNames);
    }

    public Citizen Find(IdCard card) =>
        Citizens.FirstOrDefault(c => c.FirstName == card.FirstName && c.LastName == card.LastName);
}
