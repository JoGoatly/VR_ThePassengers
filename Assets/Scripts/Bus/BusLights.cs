using HauntedPSX.RenderPipelines.PSX.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    [Header("High beam")]
    [Tooltip("Extra far lights placed this far ahead of the bus, so the road far away gets light without blinding the near field")]
    public float farLightAhead = 12f;
    public float farRange = 45f, farAngle = 34f, farIntensity = 22f, farPitch = 3f;
    [Header("Fog per light mode (distance where the fog is opaque)")]
    public float fogOff = 10f, fogLow = 12f, fogHigh = 24f;
    public float drawOff = 14f, drawLow = 16f, drawHigh = 30f;
    public Material glowMaterial;

    [Header("Tail and cabin lights")]
    public Vector3 tailLight = new Vector3(0f, 0.9f, -5.6f);
    public Vector3 cabinLight = new Vector3(0f, 2.55f, 1.5f);
    public float cabinIntensity = 2f;

    /// <summary>Raised when the mode changes (for the switch sound).</summary>
    public event System.Action<Mode> Switched;

    bool powerCut;
    /// <summary>A blown fuse: every light of the bus is off until it is fixed.</summary>
    public bool PowerCut
    {
        get => powerCut;
        set { powerCut = value; Apply(); }
    }

    Light left, right, farLeft, farRight, tail, cabin;
    FogVolume fog;
    PrecisionVolume precision;
    GameObject glowLeft, glowRight;

    void Awake()
    {
        left = CreateSpot("Headlight L", headlightLeft);
        right = CreateSpot("Headlight R", headlightRight);
        farLeft = CreateSpot("High Beam L", headlightLeft + new Vector3(0f, 1.4f, farLightAhead));
        farRight = CreateSpot("High Beam R", headlightRight + new Vector3(0f, 1.4f, farLightAhead));
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

    void Start()
    {
        // Runtime copy of the volume profile, so the fog can follow the light mode.
        var volume = FindAnyObjectByType<Volume>();
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out fog);
            volume.profile.TryGet(out precision);
        }
    }

    void LateUpdate()
    {
        // Xenon upgrade sees further; the fog gets thicker every night.
        float high = Progress.Owns("highbeam2") ? fogHigh + 10f : fogHigh;
        float targetFog = (mode == Mode.HighBeam ? high : mode == Mode.LowBeam ? fogLow : fogOff) * (1f - 0.25f * DayManager.Dread);
        float targetDraw = mode == Mode.HighBeam ? drawHigh : mode == Mode.LowBeam ? drawLow : drawOff;
        float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
        if (fog != null) fog.distanceMax.value = Mathf.Lerp(fog.distanceMax.value, targetFog, k);
        if (precision != null) precision.drawDistance.value = Mathf.Lerp(precision.drawDistance.value, targetDraw, k);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && GameKeys.Pressed(GameAction.Lights) && !GameUI.AnyOpen)
        {
            mode = (Mode)(((int)mode + 1) % 3);
            Apply();
            Switched?.Invoke(mode);
        }
    }

    void Apply()
    {
        if (left == null) return;
        bool on = mode != Mode.Off && !powerCut;
        bool high = mode == Mode.HighBeam && !powerCut;
        if (cabin != null) cabin.enabled = !powerCut;
        // Low beam lights the near field in both modes; high beam adds far lights.
        foreach (var l in new[] { left, right })
        {
            l.enabled = on;
            l.range = lowRange;
            l.spotAngle = lowAngle;
            l.intensity = lowIntensity;
            l.transform.localRotation = Quaternion.Euler(lowPitch, 0f, 0f);
        }
        foreach (var l in new[] { farLeft, farRight })
        {
            l.enabled = high;
            bool xenon = Progress.Owns("highbeam2");
            l.range = xenon ? farRange * 1.45f : farRange;
            l.spotAngle = farAngle;
            l.intensity = xenon ? farIntensity * 1.3f : farIntensity;
            l.transform.localRotation = Quaternion.Euler(farPitch, 0f, 0f);
        }
        if (glowLeft) glowLeft.SetActive(on);
        if (glowRight) glowRight.SetActive(on);
        tail.enabled = on;
    }

    public string ModeText => mode switch
    {
        Mode.LowBeam => Loc.T("ABBLENDLICHT", "LOW BEAM"),
        Mode.HighBeam => Loc.T("FERNLICHT", "HIGH BEAM"),
        _ => Loc.T("LICHT AUS", "LIGHTS OFF"),
    };
}
