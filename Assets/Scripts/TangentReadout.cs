using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows the selected point and its tangent-plane equation in the top-right corner.
///
/// This replaces the world-space text that used to float above the clicked point, which
/// sat on top of the surface and fought with it for legibility. The panel is built at
/// runtime and stays hidden until a tangent plane is actually rendered.
/// </summary>
[AddComponentMenu("Spatial Math/Tangent Readout")]
public class TangentReadout : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    public TangentPlaneRenderer tangentPlaneRenderer;
    public Canvas targetCanvas;

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(320f, 132f);

    [Tooltip("Inset from the top-right corner of the canvas, in pixels.")]
    public Vector2 panelOffset = new Vector2(-16f, -16f);

    private RectTransform panel;
    private TMP_Text pointText;
    private TMP_Text equationText;
    private TMP_Text derivativeText;

    void Start()
    {
        if (tangentPlaneRenderer == null)
            tangentPlaneRenderer = FindFirstObjectByType<TangentPlaneRenderer>();

        if (tangentPlaneRenderer == null)
        {
            Debug.LogWarning("[TangentReadout] No TangentPlaneRenderer in the scene — disabling.");
            enabled = false;
            return;
        }

        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[TangentReadout] No Canvas in the scene — disabling.");
            enabled = false;
            return;
        }

        // Take over from the floating world-space label. Set explicitly rather than by
        // changing the field default, because the scene already has its own saved value.
        tangentPlaneRenderer.showLabel = false;

        Build();
        Hide();

        tangentPlaneRenderer.OnTangentPlaneShown += Show;
        tangentPlaneRenderer.OnTangentPlaneCleared += Hide;
    }

    void OnDestroy()
    {
        if (tangentPlaneRenderer == null) return;
        tangentPlaneRenderer.OnTangentPlaneShown -= Show;
        tangentPlaneRenderer.OnTangentPlaneCleared -= Hide;
    }

    private void Show(Vector3 point, string equation, string derivatives)
    {
        if (panel == null) return;

        panel.gameObject.SetActive(true);

        // The surface is a height function: world X and Z are the two inputs, world Y is
        // the output. Label them x, y and f so the panel matches the maths, not Unity.
        pointText.text = $"x = {point.x:F2}     y = {point.z:F2}     f = {point.y:F2}";
        equationText.text = equation;
        derivativeText.text = derivatives;
    }

    private void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void Build()
    {
        panel = UIKit.NeonPanel("TangentReadout", targetCanvas.transform,
                                UIKit.PanelDark, UIKit.NeonSoft);
        UIKit.Corner(panel, new Vector2(1f, 1f), panelSize, panelOffset);

        RectTransform column = UIKit.Stretch(UIKit.NewRect("Column", panel), 14f);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2f;

        AddHeading(column, "Selected point");
        pointText = AddValue(column, 15f, UIKit.Ink);

        AddHeading(column, "Tangent plane");
        equationText = AddValue(column, 15f, UIKit.Ink);
        derivativeText = AddValue(column, 13f, UIKit.InkDim);
    }

    private static void AddHeading(Transform parent, string text)
    {
        TextMeshProUGUI heading = UIKit.Label("Heading", parent, text.ToUpperInvariant(),
                                             11f, UIKit.Neon, TextAlignmentOptions.Left);
        heading.characterSpacing = 6f;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;
    }

    private static TMP_Text AddValue(Transform parent, float size, Color color)
    {
        TextMeshProUGUI value = UIKit.Label("Value", parent, "", size, color, TextAlignmentOptions.Left);
        value.gameObject.AddComponent<LayoutElement>().preferredHeight = size + 6f;
        return value;
    }
}
