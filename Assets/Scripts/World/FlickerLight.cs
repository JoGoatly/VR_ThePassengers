using UnityEngine;

/// <summary>Old lamp: mostly steady, sometimes flickers or dies for a moment - with a buzzing crackle.</summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Range(0f, 1f)] public float flickerChance = 0.08f;
    public float minIntensity = 0.1f;
    [Tooltip("Buzz while flickering (default: Resources/Sfx/light_flicker)")]
    public AudioClip flickerSound;
    public float soundVolume = 0.55f;
    public float soundDistance = 14f;

    static AudioClip defaultSound;
    static bool defaultLoaded;

    Light lamp;
    AudioSource buzz;
    float baseIntensity;
    float flickerUntil;
    float nextCheck;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        if (flickerSound == null)
        {
            if (!defaultLoaded) { defaultSound = Resources.Load<AudioClip>("Sfx/light_flicker"); defaultLoaded = true; }
            flickerSound = defaultSound;
        }
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + Random.Range(0.4f, 2.5f);
            if (Random.value < flickerChance && Time.time >= flickerUntil)
            {
                flickerUntil = Time.time + Random.Range(0.15f, 1.2f);
                StartBuzz();
            }
        }

        if (Time.time < flickerUntil)
            lamp.intensity = Random.value < 0.5f ? baseIntensity * minIntensity : baseIntensity * Random.Range(0.5f, 1f);
        else
        {
            lamp.intensity = baseIntensity * (0.94f + Mathf.PerlinNoise(Time.time * 3f, transform.position.x) * 0.06f);
            if (buzz != null && buzz.isPlaying) buzz.Stop();
        }
    }

    void StartBuzz()
    {
        if (flickerSound == null || !lamp.enabled || !gameObject.activeInHierarchy) return;
        if (buzz == null)
        {
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.playOnAwake = false;
            buzz.spatialBlend = 1f;
            buzz.rolloffMode = AudioRolloffMode.Linear;
            buzz.minDistance = 1f;
            buzz.maxDistance = soundDistance;
            buzz.dopplerLevel = 0f;
        }
        buzz.clip = flickerSound;
        buzz.volume = soundVolume * GameSettings.Effects;
        buzz.pitch = Random.Range(0.9f, 1.1f);
        buzz.time = Random.Range(0f, Mathf.Max(0f, flickerSound.length - 1.2f));
        buzz.Play();
    }
}
