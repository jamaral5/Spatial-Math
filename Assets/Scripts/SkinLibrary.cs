using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One look for the graph: what covers the surface, and what sits behind it.
/// A null texture means a flat colour.
/// </summary>
public class GraphSkin
{
    public string name;
    public Texture2D texture;
    public Color tint = Color.white;
    public Vector2 tiling = Vector2.one;

    public Color skyTop;
    public Color skyHorizon;
    public Color skyBottom;
}

/// <summary>
/// Builds the preset skins. Every pattern is drawn into a Texture2D in code rather than
/// imported as art, so the whole set travels with the script and costs nothing in the
/// Project window.
///
/// The graph mesh already lays UVs 0..1 across the surface, so a repeating tile assigned
/// to _BaseMap stretches over the shape and follows its folds.
/// </summary>
public static class SkinLibrary
{
    private const int Tile = 256;

    private static List<GraphSkin> presets;

    public static List<GraphSkin> Presets()
    {
        if (presets != null) return presets;

        presets = new List<GraphSkin>
        {
            Flat("Classic",  new Color(0.95f, 0.25f, 0.20f), Night()),
            Flat("Ocean",    new Color(0.15f, 0.70f, 0.85f), Ocean()),
            Flat("Meadow",   new Color(0.40f, 0.85f, 0.35f), Meadow()),
            Flat("Sunset",   new Color(1.00f, 0.55f, 0.20f), Sunset()),

            Patterned("Polka Dots", PolkaDots(), new Vector2(6f, 6f), Night()),
            Patterned("Zig Zag",    ZigZag(),    new Vector2(5f, 5f), Sunset()),
            Patterned("Smileys",    Smileys(),   new Vector2(4f, 4f), Meadow()),
            Patterned("Checkers",   Checkers(),  new Vector2(8f, 8f), Ocean()),
        };

        return presets;
    }

    // ─── Skin construction helpers ─────────────────────────────────────

    private static GraphSkin Flat(string name, Color color, (Color, Color, Color) sky) =>
        new GraphSkin
        {
            name = name,
            texture = null,
            tint = color,
            skyTop = sky.Item1,
            skyHorizon = sky.Item2,
            skyBottom = sky.Item3
        };

    private static GraphSkin Patterned(string name, Texture2D texture, Vector2 tiling,
                                       (Color, Color, Color) sky) =>
        new GraphSkin
        {
            name = name,
            texture = texture,
            tint = Color.white,     // let the pattern's own colours through
            tiling = tiling,
            skyTop = sky.Item1,
            skyHorizon = sky.Item2,
            skyBottom = sky.Item3
        };

    // ─── Sky palettes ──────────────────────────────────────────────────

    private static (Color, Color, Color) Night() => (
        new Color(0.020f, 0.028f, 0.070f),
        new Color(0.070f, 0.120f, 0.210f),
        new Color(0.010f, 0.012f, 0.022f));

    private static (Color, Color, Color) Ocean() => (
        new Color(0.010f, 0.060f, 0.110f),
        new Color(0.030f, 0.230f, 0.310f),
        new Color(0.005f, 0.020f, 0.040f));

    private static (Color, Color, Color) Meadow() => (
        new Color(0.030f, 0.070f, 0.050f),
        new Color(0.120f, 0.240f, 0.140f),
        new Color(0.010f, 0.025f, 0.018f));

    private static (Color, Color, Color) Sunset() => (
        new Color(0.090f, 0.030f, 0.120f),
        new Color(0.420f, 0.140f, 0.180f),
        new Color(0.030f, 0.010f, 0.040f));

    // ─── Pattern textures ──────────────────────────────────────────────

    private static Texture2D PolkaDots()
    {
        var pixels = Filled(new Color(0.98f, 0.96f, 0.92f));
        var dot = new Color(0.90f, 0.25f, 0.45f);

        // Two offset rows of dots, which is what makes the tile read as staggered
        // rather than as an obvious grid once it repeats.
        Disc(pixels, Tile * 0.25f, Tile * 0.25f, Tile * 0.14f, dot);
        Disc(pixels, Tile * 0.75f, Tile * 0.25f, Tile * 0.14f, dot);
        Disc(pixels, Tile * 0.50f, Tile * 0.75f, Tile * 0.14f, dot);
        Disc(pixels, Tile * 0.00f, Tile * 0.75f, Tile * 0.14f, dot);
        Disc(pixels, Tile * 1.00f, Tile * 0.75f, Tile * 0.14f, dot);

        return Build(pixels, "Skin_PolkaDots");
    }

    private static Texture2D ZigZag()
    {
        var pixels = new Color[Tile * Tile];
        var a = new Color(0.99f, 0.80f, 0.25f);
        var b = new Color(0.20f, 0.22f, 0.32f);

        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                // A triangle wave across x, shifted by y, gives chevrons. Banding the
                // result produces alternating stripes that meet in points.
                float wave = Mathf.PingPong(x * 4f / Tile, 1f);
                float band = Mathf.Repeat(y / (float)Tile * 4f + wave, 1f);
                pixels[y * Tile + x] = band < 0.5f ? a : b;
            }
        }

        return Build(pixels, "Skin_ZigZag");
    }

    private static Texture2D Smileys()
    {
        var pixels = Filled(new Color(0.10f, 0.12f, 0.18f));

        // Two faces per tile on opposite diagonals, alternating colour.
        Face(pixels, Tile * 0.28f, Tile * 0.28f, Tile * 0.20f, new Color(1.00f, 0.85f, 0.15f));
        Face(pixels, Tile * 0.72f, Tile * 0.72f, Tile * 0.20f, new Color(0.45f, 0.90f, 0.35f));

        return Build(pixels, "Skin_Smileys");
    }

    private static Texture2D Checkers()
    {
        var pixels = new Color[Tile * Tile];
        var a = new Color(0.95f, 0.95f, 0.97f);
        var b = new Color(0.15f, 0.17f, 0.24f);

        for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
                pixels[y * Tile + x] = ((x / (Tile / 2)) + (y / (Tile / 2))) % 2 == 0 ? a : b;

        return Build(pixels, "Skin_Checkers");
    }

    // ─── Drawing primitives ────────────────────────────────────────────

    private static Color[] Filled(Color color)
    {
        var pixels = new Color[Tile * Tile];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        return pixels;
    }

    /// <summary>Draws a filled circle with a soft one-pixel edge, clipped to the tile.</summary>
    private static void Disc(Color[] pixels, float cx, float cy, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f));
        int maxX = Mathf.Min(Tile - 1, Mathf.CeilToInt(cx + radius + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f));
        int maxY = Mathf.Min(Tile - 1, Mathf.CeilToInt(cy + radius + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float coverage = Mathf.Clamp01(radius - d);
                if (coverage <= 0f) continue;

                int i = y * Tile + x;
                pixels[i] = Color.Lerp(pixels[i], color, coverage);
            }
        }
    }

    /// <summary>A smiley: head, two eyes and an upturned mouth.</summary>
    private static void Face(Color[] pixels, float cx, float cy, float radius, Color skin)
    {
        var features = new Color(0.12f, 0.10f, 0.08f);

        Disc(pixels, cx, cy, radius, skin);
        Disc(pixels, cx - radius * 0.35f, cy + radius * 0.28f, radius * 0.10f, features);
        Disc(pixels, cx + radius * 0.35f, cy + radius * 0.28f, radius * 0.10f, features);

        // The mouth is a row of small dots swept along an arc — cheaper to reason about
        // than rasterising a stroked curve, and it reads correctly at tile size.
        const int steps = 24;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(200f, 340f, i / (float)steps) * Mathf.Deg2Rad;
            float mx = cx + Mathf.Cos(angle) * radius * 0.55f;
            float my = cy + Mathf.Sin(angle) * radius * 0.55f;
            Disc(pixels, mx, my, radius * 0.075f, features);
        }
    }

    private static Texture2D Build(Color[] pixels, string name)
    {
        var texture = new Texture2D(Tile, Tile, TextureFormat.RGBA32, true)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontSave
        };

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }
}
