using UnityEngine;

/// <summary>
/// Displays inside the bus instead of screen overlays:
///   - the stop display on the panel above the windscreen (amber LED, next stop + distance,
///     "HALT" when someone wants to get off),
///   - a small display in the upper left corner of the windscreen: night, passengers, money.
/// Positions are in bus model space (like the seats) and face the driver's eyes.
/// </summary>
public class BusDisplays : MonoBehaviour
{
    public BusController bus;
    public BoardingManager game;
    public Material screenMaterial;

    [Header("Stop display (model space)")]
    public Vector3 stopDisplayCentre = new Vector3(-2.10f, 1.085f, 0.12f);
    public Vector2 stopDisplaySize = new Vector2(0.36f, 0.055f);

    [Header("Info display (model space)")]
    public Vector3 infoDisplayCentre = new Vector3(-2.12f, 1.035f, -0.40f);
    public Vector2 infoDisplaySize = new Vector2(0.15f, 0.07f);

    PixelCanvas stopCanvas, infoCanvas;
    float redraw, scroll;
    Vector3 eyeLocal;

    static readonly Color32 Off = new Color32(12, 6, 2, 255);
    static readonly Color32 Amber = new Color32(255, 150, 20, 255);
    static readonly Color32 AmberDim = new Color32(90, 45, 8, 255);
    static readonly Color32 Red = new Color32(255, 50, 30, 255);
    static readonly Color32 Green = new Color32(120, 230, 140, 255);
    static readonly Color32 GreenDim = new Color32(40, 90, 50, 255);

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) return;
        var inside = BusInterior.Of(bus.transform);
        if (inside == null || !inside.Valid) { Debug.LogWarning("BusDisplays: bus model parts not found.", this); return; }

        var cam = FindAnyObjectByType<DriverCamera>();
        eyeLocal = cam != null ? bus.transform.InverseTransformPoint(cam.transform.position) : inside.ToBus(new Vector3(-1.85f, 0.95f, -0.36f));

        stopCanvas = new PixelCanvas(128, 20);
        infoCanvas = new PixelCanvas(84, 38);
        Build("Stop Display", inside, stopDisplayCentre, stopDisplaySize, stopCanvas);
        Build("Info Display", inside, infoDisplayCentre, infoDisplaySize, infoCanvas);
    }

    // A quad in bus space, turned towards the driver's eyes.
    void Build(string name, BusInterior inside, Vector3 modelCentre, Vector2 modelSize, PixelCanvas canvas)
    {
        float scale = inside.Scale;
        Vector3 c = inside.ToBus(modelCentre);
        Vector3 f = (eyeLocal - c).normalized;          // front normal, towards the viewer
        Vector3 view = -f;
        Vector3 right = Vector3.Cross(Vector3.up, view).normalized;
        Vector3 up = Vector3.Cross(view, right).normalized;
        float w = modelSize.x * scale * 0.5f, h = modelSize.y * scale * 0.5f;
        c += f * 0.01f;   // just in front of the panel
        var mb = new MeshKit.Builder();
        mb.Quad(c - right * w - up * h, c - right * w + up * h, c + right * w + up * h, c + right * w - up * h,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var mat = screenMaterial != null ? new Material(screenMaterial) : null;
        if (mat != null) mat.SetTexture("_MainTex", canvas.Texture);
        var go = MeshKit.Spawn(name, bus.transform, mb.ToMesh(name), mat, bus.transform.position, bus.transform.rotation, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        if (stopCanvas == null || game == null) return;
        scroll += Time.deltaTime * 24f;
        redraw -= Time.deltaTime;
        if (redraw > 0f) return;
        redraw = 0.06f;
        DrawStop();
        DrawInfo();
        stopCanvas.Apply();
        infoCanvas.Apply();
    }

    void DrawStop()
    {
        var c = stopCanvas;
        c.Clear(Off);
        // LED rows.
        for (int y = 1; y < c.Height; y += 2) c.Fill(0, y, c.Width, 1, new Color32(6, 3, 1, 255));

        var next = game.NextStop(out float dist);
        bool halt = game.StopRequested && Mathf.Repeat(Time.time, 1f) < 0.6f;
        string distance = next != null ? $"{Mathf.Max(0f, dist):0} m" : "";
        // With a time limit the display shows the time left instead of the distance.
        var rules = ShiftRules.Instance;
        bool late = false;
        if (next != null && rules != null && rules.TimeLeft >= 0f)
        {
            int secs = Mathf.CeilToInt(rules.TimeLeft);
            distance = $"{secs / 60}:{secs % 60:00}";
            late = rules.TimeLeft < 15f;
        }
        int distW = PixelCanvas.TextWidth(distance) + 4;
        if (halt)
        {
            string text = Loc.T("HALT", "STOP");
            c.Text((c.Width - distW - PixelCanvas.TextWidth(text)) / 2, 4, text, Red);
        }
        else
        {
            string text = next != null ? Loc.T("NÄCHSTER HALT: ", "NEXT STOP: ") + next.stopName.ToUpperInvariant() : Loc.T("NACHTLINIE 13", "NIGHT LINE 13");
            int room = c.Width - distW - 4;
            int tw = PixelCanvas.TextWidth(text);
            int x = 3;
            if (tw > room)
            {
                // Too long: scroll through.
                int span = tw + 30;
                x = 3 - Mathf.FloorToInt(scroll) % span;
                c.Text(x + span, 4, text, Amber);
            }
            c.Text(x, 4, text, Amber);
            c.Fill(room + 2, 0, c.Width - room - 2, c.Height, Off);   // keep the distance readable
        }
        c.Text(c.Width - distW + 1, 4, distance, late && Mathf.Repeat(Time.time, 0.6f) < 0.35f ? Red : AmberDim);
    }

    void DrawInfo()
    {
        var c = infoCanvas;
        c.Clear(new Color32(4, 10, 6, 255));
        c.Frame(0, 0, c.Width, c.Height, GreenDim);
        int quota = DayManager.QuotaFor(Progress.Day);
        c.Text(4, 2, Loc.T($"NACHT {Progress.Day}/{Progress.LastDay}", $"NIGHT {Progress.Day}/{Progress.LastDay}"), Green);
        c.Text(4, 13, Loc.T($"FAHRG. {Mathf.Min(game.Decisions, quota)}/{quota}", $"PASS. {Mathf.Min(game.Decisions, quota)}/{quota}"), Green);
        c.Text(4, 24, $"{Progress.Money} EUR", Progress.Money < 0 ? Red : Green);
        if (Features.Has(Feature.Fatigue))
        {
            // Energy drinks and how awake you are.
            c.Text(c.Width - 22, 24, $"x{Progress.Data.energyDrinks}", Green);
            float awake = ShiftRules.Instance != null ? ShiftRules.Instance.Alertness : 1f;
            c.Fill(c.Width - 6, 3, 3, c.Height - 6, new Color32(20, 40, 25, 255));
            int hgt = Mathf.RoundToInt((c.Height - 6) * awake);
            c.Fill(c.Width - 6, c.Height - 3 - hgt, 3, hgt, awake < 0.3f ? Red : Green);
        }
    }
}
