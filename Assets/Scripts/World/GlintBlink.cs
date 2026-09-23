using UnityEngine;

/// <summary>Makes the little glint on a pickup twinkle.</summary>
public class GlintBlink : MonoBehaviour
{
    float phase;
    void Start() => phase = Random.value * 10f;
    void Update()
    {
        bool on = Mathf.Repeat(Time.time * 0.7f + phase, 1f) < 0.55f;
        transform.localScale = Vector3.one * (on ? 1f : 0.3f);
    }
}
