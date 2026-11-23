using UnityEngine;

/// <summary>
/// Rear-view mirrors: one outside on each side at the front and one inside above the
/// windscreen (you see who sits behind you...). Each mirror is a small camera rendering a
/// low-res picture onto the glass, flipped like a real mirror, angled for the driver's eye.
/// </summary>
[DefaultExecutionOrder(200)]
public class BusMirrors : MonoBehaviour
{
    public bool sideMirrors = true;
    public bool interiorMirror = true;
    [Tooltip("Mirror picture resolution (pixels per metre of glass)")]
    public float pixelsPerMetre = 420f;
    public float mirrorFov = 28f;

    Material housing;

    void Start()
    {
        var cam = Camera.main;
        if (cam == null) return;
        // The real body (mesh vertices): the mirrors sit right on it.
        var shape = BusShape.Measure(transform);
        Vector3 min = shape.Valid ? shape.min : new Vector3(-1.25f, 0f, -6f);
        Vector3 max = shape.Valid ? shape.max : new Vector3(1.25f, 3.2f, 6f);
        Vector3 eye = transform.InverseTransformPoint(cam.transform.position);

        var lit = Resources.Load<Material>("PSX/PsxLit");
        housing = new Material(lit) { name = "Mirror Housing" };
        if (housing.HasProperty("_MainColor")) housing.SetColor("_MainColor", new Color(0.08f, 0.08f, 0.09f));

        if (sideMirrors)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float y = eye.y + 0.05f;
                float frontZ = shape.Valid ? shape.EndZ(true, y - 0.3f, y + 0.3f, (max.x - min.x) * 0.5f) : max.z;
                // The body side a little behind the front, at this height (ray from outside).
                float bodyZ = frontZ - 0.25f;
                float far = Mathf.Max(-min.x, max.x) + 1f;
                float bodyX = side < 0 ? min.x : max.x;
                if (shape.Valid && shape.Raycast(new Vector3(side * far, y, bodyZ), new Vector3(-side, 0f, 0f), 4f, out float d))
                    bodyX = side * far - side * d;
                // The mirror stands out at the front corner, a bracket goes back to the body.
                var pos = new Vector3(bodyX + side * 0.16f, y, frontZ + 0.05f);
                var anchor = new Vector3(bodyX - side * 0.04f, y, bodyZ);
                // Looking back along the bus side, a little outwards.
                var look = new Vector3(side * 0.07f, -0.04f, -1f).normalized;
                Build(side < 0 ? "Mirror Left" : "Mirror Right", pos, eye, look, new Vector2(0.24f, 0.36f), anchor - pos);
            }
        }
        if (interiorMirror)
        {
            var pos = new Vector3(eye.x + 0.75f, eye.y + 0.38f, Mathf.Min(max.z - 0.5f, eye.z + 0.65f));
            // Mounted on the windscreen: a rod forward to the glass (or up to the ceiling).
            Vector3 arm = new Vector3(0f, 0.15f, 0f);
            if (shape.Valid && shape.Raycast(pos, Vector3.forward, 1.5f, out float toGlass)) arm = new Vector3(0f, 0f, toGlass + 0.02f);
            else if (shape.Valid && shape.Raycast(pos, Vector3.up, 1.2f, out float toCeiling)) arm = new Vector3(0f, toCeiling + 0.03f, 0f);
            Build("Mirror Inside", pos, eye, new Vector3(0f, -0.06f, -1f).normalized, new Vector2(0.42f, 0.12f), arm);
        }
    }

    // A mirror at 'pos' (bus space) whose camera looks along 'look'; the glass is angled so the
    // driver sees exactly that. 'arm' = offset of the bracket that holds it.
    void Build(string name, Vector3 pos, Vector3 eye, Vector3 look, Vector2 size, Vector3 arm)
    {
        Vector3 incoming = (pos - eye).normalized;
        Vector3 normal = (look - incoming).normalized;   // mirror normal halves the angle (towards the driver)

        var root = new GameObject(name).transform;
        root.SetParent(transform, false);
        root.localPosition = pos;
        root.localRotation = Quaternion.LookRotation(-normal, Vector3.up);   // glass faces -forward

        // Picture.
        var rt = new RenderTexture(Mathf.Max(32, Mathf.RoundToInt(size.x * pixelsPerMetre)), Mathf.Max(16, Mathf.RoundToInt(size.y * pixelsPerMetre)), 16)
        { filterMode = FilterMode.Point, name = name + " Picture" };
        var camGo = new GameObject(name + " Camera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = pos - normal * 0.02f;
        camGo.transform.localRotation = Quaternion.LookRotation(look, Vector3.up);
        var c = camGo.AddComponent<Camera>();
        c.targetTexture = rt;
        c.fieldOfView = mirrorFov;
        c.aspect = size.x / size.y;
        c.nearClipPlane = 0.2f;
        c.farClipPlane = 160f;
        c.cullingMask &= ~(1 << 31);   // not the passport photo layer
        c.depth = -10f;

        // Glass: the picture, flipped left/right like a real mirror, unlit.
        var glow = Resources.Load<Material>("PSX/PsxGlow");
        var glassMat = new Material(glow != null ? glow : Resources.Load<Material>("PSX/PsxLit")) { name = name + " Glass" };
        glassMat.SetTexture("_MainTex", rt);
        if (glassMat.HasProperty("_MainColor")) glassMat.SetColor("_MainColor", new Color(0.85f, 0.88f, 0.9f));
        if (glassMat.HasProperty("_AffineTextureWarpingWeight")) glassMat.SetFloat("_AffineTextureWarpingWeight", 0f);
        var mb = new MeshKit.Builder();
        float w = size.x * 0.5f, h = size.y * 0.5f;
        mb.Quad(new Vector3(-w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(w, h, 0f), new Vector3(w, -h, 0f),
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0));
        var glass = MeshKit.Spawn("Glass", root, mb.ToMesh("Mirror Glass"), glassMat, root.position, root.rotation, false);
        glass.transform.localPosition = new Vector3(0f, 0f, -0.012f);

        // Housing and bracket.
        var back = MeshKit.Spawn("Housing", root, MeshKit.Box(new Vector3(size.x + 0.03f, size.y + 0.03f, 0.035f), 1f), housing, root.position, root.rotation, false);
        back.transform.localPosition = new Vector3(0f, -(size.y + 0.03f) * 0.5f, 0.012f);
        if (arm.sqrMagnitude > 0.0001f)
        {
            var bracket = MeshKit.Spawn("Bracket", transform, MeshKit.Box(new Vector3(0.03f, 0.03f, arm.magnitude), 1f), housing, transform.position, transform.rotation, false);
            bracket.transform.localPosition = pos + arm * 0.5f - new Vector3(0f, 0.02f, 0f);
            bracket.transform.localRotation = Quaternion.LookRotation(arm.normalized, Mathf.Abs(arm.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
        }
    }
}
