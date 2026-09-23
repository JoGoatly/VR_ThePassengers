using UnityEngine;

/// <summary>
/// Calling 110 on the phone: a patrol car with blue lights comes up behind the bus and
/// stops. A wanted passenger is taken away (bonus); anyone else means a false alarm (fine).
/// Then the car overtakes the bus and disappears into the night.
/// </summary>
public class PoliceDispatch : MonoBehaviour
{
    public BoardingManager game;
    public BusController bus;
    public ForestRoad road;
    public SoundManager sound;
    public Material bodyMaterial;
    [Tooltip("Unlit glow material (blue lights, headlights)")]
    public Material glowMaterial;
    public AudioClip siren, carDoor;

    public int arrestBonus = 60;
    public int falseAlarmFine = 30;
    public float approachSpeed = 18f;

    enum State { None, Approaching, Parked, Leaving }
    State state = State.None;
    GameObject car;
    AudioSource sirenSource;
    Light blueA, blueB;
    GameObject glowA, glowB;
    float s, speed, lateral, parkedAt;
    bool handled;

    public bool EnRoute => state != State.None;

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (sound == null) sound = FindAnyObjectByType<SoundManager>();
    }

    /// <summary>The emergency call. Returns what the police say.</summary>
    public string Call()
    {
        if (EnRoute) return Loc.T("\"Ein Streifenwagen ist bereits unterwegs.\"", "\"A patrol car is already on its way.\"");
        if (game == null || game.PendingCard == null)
            return Loc.T("\"Notruf, was ist passiert? ... Ohne Grund können wir keinen Wagen schicken.\"",
                         "\"Emergency, what happened? ... We can't send a car without a reason.\"");
        game.PoliceCalled();
        Spawn();
        return Loc.T("\"Wir schicken sofort einen Wagen. Halten Sie die Person an der Tür fest.\"",
                     "\"We're sending a car right away. Keep the person at the door.\"");
    }

    void Spawn()
    {
        float busS = road.BusArcLength;
        s = busS - 110f;
        speed = approachSpeed;
        lateral = road.laneWidth * 0.5f;
        handled = false;

        car = new GameObject("Police Car");
        var green = bodyMaterial != null ? new Material(bodyMaterial) : null;
        var white = bodyMaterial != null ? new Material(bodyMaterial) : null;
        if (green != null) green.SetColor("_MainColor", new Color(0.12f, 0.32f, 0.2f));
        if (white != null) white.SetColor("_MainColor", new Color(0.85f, 0.85f, 0.82f));
        Part("Body", new Vector3(1.8f, 0.7f, 4.4f), new Vector3(0f, 0.3f, 0f), green);
        Part("Cabin", new Vector3(1.6f, 0.55f, 2.2f), new Vector3(0f, 1.0f, -0.3f), white);
        Part("Light Bar", new Vector3(1.1f, 0.12f, 0.3f), new Vector3(0f, 1.55f, -0.2f), white);

        Material blue = glowMaterial != null ? new Material(glowMaterial) : null;
        if (blue != null)
        {
            blue.SetColor("_MainColor", new Color(0.2f, 0.4f, 1f));
            if (blue.HasProperty("_EmissionColor")) blue.SetColor("_EmissionColor", new Color(0.2f, 0.4f, 1f));
        }
        glowA = Part("Blue L", new Vector3(0.35f, 0.14f, 0.28f), new Vector3(-0.35f, 1.67f, -0.2f), blue);
        glowB = Part("Blue R", new Vector3(0.35f, 0.14f, 0.28f), new Vector3(0.35f, 1.67f, -0.2f), blue);
        blueA = BlueLight(new Vector3(-0.4f, 1.9f, -0.2f));
        blueB = BlueLight(new Vector3(0.4f, 1.9f, -0.2f));
        foreach (float side in new[] { -0.6f, 0.6f })
        {
            Part("Headlight", new Vector3(0.28f, 0.12f, 0.03f), new Vector3(side, 0.55f, 2.2f), glowMaterial);
            var l = new GameObject("Beam").AddComponent<Light>();
            l.transform.SetParent(car.transform, false);
            l.transform.localPosition = new Vector3(side, 0.55f, 2.3f);
            l.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            l.type = LightType.Spot;
            l.spotAngle = 65f;
            l.range = 28f;
            l.intensity = 12f;
        }
        var col = car.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.75f, 0f);
        col.size = new Vector3(1.8f, 1.5f, 4.4f);
        car.AddComponent<Rigidbody>().isKinematic = true;

        if (siren != null)
        {
            sirenSource = car.AddComponent<AudioSource>();
            sirenSource.clip = siren;
            sirenSource.loop = true;
            sirenSource.spatialBlend = 1f;
            sirenSource.minDistance = 6f;
            sirenSource.maxDistance = 220f;
            sirenSource.rolloffMode = AudioRolloffMode.Linear;
            sirenSource.volume = 0.8f * GameSettings.Effects;
            sirenSource.Play();
        }
        state = State.Approaching;
        Place();
    }

    GameObject Part(string name, Vector3 size, Vector3 pos, Material mat)
    {
        var go = MeshKit.Spawn(name, car.transform, MeshKit.Box(size, 1f), mat, car.transform.position, car.transform.rotation, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    Light BlueLight(Vector3 pos)
    {
        var l = new GameObject("Blue Light").AddComponent<Light>();
        l.transform.SetParent(car.transform, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point;
        l.color = new Color(0.2f, 0.35f, 1f);
        l.range = 14f;
        l.intensity = 6f;
        return l;
    }

    void Place()
    {
        if (!road.TrySample(s, out Vector3 p, out Vector3 t)) return;
        Vector3 right = Vector3.Cross(Vector3.up, t).normalized;
        car.transform.SetPositionAndRotation(p + right * lateral, Quaternion.LookRotation(t));
    }

    void Update()
    {
        if (state == State.None) return;
        if (car == null) { state = State.None; return; }
        float busS = road.BusArcLength;

        // Blue lights flash alternately.
        bool a = Mathf.Repeat(Time.time * 2.4f, 1f) < 0.5f;
        blueA.enabled = a; blueB.enabled = !a;
        if (glowA != null) glowA.SetActive(a);
        if (glowB != null) glowB.SetActive(!a);

        switch (state)
        {
            case State.Approaching:
            {
                float target = busS - 13f;
                float gap = target - s;
                speed = Mathf.Min(approachSpeed, Mathf.Max(1.5f, gap * 0.6f));
                s += speed * Time.deltaTime;
                if (gap < 0.5f)
                {
                    state = State.Parked;
                    parkedAt = Time.time;
                    if (sirenSource != null) sirenSource.Stop();
                    if (sound != null && carDoor != null) sound.PlayWorld(carDoor, car.transform.position, 1f);
                }
                break;
            }
            case State.Parked:
                if (!handled && Time.time - parkedAt > 1.5f)
                {
                    handled = true;
                    Vector3 carDoorPos = car.transform.position + car.transform.right * 1.3f;
                    bool arrested = game.PoliceArrived(carDoorPos);
                    if (arrested)
                    {
                        Progress.AddMoney(arrestBonus);
                        Progress.ShiftEarned += arrestBonus;
                        game.ShowToast(Loc.T($"Festnahme! Bonus +{arrestBonus} €", $"Arrest! Bonus +{arrestBonus} €"));
                    }
                    else
                    {
                        Progress.AddMoney(-falseAlarmFine);
                        Progress.ShiftFines += falseAlarmFine;
                        game.ShowToast(Loc.T($"Fehlalarm: -{falseAlarmFine} €", $"False alarm: -{falseAlarmFine} €"));
                    }
                    Progress.Save();
                }
                if (Time.time - parkedAt > 7f)
                {
                    state = State.Leaving;
                    if (sound != null && carDoor != null) sound.PlayWorld(carDoor, car.transform.position, 1f);
                }
                break;
            case State.Leaving:
                // Pull out onto the other lane, overtake the bus and drive off.
                speed = Mathf.MoveTowards(speed, 20f, 4f * Time.deltaTime);
                lateral = Mathf.MoveTowards(lateral, -road.laneWidth * 0.5f, 1.2f * Time.deltaTime);
                s += speed * Time.deltaTime;
                if (s > busS + 160f || !road.TrySample(s, out _, out _))
                {
                    Destroy(car);
                    state = State.None;
                    return;
                }
                break;
        }
        Place();
    }
}
