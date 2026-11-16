using UnityEngine;

/// <summary>
/// Glowing arrows floating over the lane a little ahead of the bus: they show where to drive
/// (along the route, round the corners, to the next gate or bus stop). Created by the Tutorial.
/// </summary>
public class GuideArrow : MonoBehaviour
{
    public static GuideArrow Create(Color color)
    {
        var go = new GameObject("Guide Arrows");
        var g = go.AddComponent<GuideArrow>();
        g.Build(color);
        return g;
    }

    const int Count = 3;
    readonly Transform[] arrows = new Transform[Count];
    Material material;

    void Build(Color color)
    {
        var source = Resources.Load<Material>("PSX/PsxGlow");
        if (source == null) source = Resources.Load<Material>("PSX/PsxLit");
        material = new Material(source) { name = "Guide Arrow" };
        if (material.HasProperty("_MainColor")) material.SetColor("_MainColor", color);

        // A flat chevron (pointing along +Z), seen from above and below.
        var mb = new MeshKit.Builder();
        Vector3 tip = new Vector3(0f, 0f, 0.9f), l = new Vector3(-1f, 0f, -0.3f), r = new Vector3(1f, 0f, -0.3f);
        Vector3 li = new Vector3(-1f, 0f, -0.95f), ri = new Vector3(1f, 0f, -0.95f), notch = new Vector3(0f, 0f, 0.25f);
        mb.Quad(l, tip, notch, li, 1f, 1f, 1f);
        mb.Quad(tip, r, ri, notch, 1f, 1f, 1f);
        mb.Quad(li, notch, tip, l, 1f, 1f, 1f);
        mb.Quad(notch, ri, r, tip, 1f, 1f, 1f);
        var mesh = mb.ToMesh("Chevron");
        for (int i = 0; i < Count; i++)
        {
            var a = MeshKit.Spawn("Arrow " + i, transform, mesh, material, Vector3.zero, Quaternion.identity, false);
            a.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrows[i] = a.transform;
        }
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Arrows along a path: positions and directions of the arrows (front to back).</summary>
    public void Show(Vector3[] points, Vector3[] directions)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
        for (int i = 0; i < Count; i++)
        {
            bool on = i < points.Length;
            arrows[i].gameObject.SetActive(on);
            if (!on) continue;
            Vector3 d = directions[i]; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            // Front arrow brightest; a gentle bob so they read as "alive".
            arrows[i].SetPositionAndRotation(points[i] + Vector3.up * (1.6f + 0.12f * Mathf.Sin(Time.time * 3f + i)), Quaternion.LookRotation(d.normalized) * Quaternion.Euler(-50f, 0f, 0f));   // tilted towards the driver
            arrows[i].localScale = Vector3.one * (1.25f - 0.15f * i) * pulse;
        }
    }
}
