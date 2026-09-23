using UnityEngine;

/// <summary>
/// Small display on the instrument panel behind the steering wheel: time, date, speed,
/// light mode and why the bus can't drive. Replaces the old speed text in the corner.
/// Its corners are given in the bus model's own (FBX) coordinates and mapped onto the
/// model with its wheel and steering wheel pivots, so it sits on the panel however the
/// model is scaled or mirrored.
/// </summary>
public class DashDisplay : MonoBehaviour
{
    public BusController bus;
    public BoardingManager game;
    [Tooltip("Unlit PSXLit material, its texture is replaced by the display image")]
    public Material screenMaterial;

    [Header("Corners in bus model coordinates (front = away from the driver)")]
    public Vector3 backLeft = new Vector3(-2.098f, 0.503f, -0.455f);
    public Vector3 frontLeft = new Vector3(-2.172f, 0.536f, -0.455f);
    public Vector3 frontRight = new Vector3(-2.172f, 0.536f, -0.265f);
    public Vector3 backRight = new Vector3(-2.098f, 0.503f, -0.265f);

    const int W = 128, H = 52;
    PixelCanvas canvas;
    BusLights lights;
    float redraw;

    static readonly Color32 Bg = new Color32(6, 10, 14, 255);
    static readonly Color32 Text = new Color32(150, 215, 255, 255);
    static readonly Color32 Dim = new Color32(60, 95, 125, 255);
    static readonly Color32 Warn = new Color32(255, 90, 70, 255);
    static readonly Color32 Beam = new Color32(90, 160, 255, 255);

    // Pivots of model parts (FBX coordinates) used to map model space onto the bus.
    static readonly (string name, Vector3 model)[] Anchors =
    {
        ("SteeringWheel", new Vector3(-1.97861f, 0.55869f, -0.36051f)),
        ("Wheel_FL", new Vector3(-1.26508f, 0.20523f, 0.48111f)),
        ("Wheel_FR", new Vector3(-1.26508f, 0.20523f, -0.48111f)),
        ("Wheels_B", new Vector3(1.07503f, 0.20523f, 0f)),
    };

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) return;
        lights = bus.GetComponent<BusLights>();
        canvas = new PixelCanvas(W, H);
        if (!TryModelToBus(out Matrix4x4 m)) { Debug.LogWarning("DashDisplay: bus model parts not found.", this); return; }

        Vector3 up = new Vector3(0f, 0.004f, 0f);   // just above the panel
        Vector3 a = m.MultiplyPoint3x4(backLeft + up), b = m.MultiplyPoint3x4(frontLeft + up);
        Vector3 c = m.MultiplyPoint3x4(frontRight + up), d = m.MultiplyPoint3x4(backRight + up);
        var mb = new MeshKit.Builder();
        // Both windings, so it shows whatever way the model is mirrored.
        mb.Quad(a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        mb.Quad(a, d, c, b, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        var mat = screenMaterial != null ? new Material(screenMaterial) : null;
        if (mat != null) mat.SetTexture("_MainTex", canvas.Texture);
        var go = MeshKit.Spawn("Dash Display", bus.transform, mb.ToMesh("Dash Display"), mat, bus.transform.position, bus.transform.rotation, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
    }

    // Affine map from model coordinates to bus (root) space, solved from four part pivots.
    bool TryModelToBus(out Matrix4x4 m)
    {
        m = Matrix4x4.identity;
        var p = Matrix4x4.identity;
        var w = Matrix4x4.identity;
        for (int i = 0; i < Anchors.Length; i++)
        {
            var t = Find(bus.transform, Anchors[i].name);
            if (t == null) return false;
            Vector3 world = bus.transform.InverseTransformPoint(t.position);
            p.SetColumn(i, new Vector4(Anchors[i].model.x, Anchors[i].model.y, Anchors[i].model.z, 1f));
            w.SetColumn(i, new Vector4(world.x, world.y, world.z, 1f));
        }
        if (Mathf.Abs(p.determinant) < 1e-6f) return false;
        m = w * p.inverse;
        return true;
    }

    static Transform Find(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = Find(child, name);
            if (f != null) return f;
        }
        return null;
    }

    void Update()
    {
        if (canvas == null) return;
        redraw -= Time.deltaTime;
        if (redraw > 0f) return;
        redraw = 0.1f;
        Draw();
        canvas.Apply();
    }

    void Draw()
    {
        canvas.Clear(Bg);
        canvas.Frame(0, 0, W, H, Dim);

        // Time and date (after midnight it is the next day).
        string clock = game != null ? game.ClockText : "--:--";
        var date = CitizenRegistry.Today.AddDays(Progress.Day - 1);
        if (clock.Length >= 2 && int.TryParse(clock.Substring(0, 2), out int hour) && hour < 12) date = date.AddDays(1);
        canvas.Text(4, 2, clock, Text);
        string d = date.ToString("dd.MM.yy");
        canvas.Text(W - PixelCanvas.TextWidth(d) - 4, 2, d, Dim);
        canvas.Fill(3, 14, W - 6, 1, Dim);

        // Speed, big.
        string speed = Mathf.RoundToInt(Mathf.Abs(bus.SpeedKmh)).ToString();
        int sw = speed.Length * PixelFont.CellWidth * 2;
        int sx = (W - sw - PixelCanvas.TextWidth(" km/h")) / 2;
        canvas.BigText(sx, 16, speed, Text, 2);
        canvas.Text(sx + sw + 2, 25, "km/h", Dim);
        if (bus.Speed < -0.1f) canvas.Text(4, 25, "R", Warn);

        // Lights and the reason the bus can't drive.
        string light = lights == null ? "" : lights.mode switch
        {
            BusLights.Mode.HighBeam => Loc.T("FERN", "HIGH"),
            BusLights.Mode.LowBeam => Loc.T("ABBL", "LOW"),
            _ => Loc.T("AUS", "OFF"),
        };
        if (lights != null) canvas.Text(4, H - 12, light, lights.mode == BusLights.Mode.HighBeam ? Beam : Dim);
        string reason = bus.DriveLockReason;
        if (!string.IsNullOrEmpty(reason) && Mathf.Repeat(Time.time, 1f) < 0.7f)
        {
            string r = reason.ToUpperInvariant();
            int max = (W - 40) / PixelFont.CellWidth;
            if (r.Length > max) r = r.Substring(0, max);
            canvas.Text(W - PixelCanvas.TextWidth(r) - 4, H - 12, r, Warn);
        }
    }
}
