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
    [Tooltip("Height of the front display (m)")]
    public float height = 0.28f;

    PixelCanvas canvas;
    Material material;
    int shown = -1;
    float nextSwitch;
    string[] texts;

    void Start()
    {
        // Size of the bus body (without the driver and the camera).
        var cam = Camera.main;
        var driver = GetComponentInChildren<DriverBody>();
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (driver != null && r.transform.IsChildOf(driver.transform)) continue;
            if (cam != null && r.transform.IsChildOf(cam.transform)) continue;
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        if (float.IsInfinity(min.x)) return;

        canvas = new PixelCanvas(128, 16);
        var glow = Resources.Load<Material>("PSX/PsxGlow");
        material = new Material(glow != null ? glow : Resources.Load<Material>("PSX/PsxLit")) { name = "Destination Sign" };
        material.SetTexture("_MainTex", canvas.Texture);
        if (material.HasProperty("_MainColor")) material.SetColor("_MainColor", Color.white);
        if (material.HasProperty("_AffineTextureWarpingWeight")) material.SetFloat("_AffineTextureWarpingWeight", 0f);

        float width = (max.x - min.x) * 0.62f;
        Spawn("Destination Sign Front", new Vector3((min.x + max.x) * 0.5f, max.y - height * 0.5f - 0.12f, max.z + 0.015f), width, height, true);
        Spawn("Destination Sign Back", new Vector3((min.x + max.x) * 0.5f, max.y - 0.2f, min.z - 0.015f), width * 0.45f, height * 0.7f, false);
    }

    // A glowing quad facing forward (+Z) or backward, text readable from outside.
    void Spawn(string name, Vector3 localCentre, float w, float h, bool front)
    {
        var mb = new MeshKit.Builder();
        float x = w * 0.5f, y = h * 0.5f;
        if (front)
            mb.Quad(new Vector3(x, -y, 0f), new Vector3(x, y, 0f), new Vector3(-x, y, 0f), new Vector3(-x, -y, 0f),
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        else
            mb.Quad(new Vector3(-x, -y, 0f), new Vector3(-x, y, 0f), new Vector3(x, y, 0f), new Vector3(x, -y, 0f),
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var go = MeshKit.Spawn(name, transform, mb.ToMesh(name), material, transform.position, transform.rotation, false);
        go.transform.localPosition = localCentre;
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
