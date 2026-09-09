using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Restyles the equation panel at runtime: drops the translucent grey plate behind it,
/// tucks the controls into the top-left corner, and gives the input box a neon-glow
/// border over a gradient fill.
///
/// This runs over the objects already in the scene rather than rebuilding them, so the
/// wiring you set up in the Inspector (UIManager -> input field, status text) is left
/// alone. Every colour is exposed below so the look can be tuned without code.
/// </summary>
[AddComponentMenu("Spatial Math/Equation Panel Styler")]
public class EquationPanelStyler : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    [Tooltip("The panel holding the prompt, input field and status text.")]
    public RectTransform equationPanel;
    public TMP_InputField equationInput;
    public TMP_Text promptLabel;
    public TMP_Text statusText;

    [Header("Corner Placement")]
    public Vector2 panelSize = new Vector2(400f, 168f);

    [Tooltip("Inset from the top-left corner of the canvas, in pixels.")]
    public Vector2 panelOffset = new Vector2(16f, -16f);

    [Header("Input Box")]
    public Color gradientTop = new Color(0.13f, 0.17f, 0.25f, 0.96f);
    public Color gradientBottom = new Color(0.05f, 0.07f, 0.12f, 0.96f);
    public Color glowColor = new Color(1f, 0.82f, 0.29f, 0.85f);
    public Color typedTextColor = new Color(0.94f, 0.97f, 1f);
    public float inputHeight = 52f;

    [Header("Type Sizes")]
    public float promptFontSize = 18f;
    public float inputFontSize = 20f;
    public float statusFontSize = 15f;

    /// <summary>Vertical space the prompt line occupies, derived from its type size.</summary>
    private float PromptRow => promptFontSize + 18f;

    void Start()
    {
        Resolve();

        if (equationPanel == null)
        {
            Debug.LogWarning("[EquationPanelStyler] Could not find the equation panel — disabling.");
            enabled = false;
            return;
        }

        StripBackingPlate();
        StyleInput();
        ApplyLayout();

        // Nothing to type into until the user has entered.
        equationPanel.gameObject.SetActive(false);
        StartScreen.WhenDismissed(() =>
        {
            if (equationPanel != null) equationPanel.gameObject.SetActive(true);
        });
    }

    /// <summary>Fills in whatever was left unassigned in the Inspector.</summary>
    private void Resolve()
    {
        if (equationInput == null) equationInput = FindFirstObjectByType<TMP_InputField>();

        if (equationPanel == null && equationInput != null)
            equationPanel = equationInput.transform.parent as RectTransform;

        if (statusText == null)
        {
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) statusText = uiManager.statusText;
        }

        // The prompt is the one label on the panel that is neither the status line nor
        // part of the input field's own hierarchy.
        if (promptLabel == null && equationPanel != null)
        {
            foreach (var candidate in equationPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate == statusText) continue;
                if (equationInput != null && candidate.transform.IsChildOf(equationInput.transform)) continue;
                promptLabel = candidate;
                break;
            }
        }
    }

    /// <summary>
    /// Removes the grey plate and stands the panel's layout group down.
    ///
    /// The VerticalLayoutGroup has to go: it DRIVES its children's RectTransforms, so
    /// anything this script positions by hand would be silently overwritten on the next
    /// layout pass. Both are disabled rather than destroyed, so ticking them back on in
    /// the Inspector restores the original look exactly.
    /// </summary>
    private void StripBackingPlate()
    {
        var plate = equationPanel.GetComponent<Image>();
        if (plate != null) plate.enabled = false;

        var layout = equationPanel.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
    }

    private void PlaceInCorner()
    {
        UIKit.Corner(equationPanel, new Vector2(0f, 1f), panelSize, panelOffset);
    }

    private void StyleInput()
    {
        if (equationInput == null) return;

        // Gradient fill in place of the flat white box.
        var background = equationInput.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = UIKit.VerticalGradient(gradientTop, gradientBottom);
            background.type = Image.Type.Simple;
            background.color = Color.white;   // let the gradient's own colours through
        }

        // The glow sits in a child drawn after the background, and must not swallow
        // clicks meant for the field underneath it.
        RectTransform glow = UIKit.Stretch(UIKit.NewRect("NeonGlow", equationInput.transform), -6f);
        glow.SetAsLastSibling();

        var glowImage = glow.gameObject.AddComponent<Image>();
        glowImage.sprite = UIKit.NeonOutline();
        glowImage.type = Image.Type.Sliced;
        glowImage.color = glowColor;
        glowImage.raycastTarget = false;

    }

    /// <summary>
    /// Everything that can safely be re-run. Kept apart from StyleInput, which CREATES
    /// the glow child — running that twice would stack a second glow on top.
    /// </summary>
    private void ApplyLayout()
    {
        PlaceInCorner();

        if (equationInput != null && equationInput.transform is RectTransform inputRect)
        {
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0.5f, 1f);
            inputRect.offsetMin = new Vector2(0f, -inputHeight - PromptRow);
            inputRect.offsetMax = new Vector2(0f, -PromptRow);
        }

        StyleText();
    }

    /// <summary>Live layout tweaking during Play mode. See GraphOpacityUI.OnValidate.</summary>
    void OnValidate()
    {
        if (!Application.isPlaying || equationPanel == null) return;
        ApplyLayout();
    }

    private void StyleText()
    {
        if (promptLabel != null)
        {
            promptLabel.color = UIKit.Neon;
            promptLabel.fontSize = promptFontSize;

            var rect = promptLabel.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(4f, -(promptFontSize + 12f));
            rect.offsetMax = new Vector2(-4f, -6f);
        }

        if (equationInput != null)
        {
            if (equationInput.textComponent != null)
            {
                equationInput.textComponent.color = typedTextColor;
                equationInput.textComponent.fontSize = inputFontSize;
            }

            // The placeholder is the line telling the user what to type, so it has to be
            // as readable as the typed text itself.
            if (equationInput.placeholder is TMP_Text placeholder)
            {
                placeholder.fontSize = inputFontSize;
                placeholder.color = new Color(0.68f, 0.72f, 0.80f, 0.9f);
            }

            equationInput.pointSize = inputFontSize;
        }

        if (statusText != null)
        {
            statusText.fontSize = statusFontSize;

            var rect = statusText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(4f, -inputHeight - PromptRow - statusFontSize - 26f);
            rect.offsetMax = new Vector2(-4f, -inputHeight - PromptRow - 8f);
        }
    }
}
