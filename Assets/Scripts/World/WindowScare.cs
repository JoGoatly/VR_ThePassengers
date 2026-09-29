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
    [Tooltip("Figure prefab(s) - Killer 09")]
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
    [Tooltip("Seconds between knocks while it is at the window")]
    public Vector2 knockInterval = new Vector2(3f, 6f);

    [Header("Spots outside the windows (bus space), the figure faces into the bus")]
    [Tooltip("Driver window on the left")]
    public Vector3 driverWindow = new Vector3(-1.7f, 0.2f, 4.6f);
    [Tooltip("Windows at the back right")]
    public Vector3[] backRightWindows = { new Vector3(1.7f, 0.2f, -2.6f), new Vector3(1.7f, 0.2f, -3.8f) };

    (Vector3 pos, float yaw)[] Spots
    {
        get
        {
            var list = new System.Collections.Generic.List<(Vector3, float)> { (driverWindow, 90f) };
            foreach (var p in backRightWindows) list.Add((p, -90f));
            return list.ToArray();
        }
    }

    GameObject current;
    float nextAt, despawnAt, nextKnockAt, lookedFor, blackoutUntil = -1f;
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
        if (bus == null || cam == null || figures == null || figures.Length == 0 || Tutorial.Active) return;

        if (current == null)
        {
            if (Time.time < nextAt || GameUI.PlayerOutside || Progress.Day == 1) return;
            // From the second night on, and more often every night.
            nextAt = Time.time + Random.Range(interval.x, interval.y) / (0.6f + DayManager.Dread);
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

        if (Time.time >= nextKnockAt)
        {
            nextKnockAt = Time.time + Random.Range(knockInterval.x, knockInterval.y);
            if (sound != null) sound.PlayKnock(current.transform.position + current.transform.forward * 0.4f + Vector3.up * 1.9f);
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
        current.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(6f, 0f, 0f);
        foreach (var a in current.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var smr in current.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        PoseAgainstGlass(current.transform);

        // A faint cold light so it can be seen in the dark.
        var lightGo = new GameObject("Figure Light");
        lightGo.transform.SetParent(current.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.8f, 0.6f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.6f, 0.7f, 0.8f);
        l.intensity = 2f;
        l.range = 2.2f;

        despawnAt = Time.time + lifetime;
        nextKnockAt = Time.time + 0.5f;
        lookedFor = 0f;
    }

    // Hands up flat against the window, head tilted: no T-pose. The Animator is off,
    // so the pose set once stays.
    static void PoseAgainstGlass(Transform figure)
    {
        Vector3 f = figure.forward, u = figure.up, r = figure.right;
        foreach (var (side, outward) in new[] { ("Left", -r), ("Right", r) })
        {
            var arm = FindBone(figure, side + "Arm");
            var fore = FindBone(figure, side + "ForeArm");
            var hand = FindBone(figure, side + "Hand");
            Aim(arm, fore, (f * 0.5f + u * 0.45f + outward * 0.65f).normalized);
            Aim(fore, hand, (f * 0.3f + u * 0.95f - outward * 0.15f).normalized);
        }
        var head = FindBone(figure, "Head");
        if (head != null) head.rotation = Quaternion.AngleAxis(18f, f) * Quaternion.AngleAxis(-10f, r) * head.rotation;
    }

    static void Aim(Transform bone, Transform child, Vector3 direction)
    {
        if (bone == null || child == null) return;
        bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
    }

    static Transform FindBone(Transform root, string boneName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            if (n == boneName) return t;
        }
        return null;
    }

    void OnGUI()
    {
        if (Time.time >= blackoutUntil) return;
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
    }
}
