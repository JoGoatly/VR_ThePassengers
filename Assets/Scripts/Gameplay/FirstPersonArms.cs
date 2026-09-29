using UnityEngine;

/// <summary>
/// The player's own arms (PSX First Person Arms pack).
/// Walking: relaxed arms that swing with the steps, the flashlight in the right hand,
/// fists up in danger, punches, grabbing items, pushing trapdoors, bat and pistol in the hand.
/// Driving: the same arms hold the steering wheel (two-bone IK onto the rim grips of DriverBody).
/// The rig's own "camera" bone is aligned with the game camera.
/// </summary>
[DefaultExecutionOrder(300)] // after DriverBody (grips) and DriverCamera (camera pose)
public class FirstPersonArms : MonoBehaviour
{
    public static FirstPersonArms Instance { get; private set; }

    [Tooltip("arms_rig.fbx (imported with Legacy animation)")]
    public GameObject armsModel;
    [Tooltip("PSX material for the arms")]
    public Material armsMaterial;
    public Material flashlightMaterial;
    [Tooltip("Fine-tune where the arms sit in front of the camera (camera space)")]
    public Vector3 viewOffset = new Vector3(0f, -0.02f, 0f);
    [Tooltip("Arm swing while walking (degrees at running speed)")]
    public float walkSwing = 24f;
    public bool holdSteeringWheel = true;
    [Tooltip("Where the right hand holds the flashlight / bat / pistol (camera space, wrist position)")]
    public Vector3 holdPosition = new Vector3(0.19f, -0.21f, 0.33f);

    public PlayerOnFoot onFoot;
    public PlayerCombat combat;
    public DriverBody driver;

    /// <summary>Right hand holder for bat / pistol (aligned with the camera when built).</summary>
    public Transform RightGrip { get; private set; }
    public bool Ready => anim != null && RightGrip != null && !driving;
    /// <summary>The arms are on the steering wheel (the driver's own arms are hidden).</summary>
    public bool HoldsWheel => anim != null && driving;

    Transform holder, instance, camBone;
    Transform upperL, foreL, handL, upperR, foreR, handR;
    Vector3 hingeL, hingeR;
    Animation anim;
    string current;
    float oneShotUntil, walkPhase;
    bool punchLeft, driving, rightOneShot;
    Vector3 gripLocalPos;
    Quaternion gripLocalRot;
    float reachShift;
    Transform flashlightModel;
    Light heldLight;
    Vector3 basePosition;

    void Awake() => Instance = this;

    void Start()
    {
        if (onFoot == null) onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (combat == null) combat = FindAnyObjectByType<PlayerCombat>();
        if (driver == null) driver = FindAnyObjectByType<DriverBody>();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        bool outside = GameUI.PlayerOutside && onFoot != null && onFoot.Walker != null && cam != null;
        bool inSeat = !GameUI.PlayerOutside && holdSteeringWheel && cam != null && driver != null && driver.isActiveAndEnabled && driver.HasGrips;
        if (!outside && !inSeat) { Remove(); return; }

        if (holder == null || holder.parent != cam.transform || driving != inSeat)
        {
            driving = inSeat;
            Build(cam.transform);
        }
        if (anim == null) return;

        if (driving) DriveWheel();
        else
        {
            if (Time.time >= oneShotUntil) { Loop(IdleState()); rightOneShot = false; }
            Walk(cam.transform);
            HoldRightHand(cam.transform);
            UpdateFlashlight(cam.transform);
        }
    }

    // ---------------------------------------------------------------- building

    void Build(Transform cam)
    {
        Remove();
        if (armsModel == null) return;
        holder = new GameObject("First Person Arms").transform;
        holder.SetParent(cam, false);
        instance = Instantiate(armsModel, holder).transform;
        instance.localPosition = Vector3.zero;
        instance.localRotation = Quaternion.identity;
        foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in instance.GetComponentsInChildren<Renderer>())
        {
            if (armsMaterial != null)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = armsMaterial;
                r.sharedMaterials = mats;
            }
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        anim = instance.GetComponentInChildren<Animation>();
        if (anim == null)
        {
            Debug.LogWarning("FirstPersonArms: arms_rig.fbx has no legacy Animation (set Rig > Animation Type to Legacy).", this);
            holder.gameObject.SetActive(false);
            return;
        }
        anim.playAutomatically = false;
        anim.cullingType = AnimationCullingType.AlwaysAnimate;

        camBone = Find(instance, "camera");
        upperL = Find(instance, "upper_arm.L"); foreL = Find(instance, "forearm.L"); handL = Find(instance, "hand.L");
        upperR = Find(instance, "upper_arm.R"); foreR = Find(instance, "forearm.R"); handR = Find(instance, "hand.R");

        // Sample the relaxed pose and put the rig's camera bone onto the game camera,
        // turned so that the hands are in front of us.
        string relax = Clip("relax") ?? Clip("rest");
        if (relax != null) { anim.Play(relax); anim.Sample(); }
        if (camBone != null && handR != null && handL != null)
        {
            Vector3 eye = holder.InverseTransformPoint(camBone.position);
            Vector3 mid = holder.InverseTransformPoint((handR.position + handL.position) * 0.5f);
            Vector3 fwd = mid - eye;
            fwd.y = 0f;
            float yaw = fwd.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(fwd, Vector3.forward, Vector3.up) : 0f;
            instance.localRotation = Quaternion.Euler(0f, yaw, 0f);
            eye = instance.localRotation * eye;
            instance.localPosition = -eye + viewOffset;
        }

        basePosition = instance.localPosition;

        // Elbow bend axes for the steering wheel IK (elbows bend so the hands go forward).
        if (upperL != null && foreL != null && handL != null)
            hingeL = DriverBody.HingeInUpperSpace(new[] { upperL, foreL, handL }, cam.forward);
        if (upperR != null && foreR != null && handR != null)
            hingeR = DriverBody.HingeInUpperSpace(new[] { upperR, foreR, handR }, cam.forward);

        // Grip in the right hand, oriented like the camera (things held in it point forward).
        if (handR != null)
        {
            RightGrip = new GameObject("Right Grip").transform;
            RightGrip.SetParent(handR, false);
            RightGrip.SetPositionAndRotation(handR.position + cam.forward * 0.04f, cam.rotation);
            // The rig's bones are scaled (FBX units): keep held things at their real size.
            Vector3 ls = handR.lossyScale;
            RightGrip.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            gripLocalPos = RightGrip.localPosition;
            gripLocalRot = RightGrip.localRotation;
        }
        reachShift = 0f;
        current = null;
    }

    void Remove()
    {
        ReturnLightToCamera();
        if (holder != null) Destroy(holder.gameObject);
        if (flashlightModel != null) Destroy(flashlightModel.gameObject);
        holder = null;
        instance = null;
        anim = null;
        RightGrip = null;
        flashlightModel = null;
    }

    // ---------------------------------------------------------------- walking

    string IdleState()
    {
        int weapon = combat != null ? combat.CurrentWeapon : 0;
        if (weapon != 0) return Clip("knife_idle");              // bat or pistol in the right hand
        if (heldLight != null) return Clip("knife_idle");         // holding the flashlight
        if (combat != null && combat.InDanger) return Clip("guard_idle") ?? Clip("relax");
        return Clip("rest") ?? Clip("relax");
    }

    // Arms swing with the steps like when really walking: the free arms swing
    // forwards and backwards in opposite directions, the whole view bobs a little.
    void Walk(Transform cam)
    {
        float speed = onFoot != null ? onFoot.MoveSpeed : 0f;
        float amount = Mathf.Clamp01(speed / 4f);
        walkPhase += Time.deltaTime * (speed > 0.1f ? 2.2f + speed * 1.1f : 0f);
        float s = Mathf.Sin(walkPhase);
        bool rightBusy = heldLight != null || (combat != null && combat.CurrentWeapon != 0) || Time.time < oneShotUntil;
        bool leftBusy = Time.time < oneShotUntil || (combat != null && combat.InDanger && combat.CurrentWeapon == 0 && heldLight == null);
        float angle = s * walkSwing * amount;
        if (upperL != null && !leftBusy) upperL.rotation = Quaternion.AngleAxis(angle, cam.right) * upperL.rotation;
        if (upperR != null && !rightBusy) upperR.rotation = Quaternion.AngleAxis(-angle, cam.right) * upperR.rotation;
        // Held things sway a little less.
        if (upperR != null && rightBusy && Time.time >= oneShotUntil) upperR.rotation = Quaternion.AngleAxis(-angle * 0.2f, cam.right) * upperR.rotation;
        instance.localPosition = basePosition - Vector3.up * Mathf.Abs(Mathf.Cos(walkPhase)) * 0.012f * amount;
    }

    // Holding something: the right hand is brought to a fixed place in front of us (IK),
    // the lamp / weapon in it points where we look. Right-hand animations (swing, grab)
    // take over while they play.
    void HoldRightHand(Transform cam)
    {
        if (RightGrip == null) return;
        bool holding = (combat != null && combat.CurrentWeapon != 0) || heldLight != null ||
                       (onFoot != null && onFoot.Flashlight != null && onFoot.Flashlight.enabled);
        if (!holding || rightOneShot || upperR == null || foreR == null || handR == null)
        {
            RightGrip.localPosition = gripLocalPos;
            RightGrip.localRotation = gripLocalRot;
            return;
        }
        float amount = Mathf.Clamp01((onFoot != null ? onFoot.MoveSpeed : 0f) / 4f);
        Vector3 sway = new Vector3(Mathf.Sin(walkPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(walkPhase)) * 0.01f, 0f) * amount;
        Vector3 wrist = cam.TransformPoint(holdPosition + sway);
        Vector3 pole = upperR.position - cam.up * 0.5f + cam.right * 0.35f;
        DriverBody.SolveTwoBone(upperR, foreR, handR, hingeR, wrist, pole);
        RightGrip.SetPositionAndRotation(handR.position + cam.forward * 0.05f + cam.up * 0.01f, cam.rotation);
    }

    // ---------------------------------------------------------------- driving

    void DriveWheel()
    {
        Loop(Clip("guard_idle") ?? Clip("relax"));   // closed hands, as if gripping
        if (driver == null || !driver.HasGrips) return;
        var up = driver.transform.parent != null ? driver.transform.parent.up : Vector3.up;
        var right = driver.transform.parent != null ? driver.transform.parent.right : Vector3.right;

        // The rig's arms are short: move them towards the wheel until both hands reach the rim.
        instance.localPosition = basePosition;
        float need = Mathf.Max(Excess(upperL, foreL, handL, driver.LeftGrip), Excess(upperR, foreR, handR, driver.RightGrip));
        reachShift = Mathf.MoveTowards(reachShift, Mathf.Clamp(need, 0f, 0.45f), Time.deltaTime * 0.5f);
        if (reachShift > 0f && upperL != null && upperR != null)
        {
            Vector3 dir = ((driver.LeftGrip + driver.RightGrip) - (upperL.position + upperR.position)).normalized;
            instance.position += dir * reachShift;
        }
        Solve(upperL, foreL, handL, hingeL, driver.LeftGrip, -right, up);
        Solve(upperR, foreR, handR, hingeR, driver.RightGrip, right, up);
    }

    // How much further the hand would have to reach (0 when the grip is in reach).
    static float Excess(Transform upper, Transform fore, Transform hand, Vector3 grip)
    {
        if (upper == null || fore == null || hand == null) return 0f;
        float reach = Vector3.Distance(upper.position, fore.position) + Vector3.Distance(fore.position, hand.position);
        return Vector3.Distance(upper.position, grip) + 0.02f - reach * 0.93f;
    }

    void Solve(Transform upper, Transform fore, Transform hand, Vector3 hinge, Vector3 grip, Vector3 outward, Vector3 up)
    {
        if (upper == null || fore == null || hand == null) return;
        Vector3 wrist = grip + (upper.position - grip).normalized * 0.06f;
        Vector3 pole = upper.position - up * 0.5f + outward * 0.3f;
        DriverBody.SolveTwoBone(upper, fore, hand, hinge, wrist, pole);
    }

    // ---------------------------------------------------------------- animation

    string Clip(string suffix)
    {
        if (anim == null) return null;
        foreach (AnimationState st in anim)
            if (st.name.EndsWith(suffix)) return st.name;
        return null;
    }

    void Loop(string clip)
    {
        if (clip == null || clip == current) return;
        anim[clip].wrapMode = WrapMode.Loop;
        anim.CrossFade(clip, 0.25f);
        current = clip;
    }

    void Once(string clip, float speed = 1f)
    {
        if (anim == null || clip == null) return;
        var st = anim[clip];
        st.wrapMode = WrapMode.Once;
        st.speed = speed;
        anim.CrossFade(clip, 0.08f);
        anim[clip].time = 0f;
        current = clip;
        oneShotUntil = Time.time + st.length / Mathf.Max(0.1f, speed) - 0.1f;
    }

    /// <summary>A punch with the free hand (left if the right one holds something).</summary>
    public void Punch()
    {
        if (anim == null || driving) return;
        bool left = heldLight != null || punchLeft;
        punchLeft = !punchLeft;
        Once(Clip(left ? "jab.L" : "jab.R"), 1.3f);
        rightOneShot = !left;
    }

    public void Swing()
    {
        if (driving) return;
        Once(Random.value < 0.5f ? Clip("knife_hit_01") : Clip("knife_hit_02"), 1.1f);
        rightOneShot = true;
    }

    // Grab / push with the free hand (the left one while the right holds something).
    public void Grab() { if (!driving) OnceWithFreeHand("grab"); }
    public void Push() { if (!driving) OnceWithFreeHand("push"); }

    void OnceWithFreeHand(string action)
    {
        bool rightBusy = heldLight != null || (combat != null && combat.CurrentWeapon != 0);
        string clip = rightBusy ? Clip(action + ".L") ?? Clip(action + ".R") : Clip(action + ".R") ?? Clip(action + ".L");
        Once(clip, 1.4f);
        rightOneShot = clip != null && clip.EndsWith(".R");
    }

    // ---------------------------------------------------------------- flashlight in the hand

    // The lamp sits in the right hand (in the grip, which points where we look).
    void UpdateFlashlight(Transform cam)
    {
        var light = onFoot.Flashlight;
        bool hold = light != null && light.enabled && (combat == null || combat.CurrentWeapon == 0) && RightGrip != null;
        if (hold && heldLight == null)
        {
            if (flashlightModel == null)
            {
                flashlightModel = new GameObject("Flashlight Model").transform;
                flashlightModel.SetParent(RightGrip, false);
                flashlightModel.localPosition = Vector3.zero;
                flashlightModel.localRotation = Quaternion.identity;
                if (!FlashlightModel(flashlightModel))
                {
                    var body = MeshKit.Spawn("Body", flashlightModel, MeshKit.Prism(0.018f, 0.16f, 6, 1f), flashlightMaterial, Vector3.zero, Quaternion.identity, false);
                    body.transform.localPosition = new Vector3(0f, 0f, -0.06f);
                    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // prism along +Z
                    var head = MeshKit.Spawn("Head", flashlightModel, MeshKit.Prism(0.026f, 0.04f, 6, 1f), flashlightMaterial, Vector3.zero, Quaternion.identity, false);
                    head.transform.localPosition = new Vector3(0f, 0f, 0.09f);
                    head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }
            heldLight = light;
            heldLight.transform.SetParent(flashlightModel, false);
            heldLight.transform.localPosition = new Vector3(0f, 0f, 0.14f);
            heldLight.transform.localRotation = Quaternion.identity;
        }
        else if (!hold && heldLight != null)
        {
            ReturnLightToCamera();
        }
        if (flashlightModel != null) flashlightModel.gameObject.SetActive(heldLight != null);
        // The light always shines where we look, even while the hand moves.
        if (heldLight != null) heldLight.transform.rotation = cam.rotation;
    }

    [Tooltip("Turn the flashlight model around if the lamp points backwards")]
    public bool flipFlashlightModel;

    // The flashlight model (Resources/Props/flashlight): about 20 cm long, pointing along +Z.
    bool FlashlightModel(Transform parent)
    {
        var holder = new GameObject("Flashlight Mesh").transform;
        holder.SetParent(parent, false);
        var model = PsxConvert.Spawn("Props/flashlight", holder);
        if (model == null) { Destroy(holder.gameObject); return false; }
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        var b = PsxConvert.LocalBounds(model.transform, holder);
        // Longest side becomes the lamp's axis (+Z).
        if (b.size.x >= b.size.y && b.size.x >= b.size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
        else if (b.size.y >= b.size.z) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * model.transform.localRotation;
        if (flipFlashlightModel) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
        b = PsxConvert.LocalBounds(model.transform, holder);
        float length = Mathf.Max(0.001f, b.size.z);
        model.transform.localScale *= 0.2f / length;
        b = PsxConvert.LocalBounds(model.transform, holder);
        model.transform.localPosition -= b.center - new Vector3(0f, 0f, 0.02f);
        return true;
    }

    void ReturnLightToCamera()
    {
        if (heldLight != null && Camera.main != null)
        {
            heldLight.transform.SetParent(Camera.main.transform, false);
            heldLight.transform.localPosition = new Vector3(0.2f, -0.2f, 0.1f);
            heldLight.transform.localRotation = Quaternion.identity;
        }
        heldLight = null;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
