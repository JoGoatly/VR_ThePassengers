using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Arcade bus driving based on a kinematic bicycle model: the bus turns around
/// its rear axle like a real bus and never drifts. Put this on the root object;
/// the bus model is a child whose parts (Wheel_FL, Wheel_FR, Wheels_B,
/// SteeringWheel, Door_*) are found by name.
///
/// Controls (keyboard / gamepad):
///   W / Up / RT      accelerate
///   S / Down / LT    brake, reverse when stopped
///   A D / Left Right / left stick   steer
///   Space / A        handbrake
///   F / Y            open / close doors
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    [Header("Engine")]
    [Tooltip("Top speed in km/h")]
    public float maxSpeedKmh = 60f;
    [Tooltip("Top reverse speed in km/h")]
    public float maxReverseSpeedKmh = 12f;
    [Tooltip("Acceleration in m/s²")]
    public float acceleration = 2.2f;
    [Tooltip("Reverse acceleration in m/s²")]
    public float reverseAcceleration = 1.5f;
    [Tooltip("Service brake deceleration in m/s²")]
    public float brakeDeceleration = 6f;
    [Tooltip("Handbrake deceleration in m/s²")]
    public float handbrakeDeceleration = 8f;
    [Tooltip("Deceleration when rolling without input in m/s²")]
    public float rollingDeceleration = 0.4f;

    [Header("Steering")]
    [Tooltip("Maximum front wheel angle in degrees. Fixed lock, the steering wheel never turns further.")]
    public float maxSteerAngle = 38f;
    [Tooltip("How fast the front wheels turn when standing still, degrees per second")]
    public float steerSpeed = 70f;
    [Tooltip("How fast the front wheels turn at top speed, degrees per second")]
    public float steerSpeedAtTopSpeed = 20f;
    [Tooltip("How fast the front wheels return to center, degrees per second")]
    public float steerReturnSpeed = 90f;
    [Tooltip("Steering wheel rotation at full lock in degrees (keep it below DriverBody.maxHandFollow so the hands stay on the rim)")]
    public float maxSteeringWheelTurn = 100f;
    [Tooltip("Rotation axis of the steering wheel (steering column) in the bus model's space")]
    public Vector3 steeringColumnAxis = new Vector3(0.688f, -0.726f, 0f);
    public bool invertSteeringWheel = false;

    [Header("Doors")]
    public float doorOpenAngle = 85f;
    [Tooltip("Degrees per second")]
    public float doorSpeed = 140f;
    public bool doorsOpen = false;
    [Tooltip("Doors only open below this speed (km/h)")]
    public float maxDoorOpenSpeedKmh = 5f;

    /// <summary>Set by the game (e.g. passenger at the door): no throttle while not null.</summary>
    [System.NonSerialized] public string throttleLockReason;
    /// <summary>Set by the game: doors can't be opened or closed.</summary>
    [System.NonSerialized] public bool doorsLocked;

    [Header("Parts (found by name if empty)")]
    public Transform[] frontWheels;
    public Transform[] rearWheels;
    public Transform steeringWheel;
    public Transform[] doors;

    [Header("HUD")]
    public bool showSpeedometer = true;

    /// <summary>Raised when the doors start opening (true) or closing (false).</summary>
    public event System.Action<bool> DoorsChanged;

    /// <summary>Signed speed along the bus forward axis in m/s.</summary>
    public float Speed { get; private set; }
    public float SpeedKmh => Speed * 3.6f;
    public float SteerAngle => steerAngle;
    /// <summary>Steering wheel rotation in degrees, positive = clockwise (right) as seen by the driver.</summary>
    public float SteeringWheelTurn => maxSteerAngle > 0f ? steerAngle / maxSteerAngle * maxSteeringWheelTurn : 0f;
    public float ThrottleInput => throttleInput;
    public bool DoorsFullyOpen => doorAmount >= 0.99f;
    public bool DoorsFullyClosed => doorAmount <= 0.01f;
    /// <summary>Why the bus can't drive right now (null = it can).</summary>
    public string DriveLockReason => throttleLockReason ?? (DoorsFullyClosed ? null : Loc.T("Türen offen", "doors open"));
    public float BrakeInput => brakeInput;

    Rigidbody rb;
    float steerAngle;
    float wheelSpin;
    float wheelRadius = 0.45f;
    float doorAmount;
    Vector3 rearAxleLocal;
    float wheelbase = 5f;

    Quaternion[] frontWheelRest, rearWheelRest, doorRest;
    Quaternion steeringWheelRest;
    float[] doorSign;

    float throttleInput, brakeInput, steerInput;
    bool handbrakeInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        FindParts();
        CacheRestPoses();
        SetupGeometry();
        EnsureCollider();
    }

    void FindParts()
    {
        if (frontWheels == null || frontWheels.Length == 0)
            frontWheels = FindAll("Wheel_FL", "Wheel_FR");
        if (rearWheels == null || rearWheels.Length == 0)
            rearWheels = FindAll("Wheels_B", "Wheel_BL", "Wheel_BR");
        if (steeringWheel == null)
            steeringWheel = FindChild(transform, "SteeringWheel");
        if (doors == null || doors.Length == 0)
            doors = FindAll("Door_FL", "Door_FR", "Door_BL", "Door_BR");
    }

    Transform[] FindAll(params string[] names)
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var n in names)
        {
            var t = FindChild(transform, n);
            if (t != null) list.Add(t);
        }
        return list.ToArray();
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void CacheRestPoses()
    {
        frontWheelRest = System.Array.ConvertAll(frontWheels, w => w.localRotation);
        rearWheelRest = System.Array.ConvertAll(rearWheels, w => w.localRotation);
        doorRest = System.Array.ConvertAll(doors, d => d.localRotation);
        if (steeringWheel != null) steeringWheelRest = steeringWheel.localRotation;

        // Swing each door leaf outwards, whichever side its hinge is on.
        doorSign = new float[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            var rend = door.GetComponent<Renderer>();
            if (rend == null || door.parent == null) { doorSign[i] = 1f; continue; }

            Vector3 leaf = door.parent.InverseTransformDirection(rend.bounds.center - door.position);
            Vector3 outward = door.parent.InverseTransformDirection(door.position - transform.position);
            Vector3 lateral = door.parent.InverseTransformDirection(transform.right);
            outward = Vector3.Project(outward, lateral);
            leaf.y = 0f;
            doorSign[i] = Mathf.Sign(Vector3.Cross(leaf, outward).y);
        }
    }

    void SetupGeometry()
    {
        var wheel = frontWheels.Length > 0 ? frontWheels[0] : (rearWheels.Length > 0 ? rearWheels[0] : null);
        if (wheel != null && wheel.TryGetComponent(out Renderer wheelRenderer))
            wheelRadius = Mathf.Max(0.05f, wheelRenderer.bounds.extents.y);

        if (frontWheels.Length > 0 && rearWheels.Length > 0)
        {
            Vector3 front = Vector3.zero, rear = Vector3.zero;
            foreach (var w in frontWheels) front += transform.InverseTransformPoint(w.position);
            foreach (var w in rearWheels) rear += transform.InverseTransformPoint(w.position);
            front /= frontWheels.Length;
            rear /= rearWheels.Length;
            rearAxleLocal = rear;
            wheelbase = Mathf.Max(0.5f, front.z - rear.z);
        }
    }

    // The FBX has no colliders: fit a frictionless box around the whole bus.
    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        foreach (var r in renderers)
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = (min + max) * 0.5f;
        box.size = max - min;
        box.material = new PhysicsMaterial("Bus (frictionless)")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
        };

        // Low center of mass, around the floor.
        rb.centerOfMass = new Vector3(box.center.x, min.y + box.size.y * 0.25f, box.center.z);
    }

    void Update()
    {
        ReadInput();
        AnimateParts();
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        float throttle = 0f, brake = 0f, steer = 0f;
        bool handbrake = false, toggleDoors = false;

        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
            handbrake |= kb.spaceKey.isPressed;
            toggleDoors |= kb.fKey.wasPressedThisFrame;
        }

        if (gp != null)
        {
            throttle = Mathf.Max(throttle, gp.rightTrigger.ReadValue());
            brake = Mathf.Max(brake, gp.leftTrigger.ReadValue());
            float stick = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.1f) steer = stick;
            handbrake |= gp.buttonSouth.isPressed;
            toggleDoors |= gp.buttonNorth.wasPressedThisFrame;
        }

        // Typing in the computer etc.: hold the bus.
        if (GameUI.AnyOpen)
        {
            throttle = brake = steer = 0f;
            handbrake = true;
            toggleDoors = false;
        }

        // Door interlock: no throttle and no reversing while doors are open or a passenger boards.
        if (DriveLockReason != null)
        {
            throttle = 0f;
            if (Speed < 0.3f) { brake = 0f; handbrake = true; }
        }

        throttleInput = throttle;
        brakeInput = brake;
        steerInput = Mathf.Clamp(steer, -1f, 1f);
        handbrakeInput = handbrake;
        if (toggleDoors && !doorsLocked && (doorsOpen || Mathf.Abs(SpeedKmh) <= maxDoorOpenSpeedKmh))
            SetDoors(!doorsOpen);
    }

    /// <summary>Open or close the doors (with sound event).</summary>
    public void SetDoors(bool open)
    {
        if (doorsOpen == open) return;
        doorsOpen = open;
        DoorsChanged?.Invoke(doorsOpen);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float maxSpeed = maxSpeedKmh / 3.6f;
        float maxReverse = maxReverseSpeedKmh / 3.6f;

        // Use the real velocity so collisions slow the bus down.
        float speed = Vector3.Dot(rb.linearVelocity, transform.forward);

        float accel = 0f;
        if (handbrakeInput)
        {
            accel = -Mathf.Sign(speed) * Mathf.Min(handbrakeDeceleration, Mathf.Abs(speed) / dt);
        }
        else if (throttleInput > 0.01f || brakeInput > 0.01f)
        {
            if (throttleInput > 0.01f)
            {
                if (speed < -0.3f) accel += brakeDeceleration * throttleInput;
                else if (speed < maxSpeed) accel += acceleration * throttleInput;
            }
            if (brakeInput > 0.01f)
            {
                if (speed > 0.3f) accel -= brakeDeceleration * brakeInput;
                else if (speed > -maxReverse) accel -= reverseAcceleration * brakeInput;
            }
        }
        else
        {
            accel = -Mathf.Sign(speed) * Mathf.Min(rollingDeceleration, Mathf.Abs(speed) / dt);
        }

        speed = Mathf.Clamp(speed + accel * dt, -maxReverse, maxSpeed);
        Speed = speed;

        // Fixed steering lock; at high speed the wheel just turns more slowly.
        float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
        float targetSteer = steerInput * maxSteerAngle;
        float rate = Mathf.Abs(targetSteer) < Mathf.Abs(steerAngle)
            ? steerReturnSpeed
            : Mathf.Lerp(steerSpeed, steerSpeedAtTopSpeed, speedFactor);
        steerAngle = Mathf.Clamp(Mathf.MoveTowards(steerAngle, targetSteer, rate * dt), -maxSteerAngle, maxSteerAngle);

        // Bicycle model: yaw rate = v / L * tan(delta), rotating around the rear axle.
        float yawRate = speed / wheelbase * Mathf.Tan(steerAngle * Mathf.Deg2Rad);
        Vector3 omega = transform.up * yawRate;
        Vector3 rearAxle = transform.TransformPoint(rearAxleLocal);
        Vector3 velocity = transform.forward * speed + Vector3.Cross(omega, rb.worldCenterOfMass - rearAxle);
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;
        rb.angularVelocity = omega;
    }

    void AnimateParts()
    {
        float dt = Time.deltaTime;
        wheelSpin = Mathf.Repeat(wheelSpin + Speed / wheelRadius * Mathf.Rad2Deg * dt, 360f);

        for (int i = 0; i < frontWheels.Length; i++)
            frontWheels[i].localRotation = WheelRotation(frontWheels[i], steerAngle) * frontWheelRest[i];
        for (int i = 0; i < rearWheels.Length; i++)
            rearWheels[i].localRotation = WheelRotation(rearWheels[i], 0f) * rearWheelRest[i];

        if (steeringWheel != null && steeringColumnAxis.sqrMagnitude > 0f)
        {
            float angle = SteeringWheelTurn * (invertSteeringWheel ? 1f : -1f);
            angle *= Handedness(steeringWheel.parent);
            steeringWheel.localRotation = Quaternion.AngleAxis(angle, steeringColumnAxis.normalized) * steeringWheelRest;
        }

        doorAmount = Mathf.MoveTowards(doorAmount, doorsOpen ? 1f : 0f, doorSpeed / Mathf.Max(1f, doorOpenAngle) * dt);
        float eased = Mathf.SmoothStep(0f, 1f, doorAmount);
        for (int i = 0; i < doors.Length; i++)
        {
            Vector3 up = doors[i].parent != null ? doors[i].parent.InverseTransformDirection(transform.up).normalized : Vector3.up;
            doors[i].localRotation = Quaternion.AngleAxis(doorSign[i] * doorOpenAngle * eased, up) * doorRest[i];
        }
    }

    // Steering around the bus' up axis, then rolling around the axle, both in the wheel's parent space.
    // -1 inside a mirrored (negatively scaled) model: local rotations appear reversed in the world.
    static float Handedness(Transform t)
    {
        if (t == null) return 1f;
        Vector3 s = t.lossyScale;
        return s.x * s.y * s.z < 0f ? -1f : 1f;
    }

    Quaternion WheelRotation(Transform wheel, float steer)
    {
        Transform parent = wheel.parent;
        Vector3 up = parent != null ? parent.InverseTransformDirection(transform.up).normalized : Vector3.up;
        Vector3 forward = parent != null ? parent.InverseTransformDirection(transform.forward).normalized : Vector3.forward;
        Quaternion steerRot = Quaternion.AngleAxis(steer * Handedness(parent), up);
        Vector3 axle = Vector3.Cross(up, steerRot * forward);
        return Quaternion.AngleAxis(wheelSpin, axle) * steerRot;
    }

    void OnGUI()
    {
        if (!showSpeedometer) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.black;
        string lockText = DriveLockReason != null ? "  [" + DriveLockReason.ToUpperInvariant() + "]" : "";
        var lights = GetComponent<BusLights>();
        string lightText = lights != null ? "   " + lights.ModeText : "";
        string text = $"{Mathf.Abs(SpeedKmh):0} km/h{(Speed < -0.1f ? "  R" : "")}{lockText}{lightText}";
        GUI.Label(new Rect(22, Screen.height - 58, 600, 40), text, style);
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(20, Screen.height - 60, 600, 40), text, style);
    }
}
