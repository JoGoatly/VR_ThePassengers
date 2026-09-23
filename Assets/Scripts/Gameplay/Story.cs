/// <summary>All story texts: notes, the missing drivers, what each night starts with, endings.</summary>
public static class Story
{
    public class Driver
    {
        public string name, badge, lastWordsDe, lastWordsEn;
    }

    // The four drivers of line 13 who never came back (the newspaper mentions them).
    public static readonly Driver[] Drivers =
    {
        new Driver { name = "Karl Weber", badge = "VBN 0412 - LINIE 13 - SEIT 1989",
            lastWordsDe = "Sie steigen ein, aber nie aus. Ich habe gezählt. Jede Nacht einer mehr.\nIch fahre nicht mehr. Ich bleibe hier unten, hier finden sie mich nicht.",
            lastWordsEn = "They get on, but never off. I counted. Every night one more.\nI won't drive anymore. I'll stay down here, they won't find me here." },
        new Driver { name = "Dieter Hahn", badge = "VBN 0877 - LINIE 13 - SEIT 1995",
            lastWordsDe = "Die Leitstelle hat meine Kündigung abgelehnt. Dreimal.\nHeute Nacht stand meine Frau an der Haltestelle. Meine Frau ist seit 1993 tot. Ich habe sie einsteigen lassen.",
            lastWordsEn = "Dispatch refused my resignation. Three times.\nTonight my wife stood at the bus stop. My wife has been dead since 1993. I let her on." },
        new Driver { name = "Uwe Braun", badge = "VBN 1024 - LINIE 13 - SEIT 1997",
            lastWordsDe = "Das Register ist nicht das Einwohnerregister. Es ist eine Liste.\nEine Liste von allen, die der Wald schon hat. Mein Name steht jetzt drin.",
            lastWordsEn = "The register is not the residents' register. It is a list.\nA list of everyone the forest already has. My name is in it now." },
        new Driver { name = "Jens Keller", badge = "VBN 1101 - LINIE 13 - SEIT 1998",
            lastWordsDe = "An meinen Nachfolger: Die Leitstelle hat keine Adresse. Ich bin hingefahren. Da ist nur Wald.\nFahr die sieben Nächte zu Ende. Dann halte am Depot NICHT an. Fahr einfach weiter.",
            lastWordsEn = "To whoever comes after me: dispatch has no address. I drove there. There is only forest.\nFinish the seven nights. Then do NOT stop at the depot. Just keep driving." },
    };

    public class Note
    {
        public string de, en;
    }

    // Notes lying around at stops and in houses.
    public static readonly Note[] Notes =
    {
        new Note { de = "Fahrtenbuch, Seite 3 (K. Weber):\n\"Heute sind wieder mehr eingestiegen als ausgestiegen. Die Zahl stimmt nie.\"",
                   en = "Logbook, page 3 (K. Weber):\n\"Again more got on than got off today. The count is never right.\"" },
        new Note { de = "Ein Zettel, mit Bleistift:\n\"Wenn das Radio von selbst auf 66,6 springt: NICHT anhalten. Egal wer an der Haltestelle steht.\"",
                   en = "A note, in pencil:\n\"If the radio jumps to 66.6 on its own: DO NOT stop. No matter who is standing at the bus stop.\"" },
        new Note { de = "Brief der Leitstelle an D. Hahn, 1997:\n\"Ihre Kündigung wird nicht angenommen. Die Linie 13 braucht einen Fahrer.\"",
                   en = "Letter from dispatch to D. Hahn, 1997:\n\"Your resignation is not accepted. Line 13 needs a driver.\"" },
        new Note { de = "Tagebuch, U. Braun:\n\"Die Leute, die ich abweise, stehen am nächsten Abend wieder da. An einer anderen Haltestelle. Mit einem anderen Namen.\"",
                   en = "Diary, U. Braun:\n\"The people I turn away are there again the next evening. At another stop. With another name.\"" },
        new Note { de = "Ein Foto. Auf der Rückseite:\n\"Waldfriedhof, Reihe 4. Da liegen sie alle. Auch die, die nie gestorben sind.\"",
                   en = "A photo. On the back:\n\"Waldfriedhof, row 4. They all lie there. Even the ones who never died.\"" },
        new Note { de = "Eine Kinderzeichnung: ein Bus voller Strichmännchen ohne Gesichter.\nDarunter: \"PAPA FÄHRT DIE 13\"",
                   en = "A child's drawing: a bus full of stick figures without faces.\nBelow: \"DADDY DRIVES THE 13\"" },
        new Note { de = "Zeitungsausschnitt, 1994:\n\"Nachtbus verunglückt am Waldfriedhof - 13 Tote. Der Fahrer überlebt schwer verletzt und verschwindet aus dem Krankenhaus.\"",
                   en = "Newspaper clipping, 1994:\n\"Night bus crashes at the Waldfriedhof - 13 dead. The driver survives badly hurt and disappears from hospital.\"" },
        new Note { de = "Fahrplan der Linie 13, mit rotem Stift:\nAlle Haltestellen sind durchgestrichen. Nur eine nicht: \"Endstation\".",
                   en = "Timetable of line 13, in red pen:\nEvery stop is crossed out. All but one: \"Endstation\"." },
    };

    // What the title card says before each night.
    public static string DayIntro(int day) => day switch
    {
        1 => Loc.T("Ihre erste Schicht.", "Your first shift."),
        2 => Loc.T("Die Leitstelle hat Ihren Lohn überwiesen. Im Postfach wartet eine neue Anweisung.",
                   "Dispatch has paid your wages. A new instruction is waiting in your mailbox."),
        3 => Loc.T("Ein Fahrgast von gestern wurde heute als vermisst gemeldet.",
                   "One of yesterday's passengers was reported missing today."),
        4 => Loc.T("Horst ist heute nicht zur Arbeit erschienen.", "Horst did not show up for work today."),
        5 => Loc.T("Die Strecke kommt Ihnen länger vor als gestern. Oder bilden Sie sich das ein?",
                   "The route feels longer than yesterday. Or are you imagining it?"),
        6 => Loc.T("Heute Morgen lag ein Ausweis in Ihrem Briefkasten. Ihr Name. Ihr Foto. Status: VERSTORBEN.",
                   "This morning there was an ID card in your letterbox. Your name. Your photo. Status: DECEASED."),
        _ => Loc.T("Die letzte Nacht.", "The last night."),
    };

    public static string Ending(bool allDriversFound) => allDriversFound
        ? Loc.T("Sie halten am Depot nicht an.\n\nSie fahren einfach weiter, so wie Jens Keller es geschrieben hat. Der Wald wird dünner. " +
                "Hinter Ihnen steigt niemand mehr aus, weil niemand mehr da ist.\n\nIm Morgengrauen erreichen Sie eine Stadt, die auf keinem Plan steht. " +
                "Sie stellen den Bus ab und gehen zu Fuß weiter.\n\nDie Nachtlinie 13 wird am folgenden Montag eingestellt.",
                "You do not stop at the depot.\n\nYou just keep driving, the way Jens Keller wrote it. The forest thins out. " +
                "Behind you nobody gets off anymore, because nobody is there anymore.\n\nAt dawn you reach a town that is on no map. " +
                "You park the bus and walk on.\n\nNight line 13 is discontinued the following Monday.")
        : Loc.T("Um 6:00 Uhr halten Sie am Depot. Die Türen öffnen sich.\n\nEin letzter Fahrgast steigt ein. Er zeigt Ihnen seinen Ausweis. " +
                "Es ist Ihr Name. Es ist Ihr Gesicht.\n\nSCHWARZWÄLDER BOTE, Montag:\n\"Fünfter Fahrer der Nachtlinie 13 vermisst.\"",
                "At 6:00 AM you stop at the depot. The doors open.\n\nOne last passenger gets on. He shows you his ID. " +
                "It is your name. It is your face.\n\nBLACK FOREST HERALD, Monday:\n\"Fifth driver of night line 13 missing.\"");
}
