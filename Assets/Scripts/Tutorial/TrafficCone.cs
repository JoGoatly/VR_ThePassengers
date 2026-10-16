using UnityEngine;

/// <summary>
/// A traffic cone (pylon). Place it anywhere in the scene; the cone mesh is built by itself.
/// It has a rigidbody: the bus knocks it over, which the driving test counts.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrafficCone : MonoBehaviour
{
    public float radius = 0.2f;
    public float height = 0.7f;

    static Mesh shared;
    Vector3 restPosition;
    bool rested;

    /// <summary>The cone was pushed or knocked over.</summary>
    public bool Knocked { get; private set; }

    public Material NormalMaterial { get; private set; }

    void OnEnable()
    {
        if (shared == null)
        {
            var mb = new MeshKit.Builder();
            mb.AddMesh(MeshKit.Cone(radius, height, 8), Vector3.zero, Quaternion.identity, Vector3.one);
            mb.AddMesh(MeshKit.Box(new Vector3(radius * 2.4f, 0.04f, radius * 2.4f), 1f), Vector3.zero, Quaternion.identity, Vector3.one);
            shared = mb.ToMesh("Traffic Cone");
            shared.hideFlags = HideFlags.DontSave;
        }
        GetComponent<MeshFilter>().sharedMesh = shared;
        NormalMaterial = GetComponent<MeshRenderer>().sharedMaterial;
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        restPosition = transform.position;
        rested = true;
    }

    void Update()
    {
        if (!Application.isPlaying || !rested || Knocked) return;
        if (Vector3.Distance(transform.position, restPosition) > 0.35f || Vector3.Dot(transform.up, Vector3.up) < 0.8f)
            Knocked = true;
    }

    /// <summary>Show the cone in another material (e.g. glowing: the next gate).</summary>
    public void SetMaterial(Material mat)
    {
        var r = GetComponent<MeshRenderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }
}
