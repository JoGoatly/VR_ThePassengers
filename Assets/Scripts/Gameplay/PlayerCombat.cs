using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Health and weapons of the player on foot. 1 = hands, 2 = baseball bat, 3 = pistol
/// (once bought), left click attacks, H uses a first aid kit. Dying out there means waking
/// up in the driver's seat - a quarter of the money is gone.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public static PlayerCombat Instance { get; private set; }

    public PlayerOnFoot onFoot;
    public SoundManager sound;
    public Material batMaterial, gunMaterial;
    public AudioClip gunshot, swing, hit, hurt, emptyClick;

    public int maxHealth = 100;
    public int batDamage = 1, pistolDamage = 2;

    public int Health { get; private set; }

    /// <summary>0 = hands, 1 = bat, 2 = pistol.</summary>
    public int CurrentWeapon => (int)weapon;

    /// <summary>Something is after the player (fists up).</summary>
    public bool InDanger => Dweller.Chasers > 0 || Time.time < lastHurtAt + 6f;
    float lastHurtAt = -100f;
    public int fistDamage = 1;
    bool pendingPunch;
    float punchTime;

    enum Weapon { Hands, Bat, Pistol }
    Weapon weapon = Weapon.Hands;
    Transform viewRoot, batModel, gunModel;
    Light muzzle;
    float cooldown, swingTime = -1f, recoil, hurtFlash, deathAt = -1f;
    bool pendingBatHit;
    static float fadeUntil, fadeLength = 1f;

    void Awake() => Instance = this;

    void Start()
    {
        if (onFoot == null) onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (sound == null) sound = FindAnyObjectByType<SoundManager>();
        Health = maxHealth;
    }

    /// <summary>Short black fade, e.g. when climbing through a trapdoor.</summary>
    public static void Fade(float seconds)
    {
        fadeLength = seconds;
        fadeUntil = Time.time + seconds;
    }

    public void Damage(int amount)
    {
        if (Health <= 0 || !GameUI.PlayerOutside) return;
        Health = Mathf.Max(0, Health - amount);
        hurtFlash = 1f;
        lastHurtAt = Time.time;
        Play(hurt, 1f);
        if (Health <= 0) deathAt = Time.time + 1.6f;
    }

    void Play(AudioClip clip, float volume)
    {
        if (sound != null && clip != null && Camera.main != null) sound.PlayWorld(clip, Camera.main.transform.position, volume, 0f);
    }

    void Update()
    {
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.deltaTime * 1.5f);
        bool outside = GameUI.PlayerOutside && onFoot != null && onFoot.Walker != null;
        EnsureViewModels(outside);
        if (!outside)
        {
            if (Health < maxHealth && deathAt < 0f) Health = Mathf.Min(maxHealth, Health + Mathf.CeilToInt(Time.deltaTime * 5f));
            return;
        }

        if (deathAt > 0f)
        {
            if (Time.time >= deathAt) WakeUpInBus();
            return;
        }
        if (GameUI.MenuOpen || GameUI.NoteOpen) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) weapon = Weapon.Hands;
            if (kb.digit2Key.wasPressedThisFrame && Progress.Owns("bat")) weapon = Weapon.Bat;
            if (kb.digit3Key.wasPressedThisFrame && Progress.Owns("pistol")) weapon = Weapon.Pistol;
        }
        if (GameKeys.Pressed(GameAction.Heal) && Progress.Data.medkits > 0 && Health < maxHealth)
        {
            Progress.AddMedkits(-1);
            Health = Mathf.Min(maxHealth, Health + 50);
            Progress.Save();
        }

        cooldown -= Time.deltaTime;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && cooldown <= 0f && !GameUI.PhoneOpen) Attack();

        // Bat swing hits a moment after the click.
        if (pendingBatHit && Time.time - swingTime > 0.13f)
        {
            pendingBatHit = false;
            var cam = Camera.main.transform;
            foreach (var c in Physics.OverlapSphere(cam.position + cam.forward * 1.3f, 0.9f))
            {
                var d = c.GetComponentInParent<Dweller>();
                if (d == null) continue;
                d.TakeHit(batDamage, cam.forward);
                Play(hit, 1f);
                break;
            }
        }
        // A punch lands a moment after the click.
        if (pendingPunch && Time.time - punchTime > 0.16f)
        {
            pendingPunch = false;
            var cam = Camera.main.transform;
            foreach (var c in Physics.OverlapSphere(cam.position + cam.forward * 1.0f, 0.7f))
            {
                var d = c.GetComponentInParent<Dweller>();
                if (d == null) continue;
                d.TakeHit(fistDamage, cam.forward);
                Play(hit, 0.8f);
                break;
            }
        }
        AnimateViewModels();
    }

    void Attack()
    {
        var cam = Camera.main.transform;
        switch (weapon)
        {
            case Weapon.Bat:
                cooldown = 0.65f;
                swingTime = Time.time;
                pendingBatHit = true;
                Play(swing, 0.8f);
                if (Arms != null) Arms.Swing();
                break;
            case Weapon.Pistol:
                cooldown = 0.35f;
                if (Progress.Data.ammo <= 0) { Play(emptyClick, 0.8f); break; }
                Progress.AddAmmo(-1);
                recoil = 1f;
                Play(gunshot, 1f);
                if (muzzle != null) muzzle.enabled = true;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit h, 60f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var d = h.collider.GetComponentInParent<Dweller>();
                    if (d != null) d.TakeHit(pistolDamage, cam.forward);
                }
                break;
            default:
                // Fists.
                cooldown = 0.45f;
                pendingPunch = true;
                punchTime = Time.time;
                Play(swing, 0.5f);
                if (Arms != null) Arms.Punch();
                break;
        }
    }

    void WakeUpInBus()
    {
        deathAt = -1f;
        int lost = Progress.Money / 4;
        Progress.AddMoney(-lost);
        Progress.Save();
        Health = maxHealth;
        Fade(2.5f);
        onFoot.ForceEnter();
        var game = FindAnyObjectByType<BoardingManager>();
        game?.ShowToast(Loc.T($"Sie wachen im Bus auf. {lost} € fehlen.", $"You wake up in the bus. {lost} € are missing."));
    }

    // ---------------------------------------------------------------- first person weapons

    void EnsureViewModels(bool outside)
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (!outside || cam == null)
        {
            if (viewRoot != null) Destroy(viewRoot.gameObject);
            viewRoot = null;
            return;
        }
        if (viewRoot != null && viewRoot.parent == cam) return;
        if (viewRoot != null) Destroy(viewRoot.gameObject);

        viewRoot = new GameObject("View Models").transform;
        viewRoot.SetParent(cam, false);

        batModel = new GameObject("Bat").transform;
        batModel.SetParent(viewRoot, false);
        batModel.localPosition = new Vector3(0.32f, -0.3f, 0.45f);
        batModel.localRotation = Quaternion.Euler(-35f, -10f, -20f);
        var bat = MeshKit.Spawn("Bat Mesh", batModel, MeshKit.Prism(0.035f, 0.8f, 6, 1f), batMaterial, batModel.position, batModel.rotation, false);
        bat.transform.localPosition = Vector3.zero;
        bat.transform.localRotation = Quaternion.identity;

        gunModel = new GameObject("Pistol").transform;
        gunModel.SetParent(viewRoot, false);
        gunModel.localPosition = new Vector3(0.22f, -0.2f, 0.42f);
        var slide = MeshKit.Spawn("Slide", gunModel, MeshKit.Box(new Vector3(0.04f, 0.045f, 0.2f), 0.2f), gunMaterial, gunModel.position, gunModel.rotation, false);
        slide.transform.localPosition = Vector3.zero;
        var grip = MeshKit.Spawn("Grip", gunModel, MeshKit.Box(new Vector3(0.035f, 0.11f, 0.05f), 0.2f), gunMaterial, gunModel.position, gunModel.rotation, false);
        grip.transform.localPosition = new Vector3(0f, -0.08f, -0.06f);
        grip.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        muzzle = new GameObject("Muzzle Flash").AddComponent<Light>();
        muzzle.transform.SetParent(gunModel, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
        muzzle.type = LightType.Point;
        muzzle.range = 9f;
        muzzle.intensity = 6f;
        muzzle.color = new Color(1f, 0.8f, 0.45f);
        muzzle.enabled = false;
    }

    FirstPersonArms Arms => FirstPersonArms.Instance != null && FirstPersonArms.Instance.Ready ? FirstPersonArms.Instance : null;

    void AnimateViewModels()
    {
        if (viewRoot == null) return;
        // The arms were rebuilt (and took the weapons with them): build the weapons again.
        if (batModel == null || gunModel == null)
        {
            Destroy(viewRoot.gameObject);
            viewRoot = null;
            return;
        }
        batModel.gameObject.SetActive(weapon == Weapon.Bat);
        gunModel.gameObject.SetActive(weapon == Weapon.Pistol);

        // With the arms: bat and pistol sit in the right hand and move with the animations.
        var arms = Arms;
        if (arms != null)
        {
            if (batModel.parent != arms.RightGrip)
            {
                batModel.SetParent(arms.RightGrip, false);
                batModel.localPosition = new Vector3(0f, 0.02f, 0f);
                batModel.localRotation = Quaternion.Euler(25f, 0f, 0f);   // bat pointing up, a bit forward
                gunModel.SetParent(arms.RightGrip, false);
                gunModel.localRotation = Quaternion.identity;
            }
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 6f);
            gunModel.localPosition = new Vector3(0f, 0.03f + recoil * 0.015f, 0.02f - recoil * 0.04f);
            gunModel.localRotation = Quaternion.Euler(-recoil * 14f, 0f, 0f);
            if (muzzle != null && recoil < 0.75f) muzzle.enabled = false;
            return;
        }

        float s = Time.time - swingTime;
        float swingAngle = s >= 0f && s < 0.35f ? Mathf.Sin(s / 0.35f * Mathf.PI) * 80f : 0f;
        batModel.localRotation = Quaternion.Euler(-35f + swingAngle * 0.6f, -10f - swingAngle, -20f + swingAngle * 0.3f);

        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 6f);
        gunModel.localPosition = new Vector3(0.22f, -0.2f + recoil * 0.02f, 0.42f - recoil * 0.05f);
        gunModel.localRotation = Quaternion.Euler(-recoil * 12f, 0f, 0f);
        if (muzzle != null && recoil < 0.75f) muzzle.enabled = false;
    }

    // ---------------------------------------------------------------- HUD

    void OnGUI()
    {
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        if (Time.time < fadeUntil)
        {
            GUI.depth = -800;
            float left = (fadeUntil - Time.time) / fadeLength;
            float a = left > 0.5f ? 1f : left * 2f;
            RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, a));
        }
        if (!GameUI.PlayerOutside || GameUI.MenuOpen) return;

        if (hurtFlash > 0f) RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0.6f, 0f, 0f, hurtFlash * 0.45f));
        if (deathAt > 0f)
        {
            RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, Mathf.Clamp01(1f - (deathAt - Time.time) / 1.6f)));
            return;
        }

        // Health bar and weapon.
        var bar = new Rect(10, h - 26, 90, 6);
        RetroGUI.Fill(bar, new Color(0.15f, 0.02f, 0.02f, 0.8f));
        RetroGUI.Fill(new Rect(bar.x, bar.y, bar.width * Health / maxHealth, bar.height), new Color(0.7f, 0.08f, 0.05f));
        string info = weapon switch
        {
            Weapon.Bat => Loc.T("Schläger", "Bat"),
            Weapon.Pistol => Loc.T($"Pistole  {Progress.Data.ammo} Schuss", $"Pistol  {Progress.Data.ammo} rounds"),
            _ => Loc.T("Hände", "Hands"),
        };
        if (Progress.Data.medkits > 0) info += Loc.T($"   Verband x{Progress.Data.medkits} ", $"   Kit x{Progress.Data.medkits} ") + GameKeys.Tag(GameAction.Heal);
        RetroGUI.ShadowLabel(new Rect(10, h - 42, 300, 14), info, new Color(0.85f, 0.8f, 0.7f), false, TextAnchor.UpperLeft);
        string keys = "[1] " + Loc.T("Hände", "Hands") + (Progress.Owns("bat") ? "  [2] " + Loc.T("Schläger", "Bat") : "") +
                      (Progress.Owns("pistol") ? "  [3] " + Loc.T("Pistole", "Pistol") : "");
        RetroGUI.ShadowLabel(new Rect(10, h - 16, 300, 12), keys, new Color(0.55f, 0.55f, 0.55f), false, TextAnchor.UpperLeft);
    }
}
