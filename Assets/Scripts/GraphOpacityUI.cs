using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A slider that fades the graph up and down while the app is running.
///
/// By default it builds its own little panel under the existing Canvas at start-up, so
/// there is nothing to wire in the Inspector. If you would rather lay the slider out by
/// hand in the scene, drag it into the Slider field and this script will drive that one
/// instead of generating anything.
/// </summary>
[AddComponentMenu("Spatial Math/Graph Opacity UI")]
public class GraphOpacityUI : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    public GraphManager graphManager;
    public Canvas targetCanvas;

    [Tooltip("Optional. Assign your own slider to skip the generated panel.")]
    public Slider slider;

    [Header("Range")]
    [Tooltip("Floor of the slider. A little above zero so the graph never vanishes entirely.")]
    [Range(0f, 1f)] public float minOpacity = 0.05f;
    [Range(0f, 1f)] public float maxOpacity = 1f;

    [Header("Generated Panel Layout")]
    [Tooltip("Which corner or edge of the screen the panel hangs off. (0,1) is top-left, " +
             "(0.5,1) top-centre, (1,0) bottom-right.")]
    public Vector2 anchor = new Vector2(0.5f, 1f);

    public Vector2 panelSize = new Vector2(320f, 76f);

    [Tooltip("Point size of the 'Graph opacity NN%' readout.")]
    public float readoutFontSize = 18f;

    [Tooltip("Pixels from the top-left corner of the canvas. Negative Y moves it down, " +
             "clear of the equation panel above it.")]
    public Vector2 panelOffset = new Vector2(0f, -16f);

    [Header("Generated Panel Colors")]
    public Color panelColor  = new Color(0.05f, 0.07f, 0.11f, 0.78f);
    public Color textColor   = new Color(0.82f, 0.88f, 0.96f, 1f);
    public Color grooveColor = new Color(1f, 1f, 1f, 0.16f);
    public Color fillColor   = new Color(1f, 0.82f, 0.29f, 0.95f);
    public Color handleColor = new Color(1f, 0.93f, 0.72f, 1f);

    private TMP_Text readout;
    private RectTransform builtPanel;   // only set when WE built the slider

    void Start()
    {
        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (graphManager == null)
        {
            Debug.LogWarning("[GraphOpacityUI] No GraphManager in the scene — disabling.");
            enabled = false;
            return;
        }

        if (slider == null) slider = BuildPanel();
        if (slider == null)
        {
            enabled = false;
            return;
        }

        slider.minValue = minOpacity;
        slider.maxValue = Mathf.Max(maxOpacity, minOpacity + 0.01f);
        slider.SetValueWithoutNotify(Mathf.Clamp(graphManager.GetGlobalOpacity(),
                                                 slider.minValue, slider.maxValue));
        slider.onValueChanged.AddListener(HandleSliderMoved);

        // Keep the slider honest if anything else changes opacity (a preset, a future
        // keyboard shortcut, a headset controller).
        graphManager.OnOpacityChanged += HandleOpacityChangedElsewhere;

        // Push the starting value through once so graph and readout agree immediately.
        HandleSliderMoved(slider.value);

        // The controls belong to the app, not the title card — hold them back until the
        // user has actually entered. Only ever hide the panel this script built: an
        // Inspector-assigned slider could live anywhere, and hiding its parent might take
        // half the interface with it.
        if (builtPanel != null)
        {
            builtPanel.gameObject.SetActive(false);
            StartScreen.WhenDismissed(() =>
            {
                if (builtPanel != null) builtPanel.gameObject.SetActive(true);
            });
        }
    }

    /// <summary>
    /// Re-applies the layout fields while the game is running, so nudging Panel Size or
    /// Panel Offset in the Inspector during Play mode moves the panel immediately —
    /// no stopping and restarting to see the result. The values live on this component,
    /// so unlike editing the generated objects they also survive leaving Play mode.
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying || builtPanel == null) return;

        UIKit.Corner(builtPanel, anchor, panelSize, panelOffset);
        if (readout != null)
        {
            readout.fontSize = readoutFontSize;
            readout.rectTransform.offsetMin = new Vector2(12f, -(readoutFontSize + 16f));
        }
    }

    void OnDestroy()
    {
        if (graphManager != null) graphManager.OnOpacityChanged -= HandleOpacityChangedElsewhere;
        if (slider != null) slider.onValueChanged.RemoveListener(HandleSliderMoved);
    }

    private void HandleSliderMoved(float value)
    {
        graphManager.SetGlobalOpacity(value);
        UpdateReadout(value);
    }

    /// <summary>
    /// Opacity was changed by something other than this slider. SetValueWithoutNotify is
    /// essential here: plain 'slider.value =' would fire onValueChanged and bounce the
    /// change straight back into GraphManager in an endless loop.
    /// </summary>
    private void HandleOpacityChangedElsewhere(float value)
    {
        if (slider != null) slider.SetValueWithoutNotify(value);
        UpdateReadout(value);
    }

    private void UpdateReadout(float value)
    {
        if (readout != null) readout.text = $"Graph opacity   {Mathf.RoundToInt(value * 100f)}%";
    }

    // ───────────────────────────────────────────────────────────────────
    // Panel construction
    //
    // This is the same object layout the Unity editor creates for a Slider
    // (Background / Fill Area > Fill / Handle Slide Area > Handle), just written out in
    // code. The Slider component takes over the anchors of Fill and Handle at runtime,
    // which is why those two only need their offsets and size set here.
    // ───────────────────────────────────────────────────────────────────

    private Slider BuildPanel()
    {
        if (targetCanvas == null) targetCanvas = UIKit.FindSceneCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[GraphOpacityUI] No Canvas in the scene — cannot build the slider.");
            return null;
        }

        // ── Panel background ──────────────────────────────────────────
        RectTransform panelRect = NewUIObject("OpacityPanel", targetCanvas.transform);
        UIKit.Corner(panelRect, anchor, panelSize, panelOffset);

        builtPanel = panelRect;

        var panelImage = panelRect.gameObject.AddComponent<Image>();
        panelImage.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = panelColor;

        // ── Readout text ──────────────────────────────────────────────
        RectTransform labelRect = NewUIObject("Readout", panelRect);
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(12f, -(readoutFontSize + 16f));
        labelRect.offsetMax = new Vector2(-12f, -8f);

        readout = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        readout.fontSize = readoutFontSize;
        readout.color = textColor;
        readout.alignment = TextAlignmentOptions.Left;
        readout.raycastTarget = false;

        // ── Slider row ────────────────────────────────────────────────
        RectTransform sliderRect = NewUIObject("OpacitySlider", panelRect);
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.offsetMin = new Vector2(12f, 10f);
        sliderRect.offsetMax = new Vector2(-12f, 30f);   // a 20px-tall row, 10px off the bottom

        var builtSlider = sliderRect.gameObject.AddComponent<Slider>();
        builtSlider.direction = Slider.Direction.LeftToRight;

        // Groove the handle travels along.
        RectTransform background = NewUIObject("Background", sliderRect);
        StretchHorizontally(background, 4f);
        background.gameObject.AddComponent<Image>().color = grooveColor;

        // Fill: its parent defines the travel, the Slider drives its right-hand anchor.
        RectTransform fillArea = NewUIObject("Fill Area", sliderRect);
        StretchHorizontally(fillArea, 4f, insetX: 16f);

        RectTransform fillRect = NewUIObject("Fill", fillArea);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.gameObject.AddComponent<Image>().color = fillColor;

        // Handle: the Slider drives its horizontal anchor, so only its width matters.
        RectTransform handleArea = NewUIObject("Handle Slide Area", sliderRect);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);

        RectTransform handleRect = NewUIObject("Handle", handleArea);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(16f, 0f);     // 16 wide, full height of the row

        var handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.sprite = BuiltinSprite("UI/Skin/Knob.psd");
        handleImage.color = handleColor;

        builtSlider.fillRect = fillRect;
        builtSlider.handleRect = handleRect;
        builtSlider.targetGraphic = handleImage;

        return builtSlider;
    }

    private static RectTransform NewUIObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Pins a rect to a fixed-height horizontal band centred in its parent.</summary>
    private static void StretchHorizontally(RectTransform rect, float height, float insetX = 0f)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-insetX, height);
    }

    /// <summary>
    /// Unity's built-in UI sprites give the panel rounded corners and the handle a round
    /// knob. They are not guaranteed to be present in every build target, and a null
    /// sprite simply draws a plain rectangle, so a miss here is cosmetic only.
    /// </summary>
    private static Sprite BuiltinSprite(string path)
    {
        try { return Resources.GetBuiltinResource<Sprite>(path); }
        catch { return null; }
    }
}
