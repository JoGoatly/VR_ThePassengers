using UnityEngine;

/// <summary>An item lying around: money, ammo, a first aid kit, a note or a missing driver's badge.</summary>
public class Pickup : Interactable
{
    public enum Kind { Money, Ammo, Medkit, Note, DriverBadge }

    public Kind kind;
    public int amount;
    [Tooltip("Note or driver index (Story)")]
    public int storyId;

    public static System.Action<Pickup> Collected;

    public override string Prompt => Loc.T("Aufheben: ", "Pick up: ") + kind switch
    {
        Kind.Money => Loc.T("Umschlag", "Envelope"),
        Kind.Ammo => Loc.T("Munition", "Ammo"),
        Kind.Medkit => Loc.T("Verbandskasten", "First aid kit"),
        Kind.Note => Loc.T("Zettel", "Note"),
        _ => Loc.T("Fahrerausweis", "Driver's badge"),
    };

    public override void Use()
    {
        var game = FindAnyObjectByType<BoardingManager>();
        switch (kind)
        {
            case Kind.Money:
                Progress.AddMoney(amount);
                Progress.ShiftFound += amount;
                game?.ShowToast($"+{amount} €");
                break;
            case Kind.Ammo:
                Progress.AddAmmo(amount);
                game?.ShowToast(Loc.T($"+{amount} Schuss", $"+{amount} rounds"));
                break;
            case Kind.Medkit:
                Progress.AddMedkits(1);
                game?.ShowToast(Loc.T("+1 Verbandskasten", "+1 first aid kit"));
                break;
            case Kind.Note:
                Progress.FoundNote(storyId);
                var note = Story.Notes[Mathf.Clamp(storyId, 0, Story.Notes.Length - 1)];
                NoteReader.Show(Loc.T("Zettel", "Note"), Loc.T(note.de, note.en));
                break;
            case Kind.DriverBadge:
                Progress.FoundDriver(storyId);
                var driver = Story.Drivers[Mathf.Clamp(storyId, 0, Story.Drivers.Length - 1)];
                NoteReader.Show(driver.name + "  -  " + driver.badge, Loc.T(driver.lastWordsDe, driver.lastWordsEn) +
                    Loc.T($"\n\n(Vermisste Fahrer gefunden: {Progress.Data.drivers.Count} / {Story.Drivers.Length})",
                          $"\n\n(Missing drivers found: {Progress.Data.drivers.Count} / {Story.Drivers.Length})"));
                break;
        }
        Progress.Save();
        Collected?.Invoke(this);
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- spawning

    /// <summary>Creates a small visible item with a faint glint so it can be found in the dark.</summary>
    public static Pickup Spawn(Kind kind, int amount, int storyId, Vector3 position, Transform parent, Material body, Material glow)
    {
        var go = new GameObject("Pickup " + kind);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Vector3 size = kind switch
        {
            Kind.Money => new Vector3(0.22f, 0.02f, 0.12f),
            Kind.Ammo => new Vector3(0.12f, 0.07f, 0.08f),
            Kind.Medkit => new Vector3(0.28f, 0.12f, 0.2f),
            Kind.Note => new Vector3(0.2f, 0.005f, 0.28f),
            _ => new Vector3(0.09f, 0.01f, 0.06f),
        };
        MeshKit.Spawn("Item", go.transform, MeshKit.Box(size, 0.3f), body, position, go.transform.rotation, false);
        if (glow != null)
        {
            var g = MeshKit.Spawn("Glint", go.transform, MeshKit.Box(new Vector3(0.03f, 0.03f, 0.03f), 0.1f), glow,
                position + Vector3.up * (size.y + 0.02f), Quaternion.identity, false);
            g.AddComponent<GlintBlink>();
        }
        var light = new GameObject("Glint Light").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = Vector3.up * 0.3f;
        light.type = LightType.Point;
        light.range = 1.6f;
        light.intensity = 0.7f;
        light.color = kind == Kind.DriverBadge || kind == Kind.Note ? new Color(0.8f, 0.85f, 1f) : new Color(1f, 0.85f, 0.5f);
        light.shadows = LightShadows.None;

        var p = go.AddComponent<Pickup>();
        p.kind = kind;
        p.amount = amount;
        p.storyId = storyId;
        return p;
    }
}
