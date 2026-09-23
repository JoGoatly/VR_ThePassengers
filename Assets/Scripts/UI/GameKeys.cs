using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Everything the player can press, rebindable in the menu.</summary>
public enum GameAction
{
    Forward, Backward, SteerLeft, SteerRight, Handbrake,
    Doors, Lights, Interact, Talk, Continue, LetIn, TurnAway, CenterView, Radio, Heal, Phone,
}

/// <summary>
/// Key bindings (saved in PlayerPrefs). Scripts ask GameKeys instead of fixed keys, and
/// texts show the current key with GameKeys.Tag(action), e.g. "[F]".
/// </summary>
public static class GameKeys
{
    static readonly Dictionary<GameAction, Key> Defaults = new Dictionary<GameAction, Key>
    {
        { GameAction.Forward, Key.W }, { GameAction.Backward, Key.S },
        { GameAction.SteerLeft, Key.A }, { GameAction.SteerRight, Key.D },
        { GameAction.Handbrake, Key.Space }, { GameAction.Doors, Key.F },
        { GameAction.Lights, Key.L }, { GameAction.Interact, Key.E },
        { GameAction.Talk, Key.T }, { GameAction.Continue, Key.F },
        { GameAction.LetIn, Key.J }, { GameAction.TurnAway, Key.N },
        { GameAction.CenterView, Key.V }, { GameAction.Radio, Key.R }, { GameAction.Heal, Key.H }, { GameAction.Phone, Key.Q },
    };

    static Dictionary<GameAction, Key> keys;

    static Dictionary<GameAction, Key> Keys
    {
        get
        {
            if (keys != null) return keys;
            keys = new Dictionary<GameAction, Key>();
            foreach (var pair in Defaults)
                keys[pair.Key] = (Key)PlayerPrefs.GetInt("key_" + pair.Key, (int)pair.Value);
            return keys;
        }
    }

    public static IEnumerable<GameAction> All => Defaults.Keys;

    public static Key Get(GameAction a) => Keys[a];

    public static void Set(GameAction a, Key key)
    {
        Keys[a] = key;
        PlayerPrefs.SetInt("key_" + a, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        foreach (var pair in Defaults) Set(pair.Key, pair.Value);
    }

    static KeyControl Control(GameAction a)
    {
        var kb = Keyboard.current;
        var key = Get(a);
        return kb != null && key != Key.None ? kb[key] : null;
    }

    public static bool Held(GameAction a) => Control(a)?.isPressed ?? false;
    public static bool Pressed(GameAction a) => Control(a)?.wasPressedThisFrame ?? false;

    /// <summary>Key name as shown to the player, e.g. "F".</summary>
    public static string Name(GameAction a)
    {
        var c = Control(a);
        string n = c != null ? c.displayName : Get(a).ToString();
        return string.IsNullOrEmpty(n) ? Get(a).ToString() : n.ToUpperInvariant();
    }

    /// <summary>"[F]"</summary>
    public static string Tag(GameAction a) => "[" + Name(a) + "]";

    public static string Label(GameAction a) => a switch
    {
        GameAction.Forward => Loc.T("Gas / Vorwärts", "Throttle / forward"),
        GameAction.Backward => Loc.T("Bremse / Rückwärts", "Brake / reverse"),
        GameAction.SteerLeft => Loc.T("Links lenken", "Steer left"),
        GameAction.SteerRight => Loc.T("Rechts lenken", "Steer right"),
        GameAction.Handbrake => Loc.T("Handbremse", "Handbrake"),
        GameAction.Doors => Loc.T("Türen", "Doors"),
        GameAction.Lights => Loc.T("Licht / Taschenlampe", "Lights / flashlight"),
        GameAction.Interact => Loc.T("Ausweis / Aus- und Einsteigen", "ID card / get out and in"),
        GameAction.Talk => Loc.T("Ansprechen", "Talk"),
        GameAction.Continue => Loc.T("Dialog weiter", "Next dialogue line"),
        GameAction.LetIn => Loc.T("Einlassen", "Let in"),
        GameAction.TurnAway => Loc.T("Abweisen", "Turn away"),
        GameAction.CenterView => Loc.T("Blick geradeaus", "Look straight ahead"),
        GameAction.Radio => Loc.T("Radio (Sender wechseln)", "Radio (next station)"),
        GameAction.Heal => Loc.T("Verbandskasten benutzen", "Use first aid kit"),
        GameAction.Phone => Loc.T("Handy", "Phone"),
        _ => a.ToString(),
    };
}

/// <summary>Volume settings (saved in PlayerPrefs).</summary>
public static class GameSettings
{
    static float? master, music, effects;

    public static float Master
    {
        get => master ??= PlayerPrefs.GetFloat("vol_master", 0.9f);
        set { master = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_master", master.Value); AudioListener.volume = master.Value; }
    }

    /// <summary>Menu music and radio.</summary>
    public static float Music
    {
        get => music ??= PlayerPrefs.GetFloat("vol_music", 0.7f);
        set { music = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_music", music.Value); }
    }

    /// <summary>Everything else: motor, doors, voices, ambience.</summary>
    public static float Effects
    {
        get => effects ??= PlayerPrefs.GetFloat("vol_effects", 1f);
        set { effects = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_effects", effects.Value); }
    }

    public static void Apply() => AudioListener.volume = Master;
}
