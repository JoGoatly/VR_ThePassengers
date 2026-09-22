# VR_ThePassengers


First-person PSX-style bus driving game (Unity 6, Built-in Render Pipeline).

## Steuerung

| Aktion | Tastatur / Maus | Gamepad |
|---|---|---|
| Gas | W / ↑ | RT |
| Bremsen / Rückwärts (im Stand) | S / ↓ | LT |
| Lenken | A D / ← → | linker Stick |
| Handbremse | Leertaste | A |
| Türen auf/zu | F | Y |
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
