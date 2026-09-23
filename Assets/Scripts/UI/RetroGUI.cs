using UnityEngine;

/// <summary>Shared UI state.</summary>
public static class GameUI
{
    /// <summary>Typing into the terminal: driving and hotkeys are ignored.</summary>
    public static bool TerminalTyping;
    /// <summary>ID card overlay hidden by the player (E).</summary>
    public static bool IdCardHidden;

    /// <summary>The driver has left the bus and walks around.</summary>
    public static bool PlayerOutside;

    /// <summary>The talk menu (T) is open: mouse cursor is free to pick a question.</summary>
    public static bool DialogueOpen;

    /// <summary>Start menu / intro is shown, the game is paused.</summary>
    public static bool MenuOpen;

    /// <summary>Driving input is ignored (menu, typing, talking or not in the driver's seat).</summary>
    public static bool AnyOpen => MenuOpen || TerminalTyping || PlayerOutside || DialogueOpen;
}

/// <summary>
/// IMGUI helpers for a crisp retro look: layout in a virtual 640x360 screen,
/// scaled to the real resolution (fonts are scaled too, so text stays sharp).
/// </summary>
public static class RetroGUI
{
    public const float VirtualHeight = 360f;

    public static float Scale => Screen.height / VirtualHeight;
    public static float VirtualWidth => Screen.width / Scale;

    public static Rect R(float x, float y, float w, float h)
    {
        float s = Scale;
        return new Rect(x * s, y * s, w * s, h * s);
    }

    static float builtForScale = -1f;
    static GUIStyle label, labelSmall, labelBold, header, button, field, box, wrap;
    static readonly System.Collections.Generic.Dictionary<Color, Texture2D> textures =
        new System.Collections.Generic.Dictionary<Color, Texture2D>();

    public static Texture2D Tex(Color c)
    {
        if (textures.TryGetValue(c, out var t) && t != null) return t;
        t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        textures[c] = t;
        return t;
    }

    public static void Fill(Rect virtualRect, Color c)
    {
        GUI.DrawTexture(R(virtualRect.x, virtualRect.y, virtualRect.width, virtualRect.height), Tex(c));
    }

    public static void Frame(Rect v, Color fill, Color border, float thickness = 1f)
    {
        Fill(v, border);
        Fill(new Rect(v.x + thickness, v.y + thickness, v.width - 2 * thickness, v.height - 2 * thickness), fill);
    }

    static void Build()
    {
        float s = Scale;
        if (Mathf.Approximately(s, builtForScale) && label != null) return;
        builtForScale = s;

        label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(9 * s), wordWrap = false, clipping = TextClipping.Clip };
        label.padding = new RectOffset(0, 0, 0, 0);
        labelSmall = new GUIStyle(label) { fontSize = Mathf.RoundToInt(7.5f * s) };
        labelBold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
        header = new GUIStyle(label) { fontSize = Mathf.RoundToInt(11 * s), fontStyle = FontStyle.Bold };
        wrap = new GUIStyle(label) { wordWrap = true };

        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(8.5f * s), fontStyle = FontStyle.Bold };
        button.normal.background = Tex(new Color(0.16f, 0.3f, 0.2f));
        button.hover.background = Tex(new Color(0.22f, 0.45f, 0.28f));
        button.active.background = Tex(new Color(0.1f, 0.2f, 0.12f));
        button.normal.textColor = button.hover.textColor = button.active.textColor = new Color(0.75f, 1f, 0.8f);

        field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(9 * s) };
        field.normal.background = field.focused.background = field.hover.background = Tex(new Color(0.02f, 0.06f, 0.03f));
        field.normal.textColor = field.focused.textColor = field.hover.textColor = new Color(0.6f, 1f, 0.7f);
        field.padding = new RectOffset(Mathf.RoundToInt(3 * s), 0, Mathf.RoundToInt(2 * s), 0);

        box = new GUIStyle(GUI.skin.box);
    }

    public static void Label(Rect v, string text, Color color, bool bold = false, bool small = false, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Build();
        var st = small ? labelSmall : bold ? labelBold : label;
        st.normal.textColor = st.hover.textColor = color;
        st.alignment = anchor;
        GUI.Label(R(v.x, v.y, v.width, v.height), text, st);
    }

    public static void Header(Rect v, string text, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Build();
        header.normal.textColor = header.hover.textColor = color;
        header.alignment = anchor;
        GUI.Label(R(v.x, v.y, v.width, v.height), text, header);
    }

    public static void Wrapped(Rect v, string text, Color color)
    {
        Build();
        wrap.normal.textColor = wrap.hover.textColor = color;
        GUI.Label(R(v.x, v.y, v.width, v.height), text, wrap);
    }

    public static bool Button(Rect v, string text)
    {
        Build();
        return GUI.Button(R(v.x, v.y, v.width, v.height), text, button);
    }

    public static bool Button(Rect v, string text, Color bg, Color fg)
    {
        Build();
        var st = new GUIStyle(button);
        st.normal.background = Tex(bg);
        st.hover.background = Tex(Color.Lerp(bg, Color.white, 0.15f));
        st.normal.textColor = st.hover.textColor = st.active.textColor = fg;
        return GUI.Button(R(v.x, v.y, v.width, v.height), text, st);
    }

    public static string TextField(Rect v, string text, string controlName)
    {
        Build();
        GUI.SetNextControlName(controlName);
        return GUI.TextField(R(v.x, v.y, v.width, v.height), text ?? "", 40, field);
    }

    /// <summary>Drop shadow text, readable over the 3D view.</summary>
    public static void ShadowLabel(Rect v, string text, Color color, bool bold = true, TextAnchor anchor = TextAnchor.UpperCenter)
    {
        Label(new Rect(v.x + 1, v.y + 1, v.width, v.height), text, Color.black, bold, false, anchor);
        Label(v, text, color, bold, false, anchor);
    }
}
