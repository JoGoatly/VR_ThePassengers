using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First person driver camera. Put it on a camera that is a child of the bus,
/// placed at the driver's eye position.
///
/// Mouse / right stick: look around (limited like a real head); aim at the terminal and click to use it
/// V / right stick click: look straight ahead again
/// Esc: release the mouse cursor, click to capture it again
/// </summary>
[DefaultExecutionOrder(200)] // after DriverBody posed the character
public class DriverCamera : MonoBehaviour
{
    [Tooltip("Optional: follow this point (e.g. the driver's eyes). Set automatically by DriverBody.")]
    public Transform eyeAnchor;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("Degrees per second at full stick deflection")]
    public float stickSensitivity = 140f;
    public float maxYaw = 120f;
    public float minPitch = -60f;
    public float maxPitch = 50f;
    [Tooltip("Higher = snappier, 0 = no smoothing")]
    public float lookSmoothing = 18f;

    [Header("Head motion")]
    [Tooltip("How far the head leans when the bus accelerates, brakes or turns (m per m/s²)")]
    public float swayAmount = 0.012f;
    public float maxSway = 0.08f;
    public float swaySmoothing = 4f;
    [Tooltip("Small shake that grows with speed")]
    public float roadShake = 0.004f;

    Rigidbody busBody;
    Vector3 restPosition;
    Quaternion restRotation;
    float yaw, pitch, smoothYaw, smoothPitch;
    Vector3 lastVelocity;
    Vector3 busAcceleration;
    Vector3 sway;

    void Start()
    {
        busBody = GetComponentInParent<Rigidbody>();
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        LockCursor(true);
    }

    void OnDisable() => LockCursor(false);

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Velocity only changes in physics steps, so measure acceleration there.
    void FixedUpdate()
    {
        if (busBody == null) return;
        Vector3 velocity = busBody.linearVelocity;
        busAcceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = velocity;
    }

    void LateUpdate()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var gp = Gamepad.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame && !GameUI.TerminalTyping) LockCursor(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += delta.x;
            pitch += delta.y;
        }
        if (gp != null)
        {
            Vector2 stick = gp.rightStick.ReadValue();
            yaw += stick.x * stickSensitivity * Time.deltaTime;
            pitch += stick.y * stickSensitivity * Time.deltaTime;
        }
        if ((kb != null && !GameUI.TerminalTyping && kb.vKey.wasPressedThisFrame) || (gp != null && gp.rightStickButton.wasPressedThisFrame))
        {
            yaw = 0f;
            pitch = 0f;
        }

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        float t = lookSmoothing > 0f ? 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime) : 1f;
        smoothYaw = Mathf.Lerp(smoothYaw, yaw, t);
        smoothPitch = Mathf.Lerp(smoothPitch, pitch, t);
        transform.localRotation = restRotation * Quaternion.Euler(-smoothPitch, smoothYaw, 0f);

        if (eyeAnchor != null)
            restPosition = transform.parent != null ? transform.parent.InverseTransformPoint(eyeAnchor.position) : eyeAnchor.position;
        transform.localPosition = restPosition + HeadMotion();
    }

    // The head is pushed opposite to the bus' acceleration (forward when braking,
    // outwards in curves) plus a little road rumble.
    Vector3 HeadMotion()
    {
        if (busBody == null || Time.deltaTime <= 0f) return Vector3.zero;

        Transform bus = busBody.transform;
        Vector3 localAccel = bus.InverseTransformDirection(busAcceleration);
        localAccel.y = 0f;

        Vector3 target = Vector3.ClampMagnitude(-localAccel * swayAmount, maxSway);
        sway = Vector3.Lerp(sway, target, 1f - Mathf.Exp(-swaySmoothing * Time.deltaTime));

        float speed = busBody.linearVelocity.magnitude;
        float time = Time.time * 23f;
        Vector3 shake = new Vector3(
            Mathf.PerlinNoise(time, 0.3f) - 0.5f,
            Mathf.PerlinNoise(0.7f, time) - 0.5f,
            0f) * roadShake * Mathf.Clamp01(speed / 10f);

        // Sway is in bus space, the camera's parent may be rotated/scaled differently.
        Vector3 worldOffset = bus.TransformDirection(sway) + bus.TransformDirection(shake);
        return transform.parent != null ? transform.parent.InverseTransformDirection(worldOffset) : worldOffset;
    }
}
