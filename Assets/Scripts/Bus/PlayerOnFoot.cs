using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Leaving the bus: when the bus stands still, E puts you outside next to the front door.
/// Walk with WASD (Shift runs), look with the mouse, L toggles a flashlight.
/// Back at the door, E takes you to the driver's seat again.
/// </summary>
public class PlayerOnFoot : MonoBehaviour
{
    public BusController bus;
    public BoardingManager game;

    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float eyeHeight = 1.65f;
    public float mouseSensitivity = 0.12f;
    [Tooltip("How close to the door you have to be to get back in")]
    public float enterDistance = 2.5f;

    DriverCamera driverCamera;
    DriverBody driver;
    Transform cam;
    Transform camParent;
    Vector3 camLocalPos;
    Quaternion camLocalRot;

    GameObject walker;
    CharacterController controller;
    Light flashlight;
    float pitch, verticalSpeed, stepTimer;

    Vector3 DoorOutsideLocal
    {
        get
        {
            Vector3 door = game != null ? game.DoorLocal : new Vector3(1.2f, 0f, 4.3f);
            float side = door.x >= 0f ? 1f : -1f;
            return new Vector3(door.x + side * 1.3f, 0f, door.z);
        }
    }

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        driverCamera = FindAnyObjectByType<DriverCamera>();
        driver = FindAnyObjectByType<DriverBody>();
        GameUI.PlayerOutside = false;
    }

    void OnDestroy() => GameUI.PlayerOutside = false;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || bus == null || GameUI.MenuOpen || GameUI.NoteOpen || GameUI.PcOpen || GameUI.MinigameOpen) return;
        bool ePressed = GameKeys.Pressed(GameAction.Interact) && !GameUI.JustClosed && !GameUI.TerminalTyping && !GameUI.MenuOpen && !GameUI.DialogueOpen;

        // In the bus, BusWalker handles E (stand up, get out through the door).
        if (!GameUI.PlayerOutside) return;

        Walk(kb);
        var usable = Interactable.Nearest(walker.transform.position, walker.transform.forward);
        if (ePressed && usable != null)
        {
            var arms = FirstPersonArms.Instance;
            if (arms != null && arms.Ready) { if (usable is Pickup) arms.Grab(); else arms.Push(); }
            usable.Use();
        }
        else if (ePressed && NearDoor) Enter();
        else if (GameKeys.Pressed(GameAction.Lights) && flashlight != null && !GameUI.MenuOpen) flashlight.enabled = !flashlight.enabled;
    }

    bool NearDoor
    {
        get
        {
            if (walker == null) return false;
            Vector3 d = walker.transform.position - bus.transform.TransformPoint(DoorOutsideLocal);
            d.y = 0f;
            return d.magnitude < enterDistance;
        }
    }

    /// <summary>Out of the bus through the front door (from the driver's seat / the aisle).</summary>
    public void GetOut() => Leave();

    void Leave()
    {
        if (driverCamera == null) return;
        bus.SetDoors(true);

        cam = driverCamera.transform;
        camParent = cam.parent;
        camLocalPos = cam.localPosition;
        camLocalRot = cam.localRotation;
        driverCamera.enabled = false;
        if (driver != null) driver.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;   // DriverCamera frees it when disabled
        Cursor.visible = false;

        Vector3 pos = bus.transform.TransformPoint(DoorOutsideLocal);
        pos.y = 0.05f;
        walker = new GameObject("Player (on foot)");
        walker.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(bus.transform.right * Mathf.Sign(DoorOutsideLocal.x)));
        controller = walker.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;

        cam.SetParent(walker.transform, false);
        cam.localPosition = new Vector3(0f, eyeHeight, 0f);
        cam.localRotation = Quaternion.identity;
        pitch = 0f;

        flashlight = new GameObject("Flashlight").AddComponent<Light>();
        flashlight.transform.SetParent(cam, false);
        flashlight.transform.localPosition = new Vector3(0.2f, -0.2f, 0.1f);
        flashlight.type = LightType.Spot;
        bool strong = Progress.Owns("flashlight2");
        // Soft beam: wide and gentle, so it lights things up instead of blinding.
        flashlight.spotAngle = strong ? 68f : 60f;
        flashlight.range = strong ? 30f : 20f;
        flashlight.intensity = strong ? 5f : 3.2f;
        flashlight.color = new Color(1f, 0.95f, 0.85f);
        flashlight.shadows = LightShadows.None;
        flashlight.enabled = false;

        GameUI.PlayerOutside = true;
    }

    void Enter()
    {
        cam.SetParent(camParent, false);
        cam.localPosition = camLocalPos;
        cam.localRotation = camLocalRot;
        driverCamera.enabled = true;
        if (driver != null) driver.gameObject.SetActive(true);
        // The flashlight hangs on the camera: switch it off / remove it inside the bus.
        if (flashlight != null) Destroy(flashlight.gameObject);
        flashlight = null;
        Destroy(walker);
        walker = null;
        GameUI.PlayerOutside = false;
    }

    /// <summary>Back into the driver's seat, wherever the player is (e.g. after dying).</summary>
    public void ForceEnter()
    {
        if (walker != null) Enter();
    }

    /// <summary>Move the walking player somewhere else (trapdoors, ladders).</summary>
    public void TeleportTo(Vector3 position, Vector3 lookDirection)
    {
        if (walker == null) return;
        controller.enabled = false;
        lookDirection.y = 0f;
        walker.transform.SetPositionAndRotation(position,
            lookDirection.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDirection) : walker.transform.rotation);
        controller.enabled = true;
        verticalSpeed = 0f;
    }

    /// <summary>Walking speed right now (m/s), for the arm swing.</summary>
    public float MoveSpeed { get; private set; }

    /// <summary>The flashlight while walking (null in the bus).</summary>
    public Light Flashlight => flashlight;

    /// <summary>The walking player, or null while in the bus.</summary>
    public Transform Walker => walker != null ? walker.transform : null;

    /// <summary>Put the walking player next to the bus door, looking at the bus.</summary>
    public void ReturnToBus()
    {
        if (walker == null) return;
        Vector3 door = bus.transform.TransformPoint(DoorOutsideLocal);
        Vector3 outward = bus.transform.right * Mathf.Sign(DoorOutsideLocal.x);
        Vector3 pos = door + outward * 2.5f;
        pos.y = 0.05f;
        controller.enabled = false;
        walker.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-outward));
        controller.enabled = true;
        pitch = 0f;
        cam.localRotation = Quaternion.identity;
        verticalSpeed = 0f;
    }

    void Walk(Keyboard kb)
    {
        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            walker.transform.Rotate(0f, d.x, 0f);
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !GameUI.PhoneOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Vector2 input = Vector2.zero;
        if (GameKeys.Held(GameAction.Forward)) input.y += 1f;
        if (GameKeys.Held(GameAction.Backward)) input.y -= 1f;
        if (GameKeys.Held(GameAction.SteerRight)) input.x += 1f;
        if (GameKeys.Held(GameAction.SteerLeft)) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);
        float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        MoveSpeed = input.magnitude * speed;

        Vector3 move = (walker.transform.forward * input.y + walker.transform.right * input.x) * speed;
        verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - 9.81f * Time.deltaTime;
        move.y = verticalSpeed;
        controller.Move(move * Time.deltaTime);

        // Footsteps.
        if (input.sqrMagnitude > 0.01f && controller.isGrounded)
        {
            stepTimer -= Time.deltaTime * speed;
            if (stepTimer <= 0f)
            {
                stepTimer = 1.4f;
                Passenger.RaiseStep(walker.transform.position);
            }
        }
    }

    void OnGUI()
    {
        if (bus == null) return;
        float w = RetroGUI.VirtualWidth;
        if (GameUI.PlayerOutside)
        {
            var usable = walker != null ? Interactable.Nearest(walker.transform.position, walker.transform.forward) : null;
            string text = usable != null ? usable.Prompt + "  " + GameKeys.Tag(GameAction.Interact) :
                NearDoor ? Loc.T("Einsteigen ", "Get in ") + GameKeys.Tag(GameAction.Interact) : Loc.T("Zurück zur Tür des Busses   -   Taschenlampe ", "Back to the bus door   -   Flashlight ") + GameKeys.Tag(GameAction.Lights);
            RetroGUI.ShadowLabel(new Rect(0, 318, w, 14), text, new Color(1f, 0.85f, 0.3f));
        }
        else if (!GameUI.InBus && !GameUI.MinigameOpen && Mathf.Abs(bus.Speed) < 0.3f && game != null && game.CurrentPhase == BoardingManager.Phase.Driving && !GameUI.TerminalTyping)
        {
            string seat = NightEvents.Instance != null ? NightEvents.Instance.SeatPrompt : null;
            RetroGUI.ShadowLabel(new Rect(0, 334, w, 14), (seat ?? Loc.T("Aufstehen", "Stand up")) + " " + GameKeys.Tag(GameAction.Interact), new Color(0.8f, 0.8f, 0.8f), false);
        }
    }
}
