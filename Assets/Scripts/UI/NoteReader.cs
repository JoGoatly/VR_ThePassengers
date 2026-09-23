using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shows a found note or a driver's last words on a sheet of paper. E, Esc or click closes it.</summary>
public class NoteReader : MonoBehaviour
{
    static NoteReader instance;
    string title, body;
    int openedFrame;
    public AudioClip paperSound;

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; GameUI.NoteOpen = false; }

    public static void Show(string title, string body)
    {
        if (instance == null) return;
        instance.title = title;
        instance.body = body;
        instance.openedFrame = Time.frameCount;
        GameUI.NoteOpen = true;
        var sound = FindAnyObjectByType<SoundManager>();
        if (sound != null && instance.paperSound != null) sound.PlayWorld(instance.paperSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.8f, 0f);
    }

    void Update()
    {
        if (!GameUI.NoteOpen || Time.frameCount <= openedFrame + 1) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && (GameKeys.Pressed(GameAction.Interact) || kb.escapeKey.wasPressedThisFrame)) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame))
            GameUI.NoteOpen = false;
    }

    void OnGUI()
    {
        if (!GameUI.NoteOpen) return;
        GUI.depth = -300;
        float w = RetroGUI.VirtualWidth;
        RetroGUI.Fill(new Rect(0, 0, w, RetroGUI.VirtualHeight), new Color(0f, 0f, 0f, 0.6f));
        var page = new Rect(w / 2 - 150, 50, 300, 230);
        RetroGUI.Fill(new Rect(page.x + 3, page.y + 3, page.width, page.height), new Color(0f, 0f, 0f, 0.5f));
        RetroGUI.Fill(page, new Color(0.8f, 0.76f, 0.64f));
        var ink = new Color(0.15f, 0.12f, 0.1f);
        RetroGUI.Label(new Rect(page.x + 12, page.y + 10, page.width - 24, 14), title, ink, true);
        RetroGUI.Fill(new Rect(page.x + 12, page.y + 26, page.width - 24, 1), new Color(0.4f, 0.35f, 0.3f));
        RetroGUI.Wrapped(new Rect(page.x + 12, page.y + 32, page.width - 24, page.height - 44), body, ink);
        RetroGUI.ShadowLabel(new Rect(0, page.yMax + 8, w, 12), Loc.T("Schließen ", "Close ") + GameKeys.Tag(GameAction.Interact), new Color(0.8f, 0.8f, 0.8f), false);
    }
}
