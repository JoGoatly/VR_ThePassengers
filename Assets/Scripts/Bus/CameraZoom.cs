using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zoom for the player camera - in the driver's seat, walking in the bus and outside.
/// Mouse wheel or touchpad (two fingers up/down) zooms smoothly, holding the right mouse
/// button zooms in fully. Not while a panel that uses the wheel itself is open.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    public float normalFov = 70f;
    public float zoomedFov = 22f;
    [Tooltip("Zoom change per mouse wheel notch (0..1)")]
    public float zoomStep = 0.2f;
    public float zoomSpeed = 10f;

    Camera cam;
    float zoomTarget, zoom;

    /// <summary>Current field of view relative to the normal one (for slower looking when zoomed).</summary>
    public float LookScale => cam != null && normalFov > 0f ? cam.fieldOfView / normalFov : 1f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null && cam.fieldOfView > 1f) normalFov = cam.fieldOfView;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        var mouse = Mouse.current;
        bool blocked = GameUI.MenuOpen || GameUI.PhoneOpen || GameUI.MinigameOpen || GameUI.PcOpen || GameUI.NoteOpen || GameUI.TerminalTyping || GameUI.DialogueOpen;
        if (mouse != null && !blocked)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                // A mouse wheel notch is 120 on some systems, 1 on others; a touchpad sends many small steps.
                float notches = Mathf.Abs(wheel) >= 20f ? wheel / 120f : wheel;
                zoomTarget = Mathf.Clamp01(zoomTarget + Mathf.Clamp(notches, -1f, 1f) * zoomStep);
            }
        }
        float wanted = mouse != null && mouse.rightButton.isPressed && !blocked ? 1f : zoomTarget;
        if (GameUI.MenuOpen) wanted = 0f;
        zoom = Mathf.Lerp(zoom, wanted, 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
        cam.fieldOfView = Mathf.Lerp(normalFov, zoomedFov, zoom);
    }
}
