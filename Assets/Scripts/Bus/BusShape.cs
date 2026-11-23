using UnityEngine;

/// <summary>
/// The real shape of the bus body, measured from its mesh vertices in bus space (bounding
/// boxes of the renderers are too big): size, and where the front and the back actually are
/// at a given height - for things mounted on the body (mirrors, destination signs).
/// </summary>
public class BusShape
{
    public Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
    readonly System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();

    public bool Valid => !float.IsInfinity(min.x);

    public static BusShape Measure(Transform bus)
    {
        var shape = new BusShape();
        var cam = Camera.main;
        var driver = bus.GetComponentInChildren<DriverBody>();
        // Only the bus model itself (child "Bus"), not the parts our scripts add to it.
        Transform model = bus.Find("Bus");
        if (model == null) model = bus;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (driver != null && mf.transform.IsChildOf(driver.transform)) continue;
            if (cam != null && mf.transform.IsChildOf(cam.transform)) continue;
            var mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable || mesh.vertexCount > 200000) continue;
            var toBus = bus.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in mesh.vertices)
            {
                var p = toBus.MultiplyPoint3x4(v);
                shape.points.Add(p);
                shape.min = Vector3.Min(shape.min, p);
                shape.max = Vector3.Max(shape.max, p);
            }
        }
        return shape;
    }

    /// <summary>The frontmost (or rearmost) point of the body around the middle, between two heights.</summary>
    public float EndZ(bool front, float yFrom, float yTo, float halfWidth)
    {
        float cx = (min.x + max.x) * 0.5f;
        float best = front ? float.NegativeInfinity : float.PositiveInfinity;
        foreach (var p in points)
        {
            if (p.y < yFrom || p.y > yTo || Mathf.Abs(p.x - cx) > halfWidth) continue;
            best = front ? Mathf.Max(best, p.z) : Mathf.Min(best, p.z);
        }
        return float.IsInfinity(best) ? (front ? max.z : min.z) : best;
    }

    /// <summary>The outermost point of the body on one side, around a length position and height.</summary>
    public float SideX(int side, float z, float y, float range)
    {
        float best = side > 0 ? float.NegativeInfinity : float.PositiveInfinity;
        foreach (var p in points)
        {
            if (Mathf.Abs(p.z - z) > range || Mathf.Abs(p.y - y) > range) continue;
            best = side > 0 ? Mathf.Max(best, p.x) : Mathf.Min(best, p.x);
        }
        return float.IsInfinity(best) ? (side > 0 ? max.x : min.x) : best;
    }
}
