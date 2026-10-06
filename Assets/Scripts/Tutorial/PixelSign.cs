using UnityEngine;

/// <summary>
/// A sign with pixel text (on a quad or a box face). Edit the lines and colours in the
/// inspector; the texture is drawn by itself (also in the editor). Use a white material.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class PixelSign : MonoBehaviour
{
    [TextArea] public string text = "HALT";
    public string englishText;
    public Color32 background = new Color32(250, 210, 40, 255);
    public Color32 foreground = new Color32(20, 30, 20, 255);
    [Tooltip("Width / height of the sign face")]
    public float aspect = 2f;

    MaterialPropertyBlock block;
    Texture2D texture;
    string built;

    void OnEnable() => Build();
    void OnValidate() => built = null;

    void Update()
    {
        string wanted = Current();
        if (wanted != built) Build();
    }

    string Current() => Loc.English && !string.IsNullOrEmpty(englishText) ? englishText : text;

    void Build()
    {
        string current = Current() ?? "";
        built = current;
        var lines = current.Split('\n');
        int longest = 1;
        foreach (var line in lines) longest = Mathf.Max(longest, line.Length);
        int pw = longest * PixelFont.CellWidth + 8;
        int ph = lines.Length * (PixelFont.CellHeight + 1) + 6;
        if (pw / (float)ph < aspect) pw = Mathf.CeilToInt(ph * aspect);
        else ph = Mathf.CeilToInt(pw / aspect);
        var canvas = new PixelCanvas(pw, ph);
        canvas.Clear(background);
        int top = (ph - lines.Length * (PixelFont.CellHeight + 1)) / 2;
        for (int i = 0; i < lines.Length; i++)
            canvas.Text((pw - PixelCanvas.TextWidth(lines[i])) / 2, top + i * (PixelFont.CellHeight + 1), lines[i], foreground);
        canvas.Apply();
        canvas.Texture.hideFlags = HideFlags.DontSave;

        // A property block: the material stays as it is (nothing extra gets saved in the scene).
        if (texture != null) DestroyImmediate(texture);
        texture = canvas.Texture;
        var r = GetComponent<MeshRenderer>();
        if (block == null) block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetTexture("_MainTex", texture);
        r.SetPropertyBlock(block);
    }
}
