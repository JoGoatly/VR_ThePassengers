using UnityEngine;

/// <summary>
/// Something to read in the driving school (Story.InstructorNotes[index]): walk up to it and
/// press E. Place it on any object in the scene.
/// </summary>
public class StoryNoteSpot : MonoBehaviour
{
    public int index;
    public string titleDe = "Notiz", titleEn = "Note";
    public float radius = 1.4f;

    void Awake()
    {
        if (index < 0 || index >= Story.InstructorNotes.Length) return;
        var note = Story.InstructorNotes[index];
        Inspectable.Add(gameObject, "Lesen", "Read", titleDe, titleEn, note.de, note.en, null, radius);
    }
}
