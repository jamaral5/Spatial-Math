using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shared helpers for the runtime-built UI: rect plumbing, procedurally generated
/// sprites, and the serif font.
///
/// The sprites are generated in code rather than imported as art so there is nothing to
/// keep in sync in the Project window. The glow in particular has to be faked this way:
/// the Canvas is Screen Space - Overlay, which URP draws AFTER post-processing, so a
/// bloom-based neon effect would never reach it.
/// </summary>
public static class UIKit
{
    // ─── Palette ───────────────────────────────────────────────────────
    public static readonly Color Ink        = new Color(0.90f, 0.94f, 1.00f);
    public static readonly Color InkDim     = new Color(0.62f, 0.70f, 0.82f);
    public static readonly Color Neon       = new Color(1.00f, 0.82f, 0.29f);
    public static readonly Color NeonSoft   = new Color(1.00f, 0.82f, 0.29f, 0.55f);
    public static readonly Color PanelDark  = new Color(0.04f, 0.06f, 0.10f, 0.92f);

    // ─── Canvas lookup ─────────────────────────────────────────────────

    /// <summary>
    /// Finds the Canvas that belongs to the SCENE, skipping the throwaway one the start
    /// screen builds for itself.
    ///
    /// This matters: a plain FindFirstObjectByType&lt;Canvas&gt;() can return the start
    /// screen's canvas, and anything parented to it is destroyed when the title screen
    /// is dismissed. Lowest sorting order wins, since overlays sit above the scene UI.
    /// </summary>
    public static Canvas FindSceneCanvas()
    {
        Canvas best = null;

        foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (candidate == StartScreen.OverlayCanvas) continue;
            if (best == null || candidate.sortingOrder < best.sortingOrder) best = candidate;
        }

        return best;
    }

    // ─── Rect plumbing ─────────────────────────────────────────────────

    public static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Makes a rect fill its parent completely.</summary>
    public static RectTransform Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
        return rect;
    }

    /// <summary>Anchors a rect to one corner. Pivot matches, so offset moves it inward.</summary>
    public static RectTransform Corner(RectTransform rect, Vector2 corner, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = corner;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        return rect;
    }

    // ─── Text ──────────────────────────────────────────────────────────

    public static TextMeshProUGUI Label(string objectName, Transform parent, string text,
                                        float size, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
                                        bool serif = true)
    {
        var rect = NewRect(objectName, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (serif)
        {
            TMP_FontAsset font = SerifFont();
            if (font != null) label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    // ─── Fonts ─────────────────────────────────────────────────────────

    private static TMP_FontAsset serifFont;
    private static bool serifAttempted;

    /// <summary>
    /// Times New Roman, built at runtime from the OS-installed font. TMP needs a font
    /// ASSET (an atlas plus glyph tables), not a raw .ttf, and CreateFontAsset builds one
    /// in dynamic mode — glyphs are rasterised on demand as text is assigned.
    ///
    /// Returns null if the font is not installed, in which case callers keep the TMP
    /// default rather than rendering nothing.
    /// </summary>
    public static TMP_FontAsset SerifFont()
    {
        if (serifAttempted) return serifFont;
        serifAttempted = true;

        serifFont = TMP_FontAsset.CreateFontAsset("Times New Roman", "Regular", 90);

        if (serifFont == null)
            Debug.LogWarning("[UIKit] 'Times New Roman' is not installed — falling back to the TMP default font.");
        else
            serifFont.name = "Times New Roman (runtime)";

        return serifFont;
    }

    // ─── Generated sprites ─────────────────────────────────────────────

    private static Sprite roundedFill;
    private static Sprite neonOutline;

    /// <summary>A solid rounded rectangle, 9-sliced so it stretches without distortion.</summary>
    public static Sprite RoundedFill()
    {
        if (roundedFill != null) return roundedFill;
        roundedFill = BuildRoundedSprite(fill: true);
        return roundedFill;
    }

    /// <summary>
    /// A rounded rectangle OUTLINE with a soft halo either side of the stroke — the
    /// neon-tube look. White, so an Image tint picks the colour.
    /// </summary>
    public static Sprite NeonOutline()
    {
        if (neonOutline != null) return neonOutline;
        neonOutline = BuildRoundedSprite(fill: false);
        return neonOutline;
    }

    private const int SpriteSize = 64;
    private const float CornerRadius = 14f;
    private const float GlowWidth = 5f;      // how far the halo bleeds from the stroke
    private const int SliceBorder = 26;      // must exceed radius + glow, or corners smear

    private static Sprite BuildRoundedSprite(bool fill)
    {
        var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
        {
            name = fill ? "UIKit_RoundedFill" : "UIKit_NeonOutline",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        var half = new Vector2(SpriteSize * 0.5f, SpriteSize * 0.5f);
        // Inset so the glow has room to fade out before it reaches the texture edge.
        Vector2 halfSize = half - Vector2.one * (GlowWidth + 2f);
        var pixels = new Color[SpriteSize * SpriteSize];

        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                // Signed distance to the rounded rectangle: negative inside, positive out.
                var p = new Vector2(x + 0.5f, y + 0.5f) - half;
                float d = RoundedBoxDistance(p, halfSize, CornerRadius);

                float alpha;
                if (fill)
                {
                    // One-pixel antialiased edge.
                    alpha = Mathf.Clamp01(0.5f - d);
                }
                else
                {
                    float core = Mathf.Clamp01(1.6f - Mathf.Abs(d));          // crisp stroke
                    float halo = Mathf.Exp(-Mathf.Abs(d) / GlowWidth) * 0.7f; // soft bleed
                    alpha = Mathf.Clamp01(Mathf.Max(core, halo));
                }

                pixels[y * SpriteSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, SpriteSize, SpriteSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder));

        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>
    /// Standard 2D rounded-box signed distance function. Returns how far the point sits
    /// outside the shape (positive) or inside it (negative), in pixels.
    /// </summary>
    private static float RoundedBoxDistance(Vector2 p, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + Vector2.one * radius;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        return outside + inside - radius;
    }

    /// <summary>
    /// A one-pixel-wide vertical gradient, stretched by the Image to any size. Used to
    /// give flat UI boxes some depth instead of a dead flat fill.
    /// </summary>
    public static Sprite VerticalGradient(Color top, Color bottom, int height = 64)
    {
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            name = "UIKit_VerticalGradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < height; y++)
            texture.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));

        texture.Apply(false, false);

        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // ─── Widgets ───────────────────────────────────────────────────────

    /// <summary>A neon-outlined panel: dark rounded fill with a glowing border on top.</summary>
    public static RectTransform NeonPanel(string objectName, Transform parent,
                                          Color fillColor, Color outlineColor)
    {
        RectTransform panel = NewRect(objectName, parent);

        var fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = RoundedFill();
        fill.type = Image.Type.Sliced;
        fill.color = fillColor;

        // The border is a separate stretched child so it draws over the fill.
        RectTransform border = Stretch(NewRect("Outline", panel));
        var glow = border.gameObject.AddComponent<Image>();
        glow.sprite = NeonOutline();
        glow.type = Image.Type.Sliced;
        glow.color = outlineColor;
        glow.raycastTarget = false;

        return panel;
    }

    /// <summary>A text button using the same neon styling.</summary>
    public static Button TextButton(string objectName, Transform parent, string text,
                                    float fontSize, Color outlineColor)
    {
        RectTransform root = NeonPanel(objectName, parent, new Color(0.06f, 0.10f, 0.16f, 0.9f), outlineColor);

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = Label("Label", root, text, fontSize, Ink, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        return button;
    }
}
