using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Headlights (L: off -> low beam -> high beam -> off), tail lights and a dim,
/// sometimes flickering cabin light. Positions are in bus space.
/// </summary>
public class BusLights : MonoBehaviour
{
    public enum Mode { Off, LowBeam, HighBeam }

    public Mode mode = Mode.LowBeam;

    [Header("Headlights")]
    public Vector3 headlightLeft = new Vector3(-0.85f, 0.8f, 5.35f);
    public Vector3 headlightRight = new Vector3(0.85f, 0.8f, 5.35f);
    public Color headlightColor = new Color(1f, 0.93f, 0.78f);
    // HPSXRP lights fall off with 1/distance² and the colour is gamma-linearised (intensity^2.2),
    // so a headlight needs a high intensity to still light the road 15-30 m ahead.
    public float lowRange = 35f, lowAngle = 75f, lowIntensity = 15f, lowPitch = 6f;
    public float highRange = 80f, highAngle = 40f, highIntensity = 34f, highPitch = 1.5f;
    public Material glowMaterial;

    [Header("Tail and cabin lights")]
    public Vector3 tailLight = new Vector3(0f, 0.9f, -5.6f);
    public Vector3 cabinLight = new Vector3(0f, 2.55f, 1.5f);
    public float cabinIntensity = 2f;

    /// <summary>Raised when the mode changes (for the switch sound).</summary>
    public event System.Action<Mode> Switched;

    Light left, right, tail, cabin;
    GameObject glowLeft, glowRight;

    void Awake()
    {
        left = CreateSpot("Headlight L", headlightLeft);
        right = CreateSpot("Headlight R", headlightRight);
        glowLeft = CreateGlow(headlightLeft);
        glowRight = CreateGlow(headlightRight);

        tail = CreatePoint("Tail Light", tailLight, new Color(0.9f, 0.08f, 0.05f), 6f, 2.1f);
        cabin = CreatePoint("Cabin Light", cabinLight, new Color(1f, 0.82f, 0.55f), 7.5f, cabinIntensity);
        var flicker = cabin.gameObject.AddComponent<FlickerLight>();
        flicker.flickerChance = 0.05f;

        Apply();
    }

    Light CreateSpot(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = headlightColor;
        l.shadows = LightShadows.None;
        return l;
    }

    Light CreatePoint(string name, Vector3 localPos, Color color, float range, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    GameObject CreateGlow(Vector3 localPos)
    {
        if (glowMaterial == null) return null;
        var go = MeshKit.Spawn("Headlight Glow", transform, MeshKit.Box(new Vector3(0.32f, 0.14f, 0.02f), 0.32f),
                               glowMaterial, Vector3.zero, Quaternion.identity, false);
        go.transform.localPosition = localPos + new Vector3(0f, -0.07f, 0.02f);
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame && !GameUI.AnyOpen)
        {
            mode = (Mode)(((int)mode + 1) % 3);
            Apply();
            Switched?.Invoke(mode);
        }
    }

    void Apply()
    {
        bool on = mode != Mode.Off;
        bool high = mode == Mode.HighBeam;
        foreach (var l in new[] { left, right })
        {
            l.enabled = on;
            l.range = high ? highRange : lowRange;
            l.spotAngle = high ? highAngle : lowAngle;
            l.intensity = high ? highIntensity : lowIntensity;
            l.transform.localRotation = Quaternion.Euler(high ? highPitch : lowPitch, 0f, 0f);
        }
        if (glowLeft) glowLeft.SetActive(on);
        if (glowRight) glowRight.SetActive(on);
        tail.enabled = on;
    }

    public string ModeText => mode switch
    {
        Mode.LowBeam => "ABBLENDLICHT",
        Mode.HighBeam => "FERNLICHT",
        _ => "LICHT AUS",
    };
}
