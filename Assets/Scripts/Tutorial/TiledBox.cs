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

    Mesh mesh;
    Vector3 builtScale;
    float builtTile;

    void OnEnable() => Rebuild();

    void OnValidate() => builtTile = -1f;

    void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }

    void Update()
    {
        if (transform.lossyScale != builtScale || tile != builtTile) Rebuild();
    }

    void Rebuild()
    {
        Vector3 s = transform.lossyScale;
        builtScale = s;
        builtTile = tile;
        float t = Mathf.Max(0.05f, tile);
        if (mesh == null) mesh = new Mesh { name = "Tiled Box", hideFlags = HideFlags.DontSave };
        mesh.Clear();

        var v = new Vector3[24];
        var n = new Vector3[24];
        var uv = new Vector2[24];
        var tris = new int[36];
        int k = 0, ti = 0;
        void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
        {
            Vector3 c = normal * 0.5f;
            Vector3[] corners = { c - u * 0.5f - w * 0.5f, c - u * 0.5f + w * 0.5f, c + u * 0.5f + w * 0.5f, c + u * 0.5f - w * 0.5f };
            Vector2[] uvs = { new Vector2(0, 0), new Vector2(0, sw / t), new Vector2(su / t, sw / t), new Vector2(su / t, 0) };
            for (int i = 0; i < 4; i++) { v[k + i] = corners[i]; n[k + i] = normal; uv[k + i] = uvs[i]; }
            tris[ti++] = k; tris[ti++] = k + 1; tris[ti++] = k + 2;
            tris[ti++] = k; tris[ti++] = k + 2; tris[ti++] = k + 3;
            k += 4;
        }
        // w x u = normal for every face (MeshKit's winding: clockwise seen from outside).
        Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
        Face(Vector3.down, Vector3.forward, Vector3.right, s.z, s.x);
        Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
        Face(Vector3.left, Vector3.up, Vector3.forward, s.y, s.z);
        Face(Vector3.forward, Vector3.up, Vector3.right, s.y, s.x);
        Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
        mesh.vertices = v;
        mesh.normals = n;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (TryGetComponent(out MeshCollider mc)) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
    }
}
