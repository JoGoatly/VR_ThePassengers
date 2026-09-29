using UnityEngine;

/// <summary>
/// "WOHIN???" - leaving the road is not allowed. On foot: walk too far into the forest (or
/// away from the bus) and you are back next to the bus, looking at it. With the bus: drive
/// off the road (or flip it) and it is back on the road where it last was.
/// </summary>
public class OffRoadGuard : MonoBehaviour
{
    public ForestRoad road;
    public BusController bus;
    public PlayerOnFoot onFoot;
    public SoundManager sound;

    [Tooltip("On foot: metres beyond the forest edge that are allowed")]
    public float walkIntoForest = 10f;
    [Tooltip("On foot: maximum distance from the bus")]
    public float maxDistanceFromBus = 45f;
    [Tooltip("Bus: metres beyond the road edge that are allowed")]
    public float driveOffRoad = 3f;
    public float showTime = 1.6f;

    Vector3 safePosition;
    Quaternion safeRotation;
    bool haveSafe;
    float safeTimer, flippedTime, shownAt = -100f, cooldownUntil;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (onFoot == null) onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (sound == null) sound = FindAnyObjectByType<SoundManager>();
    }

    float Lateral(Vector3 world)
    {
        float s = road.ArcLengthAt(world);
        if (!road.TrySample(s, out Vector3 p, out Vector3 t)) return 0f;
        return Vector3.Dot(world - p, Vector3.Cross(Vector3.up, t).normalized);
    }

    void Update()
    {
        if (road == null || bus == null || GameUI.MenuOpen || GameUI.AtHome || Time.time < cooldownUntil) return;

        if (GameUI.PlayerOutside && onFoot != null && onFoot.Walker != null)
        {
            Vector3 pos = onFoot.Walker.position;
            float lateral = Mathf.Abs(Lateral(pos));
            float fromBus = Vector3.Distance(pos, bus.transform.position);
            // Keep the bus' search position valid for the road.
            road.ArcLengthAt(bus.transform.position);
            if (!SideAreas.IsInside(pos) && (lateral > road.EdgeOffset + walkIntoForest || fromBus > maxDistanceFromBus))
            {
                onFoot.ReturnToBus();
                Scare();
            }
            return;
        }

        float busLateral = Lateral(bus.transform.position);
        bool upright = Vector3.Dot(bus.transform.up, Vector3.up) > 0.6f;
        flippedTime = upright ? 0f : flippedTime + Time.deltaTime;

        // Remember where the bus was last properly on the road.
        safeTimer -= Time.deltaTime;
        if (safeTimer <= 0f && upright && Mathf.Abs(busLateral) < road.laneWidth)
        {
            safeTimer = 0.3f;
            safePosition = bus.transform.position;
            safeRotation = bus.transform.rotation;
            haveSafe = true;
        }

        // Petrol stations and other flat places may be driven onto.
        bool onPlace = road.InClearing(bus.transform.position);
        if (haveSafe && ((Mathf.Abs(busLateral) > road.EdgeOffset + driveOffRoad && !onPlace) || flippedTime > 1.5f))
        {
            bus.ResetTo(safePosition + Vector3.up * 0.05f, safeRotation);
            flippedTime = 0f;
            Scare();
        }
    }

    void Scare()
    {
        shownAt = Time.time;
        cooldownUntil = Time.time + showTime + 1f;
        if (sound != null) sound.PlayScare();
    }

    void OnGUI()
    {
        float t = Time.time - shownAt;
        if (t < 0f || t > showTime) return;
        GUI.depth = -900;
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        // Pure black for a moment, then the dark fades out.
        float dark = t < 0.18f ? 1f : Mathf.Lerp(0.9f, 0f, (t - 0.18f) / (showTime - 0.18f));
        RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, dark));
        if (t < 0.12f) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(RetroGUI.Scale * (46f + Mathf.Sin(t * 40f) * 2f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        float alpha = Mathf.Clamp01((showTime - t) / 0.4f);
        string text = Loc.T("WOHIN???", "WHERE TO???");
        Vector2 jitter = new Vector2(Random.Range(-3f, 3f), Random.Range(-3f, 3f));
        style.normal.textColor = new Color(0f, 0f, 0f, alpha);
        GUI.Label(RetroGUI.R(jitter.x + 2, h / 2 - 40 + jitter.y + 2, w, 80), text, style);
        style.normal.textColor = new Color(0.85f, 0.02f, 0.02f, alpha);
        GUI.Label(RetroGUI.R(jitter.x, h / 2 - 40 + jitter.y, w, 80), text, style);
    }
}
