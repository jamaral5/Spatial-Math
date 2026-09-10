using UnityEngine;
using TMPro;

/// <summary>
/// The "Customize Tangent Plane" button and the list it opens.
///
/// Colour presets restyle the plane and its two tangent lines together, so the lines
/// stay legible against whatever the plane is. The last three rows are toggles whose
/// labels show their own state.
/// </summary>
[AddComponentMenu("Spatial Math/Customize Tangent Plane Panel")]
public class TangentPanelUI : ToolPanelUI
{
    /// <summary>One look for the tangent plane and the two slope lines.</summary>
    private class PlaneStyle
    {
        public string name;
        public Color plane;
        public Color lineAlongX;
        public Color lineAlongY;
    }

    [Header("References (found automatically if left empty)")]
    public TangentPlaneRenderer tangentPlaneRenderer;

    [Header("Label")]
    public string buttonLabel = "Customize Tangent Plane";

    [Header("Plane Opacity")]
    [Range(0f, 1f)]
    [Tooltip("How see-through the plane is. It sits ON the surface, so it needs to be " +
             "translucent or it hides the very thing it is describing.")]
    public float planeOpacity = 0.45f;

    [Header("Span Presets")]
    public float shortSpan = 0.8f;
    public float mediumSpan = 1.5f;
    public float longSpan = 3f;

    private TMP_Text linesLabel;
    private TMP_Text planeLabel;
    private TMP_Text spanLabel;

    protected override string ButtonLabel => buttonLabel;

    /// <summary>Default placement when the component is first added: directly below the
    /// Customize Graph button.</summary>
    private void Reset()
    {
        stackOrder = 1;
        panelSize = new Vector2(300f, 600f);   // taller: colours, shapes and toggles
    }

    private static readonly PlaneStyle[] Styles =
    {
        new PlaneStyle { name = "Amber",   plane = new Color(1.00f, 0.78f, 0.25f), lineAlongX = new Color(1f, 0.45f, 0.25f), lineAlongY = new Color(1f, 0.95f, 0.60f) },
        new PlaneStyle { name = "Cyan",    plane = new Color(0.30f, 0.85f, 1.00f), lineAlongX = new Color(1f, 0.40f, 0.45f), lineAlongY = new Color(0.60f, 1f, 0.85f) },
        new PlaneStyle { name = "Magenta", plane = new Color(0.95f, 0.35f, 0.80f), lineAlongX = new Color(1f, 0.85f, 0.35f), lineAlongY = new Color(0.60f, 0.75f, 1f) },
        new PlaneStyle { name = "Lime",    plane = new Color(0.55f, 0.95f, 0.35f), lineAlongX = new Color(1f, 0.45f, 0.35f), lineAlongY = new Color(0.35f, 0.70f, 1f) },
        new PlaneStyle { name = "White",   plane = new Color(0.95f, 0.96f, 1.00f), lineAlongX = new Color(1f, 0.35f, 0.35f), lineAlongY = new Color(0.35f, 0.55f, 1f) },
    };

    protected override bool Initialise()
    {
        if (tangentPlaneRenderer == null)
            tangentPlaneRenderer = FindFirstObjectByType<TangentPlaneRenderer>();

        if (tangentPlaneRenderer == null)
        {
            Debug.LogWarning("[TangentPanelUI] No TangentPlaneRenderer in the scene — disabling.");
            return false;
        }

        return true;
    }

    protected override void Populate(RectTransform column)
    {
        AddHeading(column, "Plane colour");

        foreach (PlaneStyle style in Styles)
        {
            PlaneStyle chosen = style;
            AddEntry(column, style.name, () => Apply(chosen));
        }

        AddHeading(column, "Plane shape");

        foreach (PlaneShape shape in System.Enum.GetValues(typeof(PlaneShape)))
        {
            PlaneShape chosen = shape;
            AddEntry(column, shape.ToString(), () => SetShape(chosen));
        }

        AddHeading(column, "Display");

        // The text is rewritten by RefreshLabels to show current state.
        linesLabel = AddEntry(column, "Slope lines", ToggleLines);
        planeLabel = AddEntry(column, "Plane", TogglePlane);
        spanLabel = AddEntry(column, "Size", CycleSpan);

        RefreshLabels();
    }

    // ───────────────────────────────────────────────────────────────────

    private void Apply(PlaneStyle style)
    {
        Color plane = style.plane;
        plane.a = planeOpacity;

        tangentPlaneRenderer.SetPlaneColor(plane);
        tangentPlaneRenderer.SetLineColors(style.lineAlongX, style.lineAlongY);
    }

    private void SetShape(PlaneShape shape)
    {
        tangentPlaneRenderer.SetPlaneShape(shape);
    }

    private void ToggleLines()
    {
        tangentPlaneRenderer.SetTangentLinesVisible(!tangentPlaneRenderer.TangentLinesVisible);
        RefreshLabels();
    }

    private void TogglePlane()
    {
        tangentPlaneRenderer.SetPlaneVisible(!tangentPlaneRenderer.PlaneVisible);
        RefreshLabels();
    }

    /// <summary>Steps short to medium to long and back around.</summary>
    private void CycleSpan()
    {
        float current = tangentPlaneRenderer.GetSpan();

        float next = Mathf.Approximately(current, shortSpan) ? mediumSpan
                   : Mathf.Approximately(current, mediumSpan) ? longSpan
                   : shortSpan;

        tangentPlaneRenderer.SetSpan(next);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (linesLabel != null)
            linesLabel.text = "Slope lines:  " + (tangentPlaneRenderer.TangentLinesVisible ? "on" : "off");

        if (planeLabel != null)
            planeLabel.text = "Plane:  " + (tangentPlaneRenderer.PlaneVisible ? "on" : "off");

        if (spanLabel != null)
            spanLabel.text = "Size:  " + SpanName(tangentPlaneRenderer.GetSpan());
    }

    private string SpanName(float span)
    {
        if (Mathf.Approximately(span, shortSpan)) return "small";
        if (Mathf.Approximately(span, longSpan)) return "large";
        return "medium";
    }
}
