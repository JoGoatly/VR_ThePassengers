using UnityEngine;

/// <summary>
/// Small CPU-drawn image (origin top-left) uploaded to a point-filtered texture:
/// used for the in-game terminal screen and the ID card.
/// </summary>
public class PixelCanvas
{
    public readonly int Width, Height;
    public readonly Texture2D Texture;
    readonly Color32[] pixels;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        pixels = new Color32[width * height];
        Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "PixelCanvas",
        };
    }

    public void Apply()
    {
        Texture.SetPixels32(pixels);
        Texture.Apply(false);
    }

    public void Clear(Color32 c)
    {
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
    }

    public void Pixel(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        pixels[(Height - 1 - y) * Width + x] = c;
    }

    public void Fill(int x, int y, int w, int h, Color32 c)
    {
        int x0 = Mathf.Max(0, x), y0 = Mathf.Max(0, y);
        int x1 = Mathf.Min(Width, x + w), y1 = Mathf.Min(Height, y + h);
        for (int yy = y0; yy < y1; yy++)
        {
            int row = (Height - 1 - yy) * Width;
            for (int xx = x0; xx < x1; xx++) pixels[row + xx] = c;
        }
    }

    public void Frame(int x, int y, int w, int h, Color32 c)
    {
        Fill(x, y, w, 1, c);
        Fill(x, y + h - 1, w, 1, c);
        Fill(x, y, 1, h, c);
        Fill(x + w - 1, y, 1, h, c);
    }

    public void Line(int x0, int y0, int x1, int y1, Color32 c)
    {
        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            Pixel(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Draws text (no wrapping); returns the width in pixels.</summary>
    public int Text(int x, int y, string text, Color32 c, int maxChars = int.MaxValue)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int n = Mathf.Min(text.Length, maxChars);
        for (int i = 0; i < n; i++)
        {
            char ch = text[i];
            if (ch == ' ') continue;
            int gx = x + i * PixelFont.CellWidth;
            for (int row = 0; row < PixelFont.CellHeight; row++)
            {
                int bits = PixelFont.Row(ch, row);
                if (bits == 0) continue;
                for (int col = 0; col < PixelFont.CellWidth; col++)
                    if ((bits & (1 << col)) != 0) Pixel(gx + col, y + row, c);
            }
        }
        return n * PixelFont.CellWidth;
    }

    /// <summary>Word-wrapped text; returns the number of lines drawn (skipping 'skipLines').</summary>
    public int WrappedText(int x, int y, int maxWidth, int maxLines, string text, Color32 c, int skipLines = 0)
    {
        int cols = Mathf.Max(1, maxWidth / PixelFont.CellWidth);
        int line = 0, drawn = 0;
        foreach (var paragraph in (text ?? "").Split('\n'))
        {
            var words = paragraph.Split(' ');
            string current = "";
            foreach (var word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (candidate.Length > cols && current.Length > 0)
                {
                    DrawLine(current);
                    current = word;
                }
                else current = candidate;
                while (current.Length > cols) { DrawLine(current.Substring(0, cols)); current = current.Substring(cols); }
            }
            DrawLine(current);
        }
        return line;

        void DrawLine(string s)
        {
            if (line >= skipLines && drawn < maxLines)
            {
                Text(x, y + drawn * (PixelFont.CellHeight + 1), s, c);
                drawn++;
            }
            line++;
        }
    }

    public static int TextWidth(string s) => (s?.Length ?? 0) * PixelFont.CellWidth;
}
