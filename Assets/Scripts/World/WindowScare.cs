using UnityEngine;

/// <summary>
/// Very rarely someone is pressed against a window - the driver's window on the left or a
/// window at the back right. It only appears where the player is not looking. Once the
/// player looks at it: a hit, a moment of black screen, and it is gone.
/// </summary>
public class WindowScare : MonoBehaviour
{
    public BusController bus;
    public SoundManager sound;
    public GameObject[] figures;

    [Tooltip("Seconds between scare attempts")]
    public Vector2 interval = new Vector2(120f, 300f);
    public float firstDelay = 150f;
    [Range(0f, 1f)] public float chance = 0.6f;
    [Tooltip("Seconds the figure waits to be seen before it silently leaves")]
    public float lifetime = 40f;
    [Tooltip("Seconds the player has to look at it")]
    public float lookTime = 0.35f;
    public float blackoutTime = 0.18f;

    // Bus-local spots just outside the windows, figure faces into the bus.
    static readonly (Vector3 pos, float yaw)[] Spots =
    {
        (new Vector3(-1.45f, 0.35f, 4.0f), 90f),    // driver window (left)
        (new Vector3(1.45f, 0.35f, -2.6f), -90f),   // back right
        (new Vector3(1.45f, 0.35f, -3.8f), -90f),
    };

    GameObject current;
    float nextAt, despawnAt, lookedFor, blackoutUntil = -1f;
    Texture2D black;

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (sound == null) sound = FindAnyObjectByType<SoundManager>();
        nextAt = Time.time + firstDelay + Random.Range(0f, 60f);
        black = Texture2D.blackTexture;
    }

    void Update()
    {
        var cam = Camera.main;
        if (bus == null || cam == null || figures == null || figures.Length == 0) return;

        if (current == null)
        {
            if (Time.time < nextAt || GameUI.PlayerOutside) return;
            nextAt = Time.time + Random.Range(interval.x, interval.y);
            if (Random.value > chance) return;
            var spot = Spots[Random.Range(0, Spots.Length)];
            // Only where the player is not looking right now.
            if (Angle(cam, bus.transform.TransformPoint(spot.pos + Vector3.up * 1.6f)) < 75f) return;
            Spawn(spot.pos, spot.yaw);
            return;
        }

        if (GameUI.PlayerOutside || Time.time > despawnAt)
        {
            Destroy(current);
            current = null;
            return;
        }

        Vector3 head = current.transform.position + current.transform.up * 1.6f;
        lookedFor = Angle(cam, head) < 20f ? lookedFor + Time.deltaTime : 0f;
        if (lookedFor >= lookTime)
        {
            blackoutUntil = Time.time + blackoutTime;
            if (sound != null) sound.PlayScare();
            Destroy(current);
            current = null;
        }
    }

    static float Angle(Camera cam, Vector3 point) =>
        Vector3.Angle(cam.transform.forward, point - cam.transform.position);

    void Spawn(Vector3 local, float yaw)
    {
        current = Instantiate(figures[Random.Range(0, figures.Length)], bus.transform);
        current.name = "Window Figure";
        current.transform.localPosition = local;
        current.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(8f, 0f, 0f);
        // Bind pose: arms spread out, as if pressed against the glass.
        foreach (var a in current.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var smr in current.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        // A faint cold light so it can be seen in the dark.
        var lightGo = new GameObject("Figure Light");
        lightGo.transform.SetParent(current.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.7f, 0.9f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.6f, 0.7f, 0.8f);
        l.intensity = 2f;
        l.range = 2.2f;

        despawnAt = Time.time + lifetime;
        lookedFor = 0f;
    }

    void OnGUI()
    {
        if (Time.time >= blackoutUntil) return;
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
    }
}
