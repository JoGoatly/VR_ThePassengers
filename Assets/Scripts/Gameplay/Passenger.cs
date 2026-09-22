using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A passenger character (Mixamo rig, no animations needed). Walks along waypoints with a
/// procedural walk cycle, arms hang down. Waypoints are given in the space of a Transform
/// (e.g. the bus) so the passenger can walk inside a moving vehicle.
/// </summary>
public class Passenger : MonoBehaviour
{
    public enum State { Waiting, WalkingToDoor, AtDoor, Boarding, Riding, Leaving }

    public State CurrentState { get; set; } = State.Waiting;
    public IdCard Card { get; set; }
    public BusStop Stop { get; set; }
    public bool IsWalking => waypoints.Count > 0;

    public float walkSpeed = 1.3f;
    public float turnSpeed = 360f;
    public float stepAngle = 28f;
    public float armSwing = 18f;

    readonly Queue<Vector3> waypoints = new Queue<Vector3>();
    Transform space;              // waypoints are local to this transform (null = world)
    System.Action onArrived;
    Vector3? faceDirectionLocal;  // look direction after arriving, in 'space'

    Transform hips, leftUpLeg, rightUpLeg, leftLeg, rightLeg, leftArm, rightArm, leftForeArm, rightForeArm;
    Quaternion leftUpLegRest, rightUpLegRest, leftLegRest, rightLegRest, leftArmDown, rightArmDown;
    float walkPhase;
    float walkBlend;

    void Awake()
    {
        if (TryGetComponent(out Animator animator)) animator.enabled = false;
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        hips = FindBone("Hips");
        leftUpLeg = FindBone("LeftUpLeg"); rightUpLeg = FindBone("RightUpLeg");
        leftLeg = FindBone("LeftLeg"); rightLeg = FindBone("RightLeg");
        leftArm = FindBone("LeftArm"); rightArm = FindBone("RightArm");
        leftForeArm = FindBone("LeftForeArm"); rightForeArm = FindBone("RightForeArm");

        if (leftUpLeg) leftUpLegRest = leftUpLeg.localRotation;
        if (rightUpLeg) rightUpLegRest = rightUpLeg.localRotation;
        if (leftLeg) leftLegRest = leftLeg.localRotation;
        if (rightLeg) rightLegRest = rightLeg.localRotation;

        // Arms from T/A-pose down to the sides.
        leftArmDown = ArmDown(leftArm, leftForeArm, -transform.right);
        rightArmDown = ArmDown(rightArm, rightForeArm, transform.right);
    }

    Quaternion ArmDown(Transform arm, Transform foreArm, Vector3 outward)
    {
        if (arm == null || foreArm == null) return Quaternion.identity;
        Vector3 current = foreArm.position - arm.position;
        Vector3 target = (-transform.up + outward * 0.12f).normalized;
        Quaternion world = Quaternion.FromToRotation(current, target) * arm.rotation;
        return Quaternion.Inverse(arm.parent.rotation) * world;
    }

    /// <summary>Walk through the points (local to 'inSpace', or world if null), then call arrived.</summary>
    public void WalkPath(IEnumerable<Vector3> points, Transform inSpace, System.Action arrived = null, Vector3? faceAtEnd = null)
    {
        waypoints.Clear();
        foreach (var p in points) waypoints.Enqueue(p);
        SetSpace(inSpace);
        onArrived = arrived;
        faceDirectionLocal = faceAtEnd;
    }

    /// <summary>Re-parent without moving, e.g. when stepping into the bus.</summary>
    public void SetSpace(Transform inSpace)
    {
        space = inSpace;
        transform.SetParent(inSpace, true);
    }

    Vector3 CurrentLocal => space != null ? space.InverseTransformPoint(transform.position) : transform.position;

    void Update()
    {
        float dt = Time.deltaTime;
        bool moving = false;

        if (waypoints.Count > 0)
        {
            Vector3 target = waypoints.Peek();
            Vector3 pos = CurrentLocal;
            Vector3 delta = target - pos;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float step = walkSpeed * dt;

            if (flat.magnitude <= step)
            {
                SetLocal(target);
                waypoints.Dequeue();
                if (waypoints.Count == 0)
                {
                    var callback = onArrived;
                    onArrived = null;
                    callback?.Invoke();
                }
            }
            else
            {
                // Step up/down (curb, bus entrance) proportionally to the horizontal progress.
                float t = step / flat.magnitude;
                SetLocal(pos + flat.normalized * step + Vector3.up * delta.y * t);
                Face(flat);
                moving = true;
            }
        }
        else if (faceDirectionLocal.HasValue)
        {
            Face(faceDirectionLocal.Value);
        }

        walkBlend = Mathf.MoveTowards(walkBlend, moving ? 1f : 0f, dt * 4f);
        if (moving) walkPhase += dt * walkSpeed * 4.2f;
    }

    void SetLocal(Vector3 local)
    {
        transform.position = space != null ? space.TransformPoint(local) : local;
    }

    void Face(Vector3 localDirection)
    {
        Vector3 dir = space != null ? space.TransformDirection(localDirection) : localDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        float swing = Mathf.Sin(walkPhase) * walkBlend;
        float knee = Mathf.Max(0f, -Mathf.Cos(walkPhase)) * walkBlend;
        Vector3 right = transform.right;

        PoseLeg(leftUpLeg, leftUpLegRest, leftLeg, leftLegRest, -swing, Mathf.Max(0f, Mathf.Cos(walkPhase)) * walkBlend, right);
        PoseLeg(rightUpLeg, rightUpLegRest, rightLeg, rightLegRest, swing, knee, right);

        PoseArm(leftArm, leftArmDown, swing, right);
        PoseArm(rightArm, rightArmDown, -swing, right);
    }

    // Positive swing moves the leg forward; knee bends the shin backwards.
    void PoseLeg(Transform upLeg, Quaternion upRest, Transform leg, Quaternion legRest, float swing, float knee, Vector3 right)
    {
        if (upLeg == null) return;
        upLeg.localRotation = upRest;
        upLeg.rotation = Quaternion.AngleAxis(-swing * stepAngle, right) * upLeg.rotation;
        if (leg == null) return;
        leg.localRotation = legRest;
        leg.rotation = Quaternion.AngleAxis(knee * stepAngle * 1.2f, right) * leg.rotation;
    }

    void PoseArm(Transform arm, Quaternion down, float swing, Vector3 right)
    {
        if (arm == null) return;
        arm.localRotation = down;
        arm.rotation = Quaternion.AngleAxis(-swing * armSwing, right) * arm.rotation;
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
}
