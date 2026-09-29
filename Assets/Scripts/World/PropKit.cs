using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Building helper for places made of boxes (in the local space of a root): all boxes of one
/// material become one mesh (tiled so big walls are lit and don't vanish), colliders are
/// separate boxes. Plus signs with pixel text, lamps, looping sounds and things to use (E).
/// </summary>
public class PropKit
{
    public readonly Transform root;
    readonly Dictionary<Material, MeshKit.Builder> geometry = new Dictionary<Material, MeshKit.Builder>();
    public Material signMaterial, screenMaterial;

    public PropKit(Transform root, Material signMaterial, Material screenMaterial)
    {
        this.root = root;
        this.signMaterial = signMaterial;
        this.screenMaterial = screenMaterial;
    }

    /// <summary>A copy of a material with another colour and no texture warping.</summary>
    public static Material Tinted(Material source, Color color, string name)
    {
        if (source == null) return null;
        var m = new Material(source) { name = name };
        if (m.HasProperty("_MainColor")) m.SetColor("_MainColor", color);
        if (m.HasProperty("_AffineTextureWarpingWeight")) m.SetFloat("_AffineTextureWarpingWeight", 0f);
        return m;
    }

    /// <summary>A box, pivot at the bottom centre (root space).</summary>
    public GameObject Solid(Vector3 bottomCentre, Vector3 size, Material mat, bool collider = true, float yaw = 0f)
    {
        if (mat != null)
        {
            if (!geometry.TryGetValue(mat, out var mb)) geometry[mat] = mb = new MeshKit.Builder();
            AddBox(mb, Matrix4x4.TRS(bottomCentre, Quaternion.Euler(0f, yaw, 0f), Vector3.one), size);
        }
        return collider ? Block(bottomCentre, size, yaw) : null;
    }

    /// <summary>A tilted box (e.g. a roof), pivot at the bottom centre.</summary>
    public void Tilted(Vector3 bottomCentre, Vector3 size, Material mat, Vector3 euler)
    {
        if (mat == null) return;
        if (!geometry.TryGetValue(mat, out var mb)) geometry[mat] = mb = new MeshKit.Builder();
        AddBox(mb, Matrix4x4.TRS(bottomCentre, Quaternion.Euler(euler), Vector3.one), size);
    }

    public GameObject Block(Vector3 bottomCentre, Vector3 size, float yaw = 0f)
    {
        var go = new GameObject("Collider");
        go.transform.SetParent(root, false);
        go.transform.localPosition = bottomCentre;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        box.size = size;
        return go;
    }

    static void AddBox(MeshKit.Builder mb, Matrix4x4 m, Vector3 size)
    {
        float hx = size.x * 0.5f, hz = size.z * 0.5f, h = size.y;
        Face(mb, m, new Vector3(-hx, 0, -hz), Vector3.right, Vector3.up, size.x, h);
        Face(mb, m, new Vector3(hx, 0, hz), Vector3.left, Vector3.up, size.x, h);
        Face(mb, m, new Vector3(hx, 0, -hz), Vector3.forward, Vector3.up, size.z, h);
        Face(mb, m, new Vector3(-hx, 0, hz), Vector3.back, Vector3.up, size.z, h);
        Face(mb, m, new Vector3(-hx, h, -hz), Vector3.right, Vector3.forward, size.x, size.z);
        Face(mb, m, new Vector3(-hx, 0, hz), Vector3.right, Vector3.back, size.x, size.z);
    }

    static void Face(MeshKit.Builder mb, Matrix4x4 m, Vector3 origin, Vector3 u, Vector3 v, float uLen, float vLen)
    {
        if (uLen <= 0f || vLen <= 0f) return;
        const float tile = 3f, uv = 2f;
        int nu = Mathf.Max(1, Mathf.CeilToInt(uLen / tile)), nv = Mathf.Max(1, Mathf.CeilToInt(vLen / tile));
        float du = uLen / nu, dv = vLen / nv;
        for (int i = 0; i < nu; i++)
        for (int j = 0; j < nv; j++)
        {
            Vector3 a = origin + u * (i * du) + v * (j * dv);
            Vector3 pa = m.MultiplyPoint3x4(a), pb = m.MultiplyPoint3x4(a + v * dv);
            Vector3 pc = m.MultiplyPoint3x4(a + u * du + v * dv), pd = m.MultiplyPoint3x4(a + u * du);
            float u0 = i * du / uv, u1 = (i + 1) * du / uv, v0 = j * dv / uv, v1 = (j + 1) * dv / uv;
            mb.Quad(pa, pb, pc, pd, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
        }
    }

    /// <summary>Wall along x at depth z with openings (start, end, top of the opening).</summary>
    public void WallX(float z, float x0, float x1, float h, float t, Material mat, params (float start, float end, float top)[] gaps)
    {
        float x = x0;
        System.Array.Sort(gaps, (p, q) => p.start.CompareTo(q.start));
        foreach (var g in gaps)
        {
            if (g.start > x) Solid(new Vector3((x + g.start) * 0.5f, 0f, z), new Vector3(g.start - x, h, t), mat);
            if (g.top < h) Solid(new Vector3((g.start + g.end) * 0.5f, g.top, z), new Vector3(g.end - g.start, h - g.top, t), mat, false);
            x = g.end;
        }
        if (x1 > x) Solid(new Vector3((x + x1) * 0.5f, 0f, z), new Vector3(x1 - x, h, t), mat);
    }

    /// <summary>Wall along z at x with openings.</summary>
    public void WallZ(float x, float z0, float z1, float h, float t, Material mat, params (float start, float end, float top)[] gaps)
    {
        float z = z0;
        System.Array.Sort(gaps, (p, q) => p.start.CompareTo(q.start));
        foreach (var g in gaps)
        {
            if (g.start > z) Solid(new Vector3(x, 0f, (z + g.start) * 0.5f), new Vector3(t, h, g.start - z), mat);
            if (g.top < h) Solid(new Vector3(x, g.top, (g.start + g.end) * 0.5f), new Vector3(t, h - g.top, g.end - g.start), mat, false);
            z = g.end;
        }
        if (z1 > z) Solid(new Vector3(x, 0f, (z + z1) * 0.5f), new Vector3(t, h, z1 - z), mat);
    }

    public Light Lamp(Vector3 localPos, Color color, float intensity, float range, float flicker = 0f)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.transform.localPosition = localPos;
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker > 0f) l.gameObject.AddComponent<FlickerLight>().flickerChance = flicker;
        return l;
    }

    public void Loop(Vector3 localPos, AudioClip clip, float volume, float maxDistance)
    {
        if (clip == null) return;
        var go = new GameObject("Sound");
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.volume = volume * GameSettings.Effects;
        src.Play();
    }

    /// <summary>Something to use with E; the texts are already in the chosen language.</summary>
    public Inspectable Interact(Vector3 localPos, string prompt, string title, string text = null, float radius = 1.7f)
    {
        var go = new GameObject("Inspectable");
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        return Inspectable.Add(go, prompt, prompt, title, title, text, text, null, radius);
    }

    /// <summary>A sign with pixel text, its front looking along 'facing' (root space).</summary>
    public void Sign(Vector3 centre, Vector3 facing, float width, float height, string[] lines, Color32 bg, Color32 fg, bool glowing)
    {
        int longest = 1;
        foreach (var line in lines) longest = Mathf.Max(longest, line.Length);
        int pw = longest * PixelFont.CellWidth + 8;
        int ph = lines.Length * (PixelFont.CellHeight + 1) + 6;
        float aspect = width / height;
        if (pw / (float)ph < aspect) pw = Mathf.CeilToInt(ph * aspect);
        else ph = Mathf.CeilToInt(pw / aspect);
        var canvas = new PixelCanvas(pw, ph);
        canvas.Clear(bg);
        int top = (ph - lines.Length * (PixelFont.CellHeight + 1)) / 2;
        for (int i = 0; i < lines.Length; i++)
            canvas.Text((pw - PixelCanvas.TextWidth(lines[i])) / 2, top + i * (PixelFont.CellHeight + 1), lines[i], fg);
        canvas.Apply();
        var source = glowing ? screenMaterial : signMaterial;
        if (source == null) return;
        var mat = new Material(source) { name = "Sign" };
        mat.SetTexture("_MainTex", canvas.Texture);
        if (mat.HasProperty("_MainColor")) mat.SetColor("_MainColor", Color.white);
        if (mat.HasProperty("_AffineTextureWarpingWeight")) mat.SetFloat("_AffineTextureWarpingWeight", 0f);
        Quad(centre, facing, width, height, mat);
    }

    public GameObject Quad(Vector3 centre, Vector3 facing, float width, float height, Material mat)
    {
        if (mat == null) return null;
        var mb = new MeshKit.Builder();
        float w = width * 0.5f, h = height * 0.5f;
        mb.Quad(new Vector3(-w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(w, h, 0f), new Vector3(w, -h, 0f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var go = MeshKit.Spawn("Sign", root, mb.ToMesh("Sign"), mat, root.position, root.rotation, false);
        go.transform.localPosition = centre;
        go.transform.localRotation = Quaternion.LookRotation(-facing);
        return go;
    }

    /// <summary>Turns all boxes into meshes (call once at the end).</summary>
    public void Finish()
    {
        foreach (var kv in geometry)
        {
            if (kv.Key == null) continue;
            var go = MeshKit.Spawn(kv.Key.name, root, kv.Value.ToMesh(kv.Key.name), kv.Key, root.position, root.rotation, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }
        geometry.Clear();
    }
}
