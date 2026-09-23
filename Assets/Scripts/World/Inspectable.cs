using UnityEngine;

/// <summary>
/// Something in the world to look at or use with E: shows a text on a sheet (like a note),
/// may play a sound, run an action and count as a found secret (easter egg).
/// </summary>
public class Inspectable : Interactable
{
    public string promptDe = "Ansehen", promptEn = "Look";
    public string titleDe, titleEn;
    [TextArea] public string textDe, textEn;
    [Tooltip("Easter egg id: counted once (Progress.FoundSecret)")]
    public string secret;
    public AudioClip sound;
    public float soundVolume = 0.9f;
    [Tooltip("Can only be used once")]
    public bool once;

    /// <summary>Extra behaviour when used. Returns the text to show instead (null = the normal text).</summary>
    public System.Func<Inspectable, string> Action;

    public override string Prompt => Loc.T(promptDe, promptEn);

    public override void Use()
    {
        var sm = FindAnyObjectByType<SoundManager>();
        if (sm != null && sound != null) sm.PlayWorld(sound, transform.position, soundVolume, 0.5f);

        string text = Action != null ? Action(this) : null;
        if (text == null && !string.IsNullOrEmpty(textDe)) text = Loc.T(textDe, textEn);

        if (!string.IsNullOrEmpty(secret) && Progress.FoundSecret(secret))
        {
            Progress.Save();
            text = (text ?? "") + Loc.T($"\n\n(Geheimnis gefunden: {Progress.SecretsFound} / {Depot.SecretCount})",
                                        $"\n\n(Secret found: {Progress.SecretsFound} / {Depot.SecretCount})");
        }
        if (!string.IsNullOrEmpty(text)) NoteReader.Show(Loc.T(titleDe, titleEn), text);
        if (once) enabled = false;
    }

    /// <summary>Adds an inspectable to an existing object (or a new empty one at a position).</summary>
    public static Inspectable Add(GameObject go, string promptDe, string promptEn, string titleDe, string titleEn,
                                  string textDe = null, string textEn = null, string secret = null, float radius = 1.7f)
    {
        var i = go.AddComponent<Inspectable>();
        i.promptDe = promptDe; i.promptEn = promptEn;
        i.titleDe = titleDe; i.titleEn = titleEn;
        i.textDe = textDe; i.textEn = textEn;
        i.secret = secret;
        i.radius = radius;
        return i;
    }
}
