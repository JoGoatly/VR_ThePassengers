using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Now and then a car comes towards the bus on the left (oncoming) lane, headlights on,
/// and disappears behind it. Uses the car prefabs if set, otherwise a simple box car.
/// </summary>
public class OncomingTraffic : MonoBehaviour
{
    public ForestRoad road;
    public BusController bus;

    /// <summary>A car model and its paint jobs (textures).</summary>
    [System.Serializable]
    public class CarModel
    {
        public GameObject model;
        public Texture2D[] paints;
    }

    [Header("Cars")]
    [Tooltip("Car models with their paint textures. Empty = simple low-poly box car")]
    public CarModel[] cars;
    [Tooltip("PSX material for the cars; its texture is replaced by the paint")]
    public Material carMaterial;
    [Tooltip("Extra rotation of the models if their front does not point along +Z")]
    public float modelYaw = 0f;
    [Tooltip("The PSX car pack is about 1.5x real size")]
    public float modelScale = 0.7f;
    public Material bodyMaterial;
    [Tooltip("Unlit glow material for head- and tail lights (e.g. HeadlightGlow)")]
    public Material glowMaterial;
    public AudioClip driveSound;

    [Header("Traffic")]
    [Tooltip("Seconds between cars")]
    public Vector2 interval = new Vector2(25f, 70f);
    public float firstCarAfter = 40f;
    public Vector2 speedKmh = new Vector2(55f, 85f);
    public Vector2 spawnAhead = new Vector2(170f, 230f);
    public float headlightIntensity = 12f;
    public float headlightRange = 30f;

    class Car
    {
        public GameObject go;
        public float s, speed, cruise;
        public AudioSource sound;
    }

    readonly List<Car> driving = new List<Car>();
    float nextCarAt;
    Material tailMaterial;

    void Start()
    {
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        nextCarAt = Time.time + firstCarAfter;
        if (glowMaterial != null)
        {
            tailMaterial = new Material(glowMaterial) { name = "Tail Light" };
            tailMaterial.SetColor("_MainColor", new Color(0.9f, 0.05f, 0.03f));
            if (tailMaterial.HasProperty("_EmissionColor")) tailMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.05f, 0.03f));
        }
    }

    void Update()
    {
        if (road == null || bus == null) return;
        float busS = road.BusArcLength;

        if (Time.time >= nextCarAt)
        {
            nextCarAt = Time.time + Random.Range(interval.x, interval.y) * (1f + DayManager.Dread);   // lonelier every night
            Spawn(busS + Random.Range(spawnAhead.x, spawnAhead.y));
        }

        // Where is the bus across the road? (negative = on the oncoming lane)
        float busLateral = 0f;
        if (road.TrySample(busS, out Vector3 bp, out Vector3 bt))
            busLateral = Vector3.Dot(bus.transform.position - bp, Vector3.Cross(Vector3.up, bt).normalized);

        for (int i = driving.Count - 1; i >= 0; i--)
        {
            var car = driving[i];
            // Brake if the bus blocks the lane just ahead.
            float gap = car.s - busS;
            bool blocked = busLateral < 0.3f && gap > 0f && gap < 30f;
            car.speed = Mathf.MoveTowards(car.speed, blocked ? 0f : car.cruise, (blocked ? 9f : 3f) * Time.deltaTime);
            car.s -= car.speed * Time.deltaTime;

            if (car.s < busS - 60f || !road.TrySample(car.s, out Vector3 p, out Vector3 t))
            {
                Destroy(car.go);
                driving.RemoveAt(i);
                continue;
            }
            Vector3 right = Vector3.Cross(Vector3.up, t).normalized;
            car.go.transform.SetPositionAndRotation(p - right * road.laneWidth * 0.5f, Quaternion.LookRotation(-t));
            if (car.sound != null) car.sound.volume = 0.7f * GameSettings.Effects * Mathf.Clamp01(car.speed / 8f + 0.3f);
        }
    }

    void Spawn(float s)
    {
        if (!road.TrySample(s, out Vector3 p, out Vector3 t)) return;
        var root = new GameObject("Oncoming Car");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Bounds local;
        var pick = cars != null && cars.Length > 0 ? cars[Random.Range(0, cars.Length)] : null;
        if (pick != null && pick.model != null)
        {
            var model = Instantiate(pick.model, root.transform);
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            model.transform.localScale *= modelScale;
            Texture2D paint = pick.paints != null && pick.paints.Length > 0 ? pick.paints[Random.Range(0, pick.paints.Length)] : null;
            ApplyPaint(model, paint);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            local = LocalBounds(root.transform);
            // Put the wheels on the road.
            model.transform.localPosition -= new Vector3(0f, local.min.y, 0f);
            local.center -= new Vector3(0f, local.min.y, 0f);
        }
        else
        {
            local = BuildBoxCar(root.transform);
        }

        AddLights(root.transform, local);

        var col = root.AddComponent<BoxCollider>();
        col.center = local.center;
        col.size = local.size;
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;

        var car = new Car { go = root, s = s, cruise = Random.Range(speedKmh.x, speedKmh.y) / 3.6f };
        car.speed = car.cruise;
        if (driveSound != null)
        {
            car.sound = root.AddComponent<AudioSource>();
            car.sound.clip = driveSound;
            car.sound.loop = true;
            car.sound.spatialBlend = 1f;
            car.sound.minDistance = 4f;
            car.sound.maxDistance = 90f;
            car.sound.rolloffMode = AudioRolloffMode.Linear;
            car.sound.dopplerLevel = 1.2f;
            car.sound.pitch = Random.Range(0.9f, 1.15f);
            car.sound.volume = 0f;
            car.sound.Play();
        }
        Vector3 right = Vector3.Cross(Vector3.up, t).normalized;
        root.transform.SetPositionAndRotation(p - right * road.laneWidth * 0.5f, Quaternion.LookRotation(-t));
        driving.Add(car);
    }

    readonly Dictionary<Texture2D, Material> paintMaterials = new Dictionary<Texture2D, Material>();

    // The imported OBJ materials don't work with the PSX pipeline: use the PSX car material with the paint.
    void ApplyPaint(GameObject model, Texture2D paint)
    {
        if (carMaterial == null) return;
        Material mat = carMaterial;
        if (paint != null && !paintMaterials.TryGetValue(paint, out mat))
        {
            mat = new Material(carMaterial) { name = "Car " + paint.name };
            mat.SetTexture("_MainTex", paint);
            paintMaterials[paint] = mat;
        }
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    static Bounds LocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 4.2f));
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;   // root sits at the origin without rotation, so world = local
    }

    Bounds BuildBoxCar(Transform root)
    {
        Material mat = bodyMaterial != null ? new Material(bodyMaterial) : null;
        if (mat != null)
        {
            Color[] paint = { new Color(0.25f, 0.05f, 0.05f), new Color(0.1f, 0.12f, 0.2f), new Color(0.3f, 0.3f, 0.3f), new Color(0.12f, 0.18f, 0.12f), new Color(0.35f, 0.32f, 0.25f) };
            mat.SetColor("_MainColor", paint[Random.Range(0, paint.Length)]);
        }
        MeshKit.Spawn("Body", root, MeshKit.Box(new Vector3(1.75f, 0.65f, 4.2f), 1f), mat, new Vector3(0f, 0.3f, 0f), Quaternion.identity, false);
        MeshKit.Spawn("Cabin", root, MeshKit.Box(new Vector3(1.55f, 0.55f, 2.1f), 1f), mat, new Vector3(0f, 0.95f, -0.25f), Quaternion.identity, false);
        return new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(1.75f, 1.5f, 4.2f));
    }

    void AddLights(Transform root, Bounds b)
    {
        float front = b.max.z, back = b.min.z;
        float y = b.min.y + b.size.y * 0.33f;
        float x = b.extents.x * 0.7f;
        foreach (float side in new[] { -1f, 1f })
        {
            if (glowMaterial != null)
            {
                MeshKit.Spawn("Headlight", root, MeshKit.Box(new Vector3(0.28f, 0.12f, 0.03f), 0.3f), glowMaterial,
                    new Vector3(side * x, y, front + 0.01f), Quaternion.identity, false);
            }
            if (tailMaterial != null)
            {
                MeshKit.Spawn("Tail Light", root, MeshKit.Box(new Vector3(0.25f, 0.1f, 0.03f), 0.3f), tailMaterial,
                    new Vector3(side * x, y + 0.08f, back - 0.02f), Quaternion.identity, false);
            }
            var l = new GameObject("Headlight Beam").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(side * x, y, front + 0.1f);
            l.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            l.type = LightType.Spot;
            l.spotAngle = 65f;
            l.range = headlightRange;
            l.intensity = headlightIntensity;
            l.color = new Color(1f, 0.93f, 0.8f);
            l.shadows = LightShadows.None;
        }
    }
}
