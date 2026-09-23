using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Car radio on the dashboard. Aim at it and click its buttons: power, previous / next
/// station, volume - and +. The radio key (R) switches to the next station (and off after the last).
/// Stations play "live": switching back continues where the broadcast is now.
/// </summary>
public class BusRadio : MonoBehaviour
{
    [System.Serializable]
    public class Station
    {
        public string name;
        public string frequency;
        public AudioClip clip;
    }

    public BusController bus;
    public Station[] stations;
    public AudioClip tuningStatic;

    [Header("Device (bus space)")]
    public Vector3 position = new Vector3(-0.05f, 1.36f, 4.9f);
    public Vector3 viewerPosition = new Vector3(-0.83f, 1.72f, 4.3f);
    public Vector2 screenSize = new Vector2(0.24f, 0.09f);
    public Material caseMaterial;
    public Material screenMaterial;
    public float maxUseDistance = 2f;

    [Range(0f, 1f)] public float volume = 0.5f;

    const int W = 192, H = 72;
    PixelCanvas canvas;
    Transform screen;
    AudioSource source, staticSource;
    int current = -1;           // -1 = off
    Vector2Int pointer;
    bool hover;
    float scroll, redraw;

    static readonly Color32 Bg = new Color32(10, 6, 2, 255);
    static readonly Color32 Amber = new Color32(255, 170, 60, 255);
    static readonly Color32 AmberDim = new Color32(120, 70, 20, 255);
    static readonly Color32 Btn = new Color32(30, 22, 14, 255);
    static readonly Color32 BtnHi = new Color32(70, 48, 22, 255);
    static readonly Color32 Off = new Color32(20, 16, 12, 255);

    public bool IsHovered => hover;

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (bus == null) return;
        canvas = new PixelCanvas(W, H);
        var speaker = Build();

        source = speaker.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0.5f;
        source.minDistance = 2f;
        source.maxDistance = 25f;
        staticSource = speaker.AddComponent<AudioSource>();
        staticSource.playOnAwake = false;
        staticSource.spatialBlend = 0.5f;
    }

    GameObject Build()
    {
        var root = new GameObject("Radio").transform;
        root.SetParent(bus.transform, false);
        root.localPosition = position;
        root.localRotation = Quaternion.LookRotation(position - viewerPosition);

        float w = screenSize.x, h = screenSize.y;
        var c = MeshKit.Spawn("Case", root, MeshKit.Box(new Vector3(w + 0.03f, h + 0.03f, 0.08f), 0.5f), caseMaterial, root.position, root.rotation, false);
        c.transform.localPosition = new Vector3(0f, -(h + 0.03f) * 0.5f, 0f);

        var mb = new MeshKit.Builder();
        float z = -0.041f;
        mb.Quad(new Vector3(-w / 2, -h / 2, z), new Vector3(-w / 2, h / 2, z), new Vector3(w / 2, h / 2, z), new Vector3(w / 2, -h / 2, z),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        var mat = screenMaterial != null ? new Material(screenMaterial) : null;
        if (mat != null) mat.SetTexture("_MainTex", canvas.Texture);
        screen = MeshKit.Spawn("Radio Face", root, mb.ToMesh("Radio Face"), mat, root.position, root.rotation, false).transform;

        // The sound comes from the dashboard.
        var speaker = new GameObject("Radio Speaker");
        speaker.transform.SetParent(root, false);
        return speaker;
    }

    void Update()
    {
        if (canvas == null || screen == null) return;
        UpdatePointer();

        var mouse = Mouse.current;
        bool click = hover && mouse != null && mouse.leftButton.wasPressedThisFrame && !GameUI.AnyOpen;
        if (click) HandleClick();
        if (GameKeys.Pressed(GameAction.Radio) && !GameUI.MenuOpen && !GameUI.TerminalTyping)
            Tune(current + 1 >= (stations?.Length ?? 0) ? -1 : current + 1);

        source.volume = volume * GameSettings.Music;
        staticSource.volume = volume * GameSettings.Music * 0.8f;

        scroll += Time.deltaTime * 22f;
        redraw -= Time.deltaTime;
        if (redraw <= 0f || click)
        {
            redraw = 0.05f;
            Draw();
            canvas.Apply();
        }
    }

    void UpdatePointer()
    {
        hover = false;
        var cam = Camera.main;
        if (cam == null || GameUI.PlayerOutside) return;
        Vector3 o = screen.InverseTransformPoint(cam.transform.position);
        Vector3 d = screen.InverseTransformDirection(cam.transform.forward);
        float z = -0.041f;
        if (Mathf.Abs(d.z) < 1e-5f) return;
        float t = (z - o.z) / d.z;
        if (t <= 0f) return;
        Vector3 hit = o + d * t;
        if (Vector3.Distance(screen.TransformPoint(hit), cam.transform.position) > maxUseDistance) return;
        float u = hit.x / screenSize.x + 0.5f, v = hit.y / screenSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;
        hover = true;
        pointer = new Vector2Int(Mathf.FloorToInt(u * W), Mathf.FloorToInt((1f - v) * H));
    }

    // Buttons along the bottom.
    static readonly string[] Buttons = { "PWR", "<", ">", "-", "+" };
    static RectInt ButtonRect(int i) => new RectInt(4 + i * 37, 46, 33, 22);

    void HandleClick()
    {
        for (int i = 0; i < Buttons.Length; i++)
        {
            if (!ButtonRect(i).Contains(pointer)) continue;
            int n = stations?.Length ?? 0;
            switch (i)
            {
                case 0: Tune(current < 0 ? 0 : -1); break;
                case 1: if (n > 0) Tune(current <= 0 ? n - 1 : current - 1); break;
                case 2: if (n > 0) Tune(current < 0 ? 0 : (current + 1) % n); break;
                case 3: volume = Mathf.Clamp01(Mathf.Round((volume - 0.1f) * 10f) / 10f); break;
                case 4: volume = Mathf.Clamp01(Mathf.Round((volume + 0.1f) * 10f) / 10f); break;
            }
        }
    }

    void Tune(int station)
    {
        current = station;
        if (tuningStatic != null) staticSource.PlayOneShot(tuningStatic);
        if (current < 0 || stations == null || current >= stations.Length || stations[current].clip == null)
        {
            source.Stop();
            return;
        }
        var clip = stations[current].clip;
        source.clip = clip;
        // "Live" broadcast: every station keeps running while you listen to another one.
        source.time = (Time.unscaledTime + current * 17.3f) % clip.length;
        source.Play();
    }

    void Draw()
    {
        canvas.Clear(Bg);
        canvas.Frame(0, 0, W, H, AmberDim);
        bool on = current >= 0 && stations != null && current < stations.Length;

        // Display.
        canvas.Fill(4, 4, W - 8, 38, on ? new Color32(28, 14, 4, 255) : Off);
        if (on)
        {
            var st = stations[current];
            canvas.Text(8, 7, st.frequency + " MHz", Amber);
            // Station name scrolls if too long.
            string name = st.name.ToUpperInvariant();
            int maxChars = (W - 16) / PixelFont.CellWidth;
            if (name.Length > maxChars)
            {
                string loop = name + "   ***   ";
                int start = Mathf.FloorToInt(scroll / PixelFont.CellWidth) % loop.Length;
                name = (loop + loop).Substring(start, maxChars);
            }
            canvas.Text(8, 19, name, Amber);
            // Volume bar.
            int bars = Mathf.RoundToInt(volume * 10f);
            for (int i = 0; i < 10; i++)
                canvas.Fill(8 + i * 8, 33, 6, 5, i < bars ? Amber : AmberDim);
            canvas.Text(96, 30, "VOL " + Mathf.RoundToInt(volume * 100f), AmberDim);
        }
        else
        {
            canvas.Text(8, 16, Loc.T("RADIO AUS", "RADIO OFF"), AmberDim);
        }

        for (int i = 0; i < Buttons.Length; i++)
        {
            var r = ButtonRect(i);
            bool over = hover && r.Contains(pointer);
            canvas.Fill(r.x, r.y, r.width, r.height, over ? BtnHi : Btn);
            canvas.Frame(r.x, r.y, r.width, r.height, over ? Amber : AmberDim);
            string label = Buttons[i];
            canvas.Text(r.x + (r.width - PixelCanvas.TextWidth(label)) / 2, r.y + (r.height - PixelFont.CellHeight) / 2 + 1, label,
                i == 0 && on ? Amber : new Color32(200, 170, 130, 255));
        }

        // Pointer.
        if (hover)
        {
            canvas.Fill(pointer.x, pointer.y, 1, 5, Color.white);
            canvas.Fill(pointer.x, pointer.y, 4, 1, Color.white);
        }
    }
}
