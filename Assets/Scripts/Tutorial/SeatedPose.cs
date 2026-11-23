using UnityEngine;

/// <summary>
/// Makes a character model (child of this object) sit on a chair: this object stands on the
/// floor in front of the chair's seat, facing the way the person looks. Thighs forward, shins
/// down, hands on the knees or the table. Works with the Mixamo rigs of the passenger models.
/// </summary>
[DefaultExecutionOrder(320)]
public class SeatedPose : MonoBehaviour
{
    [Tooltip("Height of the seat above this object's position (floor)")]
    public float seatHeight = 0.47f;
    [Tooltip("Hands forward onto the table instead of resting on the knees")]
    public bool handsOnTable;
    [Tooltip("Turn the head towards this (optional)")]
    public Transform lookAt;

    Transform model, hips, spine, head, lUp, rUp, lLeg, rLeg, lArm, rArm, lFore, rFore;
    Quaternion lUpRest, rUpRest, lLegRest, rLegRest, lArmRest, rArmRest, lForeRest, rForeRest, spineRest, headRest;
    Vector3 hipsRest;

    void Start()
    {
        if (transform.childCount == 0) return;
        model = transform.GetChild(0);
        foreach (var a in model.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        PsxConvert.Apply(model.gameObject);
        hips = Bone("Hips"); spine = Bone("Spine1") ?? Bone("Spine"); head = Bone("Head");
        lUp = Bone("LeftUpLeg"); rUp = Bone("RightUpLeg"); lLeg = Bone("LeftLeg"); rLeg = Bone("RightLeg");
        lArm = Bone("LeftArm"); rArm = Bone("RightArm"); lFore = Bone("LeftForeArm"); rFore = Bone("RightForeArm");
        if (hips == null) return;
        hipsRest = hips.localPosition;
        Store(lUp, ref lUpRest); Store(rUp, ref rUpRest); Store(lLeg, ref lLegRest); Store(rLeg, ref rLegRest);
        Store(lArm, ref lArmRest); Store(rArm, ref rArmRest); Store(lFore, ref lForeRest); Store(rFore, ref rForeRest);
        Store(spine, ref spineRest); Store(head, ref headRest);
    }

    static void Store(Transform t, ref Quaternion q) { if (t != null) q = t.localRotation; }

    void LateUpdate()
    {
        if (hips == null) return;
        Vector3 right = transform.right, fwd = transform.forward, up = transform.up;
        // Back to the standing pose, then bend.
        hips.localPosition = hipsRest;
        if (lUp) lUp.localRotation = lUpRest; if (rUp) rUp.localRotation = rUpRest;
        if (lLeg) lLeg.localRotation = lLegRest; if (rLeg) rLeg.localRotation = rLegRest;
        if (lArm) lArm.localRotation = lArmRest; if (rArm) rArm.localRotation = rArmRest;
        if (lFore) lFore.localRotation = lForeRest; if (rFore) rFore.localRotation = rForeRest;
        if (spine) spine.localRotation = spineRest; if (head) head.localRotation = headRest;

        // Hips down onto the seat (a little behind this object's position).
        Vector3 seat = transform.position + up * seatHeight - fwd * 0.05f;
        hips.position = new Vector3(seat.x, seat.y + 0.08f, seat.z);

        // Thighs forward, shins straight down.
        foreach (var (thigh, shin) in new[] { (lUp, lLeg), (rUp, rLeg) })
        {
            if (thigh == null || shin == null) continue;
            Point(thigh, shin.position, fwd * 1f - up * 0.05f);
            var foot = shin.childCount > 0 ? shin.GetChild(0) : null;
            if (foot != null) Point(shin, foot.position, -up + fwd * 0.08f);
        }
        // Arms down, forearms forward (onto the knees or the table).
        foreach (var (arm, fore, side) in new[] { (lArm, lFore, -1f), (rArm, rFore, 1f) })
        {
            if (arm == null || fore == null) continue;
            Point(arm, fore.position, -up * 1f + fwd * 0.25f + right * side * 0.08f);
            var hand = fore.childCount > 0 ? fore.GetChild(0) : null;
            if (hand != null) Point(fore, hand.position, fwd * 1f + (handsOnTable ? up * 0.25f : -up * 0.35f) - right * side * 0.12f);
        }
        if (spine) spine.rotation = Quaternion.AngleAxis(4f, right) * spine.rotation;
        if (head != null && lookAt != null)
        {
            Vector3 d = lookAt.position - head.position;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, up), Vector3.ProjectOnPlane(d, up), up), -60f, 60f);
            head.rotation = Quaternion.AngleAxis(yaw, up) * head.rotation;
        }
    }

    // Turns a bone so that its child (at childPos) lies along 'dir'.
    static void Point(Transform bone, Vector3 childPos, Vector3 dir)
    {
        Vector3 current = childPos - bone.position;
        if (current.sqrMagnitude < 1e-6f) return;
        bone.rotation = Quaternion.FromToRotation(current, dir.normalized) * bone.rotation;
    }

    Transform Bone(string boneName)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            if (n == boneName) return t;
        }
        return null;
    }
}
