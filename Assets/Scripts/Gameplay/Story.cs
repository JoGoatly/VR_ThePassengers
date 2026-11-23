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

    // What the title card says before each night. (Small things that don't quite fit -
    // the player is the skinwalker in the dead driver's skin from the first night on.)
    public static string DayIntro(int day) => day switch
    {
        1 => Loc.T("Ihre erste Schicht. Der Fahrlehrer hat Ihnen zum Abschied nicht die Hand gegeben.",
                   "Your first shift. The driving instructor did not shake your hand when you left."),
        2 => Loc.T("Die Leitstelle hat Ihren Lohn überwiesen. Auf ein Konto, das Sie nicht eröffnet haben.",
                   "Dispatch has paid your wages. Into an account you never opened."),
        3 => Loc.T("Ein Fahrgast von gestern wurde heute als vermisst gemeldet. Sie haben trotzdem gut geschlafen. Satt.",
                   "One of yesterday's passengers was reported missing today. You slept well anyway. Full."),
        4 => Loc.T("Horst ist heute nicht zur Arbeit erschienen. Ihr Spiegelbild hat heute einen Moment zu spät geblinzelt.",
                   "Horst did not show up for work today. Your reflection blinked a moment too late today."),
        5 => Loc.T("Ihre Haut juckt. Sie sitzt nicht mehr richtig. Wie ein Mantel, der jemand anderem gehört.",
                   "Your skin itches. It does not fit right anymore. Like a coat that belongs to someone else."),
        6 => Loc.T("Heute Morgen lag ein Ausweis in Ihrem Briefkasten. Ihr Name. Ihr Foto. Status: VERSTORBEN. Seit dem 19.12.",
                   "This morning there was an ID card in your letterbox. Your name. Your photo. Status: DECEASED. Since 19 Dec."),
        _ => Loc.T("Die letzte Nacht. Sie fühlen sich heute so... lebendig. So warm.",
                   "The last night. You feel so... alive today. So warm."),
    };

    // The last night is the 19th: the night the driver was eaten. Whatever happened before,
    // it ends with the thing in the forest putting on his skin - and on the 20th it applies.
    public static string Ending(bool allDriversFound) => allDriversFound
        ? Loc.T("Sie halten am Depot nicht an.\n\nSie fahren einfach weiter, so wie Jens Keller es geschrieben hat. Der Wald wird dünner. " +
                "Dann steht etwas auf der Straße. Groß. Dünn. Es hat Ihr Gesicht noch nicht.\n\nNoch nicht.\n\n" +
                "20. Dezember 1998, 9:00 Uhr. Fahrschule Brandt.\n\"Guten Tag. Danke, dass Sie sich für die Stelle beworben haben. Setzen Sie sich.\"\n" +
                "Der Bewerber lächelt. Ein bisschen zu breit.",
                "You do not stop at the depot.\n\nYou just keep driving, the way Jens Keller wrote it. The forest thins out. " +
                "Then something stands on the road. Tall. Thin. It does not have your face yet.\n\nNot yet.\n\n" +
                "December 20, 1998, 9:00 AM. Brandt driving school.\n\"Good day. Thank you for applying for the job. Have a seat.\"\n" +
                "The applicant smiles. A little too wide.")
        : Loc.T("Um 6:00 Uhr halten Sie am Depot. Die Türen öffnen sich.\n\nEin letzter Fahrgast steigt ein. Er zeigt Ihnen keinen Ausweis. " +
                "Er braucht keinen.\n\nEr braucht Ihren.\n\n" +
                "20. Dezember 1998, 9:00 Uhr. Fahrschule Brandt.\n\"Guten Tag. Danke, dass Sie sich für die Stelle beworben haben. Setzen Sie sich.\"\n" +
                "Der Bewerber lächelt. Ein bisschen zu breit.",
                "At 6:00 AM you stop at the depot. The doors open.\n\nOne last passenger gets on. He does not show you an ID. " +
                "He does not need one.\n\nHe needs yours.\n\n" +
                "December 20, 1998, 9:00 AM. Brandt driving school.\n\"Good day. Thank you for applying for the job. Have a seat.\"\n" +
                "The applicant smiles. A little too wide.");

    // What the driving instructor wrote about the applicant (in his office, in the driving test).
    public static readonly Note[] InstructorNotes =
    {
        new Note { de = "Notizen zum Bewerber, Nachtlinie 13 (20.12., 9:00):\n- Kam zu Fuß. Aus dem Wald.\n- Hände eiskalt. Hat beim Händedruck zu lange festgehalten.\n- Riecht wie eine Leiche.\n- Blinzelt nicht. Ich habe gezählt.",
                   en = "Notes on the applicant, night line 13 (20 Dec, 9:00):\n- Came on foot. Out of the forest.\n- Hands ice cold. Held on too long when shaking hands.\n- Smells like a corpse.\n- Does not blink. I counted." },
        new Note { de = "Fax der Verkehrsbetriebe, 20.12., 6:14 Uhr:\n\"Fahrer der Nachtlinie 13 nach der Schicht vom 19.12. nicht zurückgekehrt. Bus leer am Waldfriedhof. Bitte umgehend Ersatz prüfen.\"",
                   en = "Fax from the transport company, 20 Dec, 6:14 AM:\n\"Driver of night line 13 did not return from the shift of 19 Dec. Bus empty at the Forest Cemetery. Please test a replacement immediately.\"" },
        new Note { de = "Bewerbungsunterlagen. Das Passfoto ist alt und verblasst.\nDer Mann auf dem Foto sieht dem Bewerber ähnlich. Sehr ähnlich.\nAm Rand, mit Bleistift: \"Ist das nicht der Fahrer, der vermisst wird?\" - wieder ausradiert.",
                   en = "Application papers. The passport photo is old and faded.\nThe man in the photo looks like the applicant. Very much like him.\nIn the margin, in pencil: \"Isn't that the driver who went missing?\" - erased again." },
        new Note { de = "Wandkalender, Dezember 1998.\nDer 19. ist rot eingekreist: \"Vollmond\".\nAm 20.: \"Neuer Fahrer, 9 Uhr. Er stand schon um 5 vor der Tür.\"",
                   en = "Wall calendar, December 1998.\nThe 19th is circled in red: \"Full moon\".\nOn the 20th: \"New driver, 9 AM. He was already standing at the door at 5.\"" },
    };
}
