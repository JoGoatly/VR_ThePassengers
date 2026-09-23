/// <summary>What happens on the phone each night: family messages (with two possible replies) and news.</summary>
public static class PhoneContent
{
    public class Sms
    {
        public float delay;              // seconds after the shift starts
        public string from;              // contact id
        public string de, en;
        public string[] replyDe, replyEn;    // two possible replies of the player
        public string[] answerDe, answerEn;  // what the contact answers to each reply
    }

    public class Article
    {
        public int night;
        public string titleDe, titleEn, textDe, textEn;
    }

    public const string Anna = "anna", Mia = "mia", Unknown = "unknown";

    public static string ContactName(string id) => id switch
    {
        Anna => Loc.T("Anna (Frau)", "Anna (wife)"),
        Mia => Loc.T("Mia (Tochter)", "Mia (daughter)"),
        _ => Loc.T("Unbekannt", "Unknown"),
    };

    public static Sms[] ForNight(int night) => night switch
    {
        1 => new[] { new Sms { delay = 60, from = Anna,
            de = "Na, wie ist die erste Schicht? Fahr vorsichtig. Kuss", en = "So, how's the first shift? Drive safe. Love you",
            replyDe = new[] { "Alles ruhig. Nur dunkel hier.", "Seltsame Leute hier..." },
            replyEn = new[] { "All quiet. Just dark out here.", "Strange people here..." },
            answerDe = new[] { "Mia wollte wach bleiben, bis du heimkommst. Sie schläft jetzt.", "Nachts fahren halt komische Leute Bus :) Pass auf dich auf." },
            answerEn = new[] { "Mia wanted to stay up until you're home. She's asleep now.", "Strange people take the bus at night :) Take care." } } },
        2 => new[] { new Sms { delay = 80, from = Mia,
            de = "papa wann kommst du heim? ich hab ein bild von deinem bus gemalt", en = "daddy when are you coming home? i drew a picture of your bus",
            replyDe = new[] { "Morgen früh, Schatz. Schlaf gut.", "Zeig mal!" },
            replyEn = new[] { "In the morning, sweetie. Sleep well.", "Show me!" },
            answerDe = new[] { "ok gute nacht papa", "da sind ganz viele leute drin ohne gesicht. so wie die leute die bei uns im garten stehen" },
            answerEn = new[] { "ok good night daddy", "there are lots of people in it without faces. like the people standing in our garden" } } },
        3 => new[] { new Sms { delay = 70, from = Anna,
            de = "Heute stand ein Mann vor unserem Haus. Er hat nur hochgeschaut. Als ich rausging, war er weg.",
            en = "A man stood in front of our house today. He just looked up. When I went out, he was gone.",
            replyDe = new[] { "Ruf die Polizei!", "Schließ ab. Ich bin bald da." },
            replyEn = new[] { "Call the police!", "Lock the door. I'll be home soon." },
            answerDe = new[] { "Die sagen, unter unserer Adresse ist niemand gemeldet. Nicht mal wir.", "Hab abgeschlossen. Er steht wieder da." },
            answerEn = new[] { "They say nobody is registered at our address. Not even us.", "I locked it. He is standing there again." } } },
        4 => new[] { new Sms { delay = 60, from = Anna,
            de = "Horsts Frau hat angerufen. Er ist nicht nach Hause gekommen.", en = "Horst's wife called. He didn't come home.",
            replyDe = new[] { "Ich weiß nichts.", "Sag ihr, ich suche ihn." },
            replyEn = new[] { "I don't know anything.", "Tell her I'll look for him." },
            answerDe = new[] { "Sie sagt, er hat von einem Haus im Wald erzählt. Mit einem Keller.", "Bitte nicht. Bitte komm einfach heim." },
            answerEn = new[] { "She says he talked about a house in the woods. With a cellar.", "Please don't. Please just come home." } } },
        5 => new[] { new Sms { delay = 90, from = Mia,
            de = "papa du warst doch schon zuhause? du hast an mein fenster geklopft", en = "daddy you were already home? you knocked on my window",
            replyDe = new[] { "Nein, ich bin im Bus.", "Schlaf weiter, Mia." },
            replyEn = new[] { "No, I'm on the bus.", "Go back to sleep, Mia." },
            answerDe = new[] { "aber du hast gewunken. du hattest keine augen", "ich kann nicht. du stehst immer noch da" },
            answerEn = new[] { "but you waved. you had no eyes", "i can't. you're still standing there" } } },
        6 => new[] { new Sms { delay = 60, from = Anna,
            de = "Wer bist du?", en = "Who are you?",
            replyDe = new[] { "Ich bin es, Anna!", "Was meinst du?" },
            replyEn = new[] { "It's me, Anna!", "What do you mean?" },
            answerDe = new[] { "Mein Mann liegt neben mir und schläft.", "Hör auf, mir zu schreiben." },
            answerEn = new[] { "My husband is lying next to me, asleep.", "Stop texting me." } } },
        _ => new[] { new Sms { delay = 60, from = Unknown,
            de = "Komm nach Hause. Wir warten. Alle 13.", en = "Come home. We are waiting. All 13 of us.",
            replyDe = new[] { "Wer ist da?", "..." },
            replyEn = new[] { "Who is this?", "..." },
            answerDe = new[] { "Du. In sieben Nächten.", "Endstation." },
            answerEn = new[] { "You. Seven nights from now.", "Last stop." } } },
    };

    public static readonly Article[] News =
    {
        new Article { night = 1, titleDe = "Nachtlinie 13 wieder in Betrieb", titleEn = "Night line 13 running again",
            textDe = "Nach vier Monaten Pause nimmt die Linie 13 wieder den Nachtbetrieb auf. Die Verkehrsbetriebe haben einen neuen Fahrer eingestellt.",
            textEn = "After a four month break, line 13 is back in night service. The transport company has hired a new driver." },
        new Article { night = 1, titleDe = "Wetter: Dichter Nebel", titleEn = "Weather: dense fog",
            textDe = "Im Schwarzwald ist in den nächsten Nächten mit dichtem Nebel zu rechnen. Sichtweiten unter 20 Metern.",
            textEn = "Dense fog is expected in the Black Forest over the next nights. Visibility below 20 metres." },
        new Article { night = 2, titleDe = "Anwohner melden Gestalten am Waldrand", titleEn = "Residents report figures at the forest edge",
            textDe = "Mehrere Anwohner der B13 berichten von Personen, die nachts reglos am Straßenrand stehen. Die Polizei fand keine Spuren.",
            textEn = "Several residents along the B13 report people standing motionless at the roadside at night. Police found no traces." },
        new Article { night = 3, titleDe = "Frau begegnet sich selbst am Bahnhof", titleEn = "Woman meets herself at the station",
            textDe = "\"Sie trug meinen Mantel und kannte meinen Namen, aber nicht meinen Geburtstag\", sagt Petra K. (41). Die Frau stieg in einen Nachtbus.",
            textEn = "\"She wore my coat and knew my name, but not my birthday,\" says Petra K. (41). The woman got on a night bus." },
        new Article { night = 3, titleDe = "Fahrscheine der Linie 13 'nicht auffindbar'", titleEn = "Line 13 tickets 'cannot be found'",
            textDe = "Kein Automat im Landkreis verkauft Fahrscheine mit Richtung ENDSTATION. Trotzdem sind sie im Umlauf.",
            textEn = "No machine in the district sells tickets for the direction ENDSTATION. Yet they are in circulation." },
        new Article { night = 4, titleDe = "Busfahrer Horst M. vermisst", titleEn = "Bus driver Horst M. missing",
            textDe = "Der 52-jährige Busfahrer kehrte nach seiner Schicht nicht zurück. Sein Wagen wurde an einem Feldweg nahe der B13 gefunden.",
            textEn = "The 52-year-old bus driver did not return after his shift. His car was found at a dirt track near the B13." },
        new Article { night = 5, titleDe = "Polizei fahndet nach Personen, die es nicht gibt", titleEn = "Police hunt people who don't exist",
            textDe = "Mehrere per Notruf gemeldete Gesuchte stehen in keinem Register des Landes. \"Wir fahren trotzdem raus\", so ein Sprecher.",
            textEn = "Several wanted persons reported by emergency calls are in no register of the state. \"We still go out,\" a spokesman said." },
        new Article { night = 5, titleDe = "Kinder: 'Der Mann ohne Augen'", titleEn = "Children: 'The man without eyes'",
            textDe = "In drei Familien berichten Kinder von einem Mann, der nachts ans Fenster klopft und aussieht wie ihr Vater.",
            textEn = "In three families children report a man who knocks on the window at night and looks like their father." },
        new Article { night = 6, titleDe = "Kein Fahrgast der 13 kam je zu Hause an", titleEn = "No passenger of the 13 ever got home",
            textDe = "Recherchen zeigen: Keine einzige Person, die nachts in die Linie 13 stieg, ist jemals zu Hause angekommen.",
            textEn = "Research shows: not a single person who boarded line 13 at night ever got home." },
        new Article { night = 7, titleDe = "Linie 13 wurde 1994 eingestellt", titleEn = "Line 13 was discontinued in 1994",
            textDe = "Nach dem Unglück am Waldfriedhof 1994 wurde die Linie 13 eingestellt. Es gibt keinen Nachtbus. Es gibt keinen Fahrer.",
            textEn = "After the accident at the Waldfriedhof in 1994, line 13 was discontinued. There is no night bus. There is no driver." },
    };
}
