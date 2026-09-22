using UnityEngine;

/// <summary>
/// Poses a rigged character (any Mixamo-style rig, no Humanoid avatar needed)
/// as the bus driver: hips on the seat, hands on the steering wheel rim,
/// feet on the pedals, all solved with two-bone IK every frame.
/// The head is hidden and the driver camera follows the character's eyes.
///
/// Put this on the character root, which is a child of the bus.
/// Move the Seat / pedal target objects to adjust the pose.
/// </summary>
[DefaultExecutionOrder(100)] // after BusController animated the steering wheel, before DriverCamera
public class DriverBody : MonoBehaviour
{
    [Header("Targets (children of the bus)")]
    public Transform seat;
    public Transform leftFootRest;
    public Transform throttlePedal;
    public Transform brakePedal;

    [Header("Body")]
    [Tooltip("Forward lean of the upper body in degrees")]
    public float spineLean = 12f;
    public bool hideHead = true;
    [Tooltip("Eye position relative to the head bone, in bus space (x right, y up, z forward)")]
    public Vector3 eyeOffset = new Vector3(0f, 0.09f, 0.1f);

    [Header("Hands")]
    [Tooltip("Grip position on the rim, clock angle in degrees (0 = top, -90 = 9 o'clock)")]
    public float leftGripAngle = -80f;
    public float rightGripAngle = 80f;
    [Tooltip("Rim radius as a fraction of the steering wheel mesh size")]
    public float rimRadiusFactor = 0.9f;
    [Tooltip("How far the hands turn with the wheel before they slide along the rim")]
    public float maxHandFollow = 110f;
    [Tooltip("Wrist distance from the grip point towards the shoulder")]
    public float wristOffset = 0.07f;
    public float fingerCurl = 55f;
    [Tooltip("Local axis the finger bones curl around")]
    public Vector3 fingerCurlAxis = Vector3.right;

    [Header("Feet")]
    [Tooltip("Toe-down angle when a pedal is fully pressed")]
    public float pedalPressAngle = 18f;
    public float pedalPressDistance = 0.04f;

    /// <summary>World position of the driver's eyes, updated every frame.</summary>
    public Transform EyeAnchor { get; private set; }

    BusController bus;
    Transform steeringWheel;
    float rimRadius = 0.25f;

    Transform hips, spine, head;
    Transform[] leftArm, rightArm, leftLeg, rightLeg; // upper, lower, end
    Vector3 leftArmHinge, rightArmHinge, leftLegHinge, rightLegHinge; // bend axes in upper bone space
    Quaternion leftFootRestRot, rightFootRestRot; // relative to character root
    Transform[] leftFingers, rightFingers;

    Transform[] posedBones;
    Quaternion[] restRotations;
    Vector3 headRestScale;
    float rightFootBrakeBlend;

    void Awake()
    {
        bus = GetComponentInParent<BusController>();
        Transform busRoot = bus != null ? bus.transform : transform.parent;

        if (TryGetComponent(out Animator animator) && animator.runtimeAnimatorController == null)
            animator.enabled = false; // nothing to play, don't let it touch the bones

        // The IK pose is far from the bind pose, keep the bounds correct.
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;

        hips = FindBone("Hips");
        spine = FindBone("Spine");
        head = FindBone("Head");
        leftArm = new[] { FindBone("LeftArm"), FindBone("LeftForeArm"), FindBone("LeftHand") };
        rightArm = new[] { FindBone("RightArm"), FindBone("RightForeArm"), FindBone("RightHand") };
        leftLeg = new[] { FindBone("LeftUpLeg"), FindBone("LeftLeg"), FindBone("LeftFoot") };
        rightLeg = new[] { FindBone("RightUpLeg"), FindBone("RightLeg"), FindBone("RightFoot") };

        if (hips == null || !Valid(leftArm) || !Valid(rightArm) || !Valid(leftLeg) || !Valid(rightLeg))
        {
            Debug.LogError($"{name}: DriverBody could not find the rig bones (Hips, LeftArm, LeftForeArm, LeftHand, LeftUpLeg, ...).", this);
            enabled = false;
            return;
        }

        leftFingers = leftArm[2].GetComponentsInChildren<Transform>();
        rightFingers = rightArm[2].GetComponentsInChildren<Transform>();

        // Natural bend axes from the rest pose: elbows bend so the hand moves forward,
        // knees bend so the foot moves backward.
        Vector3 fwd = busRoot.forward;
        leftArmHinge = HingeInUpperSpace(leftArm, fwd);
        rightArmHinge = HingeInUpperSpace(rightArm, fwd);
        leftLegHinge = HingeInUpperSpace(leftLeg, -fwd);
        rightLegHinge = HingeInUpperSpace(rightLeg, -fwd);

        leftFootRestRot = Quaternion.Inverse(transform.rotation) * leftLeg[2].rotation;
        rightFootRestRot = Quaternion.Inverse(transform.rotation) * rightLeg[2].rotation;

        var bones = new System.Collections.Generic.List<Transform> { hips, spine };
        bones.AddRange(leftArm); bones.AddRange(rightArm);
        bones.AddRange(leftLeg); bones.AddRange(rightLeg);
        bones.AddRange(leftFingers); bones.AddRange(rightFingers);
        bones.RemoveAll(b => b == null);
        posedBones = bones.ToArray();
        restRotations = System.Array.ConvertAll(posedBones, b => b.localRotation);

        if (head != null)
        {
            headRestScale = head.localScale;
            if (hideHead) head.localScale = headRestScale * 0.001f;
        }

        steeringWheel = FindChildRecursive(busRoot, "SteeringWheel");
        if (steeringWheel != null && steeringWheel.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
        {
            Vector3 ext = Vector3.Scale(mf.sharedMesh.bounds.extents, steeringWheel.lossyScale);
            rimRadius = Mathf.Max(ext.x, ext.y, ext.z) * rimRadiusFactor;
        }

        EyeAnchor = new GameObject("DriverEyes").transform;
        EyeAnchor.SetParent(busRoot, false);

        var cam = busRoot.GetComponentInChildren<DriverCamera>();
        if (cam != null && cam.eyeAnchor == null) cam.eyeAnchor = EyeAnchor;
    }

    void LateUpdate()
    {
        Transform busRoot = bus != null ? bus.transform : transform.parent;
        Vector3 up = busRoot.up, fwd = busRoot.forward, right = busRoot.right;

        for (int i = 0; i < posedBones.Length; i++)
            posedBones[i].localRotation = restRotations[i];

        // Sit: move the whole character so the hips are on the seat.
        if (seat != null) transform.position += seat.position - hips.position;

        if (spine != null) spine.rotation = Quaternion.AngleAxis(spineLean, right) * spine.rotation;

        // Hands on the rim.
        if (steeringWheel != null)
        {
            float turn = bus != null ? Mathf.Clamp(bus.SteeringWheelTurn, -maxHandFollow, maxHandFollow) : 0f;
            SolveArm(leftArm, leftArmHinge, GripPoint(leftGripAngle + turn, busRoot), -right, up);
            SolveArm(rightArm, rightArmHinge, GripPoint(rightGripAngle + turn, busRoot), right, up);
            CurlFingers(leftFingers, leftArm[2]);
            CurlFingers(rightFingers, rightArm[2]);
        }

        // Feet: left foot resting, right foot on throttle or brake.
        float throttle = bus != null ? bus.ThrottleInput : 0f;
        float brake = bus != null ? bus.BrakeInput : 0f;
        rightFootBrakeBlend = Mathf.MoveTowards(rightFootBrakeBlend, brake > 0.01f ? 1f : 0f, Time.deltaTime * 6f);

        if (leftFootRest != null)
            SolveLeg(leftLeg, leftLegHinge, leftFootRestRot, leftFootRest.position, 0f, -right, up, fwd);
        if (throttlePedal != null)
        {
            Vector3 pedal = brakePedal != null
                ? Vector3.Lerp(throttlePedal.position, brakePedal.position, rightFootBrakeBlend)
                : throttlePedal.position;
            float press = Mathf.Lerp(throttle, brake, rightFootBrakeBlend);
            SolveLeg(rightLeg, rightLegHinge, rightFootRestRot, pedal + fwd * pedalPressDistance * press, press, right, up, fwd);
        }

        if (head != null)
        {
            head.localScale = hideHead ? headRestScale * 0.001f : headRestScale;
            EyeAnchor.position = head.position + busRoot.TransformDirection(eyeOffset);
        }
    }

    Vector3 GripPoint(float clockAngle, Transform busRoot)
    {
        // Column axis pointing away from the driver; build the rim plane from the bus' up/right.
        Vector3 axis = steeringWheel.parent != null
            ? steeringWheel.parent.TransformDirection(bus != null ? bus.steeringColumnAxis : Vector3.forward).normalized
            : busRoot.forward;
        if (Vector3.Dot(axis, busRoot.forward) < 0f) axis = -axis;

        Vector3 rimUp = Vector3.ProjectOnPlane(busRoot.up, axis).normalized;
        Vector3 rimRight = Vector3.ProjectOnPlane(busRoot.right, axis).normalized;
        float a = clockAngle * Mathf.Deg2Rad;
        return steeringWheel.position + (rimUp * Mathf.Cos(a) + rimRight * Mathf.Sin(a)) * rimRadius;
    }

    void SolveArm(Transform[] arm, Vector3 hinge, Vector3 grip, Vector3 outward, Vector3 up)
    {
        Vector3 shoulder = arm[0].position;
        Vector3 wrist = grip + (shoulder - grip).normalized * wristOffset;
        // Elbows point down and slightly outwards.
        Vector3 pole = shoulder - up * 0.5f + outward * 0.25f;
        SolveTwoBone(arm[0], arm[1], arm[2], hinge, wrist, pole);
    }

    void SolveLeg(Transform[] leg, Vector3 hinge, Quaternion footRest, Vector3 ankle, float press,
                  Vector3 outward, Vector3 up, Vector3 fwd)
    {
        // Knees point forward, up and a bit outwards (around the steering column).
        Vector3 pole = leg[0].position + fwd * 0.6f + up * 0.4f + outward * 0.15f;
        SolveTwoBone(leg[0], leg[1], leg[2], hinge, ankle, pole);

        // Keep the foot flat on the floor, tip the toes down when pressing a pedal.
        Vector3 right = Vector3.Cross(up, fwd);
        leg[2].rotation = Quaternion.AngleAxis(pedalPressAngle * press, right) * transform.rotation * footRest;
    }

    void CurlFingers(Transform[] fingers, Transform hand)
    {
        if (Mathf.Approximately(fingerCurl, 0f)) return;
        Quaternion curl = Quaternion.AngleAxis(fingerCurl, fingerCurlAxis);
        foreach (var f in fingers)
            if (f != hand) f.localRotation = f.localRotation * curl;
    }

    /// <summary>
    /// Two-bone IK that keeps the joint's natural bend axis, so elbows and knees
    /// never bend the wrong way. hinge is the bend axis in the upper bone's local space.
    /// </summary>
    static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 hinge, Vector3 target, Vector3 pole)
    {
        Vector3 a = upper.position;
        float lenA = Vector3.Distance(a, lower.position);
        float lenB = Vector3.Distance(lower.position, end.position);
        if (lenA < 1e-4f || lenB < 1e-4f) return;

        Vector3 toTarget = target - a;
        float dist = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(lenA - lenB) + 1e-3f, lenA + lenB - 1e-3f);
        Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : upper.up;

        Vector3 poleDir = Vector3.ProjectOnPlane(pole - a, dir);
        if (poleDir.sqrMagnitude < 1e-8f) poleDir = Vector3.ProjectOnPlane(Vector3.up, dir);
        poleDir.Normalize();

        float cosA = Mathf.Clamp((lenA * lenA + dist * dist - lenB * lenB) / (2f * lenA * dist), -1f, 1f);
        float sinA = Mathf.Sqrt(1f - cosA * cosA);
        Vector3 jointPos = a + dir * (lenA * cosA) + poleDir * (lenA * sinA);
        Vector3 endPos = a + dir * dist;

        // Upper bone: point at the joint position with the hinge axis perpendicular to the limb plane.
        Vector3 curDir = (lower.position - a).normalized;
        Vector3 curHinge = Vector3.ProjectOnPlane(upper.TransformDirection(hinge), curDir).normalized;
        Vector3 newDir = (jointPos - a).normalized;
        Vector3 newHinge = Vector3.Cross(newDir, (endPos - jointPos).normalized);
        if (newHinge.sqrMagnitude < 1e-8f) newHinge = Vector3.Cross(newDir, poleDir);
        newHinge.Normalize();

        if (curHinge.sqrMagnitude > 1e-8f)
        {
            Quaternion from = Quaternion.LookRotation(curDir, curHinge);
            Quaternion to = Quaternion.LookRotation(newDir, newHinge);
            upper.rotation = to * Quaternion.Inverse(from) * upper.rotation;
        }
        else
        {
            upper.rotation = Quaternion.FromToRotation(curDir, newDir) * upper.rotation;
        }

        // Lower bone: bend around the (now aligned) hinge so the end reaches the target.
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, endPos - lower.position) * lower.rotation;
    }

    // Bend axis for a limb whose lower bone should swing towards bendDirection.
    static Vector3 HingeInUpperSpace(Transform[] limb, Vector3 bendDirection)
    {
        Vector3 limbDir = (limb[1].position - limb[0].position).normalized;
        Vector3 hinge = Vector3.Cross(limbDir, bendDirection);
        if (hinge.sqrMagnitude < 1e-6f) hinge = Vector3.Cross(limbDir, Vector3.up);
        return limb[0].InverseTransformDirection(hinge.normalized);
    }

    Transform FindBone(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            if (n == boneName) return t;
        }
        return null;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            var found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    static bool Valid(Transform[] chain) => chain[0] != null && chain[1] != null && chain[2] != null;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (seat != null) Gizmos.DrawWireCube(seat.position, Vector3.one * 0.12f);
        Gizmos.color = Color.green;
        if (leftFootRest != null) Gizmos.DrawWireSphere(leftFootRest.position, 0.05f);
        if (throttlePedal != null) Gizmos.DrawWireSphere(throttlePedal.position, 0.05f);
        Gizmos.color = Color.red;
        if (brakePedal != null) Gizmos.DrawWireSphere(brakePedal.position, 0.05f);
    }
}
