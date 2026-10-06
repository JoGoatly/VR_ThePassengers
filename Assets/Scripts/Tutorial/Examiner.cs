using UnityEngine;

/// <summary>
/// The driving examiner: stands in the bus to the right of the driver for the whole test,
/// looks at you, nods while talking and points where to go. The character model is a child
/// of this object (any of the passenger models; move this object to place him).
/// What he says is shown by the Tutorial in his speech box.
/// </summary>
[DefaultExecutionOrder(310)]    // after the bus and the driver camera moved
public class Examiner : MonoBehaviour
{
    [Tooltip("Name shown above his speech box")]
    public string displayName = "Herr Brandt";

    Transform hips, spine, neck, head, rightArm, rightForeArm, leftArm, leftForeArm;
    Quaternion spineRest, headRest, neckRest, leftForeRest, rightForeRest, leftArmDown, rightArmDown;
    Transform model;
    float talkUntil, pointUntil;
    Vector3 pointTarget;

    /// <summary>He says something (nods for a while).</summary>
    public void Talk(float seconds = 3f) => talkUntil = Time.time + seconds;

    /// <summary>He points at a place in the world for a while.</summary>
    public void PointAt(Vector3 world, float seconds = 3.5f)
    {
        pointTarget = world;
        pointUntil = Time.time + seconds;
    }

    void Start()
    {
        foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        // Imported models get PSX materials (otherwise they are pink).
        if (transform.childCount > 0)
        {
            model = transform.GetChild(0);
            PsxConvert.Apply(model.gameObject);
        }
        hips = Bone("Hips");
        spine = Bone("Spine1") ?? Bone("Spine");
        neck = Bone("Neck");
        head = Bone("Head");
        leftArm = Bone("LeftArm"); rightArm = Bone("RightArm");
        leftForeArm = Bone("LeftForeArm"); rightForeArm = Bone("RightForeArm");
        if (spine != null) spineRest = spine.localRotation;
        if (neck != null) neckRest = neck.localRotation;
        if (head != null) headRest = head.localRotation;
        if (leftForeArm != null) leftForeRest = leftForeArm.localRotation;
        if (rightForeArm != null) rightForeRest = rightForeArm.localRotation;
        leftArmDown = ArmDown(leftArm, leftForeArm, -transform.right);
        rightArmDown = ArmDown(rightArm, rightForeArm, transform.right);
    }

    Quaternion ArmDown(Transform arm, Transform fore, Vector3 outward)
    {
        if (arm == null || fore == null) return Quaternion.identity;
        Vector3 current = fore.position - arm.position;
        Vector3 target = (-transform.up + outward * 0.12f).normalized;
        return Quaternion.Inverse(arm.parent.rotation) * (Quaternion.FromToRotation(current, target) * arm.rotation);
    }

    void LateUpdate()
    {
        if (model == null) return;
        float t = Time.time;

        // Standing, arms hanging, breathing a little; holds his clipboard arm slightly bent.
        if (spine != null) spine.localRotation = spineRest * Quaternion.Euler(Mathf.Sin(t * 1.3f) * 1.2f, 0f, 0f);
        if (leftArm != null) leftArm.localRotation = leftArmDown;
        if (leftForeArm != null) leftForeArm.localRotation = leftForeRest;
        if (leftForeArm != null) leftForeArm.rotation = Quaternion.AngleAxis(-55f, transform.right) * leftForeArm.rotation;
        if (rightForeArm != null) rightForeArm.localRotation = rightForeRest;

        // Pointing: the right arm stretched towards the target.
        float point = t < pointUntil ? 1f : 0f;
        if (rightArm != null)
        {
            rightArm.localRotation = rightArmDown;
            if (point > 0f && rightForeArm != null)
            {
                Vector3 current = rightForeArm.position - rightArm.position;
                Vector3 wanted = (pointTarget - rightArm.position).normalized;
                // Not through the windscreen into the floor: keep it roughly level.
                wanted.y = Mathf.Clamp(wanted.y, -0.2f, 0.3f);
                rightArm.rotation = Quaternion.FromToRotation(current, wanted) * rightArm.rotation;
            }
        }

        // The head looks at the driver (the camera); nods while talking.
        var cam = Camera.main;
        if (head != null && cam != null)
        {
            if (neck != null) neck.localRotation = neckRest;
            head.localRotation = headRest;
            Vector3 lookAt = point > 0f ? pointTarget : cam.transform.position;
            Vector3 dir = lookAt - head.position;
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(flatFwd, Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up), -70f, 70f);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -30f, 30f);
            if (t < talkUntil) pitch += Mathf.Sin(t * 9f) * 4f;
            head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, transform.right) * head.rotation;
        }
    }

    Transform Bone(string boneName)
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
}
