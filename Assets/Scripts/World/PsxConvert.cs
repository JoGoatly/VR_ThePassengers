using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes imported models (Standard materials, pink in the PSX pipeline) look right:
/// every material is replaced by a PSX material with the same texture and colour.
/// Leaves / fences / plants get alpha clipping, glass is see-through, lamps and signs glow.
/// Textures the importer didn't find are looked up by material name in a Resources folder.
/// Also has helpers to load a model from Resources and fit it to a real size.
/// </summary>
public static class PsxConvert
{
    static Material lit, cutout, glow, glass;
    static readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
    static readonly Dictionary<string, Dictionary<string, Texture2D>> folders = new Dictionary<string, Dictionary<string, Texture2D>>();

    static void Load()
    {
        if (lit != null) return;
        lit = Resources.Load<Material>("PSX/PsxLit");
        cutout = Resources.Load<Material>("PSX/PsxCutout") ?? lit;
        glow = Resources.Load<Material>("PSX/PsxGlow") ?? lit;
        glass = Resources.Load<Material>("PSX/PsxGlass") ?? lit;
    }

    /// <summary>Instantiate a model from Resources, converted to PSX materials (null if missing).</summary>
    public static GameObject Spawn(string path, Transform parent, string textureFolder = null)
    {
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("PsxConvert: model not found: Resources/" + path); return null; }
        var go = Object.Instantiate(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        Apply(go, textureFolder);
        return go;
    }

    public static void Apply(GameObject go, string textureFolder = null)
    {
        Load();
        if (lit == null || go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = Convert(mats[i], textureFolder);
            r.sharedMaterials = mats;
        }
    }

    static Material Convert(Material m, string folder)
    {
        if (m == null) return lit;
        if (m.shader != null && m.shader.name.Contains("PSX")) return m;
        if (cache.TryGetValue(m, out var done) && done != null) return done;

        string name = m.name.Replace(" (Instance)", "");
        string lower = name.ToLowerInvariant();
        Texture tex = null;
        if (m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
        if (tex == null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
        if (tex == null && folder != null) tex = FindTexture(folder, name);
        Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        if (tex != null) color = new Color(Mathf.Max(color.r, 0.85f), Mathf.Max(color.g, 0.85f), Mathf.Max(color.b, 0.85f), color.a);

        Material source = lit;
        if (lower.Contains("glass") || lower.Contains("window_glass")) source = glass;
        else if (lower.Contains("emissor") || lower.Contains("emission") || lower.Contains("neon") || lower == "light" || lower.Contains("_sign")) source = glow;
        else if (lower.Contains("plant") || lower.Contains("tree") || lower.Contains("grass") || lower.Contains("fence") || lower.Contains("wire") ||
                 lower.Contains("bush") || lower.Contains("flower") || lower.Contains("leaf") || lower.Contains("wheat") || lower.Contains("mesh") ||
                 lower.Contains("curtain")) source = cutout;

        var result = new Material(source) { name = name + " (PSX)" };
        if (tex != null) result.SetTexture("_MainTex", tex);
        if (source != glass && result.HasProperty("_MainColor")) result.SetColor("_MainColor", tex != null ? Color.white : color);
        if (tex is Texture2D t2) t2.filterMode = FilterMode.Point;
        cache[m] = result;
        return result;
    }

    static Texture2D FindTexture(string folder, string materialName)
    {
        if (!folders.TryGetValue(folder, out var map))
        {
            map = new Dictionary<string, Texture2D>();
            foreach (var t in Resources.LoadAll<Texture2D>(folder)) map[t.name.ToLowerInvariant()] = t;
            folders[folder] = map;
        }
        string key = materialName.ToLowerInvariant();
        if (map.TryGetValue(key, out var tex)) return tex;
        // "Couch.001", "Mat_Couch" ...
        foreach (var kv in map)
            if (key.Contains(kv.Key) || kv.Key.Contains(key)) return kv.Value;
        return null;
    }

    /// <summary>Bounds of all renderers in the space of 'space'.</summary>
    public static Bounds LocalBounds(Transform model, Transform space)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            var lb = mesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    /// <summary>
    /// Scales a model so its largest horizontal size is 'size' metres (only if its units are
    /// clearly off - centimetres or tiny) and puts its bottom centre on the holder's origin.
    /// </summary>
    public static void Fit(GameObject model, Transform holder, float expectedSize)
    {
        var b = LocalBounds(model.transform, holder);
        float largest = Mathf.Max(b.size.x, b.size.z);
        if (largest > 0.001f && (largest > expectedSize * 4f || largest < expectedSize / 4f))
        {
            model.transform.localScale *= expectedSize / largest;
            b = LocalBounds(model.transform, holder);
        }
        model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
    }

    /// <summary>Box colliders roughly around every mesh part bigger than 'minSize' (walls, counters).</summary>
    public static void AddColliders(GameObject model, float minSize = 0.4f)
    {
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            var s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            if (Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) < minSize) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
    }
}
