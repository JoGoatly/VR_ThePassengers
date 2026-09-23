using UnityEngine;

/// <summary>
/// Small navigation screen on the dashboard: heading-up map of the road ahead (you see the
/// curves before the fog shows them), upcoming bus stops, and a stopping guide that tells
/// you when the front door is at the stop.
/// </summary>
public class GpsDisplay : MonoBehaviour
{
    public ForestRoad road;
    public BusController bus;
    public BoardingManager game;

    [Header("Screen (bus space)")]
    public Vector3 position = new Vector3(-0.36f, 1.43f, 4.88f);
    [Tooltip("The screen turns towards this point (the driver's eyes)")]
    public Vector3 viewerPosition = new Vector3(-0.83f, 1.72f, 4.3f);
    public Vector2 screenSize = new Vector2(0.22f, 0.165f);
    public Material caseMaterial;
    [Tooltip("Unlit PSXLit material, its texture is replaced by the GPS image")]
    public Material screenMaterial;

    [Header("Map")]
    [Tooltip("Metres per screen pixel")]
    public float metresPerPixel = 1.6f;
    [Tooltip("Show 'stop here' when the front door is this close to the stop (m)")]
    public float stopWindow = 3f;

    const int W = 160, H = 120;
    PixelCanvas canvas;
    float redrawTimer, blink;

    static readonly Color32 Bg = new Color32(8, 12, 24, 255);
    static readonly Color32 Bar = new Color32(18, 26, 48, 255);
    static readonly Color32 RoadCol = new Color32(90, 100, 120, 255);
    static readonly Color32 Edge = new Color32(150, 160, 180, 255);
    static readonly Color32 Text = new Color32(210, 225, 255, 255);
    static readonly Color32 Dim = new Color32(110, 125, 160, 255);
    static readonly Color32 Yellow = new Color32(255, 205, 60, 255);
    static readonly Color32 Green = new Color32(60, 230, 110, 255);
    static readonly Color32 Red = new Color32(255, 80, 70, 255);

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        canvas = new PixelCanvas(W, H);
        Build();
    }

    void Build()
    {
        if (bus == null) return;
        var root = new GameObject("GPS").transform;
        root.SetParent(bus.transform, false);
        root.localPosition = position;
        root.localRotation = Quaternion.LookRotation(position - viewerPosition);

        float w = screenSize.x, h = screenSize.y;
        var c = MeshKit.Spawn("Case", root, MeshKit.Box(new Vector3(w + 0.03f, h + 0.03f, 0.05f), 0.5f), caseMaterial, root.position, root.rotation, false);
        c.transform.localPosition = new Vector3(0f, -(h + 0.03f) * 0.5f, 0.0f);

        var mb = new MeshKit.Builder();
        float z = -0.026f;
        mb.Quad(new Vector3(-w / 2, -h / 2, z), new Vector3(-w / 2, h / 2, z), new Vector3(w / 2, h / 2, z), new Vector3(w / 2, -h / 2, z),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var mat = screenMaterial != null ? new Material(screenMaterial) : null;
        if (mat != null) mat.SetTexture("_MainTex", canvas.Texture);
        MeshKit.Spawn("Screen", root, mb.ToMesh("GPS Screen"), mat, root.position, root.rotation, false);
    }

    void Update()
    {
        if (canvas == null || road == null || bus == null) return;
        blink += Time.deltaTime;
        redrawTimer -= Time.deltaTime;
        if (redrawTimer > 0f) return;
        redrawTimer = 0.08f;
        Draw();
        canvas.Apply();
    }

    void Draw()
    {
        canvas.Clear(Bg);
        Transform t = bus.transform;
        float busS = road.BusArcLength;
        Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector2 origin = new Vector2(W * 0.5f, H - 24);

        Vector2 ToScreen(Vector3 world)
        {
            Vector3 d = world - t.position;
            return origin + new Vector2(Vector3.Dot(d, right), -Vector3.Dot(d, fwd)) / metresPerPixel;
        }

        // Road ahead (and a bit behind).
        Vector2? prev = null;
        for (float s = busS - 20f; s < busS + 150f; s += 4f)
        {
            if (!road.TrySample(s, out Vector3 p, out _)) { prev = null; continue; }
            Vector2 q = ToScreen(p);
            if (prev.HasValue) { Thick(prev.Value, q, 3, Edge); }
            prev = q;
        }
        prev = null;
        for (float s = busS - 20f; s < busS + 150f; s += 4f)
        {
            if (!road.TrySample(s, out Vector3 p, out _)) { prev = null; continue; }
            Vector2 q = ToScreen(p);
            if (prev.HasValue) { Thick(prev.Value, q, 2, RoadCol); }
            prev = q;
        }

        // Bus stops.
        BusStop next = null;
        float nextDist = float.MaxValue;
        float doorAhead = game != null ? game.DoorLocal.z : 4.3f;
        foreach (var stop in road.Stops)
        {
            if (stop == null) continue;
            float remaining = stop.arcLength - busS - doorAhead;
            if (remaining > -8f && remaining < nextDist) { nextDist = remaining; next = stop; }
            Vector2 q = ToScreen(stop.waitPoint.position);
            if (q.x < -5 || q.x > W + 5 || q.y < 10 || q.y > H) continue;
            int x = Mathf.RoundToInt(q.x), y = Mathf.RoundToInt(q.y);
            canvas.Fill(x - 4, y - 5, 9, 11, Yellow);
            canvas.Text(x - 2, y - 5, "H", Bg);
        }

        // Dirt tracks to houses: a brown line and a small house.
        foreach (var sp in road.SidePaths)
        {
            if (sp.chunk == null) continue;
            Vector2 a0 = ToScreen(sp.start), a1 = ToScreen(sp.house);
            if (a1.y < 10 || a1.y > H || a0.y < 10 || a0.y > H) continue;
            Thick(a0, a1, 1, new Color32(120, 90, 50, 255));
            int hx = Mathf.RoundToInt(a1.x), hy = Mathf.RoundToInt(a1.y);
            if (hx < 3 || hx > W - 4) continue;
            canvas.Fill(hx - 3, hy - 2, 7, 5, new Color32(170, 120, 60, 255));
            canvas.Fill(hx - 2, hy - 4, 5, 2, new Color32(170, 120, 60, 255));
            canvas.Fill(hx - 1, hy - 5, 3, 1, new Color32(170, 120, 60, 255));
        }

        // The bus.
        int bx = Mathf.RoundToInt(origin.x), by = Mathf.RoundToInt(origin.y);
        for (int i = 0; i < 7; i++) canvas.Fill(bx - i / 2, by - 4 + i, i + 1, 1, Text);

        // Top: next stop.
        canvas.Fill(0, 0, W, 12, Bar);
        if (next != null)
        {
            string dist = nextDist > 999f ? ">1 km" : $"{Mathf.Max(0f, nextDist):0} m";
            canvas.Text(3, 1, next.stopName, Text, 17);
            canvas.Text(W - PixelCanvas.TextWidth(dist) - 3, 1, dist, Yellow);
        }
        else canvas.Text(3, 1, Loc.T("LINIE 13", "LINE 13"), Dim);

        // Bottom: stopping guide or speed.
        canvas.Fill(0, H - 13, W, 13, Bar);
        string msg;
        Color32 col;
        bool waiting = next != null && next.WaitingPassenger != null && next.WaitingPassenger.CurrentState == Passenger.State.Waiting;
        if (next != null && waiting && Mathf.Abs(nextDist) <= stopWindow)
        {
            bool stopped = Mathf.Abs(bus.Speed) < 0.3f;
            msg = stopped ? (bus.doorsOpen ? Loc.T("TÜREN OFFEN", "DOORS OPEN") : Loc.T("TÜREN ÖFFNEN ", "OPEN DOORS ") + GameKeys.Tag(GameAction.Doors)) : Loc.T("HIER HALTEN", "STOP HERE");
            col = stopped || Mathf.Repeat(blink, 0.6f) < 0.3f ? Green : Bg;
        }
        else if (next != null && waiting && nextDist < -stopWindow)
        {
            msg = Loc.T("ZU WEIT", "TOO FAR");
            col = Red;
        }
        else if (next != null && waiting && nextDist < 80f)
        {
            msg = Loc.T($"HALT IN {nextDist:0} m", $"STOP IN {nextDist:0} m");
            col = Yellow;
        }
        else
        {
            msg = $"{Mathf.Abs(bus.SpeedKmh):0} km/h";
            col = Text;
        }
        canvas.Text((W - PixelCanvas.TextWidth(msg)) / 2, H - 12, msg, col);
    }

    void Thick(Vector2 a, Vector2 b, int radius, Color32 c)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            canvas.Fill(Mathf.RoundToInt(p.x) - radius, Mathf.RoundToInt(p.y) - radius, radius * 2 + 1, radius * 2 + 1, c);
        }
    }
}
