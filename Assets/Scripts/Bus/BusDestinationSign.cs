using UnityEngine;

/// <summary>
/// The bright LED destination display at the top of the bus front (and a small one at the
/// back): line number and destination in amber dots, switching between two texts.
/// </summary>
[DefaultExecutionOrder(150)]
public class BusDestinationSign : MonoBehaviour
{
    public Color32 ledColor = new Color32(255, 176, 40, 255);
    public float switchSeconds = 4f;
    [Tooltip("Area of the bus texture (UV) where the front destination display is painted")]
    public Rect frontDisplayUV = new Rect(0.034f, 0.147f, 0.088f, 0.014f);
    [Tooltip("Area of the bus texture (UV) of the window above the rear (rear display)")]
    public Rect backDisplayUV = new Rect(0.853f, 0.0995f, 0.118f, 0.035f);

    PixelCanvas canvas;
    Material material;
    int shown = -1;
    float nextSwitch;
    string[] texts;

    void Start()
    {
        canvas = new PixelCanvas(128, 16);
        var glow = Resources.Load<Material>("PSX/PsxGlow");
        material = new Material(glow != null ? glow : Resources.Load<Material>("PSX/PsxLit")) { name = "Destination Sign" };
        material.SetTexture("_MainTex", canvas.Texture);
        if (material.HasProperty("_MainColor")) material.SetColor("_MainColor", Color.white);
        if (material.HasProperty("_AffineTextureWarpingWeight")) material.SetFloat("_AffineTextureWarpingWeight", 0f);

        // Right on the displays painted on the bus model (where that part of its texture is),
        // the text reading the same way as the painted one.
        if (BusShape.FindTextureArea(transform, frontDisplayUV, true, out var c, out var reading, out var up, out var size))
            Place("Destination Sign Front", c, reading, up, size);
        else
            canvas = null;   // no display found: better nothing than a sign floating somewhere
        if (canvas != null && BusShape.FindTextureArea(transform, backDisplayUV, false, out c, out reading, out up, out size))
            Place("Destination Sign Back", c, reading, up, size);
    }

    // A glowing quad over the painted display, facing outwards, the text upright and readable
    // from outside (however the painted text is turned in the texture).
    void Place(string name, Vector3 localCentre, Vector3 reading, Vector3 paintedUp, Vector2 size)
    {
        Vector3 forward = Vector3.Cross(reading, paintedUp).normalized;
        if (Vector3.Dot(forward, new Vector3(0f, 0f, localCentre.z)) < 0f) forward = -forward;   // outwards
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
        if (up.sqrMagnitude < 0.5f) up = paintedUp;
        // Wider than high: if the painted text runs up/down the width and height swap.
        if (Mathf.Abs(Vector3.Dot(reading, up)) > 0.7f) size = new Vector2(size.y, size.x);
        var mb = new MeshKit.Builder();
        float x = size.x * 0.5f, y = size.y * 0.5f;
        mb.Quad(new Vector3(x, -y, 0f), new Vector3(x, y, 0f), new Vector3(-x, y, 0f), new Vector3(-x, -y, 0f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var go = MeshKit.Spawn(name, transform, mb.ToMesh(name), material, transform.position, transform.rotation, false);
        go.transform.localPosition = localCentre + forward.normalized * 0.012f;
        go.transform.localRotation = Quaternion.LookRotation(forward, up);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (canvas == null) return;
        texts = Tutorial.Active
            ? new[] { Loc.T("FAHRSCHULE", "DRIVING SCHOOL"), Loc.T("KEIN EINSTIEG", "NOT IN SERVICE") }
            : new[] { Loc.T("13 NACHTLINIE", "13 NIGHT LINE"), Loc.T("13 WALDFRIEDHOF", "13 FOREST CEMETERY") };
        if (Time.time < nextSwitch && shown >= 0) return;
        nextSwitch = Time.time + switchSeconds;
        shown = (shown + 1) % texts.Length;
        Draw(texts[shown]);
    }

    void Draw(string text)
    {
        var off = new Color32(14, 8, 2, 255);
        canvas.Clear(off);
        int tw = PixelCanvas.TextWidth(text);
        canvas.Text(Mathf.Max(1, (canvas.Width - tw) / 2), (canvas.Height - PixelFont.CellHeight) / 2, text, ledColor);
        canvas.Apply();
    }
}
