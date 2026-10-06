using UnityEngine;

/// <summary>
/// The route through the practice ground: the children are the corners, in driving order
/// (a closed loop). Move them in the scene view to change the route; the examiner uses it to
/// announce turns ("gleich rechts").
/// </summary>
public class CourseRoute : MonoBehaviour
{
    public int Count => transform.childCount;
    public Vector3 Point(int i) => transform.GetChild(((i % Count) + Count) % Count).position;

    /// <summary>The segment the point is closest to (from corner i to i+1).</summary>
    public int Segment(Vector3 p)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            Vector3 a = Point(i), b = Point(i + 1);
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = p - a; ap.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
            float d = (ap - ab * t).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>The next corner ahead: distance to it and the turn there (-1 left, 0 straight, 1 right).</summary>
    public void NextTurn(Vector3 p, out float distance, out int turn)
    {
        distance = 0f;
        turn = 0;
        if (Count < 3) return;
        int i = Segment(p);
        Vector3 corner = Point(i + 1);
        Vector3 d = corner - p; d.y = 0f;
        distance = d.magnitude;
        Vector3 inDir = corner - Point(i), outDir = Point(i + 2) - corner;
        inDir.y = outDir.y = 0f;
        float angle = Vector3.SignedAngle(inDir, outDir, Vector3.up);
        turn = Mathf.Abs(angle) < 20f ? 0 : angle > 0f ? 1 : -1;
    }

    void OnDrawGizmos()
    {
        if (Count < 2) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        for (int i = 0; i < Count; i++)
        {
            Gizmos.DrawLine(Point(i) + Vector3.up * 0.5f, Point(i + 1) + Vector3.up * 0.5f);
            Gizmos.DrawSphere(Point(i) + Vector3.up * 0.5f, 0.6f);
        }
    }
}
