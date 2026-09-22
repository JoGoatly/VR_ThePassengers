using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny procedural low-poly mesh helpers (flat shaded, world-scaled UVs) for PSX-style scenery.
/// </summary>
public static class MeshKit
{
    /// <summary>Box with its pivot at the bottom centre. UVs repeat every uvTile metres.</summary>
    public static Mesh Box(Vector3 size, float uvTile = 1f)
    {
        var mb = new Builder();
        float x = size.x * 0.5f, z = size.z * 0.5f, h = size.y;
        Vector3[] c =
        {
            new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z),
            new Vector3(-x, h, -z), new Vector3(x, h, -z), new Vector3(x, h, z), new Vector3(-x, h, z),
        };
        mb.Quad(c[0], c[4], c[5], c[1], size.x, h, uvTile); // front (-z)
        mb.Quad(c[1], c[5], c[6], c[2], size.z, h, uvTile); // right (+x)
        mb.Quad(c[2], c[6], c[7], c[3], size.x, h, uvTile); // back (+z)
        mb.Quad(c[3], c[7], c[4], c[0], size.z, h, uvTile); // left (-x)
        mb.Quad(c[4], c[7], c[6], c[5], size.x, size.z, uvTile); // top
        mb.Quad(c[3], c[0], c[1], c[2], size.x, size.z, uvTile); // bottom
        return mb.ToMesh("Box");
    }

    /// <summary>Vertical prism (poles, tree trunks) with its pivot at the bottom centre.</summary>
    public static Mesh Prism(float radius, float height, int sides = 6, float uvTile = 1f)
    {
        var mb = new Builder();
        float circumference = 2f * Mathf.PI * radius;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
            var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
            var up = Vector3.up * height;
            mb.Quad(p0, p0 + up, p1 + up, p1, circumference / sides, height, uvTile);
            mb.Tri(Vector3.up * height, p1 + up, p0 + up);
        }
        return mb.ToMesh("Prism");
    }

    /// <summary>Low-poly "blob" (octahedron) for tree tops.</summary>
    public static Mesh Blob(float radius, float height)
    {
        var mb = new Builder();
        Vector3 top = Vector3.up * height, bottom = Vector3.zero;
        Vector3 mid = Vector3.up * height * 0.45f;
        var ring = new Vector3[5];
        for (int i = 0; i < ring.Length; i++)
        {
            float a = i * Mathf.PI * 2f / ring.Length;
            ring[i] = mid + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
        }
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            mb.Tri(top, b, a);
            mb.Tri(bottom, a, b);
        }
        return mb.ToMesh("Blob");
    }

    /// <summary>Cone (fir tree layer) with its pivot at the bottom centre, open at the bottom.</summary>
    public static Mesh Cone(float radius, float height, int sides = 7)
    {
        var mb = new Builder();
        Vector3 tip = Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
            var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
            mb.Tri(p0, tip, p1);
            mb.Tri(p0, p1, Vector3.zero);
        }
        return mb.ToMesh("Cone");
    }

    public static GameObject Spawn(string name, Transform parent, Mesh mesh, Material material,
                                   Vector3 position, Quaternion rotation, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (collider)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
        }
        return go;
    }

    /// <summary>Collects flat-shaded triangles and quads.</summary>
    public class Builder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        /// <summary>Quad a-b-c-d (clockwise when seen from the front); width/height in metres for UVs.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float height, float uvTile)
        {
            float u = width / uvTile, v = height / uvTile;
            Quad(a, b, c, d, new Vector2(0, 0), new Vector2(0, v), new Vector2(u, v), new Vector2(u, 0));
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.5f, 1)); uvs.Add(new Vector2(1, 0));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>Appends a (readable) mesh with a transform, e.g. to merge many trees into one mesh.</summary>
        public void AddMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var m = Matrix4x4.TRS(position, rotation, scale);
            var normalMatrix = m.inverse.transpose;
            int offset = verts.Count;
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(m.MultiplyPoint3x4(v[i]));
                normals.Add(normalMatrix.MultiplyVector(n[i]).normalized);
                uvs.Add(i < uv.Length ? uv[i] : Vector2.zero);
            }
            foreach (int t in mesh.triangles) tris.Add(offset + t);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
