using UnityEngine;

/// <summary>
/// A scaled box (like Unity's cube) whose texture repeats every 'tile' metres instead of
/// stretching - for ground plates, kerbs, walls. Scale the object in the scene view as you
/// like; the mesh (and the mesh collider, which the bus drives on) follow.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class TiledBox : MonoBehaviour
{
    [Tooltip("Texture repeat in metres")]
    public float tile = 4f;
    [Tooltip("Faces are split into cells of this size: big single polygons wobble and flicker with PSX vertex snapping")]
    public float cell = 4f;

    Mesh mesh;
    Vector3 builtScale;
    float builtTile, builtCell;

    void OnEnable() => Rebuild();

    void OnValidate() => builtTile = -1f;

    void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }

    void Update()
    {
        if (transform.lossyScale != builtScale || tile != builtTile || cell != builtCell) Rebuild();
    }

    void Rebuild()
    {
        Vector3 s = transform.lossyScale;
        builtScale = s;
        builtTile = tile;
        float t = Mathf.Max(0.05f, tile);
        if (mesh == null) mesh = new Mesh { name = "Tiled Box", hideFlags = HideFlags.DontSave };
        mesh.Clear();

        builtCell = cell;
        float c = Mathf.Max(0.5f, cell);
        var v = new System.Collections.Generic.List<Vector3>();
        var n = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        // One face as a grid of cells; u/w are the face axes (unit cube), su/sw their size in metres.
        void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
        {
            int nu = Mathf.Clamp(Mathf.CeilToInt(su / c), 1, 64), nw = Mathf.Clamp(Mathf.CeilToInt(sw / c), 1, 64);
            int start = v.Count;
            for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nw; j++)
            {
                float fu = i / (float)nu, fw = j / (float)nw;
                v.Add(normal * 0.5f + u * (fu - 0.5f) + w * (fw - 0.5f));
                n.Add(normal);
                uv.Add(new Vector2(fu * su / t, fw * sw / t));
            }
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nw; j++)
            {
                int a = start + i * (nw + 1) + j, b = a + 1, d = a + nw + 1, e = d + 1;
                // a (u0,w0) - b (u0,w1) - e (u1,w1) - d (u1,w0), same winding as before.
                tris.Add(a); tris.Add(b); tris.Add(e);
                tris.Add(a); tris.Add(e); tris.Add(d);
            }
        }
        // w x u = normal for every face (MeshKit's winding: clockwise seen from outside).
        Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
        Face(Vector3.down, Vector3.forward, Vector3.right, s.z, s.x);
        Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
        Face(Vector3.left, Vector3.up, Vector3.forward, s.y, s.z);
        Face(Vector3.forward, Vector3.up, Vector3.right, s.y, s.x);
        Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
        mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (TryGetComponent(out MeshCollider mc)) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
    }
}
