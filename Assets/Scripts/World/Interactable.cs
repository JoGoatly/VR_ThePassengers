using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something the player on foot can use with the interact key (E): pickups, trapdoors,
/// ladders. The nearest one in front of the player wins.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    public float radius = 1.7f;

    static readonly List<Interactable> all = new List<Interactable>();

    /// <summary>Text for the prompt, e.g. "Aufheben: Munition".</summary>
    public abstract string Prompt { get; }
    public abstract void Use();

    protected virtual void OnEnable() => all.Add(this);
    protected virtual void OnDisable() => all.Remove(this);

    public static Interactable Nearest(Vector3 position, Vector3 forward)
    {
        Interactable best = null;
        float bestScore = float.MaxValue;
        foreach (var i in all)
        {
            if (i == null) continue;
            Vector3 d = i.transform.position - position;
            if (Mathf.Abs(d.y) > 2.2f) continue;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > i.radius) continue;
            // Prefer what is in front of the player.
            float score = dist - (dist > 0.01f ? Vector3.Dot(d / dist, forward) * 0.6f : 0.6f);
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}
