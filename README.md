# VR_ThePassengers


First-person PSX-style bus driving game (Unity 6, Built-in Render Pipeline).

## Steuerung

| Aktion | Tastatur / Maus | Gamepad |
|---|---|---|
| Gas | W / ↑ | RT |
| Bremsen / Rückwärts (im Stand) | S / ↓ | LT |
| Lenken | A D / ← → | linker Stick |
| Handbremse | Leertaste | A |
| Türen auf/zu (nur unter 5 km/h) | F | Y |
| Licht: aus → Abblendlicht → Fernlicht | L | – |
| Umschauen | Maus | rechter Stick |
| Bordcomputer bedienen | mit dem Fadenkreuz auf den Bildschirm zielen + Linksklick | – |
| Suchfeld: tippen, Enter = suchen, Esc = fertig | Tastatur | – |
| Ausweis aus-/einblenden | E | – |
| Fahrgast einlassen / abweisen | J / N (oder Terminal → KONTROLLE) | – |
| Blick nach vorne | V | rechter Stick drücken |

## Rendering

Das Projekt nutzt die [HauntedPS1 Render Pipeline](https://github.com/pastasfuture/com.hauntedpsx.render-pipelines.psx)
(eingebettet unter `Packages/com.hauntedpsx.render-pipelines.psx`, v1.7.0, MIT-Lizenz).
Alle Materialien müssen den Shader `PSX/PSXLit` benutzen. Für weitere Charaktere:
FBX im Project-Fenster auswählen → **Tools → PSX → Convert Selected Models To PSXLit**.

Der Bus hat zwei Materialien: `Bus` (alles außer Glas) und `Bus_Glass` (nur die
halbtransparenten Texel der Textur, Alpha < 0.85).

### Nebel & Sichtweite

Das Objekt `PSX Volume (Fog)` in der Szene nutzt das Profil `Assets/Environment/PSX_Volume_Profile.asset`:

| Einstellung | Wert |
|---|---|
| Nebel beginnt / ist dicht bei | 1,5 m / 12 m (zylindrisch um die Kamera) |
| Nebelfarbe (= Himmelfarbe) | fast schwarz |
| Sichtweite (Geometrie wird danach ausgeblendet) | 16 m |
| PSX-Qualität (Hauptschalter für Licht, Nebel, Pixelung) | an, 480×270 |
| Dynamisches Licht (Scheinwerfer, Laternen) | an, bis 8 Lichter pro Objekt |

Weitere Overrides (Auflösung, Farbtiefe, CRT-Effekt, …) über **Add Override → HauntedPS1** im Profil.

## Fahrer

`PlayerBus/Driver` (Character_Male_38) wird per IK auf den Sitz gesetzt, die Hände
greifen das Lenkrad, die Füße stehen auf den Pedalen. Die Position lässt sich über die
Objekte `DriverSeat`, `LeftFootRest`, `ThrottlePedal` und `BrakePedal` unter `PlayerBus`
anpassen; weitere Einstellungen (Griffposition, Oberkörper-Neigung, Fingerkrümmung) an
der Komponente `DriverBody`.

## Spielablauf

Nachtschicht auf der Linie 13: eine endlose Landstraße durch den Wald (Rechtsverkehr,
Haltestellen rechts), meist geradeaus, ab und zu eine Kurve. An jeder Haltestelle wartet eine Person.

1. Halte so, dass die vordere Tür (rechts) bei der Person ist, und öffne die Türen (**F**).
2. Die Person kommt zur Tür. Ihr Ausweis erscheint **links**, sie selbst steht in der **Mitte**,
   der Bordcomputer ist **rechts** neben dir.
3. Ziele auf den Bildschirm und klicke: **REGISTER** (Namen suchen), **POSTFACH** (Regeln und
   Nachrichten der Leitstelle), **KONTROLLE** (einlassen / abweisen).
4. Vergleiche Name, Geburtsdatum, Ausweisnummer, Gültigkeit und Status. Neue Regeln kommen per Mail.
5. Einlassen (**J**) oder abweisen (**N**). Vorher und mit offenen Türen fährt der Bus nicht.

Die Straße erzeugt `Forest Road` (`ForestRoad`, `ForestWatchers`); Länge der Geraden, Kurven und
Haltestellenabstände sind dort einstellbar. Spiellogik: `GameManager` (`BoardingManager`,
`ComputerTerminal`, `IdCardView`, `SoundManager`). Nebel/Licht: `PSX_Volume_Profile`.
