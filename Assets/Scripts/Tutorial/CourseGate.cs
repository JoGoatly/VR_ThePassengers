using UnityEngine;

/// <summary>
/// A slalom gate between two cones: drive through it in the gate's forward direction (blue
/// arrow in the scene view). Gates are taken in 'order'. The next gate glows.
/// </summary>
public class CourseGate : MonoBehaviour
{
    public int order;
    public TrafficCone left, right;
    [Tooltip("Material for the cones of the gate you have to drive through next")]
    public Material highlight;

    public float HalfWidth => left != null && right != null ? Vector3.Distance(left.transform.position, right.transform.position) * 0.5f : 2.5f;
    public Vector3 Centre => left != null && right != null ? (left.transform.position + right.transform.position) * 0.5f : transform.position;

    public void Highlight(bool on)
    {
        foreach (var c in new[] { left, right })
            if (c != null) c.SetMaterial(on ? highlight : c.NormalMaterial);
    }

    /// <summary>Which side of the gate line a point is on (positive = through).</summary>
    public float Side(Vector3 p) => Vector3.Dot(p - Centre, transform.forward);

    /// <summary>Distance from the gate centre along the gate line.</summary>
    public float Across(Vector3 p) => Mathf.Abs(Vector3.Dot(p - Centre, transform.right));

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f);
        Vector3 c = Centre + Vector3.up * 0.3f;
        Gizmos.DrawLine(c - transform.right * HalfWidth, c + transform.right * HalfWidth);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(c, c + transform.forward * 3f);
    }
}
