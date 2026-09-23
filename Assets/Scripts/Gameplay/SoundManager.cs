using UnityEngine;

/// <summary>
/// All game sounds: electric bus motor hum and road noise (silent when standing), doors, brake hiss, light switch,
/// terminal beeps, mails, footsteps, forest ambience with a dark drone, and now and then
/// an owl or a cracking branch somewhere in the dark.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    public BusController bus;
    public BoardingManager game;
    public ComputerTerminal terminal;
    public ForestWatchers watchers;

    [Header("Clips")]
    public AudioClip engineLoop;
    public AudioClip doorOpen, doorClose, brakeHiss, lightSwitch;
    public AudioClip terminalClick, terminalKey, terminalError, mail, decisionOk, decisionReject;
    public AudioClip[] steps;
    public AudioClip forestAmbience, drone;
    public AudioClip[] forestNoises;
    public AudioClip vanish;
    public AudioClip roadNoise;
    public AudioClip[] maleVoices, femaleVoices;
    public AudioClip scare;
    public AudioClip knock;

    [Header("Mix")]
    [Range(0f, 1f)] public float engineVolume = 0.35f;
    [Range(0f, 1f)] public float roadVolume = 0.35f;
    [Range(0f, 1f)] public float ambienceVolume = 0.12f;
    [Tooltip("Muffles the wind outside the bus (Hz)")]
    public float ambienceLowPass = 900f;
    [Range(0f, 1f)] public float droneVolume = 0.12f;
    [Range(0f, 1f)] public float uiVolume = 0.25f;
    public Vector2 forestNoiseInterval = new Vector2(12f, 40f);

    AudioSource engine, road, ambience, droneSource, ui;
    float lastSpeed;
    float nextForestNoise;

    void Start()
    {
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (terminal == null) terminal = FindAnyObjectByType<ComputerTerminal>();
        if (watchers == null) watchers = FindAnyObjectByType<ForestWatchers>();

        ui = CreateSource("UI", transform, false, uiVolume);
        ambience = CreateSource("Ambience", transform, true, ambienceVolume, forestAmbience);
        droneSource = CreateSource("Drone", transform, true, droneVolume, drone);
        ambience.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = ambienceLowPass;

        if (bus != null)
        {
            engine = CreateSource("Engine", bus.transform, true, engineVolume, engineLoop);
            engine.transform.localPosition = new Vector3(0f, 1f, -4.5f);
            engine.spatialBlend = 0.3f;
            engine.volume = 0f;
            road = CreateSource("Road", bus.transform, true, 0f, roadNoise);
            bus.DoorsChanged += open => PlayAt(open ? doorOpen : doorClose, bus.transform.TransformPoint(new Vector3(1f, 1.5f, 4.3f)), 0.9f);
            var lights = bus.GetComponent<BusLights>();
            if (lights != null) lights.Switched += _ => ui.PlayOneShot(lightSwitch, 0.8f);
        }
        if (terminal != null)
        {
            terminal.Clicked += () => ui.PlayOneShot(terminalClick, 0.5f);
            terminal.Typed += () => ui.PlayOneShot(terminalKey, 0.4f);
            terminal.ErrorBeep += () => ui.PlayOneShot(terminalError, 0.6f);
        }
        if (game != null)
        {
            game.Mail.Received += _ => ui.PlayOneShot(mail, 0.5f);
            game.Decided += letIn => ui.PlayOneShot(letIn ? decisionOk : decisionReject, 0.7f);
            game.PassengerVanished += pos => PlayAt(vanish, pos, 1f, 0.2f);
        }
        if (watchers != null) watchers.Vanished += pos => PlayAt(vanish, pos, 0.8f, 0.6f);
        var dialogue = FindAnyObjectByType<DialogueView>();
        if (dialogue != null) dialogue.Spoke += PlayVoice;
        Passenger.StepTaken += OnStep;

        nextForestNoise = Time.time + Random.Range(forestNoiseInterval.x, forestNoiseInterval.y);
    }

    void OnDestroy() => Passenger.StepTaken -= OnStep;

    // Male / female murmur, and every person has a slightly different pitch.
    void PlayVoice(IdCard card)
    {
        var pool = card.Gender == Gender.Male ? maleVoices : femaleVoices;
        if (pool == null || pool.Length == 0) return;
        int hash = Mathf.Abs(card.FullName.GetHashCode());
        var clip = pool[hash % pool.Length];
        float pitch = 0.9f + (hash / 7 % 20) / 100f;
        if (card.Truth == Discrepancy.Doppelganger) pitch *= 0.93f;   // a little off
        var go = new GameObject("Voice");
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.pitch = pitch;
        s.volume = uiVolume * 2.4f * GameSettings.Effects;
        s.spatialBlend = 0f;
        s.Play();
        Destroy(go, clip.length / pitch + 0.1f);
    }

    /// <summary>Loud hit for jump scares.</summary>
    public void PlayScare()
    {
        if (scare != null) ui.PlayOneShot(scare, 3.5f);
    }

    /// <summary>Any clip at a place in the world.</summary>
    public void PlayWorld(AudioClip clip, Vector3 position, float volume = 1f, float spatial = 1f) => PlayAt(clip, position, volume, spatial);

    /// <summary>Knocking on a window at the given position.</summary>
    public void PlayKnock(Vector3 position) => PlayAt(knock, position, 1f, 0.85f);

    AudioSource CreateSource(string name, Transform parent, bool loop, float volume, AudioClip clip = null)
    {
        var go = new GameObject("Sound " + name);
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<AudioSource>();
        s.loop = loop;
        s.volume = volume;
        s.spatialBlend = 0f;
        s.playOnAwake = false;
        s.clip = clip;
        if (loop && clip != null) s.Play();
        return s;
    }

    void PlayAt(AudioClip clip, Vector3 position, float volume, float spatial = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("OneShot " + clip.name);
        go.transform.position = position;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume * GameSettings.Effects;
        s.spatialBlend = spatial;
        s.minDistance = 2f;
        s.maxDistance = 60f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.pitch = Random.Range(0.94f, 1.06f);
        s.Play();
        Destroy(go, clip.length / s.pitch + 0.1f);
    }

    void OnStep(Vector3 pos)
    {
        if (steps == null || steps.Length == 0) return;
        PlayAt(steps[Random.Range(0, steps.Length)], pos, 0.5f);
    }

    void Update()
    {
        float fx = GameSettings.Effects;
        if (ui != null) ui.volume = uiVolume * fx;
        if (ambience != null) ambience.volume = ambienceVolume * fx;
        if (droneSource != null) droneSource.volume = droneVolume * fx;

        if (bus != null && engine != null)
        {
            float speed01 = Mathf.Clamp01(Mathf.Abs(bus.Speed) / (bus.maxSpeedKmh / 3.6f));
            // Electric motor: pitch rises with speed, silent when standing still.
            float moving = Mathf.Clamp01(speed01 * 6f);
            engine.pitch = Mathf.Lerp(engine.pitch, 0.6f + speed01 * 1.1f, Time.deltaTime * 4f);
            float targetVolume = GameSettings.Effects * engineVolume * moving * (0.65f + 0.35f * bus.ThrottleInput);
            engine.volume = Mathf.Lerp(engine.volume, targetVolume, Time.deltaTime * 4f);
            if (road != null)
            {
                road.volume = Mathf.Lerp(road.volume, GameSettings.Effects * roadVolume * speed01, Time.deltaTime * 3f);
                road.pitch = 0.8f + speed01 * 0.4f;
            }

            // Air brake hiss when the bus comes to a stop.
            if (lastSpeed > 2.5f && Mathf.Abs(bus.Speed) < 0.3f) PlayAt(brakeHiss, bus.transform.position, 0.7f, 0.3f);
            if (Mathf.Abs(bus.Speed) > 2.5f || Mathf.Abs(bus.Speed) < 0.3f) lastSpeed = Mathf.Abs(bus.Speed);
        }

        if (Time.time >= nextForestNoise && forestNoises != null && forestNoises.Length > 0 && bus != null)
        {
            nextForestNoise = Time.time + Random.Range(forestNoiseInterval.x, forestNoiseInterval.y);
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(18f, 40f);
            PlayAt(forestNoises[Random.Range(0, forestNoises.Length)], bus.transform.position + new Vector3(dir.x, 2f, dir.y), 0.35f);
        }
    }
}
