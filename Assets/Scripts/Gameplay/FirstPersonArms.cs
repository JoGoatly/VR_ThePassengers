using UnityEngine;

/// <summary>
/// The player's own arms (PSX First Person Arms pack) while walking outside the bus.
/// Poses and moves with the pack's animations: relaxed, fists up, a hand holding the
/// flashlight / bat / pistol, punches, grabbing items and pushing trapdoors.
/// The rig's own "camera" bone is aligned with the game camera.
/// </summary>
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

    public PlayerOnFoot onFoot;
    public PlayerCombat combat;

    /// <summary>Right hand holder for flashlight / bat / pistol (aligned with the camera).</summary>
    public Transform RightGrip { get; private set; }
    public bool Ready => anim != null && RightGrip != null;

    Transform holder, instance, handR;
    Animation anim;
    string current;
    float oneShotUntil;
    bool punchLeft;
    Transform flashlightModel;
    Light heldLight;

    void Awake() => Instance = this;

    void Start()
    {
        if (onFoot == null) onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (combat == null) combat = FindAnyObjectByType<PlayerCombat>();
    }

    void LateUpdate()
    {
        bool outside = GameUI.PlayerOutside && onFoot != null && onFoot.Walker != null && Camera.main != null;
        if (!outside) { Remove(); return; }
        if (holder == null || holder.parent != Camera.main.transform) Build(Camera.main.transform);
        if (anim == null) return;

        UpdateFlashlight();
        if (Time.time >= oneShotUntil) Loop(IdleState());
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

        // Sample the relaxed pose and put the rig's camera bone onto the game camera,
        // turned so that the hands are in front of us.
        string relax = Clip("relax") ?? Clip("rest");
        if (relax != null) { anim.Play(relax); anim.Sample(); }
        var camBone = Find(instance, "camera");
        handR = Find(instance, "hand.R");
        var handL = Find(instance, "hand.L");
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

        // Grip in the right hand, oriented like the camera (things held in it point forward).
        if (handR != null)
        {
            RightGrip = new GameObject("Right Grip").transform;
            RightGrip.SetParent(handR, false);
            RightGrip.SetPositionAndRotation(handR.position + cam.forward * 0.04f, cam.rotation);
        }
        current = null;
    }

    void Remove()
    {
        if (heldLight != null && onFoot != null && onFoot.Walker != null && Camera.main != null) ReturnLightToCamera();
        if (holder != null) Destroy(holder.gameObject);
        holder = null;
        instance = null;
        anim = null;
        RightGrip = null;
        flashlightModel = null;
        heldLight = null;
    }

    // ---------------------------------------------------------------- animation

    string IdleState()
    {
        int weapon = combat != null ? combat.CurrentWeapon : 0;
        if (weapon != 0) return Clip("knife_idle");              // bat or pistol in the right hand
        if (heldLight != null) return Clip("knife_idle");         // holding the flashlight
        return combat != null && combat.InDanger ? Clip("guard_idle") ?? Clip("relax") : Clip("relax");
    }

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
        if (anim == null) return;
        bool left = heldLight != null || punchLeft;
        punchLeft = !punchLeft;
        Once(Clip(left ? "jab.L" : "jab.R"), 1.3f);
    }

    public void Swing() => Once(Random.value < 0.5f ? Clip("knife_hit_01") : Clip("knife_hit_02"), 1.1f);
    public void Grab() => Once(Clip("grab.R") ?? Clip("grab.L"), 1.4f);
    public void Push() => Once(Clip("push.R") ?? Clip("push.L"), 1.3f);

    // ---------------------------------------------------------------- flashlight in the hand

    void UpdateFlashlight()
    {
        var light = onFoot.Flashlight;
        bool hold = light != null && light.enabled && (combat == null || combat.CurrentWeapon == 0) && RightGrip != null;
        if (hold && heldLight == null)
        {
            if (flashlightModel == null)
            {
                flashlightModel = new GameObject("Flashlight Model").transform;
                flashlightModel.SetParent(RightGrip, false);
                var body = MeshKit.Spawn("Body", flashlightModel, MeshKit.Prism(0.022f, 0.17f, 6, 1f), flashlightMaterial, flashlightModel.position, flashlightModel.rotation, false);
                body.transform.localPosition = new Vector3(0f, 0f, -0.06f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // prism along +Z
            }
            flashlightModel.gameObject.SetActive(true);
            heldLight = light;
            heldLight.transform.SetParent(flashlightModel, false);
            heldLight.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            heldLight.transform.localRotation = Quaternion.identity;
        }
        else if (!hold && heldLight != null)
        {
            ReturnLightToCamera();
        }
        if (flashlightModel != null) flashlightModel.gameObject.SetActive(heldLight != null);
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
