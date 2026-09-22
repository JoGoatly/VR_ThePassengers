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
| Ausweis ansehen | E | – |
| Bordcomputer (Register & Mails) | Tab | – |
| Fahrgast einsteigen lassen / abweisen | J / N | – |
| Umschauen | Maus | rechter Stick |
| Blick nach vorne | V | rechter Stick drücken |
| Maus freigeben / einfangen | Esc / Linksklick | – |

Szene: `Assets/Scenes/SampleScene.unity`

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
| Nebel beginnt / ist dicht bei | 12 m / 85 m (zylindrisch um die Kamera) |
| Nebelfarbe (= Himmelfarbe) | graublau `#707A8A` |
| Sichtweite (Geometrie wird danach ausgeblendet) | 95 m |

Weitere Overrides (Auflösung, Farbtiefe, CRT-Effekt, …) über **Add Override → HauntedPS1** im Profil.

## Fahrer

`PlayerBus/Driver` (Character_Male_38) wird per IK auf den Sitz gesetzt, die Hände
greifen das Lenkrad, die Füße stehen auf den Pedalen. Die Position lässt sich über die
Objekte `DriverSeat`, `LeftFootRest`, `ThrottlePedal` und `BrakePedal` unter `PlayerBus`
anpassen; weitere Einstellungen (Griffposition, Oberkörper-Neigung, Fingerkrümmung) an
der Komponente `DriverBody`.

## Spielablauf

Du fährst die Nachtlinie 13 im Kreis durch die Stadt (Linksverkehr, Haltestellen links).
An jeder Haltestelle wartet eine Person.

1. Halte so, dass die vordere Tür bei der Person ist, und öffne die Türen (**F**).
2. Die Person kommt zur Tür und zeigt ihren Ausweis (**E**).
3. Suche den Namen im **Register** des Bordcomputers (**Tab**) und vergleiche Name,
   Geburtsdatum, Ausweisnummer, Ablaufdatum und Status. Die Regeln stehen im **Postfach**,
   neue Regeln kommen im Laufe der Schicht dazu.
4. **J** = einsteigen lassen, **N** = abweisen. Vorher kann der Bus nicht weiterfahren;
   mit offenen Türen gibt es kein Gas.

Die Stadt wird beim Start von `Town` (`TownBuilder`) erzeugt; Route, Anzahl und Namen der
Haltestellen lassen sich dort einstellen (die Route ist im Editor als gelbe Linie sichtbar).
Spiellogik: `GameManager` (`BoardingManager`, `ComputerTerminal`, `IdCardView`).
