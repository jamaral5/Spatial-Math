using UnityEngine;
using TMPro;

/// <summary>
/// When the user clicks a point on a 3D graph, this script:
///   1. Finds the height of the surface at that point.
///   2. Estimates the two partial derivatives (the slopes in the x and y directions).
///   3. Orients a flat "tangent plane" object so it kisses the surface at that point.
///   4. Draws the two tangent LINES that span that plane (x-direction and y-direction).
///   5. Builds a floating text label with the tangent-plane equation and the slope values.
///
/// MATH BACKGROUND (read this once and the code will make sense):
/// Your surface is a height function. You TYPE it as f(x, y), e.g. "sin(x) * cos(y)".
/// In the 3D world, that height is drawn on the VERTICAL axis. So:
///        world X  = the input we call x
///        world Z  = the second input we call y   (it is the "depth" ground axis)
///        world Y  = the OUTPUT height, f(x, y)
///
/// The tangent plane at a point (x0, y0) is the flat plane that best matches the
/// surface right there. Its equation is:
///
///        f(x, y) = f0 + fx*(x - x0) + fy*(y - y0)
///
/// where f0 is the height at the point, fx = df/dx (slope as x changes) and
/// fy = df/dy (slope as y changes). Those two slopes are the "partial derivatives".
/// We don't do symbolic calculus here — we estimate each slope numerically by
/// nudging the input a tiny amount h and measuring how the height changes.
/// </summary>
public class TangentPlaneRenderer : MonoBehaviour
{
    [Header("Scene objects you already wired up")]
    public GameObject tangentPlane;   // a flat Unity Plane/Quad that gets re-oriented
    public GameObject marker;         // the 3D ball that marks the clicked point

    [Header("Derivative Settings")]
    [Tooltip("The tiny step used to estimate slopes. Smaller = more accurate, but too small gets noisy.")]
    public float h = 0.05f;

    [Header("Tangent Lines")]
    public bool showTangentLines = true;
    [Tooltip("How far each tangent line extends from the point, in world units.")]
    public float lineHalfLength = 1.5f;
    public float lineWidth = 0.03f;
    public Color xLineColor = new Color(1f, 0.3f, 0.3f);   // red  = x-direction slope
    public Color yLineColor = new Color(0.3f, 0.5f, 1f);   // blue = y-direction slope

    [Header("Equation Label")]
    public bool showLabel = true;
    [Tooltip("Optional. If left empty, TextMeshPro uses its default font.")]
    public TMP_FontAsset labelFont;
    [Tooltip("How far above the clicked point the label floats.")]
    public float labelHeight = 1.0f;
    public float labelFontSize = 3f;
    public Color labelColor = Color.white;

    // ─── Private state (created automatically at runtime) ───────────────
    private GraphRenderer graphRenderer;   // which graph we're currently reading from
    private LineRenderer xLine;            // the x-direction tangent line
    private LineRenderer yLine;            // the y-direction tangent line
    private TextMeshPro label;             // the floating equation text
    private Transform cam;                 // cached camera, for billboarding the label

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        // Cache a graph to fall back on if the selector doesn't hand us a specific one.
        if (graphRenderer == null)
            graphRenderer = FindFirstObjectByType<GraphRenderer>();

        // Billboard: rotate the label so it always faces the camera (the same trick your
        // AxisRenderer uses). Without this, world-space text becomes unreadable from most
        // angles — and facing-the-camera text is the VR-correct approach for later.
        if (label != null && label.gameObject.activeSelf && cam != null)
        {
            label.transform.LookAt(
                label.transform.position + cam.rotation * Vector3.forward,
                cam.rotation * Vector3.up);
        }
    }

    /// <summary>
    /// Old call site still works: uses whatever graph we last found.
    /// </summary>
    public void ShowTangentPlaneAt(Vector3 point)
    {
        ShowTangentPlaneAt(point, graphRenderer);
    }

    /// <summary>
    /// Preferred call: the selector tells us exactly WHICH graph was clicked,
    /// so this stays correct when you have several equations on screen at once.
    /// </summary>
    public void ShowTangentPlaneAt(Vector3 point, GraphRenderer graph)
    {
        if (graph != null) graphRenderer = graph;
        if (graphRenderer == null) return;

        // ── Step 1: the point itself ───────────────────────────────────
        // Remember: world X is our input x, world Z is our input y (the depth axis).
        float x0 = point.x;
        float y0 = point.z;                        // "y" input  == world Z
        float f0 = graphRenderer.Evaluate(x0, y0); // height of the surface there

        // ── Step 2: the two partial derivatives, by central difference ──
        // df/dx: nudge x by +h and -h, see how the height changed, divide by the
        // total run (2h). This is just "rise over run" measured very close to the point.
        float fx = (graphRenderer.Evaluate(x0 + h, y0) - graphRenderer.Evaluate(x0 - h, y0)) / (2f * h);
        // df/dy: same idea, but nudging the SECOND input (world Z) instead.
        float fy = (graphRenderer.Evaluate(x0, y0 + h) - graphRenderer.Evaluate(x0, y0 - h)) / (2f * h);

        Vector3 worldPoint = new Vector3(x0, f0, y0);

        // ── Step 3: orient the flat tangent-plane object ───────────────
        // These two vectors point "uphill" along each axis in WORLD space:
        //   move +1 in world X  ->  height rises by fx  ->  (1, fx, 0)
        //   move +1 in world Z  ->  height rises by fy  ->  (0, fy, 1)
        Vector3 tangentX = new Vector3(1f, fx, 0f);
        Vector3 tangentZ = new Vector3(0f, fy, 1f);

        // The plane's normal is perpendicular to both tangent directions.
        // Cross(tangentZ, tangentX) works out to (-fx, 1, -fy): it points generally
        // UP, which is what we want for a Unity Plane (whose face normal is +Y).
        Vector3 normal = Vector3.Cross(tangentZ, tangentX).normalized;

        if (tangentPlane != null)
        {
            tangentPlane.SetActive(true);
            tangentPlane.transform.position = worldPoint;
            tangentPlane.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }

        // ── Step 4: the marker ball ────────────────────────────────────
        if (marker != null)
        {
            marker.SetActive(true);
            marker.transform.position = worldPoint;
        }

        // ── Step 5: draw the two tangent lines ─────────────────────────
        if (showTangentLines)
            DrawTangentLines(worldPoint, tangentX.normalized, tangentZ.normalized);
        else
            HideLines();

        // ── Step 6: build the equation text ────────────────────────────
        if (showLabel)
            UpdateLabel(worldPoint, x0, y0, f0, fx, fy);
        else if (label != null)
            label.gameObject.SetActive(false);
    }

    // ───────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────

    private void DrawTangentLines(Vector3 center, Vector3 dirX, Vector3 dirZ)
    {
        if (xLine == null) xLine = CreateLine("TangentLine_X", xLineColor);
        if (yLine == null) yLine = CreateLine("TangentLine_Y", yLineColor);

        // Each line runs from one side of the point to the other, through the point.
        xLine.gameObject.SetActive(true);
        xLine.SetPosition(0, center - dirX * lineHalfLength);
        xLine.SetPosition(1, center + dirX * lineHalfLength);

        yLine.gameObject.SetActive(true);
        yLine.SetPosition(0, center - dirZ * lineHalfLength);
        yLine.SetPosition(1, center + dirZ * lineHalfLength);
    }

    private LineRenderer CreateLine(string lineName, Color color)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;          // we give it absolute world positions
        lr.widthMultiplier = lineWidth;
        // "Sprites/Default" is a simple shader that always ships with Unity and lets
        // us tint the line with a flat color.
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 2;            // slightly rounded ends, looks nicer
        return lr;
    }

    private void HideLines()
    {
        if (xLine != null) xLine.gameObject.SetActive(false);
        if (yLine != null) yLine.gameObject.SetActive(false);
    }

    private void UpdateLabel(Vector3 worldPoint, float x0, float y0, float f0, float fx, float fy)
    {
        if (label == null) label = CreateLabel();

        label.gameObject.SetActive(true);
        label.transform.position = worldPoint + Vector3.up * labelHeight;

        // Build the two slope-terms with their own +/- signs so the equation reads
        // naturally, e.g.  "+ 0.70(x - 1.00)"  or  "- 0.30(y + 2.00)".
        string termX = SignedTerm(fx, "x", x0);
        string termY = SignedTerm(fy, "y", y0);

        // Line 1: the tangent-plane equation.
        // Line 2: the raw partial-derivative values, for the "fully explained" view.
        label.text =
            $"f(x, y) = {f0:F2} {termX} {termY}\n" +
            $"<size={labelFontSize * 0.7f}>df/dx = {fx:F2}    df/dy = {fy:F2}</size>";
    }

    /// <summary>
    /// Formats one term like "+ 0.70(x - 1.00)". 'coef' is the slope, 'v' is the
    /// variable letter, 'center' is x0 or y0 (the point we expand around).
    /// </summary>
    private string SignedTerm(float coef, string v, float center)
    {
        string lead = coef >= 0f ? "+" : "-";              // sign of the slope
        string inner = center >= 0f
            ? $"({v} - {center:F2})"                        // (x - 1.00)
            : $"({v} + {Mathf.Abs(center):F2})";           // (x + 1.00)  when x0 is negative
        return $"{lead} {Mathf.Abs(coef):F2}{inner}";
    }

    private TextMeshPro CreateLabel()
    {
        var go = new GameObject("TangentEquationLabel");
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshPro>();
        if (labelFont != null) tmp.font = labelFont;
        tmp.fontSize = labelFontSize;
        tmp.color = labelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        // A TextMeshPro lives on a RectTransform; give it room so the equation
        // isn't clipped.
        var rt = tmp.rectTransform;
        rt.sizeDelta = new Vector2(12f, 3f);
        return tmp;
    }
}
