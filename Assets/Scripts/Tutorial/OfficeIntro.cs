using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The main menu's background is the game itself: the door of the driving school. When a new
/// game starts, the camera goes through the door into the office, where the instructor and the
/// applicant (you) sit at the desk - a short conversation, then the driving test begins.
/// Place this on a camera; 'pathRoot' holds the camera path as children (menu shot, door, inside, conversation).
/// </summary>
public class OfficeIntro : MonoBehaviour
{
    public static OfficeIntro Instance { get; private set; }
    /// <summary>The office scene is playing (the driving test waits, the bus is held).</summary>
    public static bool Running { get; private set; }

    [Tooltip("The two people at the desk (hidden after the conversation)")]
    public GameObject seatedPeople;
    [Tooltip("Its children are the camera path: menu shot, outside the door, inside, conversation shot")]
    public Transform pathRoot;
    public float moveSeconds = 4.5f;

    Camera cam;
    Transform[] path;
    int line = -1;
    float lineStart, fade, typeChars;
    bool advance;

    struct Line { public bool instructor; public string de, en; }

    static readonly Line[] Conversation =
    {
        new Line { instructor = true, de = "Ah. Da sind Sie ja. Setzen Sie sich, setzen Sie sich.", en = "Ah. There you are. Sit down, sit down." },
        new Line { instructor = true, de = "Brandt. Fahrschule Brandt. Danke, dass Sie sich für die Stelle auf der Nachtlinie 13 beworben haben.", en = "Brandt. Brandt driving school. Thank you for applying for the job on night line 13." },
        new Line { instructor = true, de = "Ehrlich gesagt... es hat sich sonst niemand gemeldet. Nicht nach dem, was mit dem letzten Fahrer passiert ist.", en = "To be honest... nobody else applied. Not after what happened to the last driver." },
        new Line { instructor = false, de = "...", en = "..." },
        new Line { instructor = true, de = "Ihre Unterlagen sind vollständig. Das Foto ist etwas älter, oder? Sie sehen darauf irgendwie... anders aus. Egal.", en = "Your papers are complete. The photo is a bit older, isn't it? You look somehow... different in it. Never mind." },
        new Line { instructor = true, de = "Ist Ihnen kalt? Ihre Hände sind ja eiskalt. Und hier riecht es irgendwie... ach, das ist bestimmt der Keller.", en = "Are you cold? Your hands are ice cold. And it smells somehow... oh, that's probably the cellar." },
        new Line { instructor = false, de = "Ich bin bereit.", en = "I am ready." },
        new Line { instructor = true, de = "Gut. Dann zeigen Sie mir mal, was Sie können. Der Bus steht draußen. Ich komme mit.", en = "Good. Then show me what you can do. The bus is outside. I'm coming along." },
    };

    void Awake()
    {
        Instance = this;
        Running = false;
        cam = GetComponent<Camera>();
        var root = pathRoot != null ? pathRoot : transform;
        path = new Transform[root == transform ? 0 : root.childCount];
        for (int i = 0; i < path.Length; i++) path[i] = root.GetChild(i);
        if (path.Length > 0) { transform.position = path[0].position; transform.rotation = path[0].rotation; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Running = false;
    }

    /// <summary>Only the menu background: the camera stays outside the door.</summary>
    public void ShowMenuShot()
    {
        if (cam != null) cam.enabled = true;
        if (path.Length > 0) transform.SetPositionAndRotation(path[0].position, path[0].rotation);
    }

    /// <summary>Through the door, the conversation, then the driving test.</summary>
    public void Play()
    {
        if (Running) return;
        Running = true;
        GameUI.DialogueOpen = true;   // holds the bus and the player
        if (cam != null) cam.enabled = true;
        if (seatedPeople != null) seatedPeople.SetActive(true);
        StartCoroutine(Run());
    }

    void Update()
    {
        // Gentle breathing of the menu shot while the menu is open.
        if (!Running && GameUI.MenuOpen && path.Length > 0)
        {
            float t = Time.unscaledTime;
            transform.SetPositionAndRotation(path[0].position + new Vector3(Mathf.Sin(t * 0.3f) * 0.04f, Mathf.Sin(t * 0.5f) * 0.02f, 0f),
                                             path[0].rotation * Quaternion.Euler(Mathf.Sin(t * 0.21f) * 0.6f, Mathf.Sin(t * 0.17f) * 0.8f, 0f));
        }
        if (!Running) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame)) advance = true;
        if (kb != null && kb.backspaceKey.wasPressedThisFrame) line = Conversation.Length;   // skip
    }

    IEnumerator Run()
    {
        // Through the door: along the path points (smooth), slowly in.
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 p0 = transform.position, p1 = path[i].position;
            Quaternion r0 = transform.rotation, r1 = path[i].rotation;
            float seconds = moveSeconds / Mathf.Max(1, path.Length - 1);
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, e), Quaternion.Slerp(r0, r1, e));
                yield return null;
            }
            transform.SetPositionAndRotation(p1, r1);
        }

        // The conversation.
        for (line = 0; line < Conversation.Length; line++)
        {
            lineStart = Time.time;
            typeChars = 0f;
            advance = false;
            while (line < Conversation.Length)
            {
                string text = Text(Conversation[line]);
                typeChars += Time.deltaTime * 45f;
                if (advance)
                {
                    advance = false;
                    if (typeChars < text.Length) typeChars = text.Length;   // first click: the whole line
                    else break;
                }
                yield return null;
            }
        }

        // Fade out, then the bus.
        for (fade = 0f; fade < 1f; fade += Time.deltaTime / 1.2f) yield return null;
        fade = 1f;
        if (seatedPeople != null) seatedPeople.SetActive(false);
        if (cam != null) cam.enabled = false;
        GameUI.DialogueOpen = false;
        GameUI.ClosedFrame = Time.frameCount;
        Running = false;
        for (; fade > 0f; fade -= Time.deltaTime / 1.2f) yield return null;
        fade = 0f;
    }

    static string Text(Line l) => Loc.T(l.de, l.en);

    void OnGUI()
    {
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        if (fade > 0f) { GUI.depth = -450; RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, fade)); }
        if (!Running || line < 0 || line >= Conversation.Length) return;
        GUI.depth = -300;
        var l = Conversation[line];
        string text = Text(l);
        int n = Mathf.Clamp(Mathf.FloorToInt(typeChars), 0, text.Length);
        var box = new Rect(w / 2 - 220, h - 86, 440, 66);
        RetroGUI.Panel(box, 4);
        RetroGUI.Label(new Rect(box.x + 10, box.y + 5, 300, 12), l.instructor ? Loc.T("FAHRLEHRER BRANDT", "INSTRUCTOR BRANDT") : Loc.T("SIE", "YOU"),
                       l.instructor ? new Color(1f, 0.85f, 0.5f) : new Color(0.7f, 0.8f, 1f), true, true);
        RetroGUI.Wrapped(new Rect(box.x + 10, box.y + 18, box.width - 20, box.height - 22), text.Substring(0, n), Color.white);
        bool blink = Mathf.Repeat(Time.time, 1f) < 0.6f;
        if (n >= text.Length && blink)
            RetroGUI.Label(new Rect(box.x, box.yMax - 11, box.width - 8, 10), Loc.T("Weiter [Leertaste / Klick]", "Continue [Space / click]"), new Color(0.6f, 0.6f, 0.6f), false, true, TextAnchor.UpperRight);
        RetroGUI.Label(new Rect(0, 4, w - 8, 10), Loc.T("Überspringen [Rücktaste]", "Skip [Backspace]"), new Color(0.55f, 0.55f, 0.55f), false, true, TextAnchor.UpperRight);
    }
}
