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
    Duplicate,        // this person already got on the bus tonight - there can't be two of them
    TicketWrongNight, // ticket is for another night (from night 3)
    TicketUsed,       // ticket already stamped
    TicketWrongDirection, // ticket for the other direction
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
    /// <summary>What the person says on their own when they reach the driver (story passengers).</summary>
    public string[] IntroLines;
    /// <summary>Index of the scripted story passenger, or -1.</summary>
    public int StoryIndex = -1;

    // Ticket (from night 3).
    public bool HasTicket;
    public string TicketNumber;
    public DateTime TicketNight;      // evening date the ticket is valid for
    public string TicketDirection;    // "ENDSTATION" is the direction of the bus
    public string TicketStamp;        // non-empty = already used
    /// <summary>Stop (index along the route) where the passenger gets off.</summary>
    public int DestinationIndex = -1;

    public string FullName => FirstName + " " + LastName;
}

/// <summary>
/// The city's resident register the driver checks IDs against, plus the generator
/// for passenger ID cards. Everything is seeded, so a run is reproducible.
/// </summary>
public class CitizenRegistry
{
    /// <summary>Tonight's date (the evening the shift starts). Night 1 is Friday, 13 November 1998.</summary>
    public static DateTime Today => new DateTime(1998, 11, 13).AddDays(Progress.Day - 1);

    public const string BusDirection = "ENDSTATION";

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
    // (male, female, english) job titles
    static readonly (string m, string f, string en, string enF)[] JobTable =
    {
        ("Förster", "Försterin", "forester", "forester"), ("Bäcker", "Bäckerin", "baker", "baker"),
        ("Krankenpfleger", "Krankenschwester", "nurse", "nurse"), ("Lehrer", "Lehrerin", "teacher", "teacher"),
        ("Schreiner", "Schreinerin", "carpenter", "carpenter"), ("Metzger", "Metzgerin", "butcher", "butcher"),
        ("Kellner", "Kellnerin", "waiter", "waitress"), ("Buchhalter", "Buchhalterin", "accountant", "accountant"),
        ("Postbote", "Postbotin", "postman", "postwoman"), ("Pfarrer", "Pastorin", "priest", "pastor"),
        ("Mechaniker", "Mechanikerin", "mechanic", "mechanic"), ("Nachtwächter", "Nachtwächterin", "night watchman", "night watchwoman"),
        ("Bestatter", "Bestatterin", "undertaker", "undertaker"), ("Landwirt", "Landwirtin", "farmer", "farmer"),
        ("Verkäufer", "Verkäuferin", "salesman", "saleswoman"), ("Elektriker", "Elektrikerin", "electrician", "electrician"),
        ("Arzt", "Ärztin", "doctor", "doctor"), ("Holzfäller", "Friseurin", "lumberjack", "hairdresser"),
        ("Student", "Studentin", "student", "student"), ("Fernfahrer", "Schneiderin", "truck driver", "seamstress"),
    };
    static (string m, string f)[] Jobs => JobTable.Select(j => Loc.English ? (j.en, j.enF) : (j.m, j.f)).ToArray();
    static string[] Months => Loc.English
        ? new[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
        : new[] { "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember" };
    public static readonly string[] Destinations =
    {
        "Waldfriedhof", "Forsthaus Eichgrund", "Alte Sägemühle", "Am Moor", "Schwarzer Weiher", "Köhlerhütte",
        "Wolfsschlucht", "Kreuzweg", "Hünengrab", "Birkenhain", "Steinbruch", "Endstation",
    };

    static readonly string[] Districts =
    {
        "Altstadt", "Nordviertel", "Mühlenfeld", "Am Wasserturm", "Südhang", "Lindenau", "Bahnhofsviertel",
    };
    static string[] WantedNotes => new[]
    {
        Loc.T("Fahndung wegen Körperverletzung. Nicht befördern, Leitstelle informieren.", "Wanted for assault. Do not transport, inform dispatch."),
        Loc.T("Flüchtig. Zuletzt gesehen am Bahnhof Nord.", "On the run. Last seen at the north station."),
        Loc.T("Mehrfacher Schwarzfahrer, Beförderungsverbot bis 1999.", "Repeated fare dodger, banned from transport until 1999."),
    };
    static string[] DeceasedNotes => new[]
    {
        Loc.T("Verstorben am 02.03.1996. Grab: Friedhof Friedhofstraße, Reihe 4.", "Died 02.03.1996. Grave: cemetery on Friedhofstraße, row 4."),
        Loc.T("Verstorben 1997 (Verkehrsunfall, Linie 7).", "Died 1997 (traffic accident, line 7)."),
        Loc.T("Verstorben am 31.10.1995. Todesursache unbekannt.", "Died 31.10.1995. Cause of death unknown."),
        Loc.T("Verstorben 1994. Leiche nie gefunden.", "Died 1994. Body never found."),
    };

    public readonly List<Citizen> Citizens = new List<Citizen>();
    readonly HashSet<string> usedNames = new HashSet<string>();   // nobody shows up twice by chance
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
    public IdCard CreatePassengerCard(ICollection<Discrepancy> allowed, Discrepancy? forced = null, double anomalyChance = 0.45, bool withTicket = false)
    {
        Discrepancy kind = forced ?? Discrepancy.None;
        if (!forced.HasValue && rng.NextDouble() < anomalyChance && allowed.Count > 0)
        {
            var options = allowed.Where(d => d != Discrepancy.None).ToList();
            if (options.Count > 0) kind = options[rng.Next(options.Count)];
        }

        var fresh = Citizens.Where(c => !usedNames.Contains(c.FullName)).ToList();
        if (fresh.Count < 10) fresh = Citizens;   // (a very long night)
        Citizen source = kind switch
        {
            Discrepancy.Wanted => fresh.Where(c => c.Status == "GESUCHT").OrderBy(_ => rng.Next()).FirstOrDefault(),
            Discrepancy.Deceased => fresh.Where(c => c.Status == "VERSTORBEN").OrderBy(_ => rng.Next()).FirstOrDefault(),
            Discrepancy.Expired => fresh.Where(c => c.Status == "AKTIV" && c.IdExpiry < Today).OrderBy(_ => rng.Next()).FirstOrDefault(),
            _ => fresh.Where(c => c.Status == "AKTIV" && c.IdExpiry >= Today).OrderBy(_ => rng.Next()).FirstOrDefault(),
        };
        if (source == null) { kind = Discrepancy.None; source = fresh.FirstOrDefault(c => c.Status == "AKTIV" && c.IdExpiry >= Today) ?? Citizens[0]; }
        usedNames.Add(source.FullName);

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
        if (withTicket) WriteTicket(card);
        return card;
    }

    /// <summary>
    /// The same person again: identical papers and answers as someone who already got on
    /// tonight. Only the driver's memory (and the search history) can tell.
    /// </summary>
    public IdCard CreateDuplicate(IdCard original)
    {
        return new IdCard
        {
            FirstName = original.FirstName,
            LastName = original.LastName,
            Gender = original.Gender,
            BirthDate = original.BirthDate,
            IdNumber = original.IdNumber,
            District = original.District,
            ExpiryDate = original.ExpiryDate,
            Truth = Discrepancy.Duplicate,
            HasTicket = original.HasTicket,
            TicketNumber = original.TicketNumber,
            TicketNight = original.TicketNight,
            TicketDirection = original.TicketDirection,
            TicketStamp = original.TicketStamp,
            SaidName = original.SaidName,
            SaidBirth = original.SaidBirth,
            SaidHome = original.SaidHome,
            SaidJob = original.SaidJob,
            SaidDestination = rng.NextDouble() < 0.5 ? original.SaidDestination : Loc.T("Zurück. Ich habe etwas vergessen.", "Back. I forgot something."),
        };
    }

    // ------------------------------------------------------------------ tickets

    static readonly string[] OtherDirections = { "STADTMITTE", "BAHNHOF NORD", "DEPOT" };

    void WriteTicket(IdCard card)
    {
        card.HasTicket = true;
        card.TicketNumber = "N13-" + rng.Next(100000, 999999);
        card.TicketNight = Today;
        card.TicketDirection = BusDirection;
        card.TicketStamp = "";
        switch (card.Truth)
        {
            case Discrepancy.TicketWrongNight:
                card.TicketNight = Today.AddDays(rng.NextDouble() < 0.7 ? -rng.Next(1, 4) : rng.Next(1, 3));
                break;
            case Discrepancy.TicketUsed:
                var when = Today.AddDays(-rng.Next(0, 3));
                card.TicketStamp = $"{when:dd.MM.} {rng.Next(20, 24)}:{rng.Next(0, 60):00}";
                break;
            case Discrepancy.TicketWrongDirection:
                card.TicketDirection = OtherDirections[rng.Next(OtherDirections.Length)];
                break;
        }
    }

    // ------------------------------------------------------------------ talking

    string JobFor(Gender g, DateTime birth)
    {
        int age = Today.Year - birth.Year;
        if (age >= 66) return Loc.English ? "retired" : g == Gender.Male ? "Rentner" : "Rentnerin";
        var j = Pick(Jobs);
        return g == Gender.Male ? j.m : j.f;
    }

    public static string SpokenDate(DateTime d) => Loc.English ? $"{Months[d.Month - 1]} {d.Day}, {d.Year}" : $"{d.Day}. {Months[d.Month - 1]} {d.Year}";

    static string HomePhrase(string district) =>
        Loc.English ? "In " + district : district.StartsWith("Am ") ? district : "In " + district;

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

        string article = Loc.English && job.Length > 0 && "aeiou".IndexOf(job[0]) >= 0 ? "an " : "a ";
        if (job == "retired") article = "";
        card.SaidName = card.FullName + ".";
        card.SaidBirth = Loc.T("Am ", "On ") + SpokenDate(birth) + ".";
        card.SaidHome = HomePhrase(home) + ".";
        card.SaidJob = Loc.T("Ich bin " + job + ".", "I'm " + article + job + ".");
        card.SaidDestination = Loc.T("Zur Haltestelle ", "To the stop ") + Pick(Destinations) + ".";

        switch (card.Truth)
        {
            case Discrepancy.Doppelganger:
                if (rng.NextDouble() < 0.5) card.SaidJob = Loc.T("Ich... bin... " + job + ". Ja. " + job + ".", "I... am... " + article + job + ". Yes. " + article + job + ".");
                if (rng.NextDouble() < 0.4) card.SaidDestination = Loc.T("Dahin, wo Sie auch hinfahren.", "Wherever you are going.");
                break;
            case Discrepancy.Deceased:
                card.SaidHome = rng.NextDouble() < 0.5 ? Loc.T("Am Waldfriedhof. Reihe vier.", "At the Waldfriedhof. Row four.") : HomePhrase(home) + Loc.T(". Früher.", ". Once.");
                card.SaidDestination = Loc.T("Nach Hause. Endlich nach Hause.", "Home. Finally home.");
                break;
            case Discrepancy.Wanted:
                if (rng.NextDouble() < 0.5) card.SaidDestination = Loc.T("Weg. Einfach nur weg hier.", "Away. Just away from here.");
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
