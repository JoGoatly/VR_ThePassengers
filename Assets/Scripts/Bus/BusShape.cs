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

    /// <summary>
    /// Where an area of the bus texture (e.g. the painted destination display) sits on the
    /// model, in bus space: its centre, size and the directions in which its painted text reads
    /// and points up ('frontMost': if the texture is used twice, take the one further forward).
    /// </summary>
    public static bool FindTextureArea(Transform bus, Rect uvArea, bool frontMost, out Vector3 centre, out Vector3 reading, out Vector3 textUp, out Vector2 size)
    {
        centre = reading = textUp = Vector3.zero;
        size = Vector2.zero;
        Transform model = bus.Find("Bus");
        if (model == null) model = bus;
        var meshes = new System.Collections.Generic.List<(Vector3[] v, Vector2[] uv, int[] tris)>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            var uv = mesh.uv;
            var v = mesh.vertices;
            if (uv == null || uv.Length != v.Length) continue;
            var toBus = bus.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length; i++) v[i] = toBus.MultiplyPoint3x4(v[i]);
            meshes.Add((v, uv, mesh.triangles));
        }

        bool At(Vector2 uvPoint, out Vector3 pos)
        {
            pos = Vector3.zero;
            bool found = false;
            foreach (var (v, uv, tris) in meshes)
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector2 a = uv[tris[t]], b = uv[tris[t + 1]], c = uv[tris[t + 2]];
                    float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(d) < 1e-12f) continue;
                    float wa = ((b.y - c.y) * (uvPoint.x - c.x) + (c.x - b.x) * (uvPoint.y - c.y)) / d;
                    float wb = ((c.y - a.y) * (uvPoint.x - c.x) + (a.x - c.x) * (uvPoint.y - c.y)) / d;
                    float wc = 1f - wa - wb;
                    if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                    Vector3 p = v[tris[t]] * wa + v[tris[t + 1]] * wb + v[tris[t + 2]] * wc;
                    if (!found || (frontMost ? p.z > pos.z : p.z < pos.z)) { pos = p; found = true; }
                }
            return found;
        }

        float u0 = uvArea.xMin, u1 = uvArea.xMax, v0 = uvArea.yMin, v1 = uvArea.yMax, uc = uvArea.center.x, vc = uvArea.center.y;
        if (!At(uvArea.center, out centre) || !At(new Vector2(u0, vc), out var left) || !At(new Vector2(u1, vc), out var right)
            || !At(new Vector2(uc, v0), out var bottom) || !At(new Vector2(uc, v1), out var top)) return false;
        // Texture text reads along +u and points up along +v (rotated / mirrored islands are handled by this).
        reading = (right - left).normalized;
        textUp = (top - bottom).normalized;
        size = new Vector2(Vector3.Distance(left, right), Vector3.Distance(bottom, top));
        return size.x > 0.05f && size.y > 0.02f;
    }
}
