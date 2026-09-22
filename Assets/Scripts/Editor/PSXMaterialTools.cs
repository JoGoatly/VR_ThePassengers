using System.Collections.Generic;
using System.IO;
using System.Linq;
using HauntedPSX.RenderPipelines.PSX.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Helpers for the HauntedPS1 render pipeline (PSX/PSXLit shader).
///
/// - On editor load, keywords/render queue of every PSXLit material are re-applied,
///   exactly like the material inspector would do.
/// - Tools > PSX > Convert Selected Models To PSXLit: creates PSXLit materials for the
///   selected FBX files (texture found by material name) and remaps them on import.
/// </summary>
[InitializeOnLoad]
public static class PSXMaterialTools
{
    const string ShaderName = "PSX/PSXLit";

    static PSXMaterialTools()
    {
        EditorApplication.delayCall += ValidateAllPSXMaterials;
    }

    [MenuItem("Tools/PSX/Validate PSXLit Materials")]
    public static void ValidateAllPSXMaterials()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) return;

        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader != shader) continue;

            string before = State(mat);
            PSXMaterialUtils.SetMaterialKeywords(mat);
            if (State(mat) != before)
            {
                EditorUtility.SetDirty(mat);
                changed++;
            }
        }
        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"PSX: updated keywords of {changed} PSXLit material(s).");
        }
    }

    static string State(Material m) =>
        string.Join(" ", m.shaderKeywords.OrderBy(k => k)) + "|" + m.renderQueue + "|" + m.GetTag("RenderType", false);

    [MenuItem("Tools/PSX/Convert Selected Models To PSXLit")]
    static void ConvertSelectedModels()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("PSX: shader " + ShaderName + " not found. Is the HauntedPS1 render pipeline installed?");
            return;
        }

        var models = Selection.objects
            .Select(o => AssetDatabase.GetAssetPath(o))
            .Where(p => AssetImporter.GetAtPath(p) is ModelImporter)
            .Distinct()
            .ToList();
        if (models.Count == 0)
        {
            Debug.LogWarning("PSX: select one or more model files (FBX) in the Project window.");
            return;
        }

        foreach (string modelPath in models)
            ConvertModel(modelPath, shader);

        AssetDatabase.SaveAssets();
    }

    static void ConvertModel(string modelPath, Shader shader)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string matFolder = folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder(folder, "Materials");

        var embedded = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList();
        var remaps = importer.GetExternalObjectMap();
        var names = new HashSet<string>(embedded.Select(m => m.name));
        foreach (var kv in remaps)
            if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);

        foreach (string matName in names)
        {
            string matPath = $"{matFolder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;

            var source = embedded.FirstOrDefault(m => m.name == matName);
            Texture tex = source != null && source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (tex == null) tex = FindTextureByName(matName);
            if (tex != null) mat.SetTexture("_MainTex", tex);

            mat.SetFloat("_TextureFilterMode", 1f);       // Point
            mat.SetFloat("_ShadingEvaluationMode", 0f);   // Per vertex
            mat.SetFloat("_LightingMode", 0f);            // Lit
            PSXMaterialUtils.SetMaterialKeywords(mat);
            EditorUtility.SetDirty(mat);

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
        }

        importer.SaveAndReimport();
        Debug.Log($"PSX: converted {names.Count} material(s) of {modelPath}");
    }

    static Texture FindTextureByName(string textureName)
    {
        foreach (string guid in AssetDatabase.FindAssets(textureName + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == textureName)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }
}
