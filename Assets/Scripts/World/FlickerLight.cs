using UnityEngine;

/// <summary>Old lamp: mostly steady, sometimes flickers or dies for a moment.</summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Range(0f, 1f)] public float flickerChance = 0.08f;
    public float minIntensity = 0.1f;

    Light lamp;
    float baseIntensity;
    float flickerUntil;
    float nextCheck;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + Random.Range(0.4f, 2.5f);
            if (Random.value < flickerChance) flickerUntil = Time.time + Random.Range(0.15f, 1.2f);
        }

        if (Time.time < flickerUntil)
            lamp.intensity = Random.value < 0.5f ? baseIntensity * minIntensity : baseIntensity * Random.Range(0.5f, 1f);
        else
            lamp.intensity = baseIntensity * (0.94f + Mathf.PerlinNoise(Time.time * 3f, transform.position.x) * 0.06f);
    }
}
