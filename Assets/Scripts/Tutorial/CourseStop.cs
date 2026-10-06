using UnityEngine;

/// <summary>
/// A bus stop on the practice ground. The object's forward (blue arrow) is the driving
/// direction, its right side is the kerb. 'sign' is the H sign: the front door belongs there.
/// </summary>
public class CourseStop : MonoBehaviour
{
    public string stopName = "A";
    public Transform sign;
    [Tooltip("Glowing marker above the stop while it is the target")]
    public GameObject beacon;

    public Vector3 SignPoint => sign != null ? sign.position : transform.position;

    /// <summary>Distance of a point (the front door) to the sign: along the platform and from the kerb.</summary>
    public void Measure(Vector3 point, out float along, out float across)
    {
        Vector3 d = SignPoint - point;
        d.y = 0f;
        along = Vector3.Dot(d, transform.forward);
        across = Mathf.Abs(Vector3.Dot(d, transform.right));
    }

    public void SetTarget(bool on)
    {
        if (beacon != null) beacon.SetActive(on);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(SignPoint, 0.5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 4f);
    }
}
