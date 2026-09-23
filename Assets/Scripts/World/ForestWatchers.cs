using UnityEngine;

/// <summary>
/// Now and then a figure stands at the edge of the forest, staring at the bus.
/// It is gone before the bus gets close.
/// </summary>
public class ForestWatchers : MonoBehaviour
{
    public ForestRoad road;
    public GameObject[] figures;
    [Tooltip("Check every ... metres driven")]
    public float checkEvery = 400f;
    [Range(0f, 1f)] public float chance = 0.12f;

    /// <summary>Raised when a figure vanishes (for a sound).</summary>
    public event System.Action<Vector3> Vanished;

    GameObject current;
    float nextCheckAt = 900f;
    float vanishDistance;
    Transform bus;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        var bc = FindAnyObjectByType<BusController>();
        bus = bc != null ? bc.transform : null;
    }

    void Update()
    {
        if (road == null || bus == null || figures == null || figures.Length == 0) return;
        float busS = road.BusArcLength;

        if (current == null)
        {
            if (busS < nextCheckAt) return;
            nextCheckAt = busS + checkEvery;
            if (Random.value > chance * (0.5f + 2.5f * DayManager.Dread)) return;   // more of them every night
            if (!road.TrySample(busS + Random.Range(18f, 28f), out Vector3 p, out Vector3 t)) return;

            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 right = Vector3.Cross(Vector3.up, t).normalized;
            Vector3 pos = p + right * side * (road.EdgeOffset + Random.Range(-0.6f, 1.0f));
            current = Instantiate(figures[Random.Range(0, figures.Length)], pos, Quaternion.LookRotation(-right * side));
            current.name = "Watcher";
            current.AddComponent<Passenger>();   // only for the standing pose
            vanishDistance = Random.Range(4.5f, 7f);
            return;
        }

        // Always face the bus.
        Vector3 toBus = bus.position - current.transform.position;
        toBus.y = 0f;
        if (toBus.sqrMagnitude > 0.01f) current.transform.rotation = Quaternion.LookRotation(toBus);

        if (toBus.magnitude < vanishDistance || Vector3.Dot(toBus, bus.forward) > 0f)
        {
            Vanished?.Invoke(current.transform.position);
            Destroy(current);
            current = null;
        }
    }
}
