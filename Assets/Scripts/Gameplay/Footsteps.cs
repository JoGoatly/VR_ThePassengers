using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Footstep sounds from the PSX footsteps pack (Resources/Footsteps/&lt;Surface&gt;), picked by
/// what is under the foot: road = concrete, shoulder / tracks = gravel, forest floor =
/// grass or dirt, wooden floors = wood, inside the bus = metal.
/// Steps of others are 3D and fade out with distance; your own are right at your ears.
/// </summary>
public static class Footsteps
{
    public enum Surface { Concrete, Dirt, Grass, Gravel, Metal, Stairs, Stone, Wood }

    static readonly Dictionary<Surface, AudioClip[]> clips = new Dictionary<Surface, AudioClip[]>();
    static readonly RaycastHit[] hits = new RaycastHit[8];
    static int lastIndex = -1;

    static AudioClip[] Clips(Surface s)
    {
        if (!clips.TryGetValue(s, out var list))
        {
            list = Resources.LoadAll<AudioClip>("Footsteps/" + s);
            clips[s] = list;
        }
        return list;
    }

    /// <summary>What is under this point.</summary>
    public static Surface SurfaceAt(Vector3 position)
    {
        if (GameUI.InBus) return Surface.Metal;
        int n = Physics.RaycastNonAlloc(position + Vector3.up * 0.6f, Vector3.down, hits, 2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider col = null;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider is CharacterController) continue;
            if (hits[i].distance < best) { best = hits[i].distance; col = hits[i].collider; }
        }
        if (col == null) return Surface.Dirt;
        var bus = Object.FindAnyObjectByType<BusController>();
        if (bus != null && col.transform.IsChildOf(bus.transform)) return Surface.Metal;

        string name = col.gameObject.name;
        if (name.Contains("Road") || name.Contains("Deck") || name.Contains("Platform")) return Surface.Concrete;
        if (name.Contains("Shoulder") || name.Contains("Track")) return Surface.Gravel;
        if (name.Contains("Stair") || name.Contains("Step")) return Surface.Stairs;
        var r = col.GetComponent<Renderer>();
        string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name.ToLowerInvariant() : "";
        if (mat.Contains("wood") || mat.Contains("carpet") || mat.Contains("plank")) return Surface.Wood;
        if (mat.Contains("metal") || mat.Contains("chrome")) return Surface.Metal;
        if (mat.Contains("gravel")) return Surface.Gravel;
        if (mat.Contains("grass")) return Surface.Grass;
        if (mat.Contains("stone") || mat.Contains("brick")) return Surface.Stone;
        if (mat.Contains("concrete") || mat.Contains("tile") || mat.Contains("paint") || mat.Contains("asphalt") || mat.Contains("floor")) return Surface.Concrete;
        if (name.Contains("Terrain"))
        {
            // Flat places (depot yard, petrol station...) are paved.
            var road = Object.FindAnyObjectByType<ForestRoad>();
            if (road != null && road.InClearing(position)) return Surface.Concrete;
            return Mathf.PerlinNoise(position.x * 0.1f, position.z * 0.1f) > 0.55f ? Surface.Dirt : Surface.Grass;
        }
        return Surface.Concrete;
    }

    /// <summary>One step at a position; mine = the player's own (not placed in 3D).</summary>
    public static void Play(Vector3 position, bool mine, float volume = 1f)
    {
        var list = Clips(SurfaceAt(position));
        if (list == null || list.Length == 0) return;
        int i = Random.Range(0, list.Length);
        if (list.Length > 1 && i == lastIndex) i = (i + 1) % list.Length;
        lastIndex = i;
        var clip = list[i];

        var go = new GameObject("Step");
        go.transform.position = position;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.pitch = Random.Range(0.92f, 1.08f);
        s.volume = volume * GameSettings.Effects * (mine ? 0.45f : 0.9f);
        // Others: full volume up close, silent at 22 m.
        s.spatialBlend = mine ? 0f : 1f;
        s.rolloffMode = AudioRolloffMode.Custom;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.08f, 0.9f), new Keyframe(0.35f, 0.3f), new Keyframe(1f, 0f)));
        s.minDistance = 1f;
        s.maxDistance = 22f;
        s.dopplerLevel = 0f;
        s.Play();
        Object.Destroy(go, clip.length / s.pitch + 0.1f);
    }
}
