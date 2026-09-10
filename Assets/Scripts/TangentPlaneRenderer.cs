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

    /// <summary>
    /// Raised whenever a tangent plane is drawn, carrying the values already formatted
    /// for display: the point in world space, the plane equation, and the two partials.
    /// TangentReadout listens to this so the numbers can be shown in a corner panel
    /// instead of floating over the surface.
    /// </summary>
    public System.Action<Vector3, string, string> OnTangentPlaneShown;

    /// <summary>Raised when the tangent plane is taken back off the graph.</summary>
    public System.Action OnTangentPlaneCleared;

    [Header("Plane Shape")]
    public bool showPlane = true;
    public PlaneShape planeShape = PlaneShape.Square;

    [Tooltip("Plane radius as a fraction of the tangent-line half length. Below 1 the " +
             "slope lines run out past the edge of the patch, which reads well.")]
    public float planeRadiusScale = 0.7f;

    // ─── Private state (created automatically at runtime) ───────────────
    private GraphRenderer graphRenderer;   // which graph we're currently reading from
    private Vector3 lastPoint;             // last point asked for, so styling can redraw
    private bool hasShown;
    private Material planeMaterial;        // our own copy of the plane's material
    private Mesh planeMesh;                // our own generated plane outline
    private LineRenderer xLine;            // the x-direction tangent line
    private LineRenderer yLine;            // the y-direction tangent line
    private TextMeshPro label;             // the floating equation text
    private Transform cam;                 // cached camera, for billboarding the label

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void Start()
    {
        ApplyPlaneShape();
    }

    void OnDestroy()
    {
        // Created with 'new', so nothing else will clean them up.
        if (planeMaterial != null) Destroy(planeMaterial);
        if (planeMesh != null) Destroy(planeMesh);
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

        lastPoint = point;
        hasShown = true;

        if (tangentPlane != null)
        {
            tangentPlane.SetActive(showPlane);
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

        // ── Step 7: hand the same numbers to whoever is displaying them ─
        // Formatted here rather than in the listener so the world label and the corner
        // panel can never drift out of agreement about how an equation is written.
        OnTangentPlaneShown?.Invoke(
            worldPoint,
            $"f(x, y) = {f0:F2} {SignedTerm(fx, "x", x0)} {SignedTerm(fy, "y", y0)}",
            $"df/dx = {fx:F2}     df/dy = {fy:F2}");
    }

    // ─── Styling, driven by the Customize Tangent Plane panel ──────────

    /// <summary>Redraws at the last picked point, so a style change shows immediately.</summary>
    public void Refresh()
    {
        if (hasShown) ShowTangentPlaneAt(lastPoint, graphRenderer);
    }

    /// <summary>
    /// Recolours the plane. Works on a private copy of the material — editing the shared
    /// asset would change it on disk and survive leaving play mode.
    /// </summary>
    public void SetPlaneColor(Color color)
    {
        if (tangentPlane == null) return;

        var renderer = tangentPlane.GetComponent<Renderer>();
        if (renderer == null) return;

        if (planeMaterial == null)
        {
            planeMaterial = new Material(renderer.sharedMaterial);
            planeMaterial.name = "Tangent Plane (runtime copy)";
            MakeTransparent(planeMaterial);
            renderer.sharedMaterial = planeMaterial;
        }

        if (planeMaterial.HasProperty("_BaseColor")) planeMaterial.SetColor("_BaseColor", color);
        if (planeMaterial.HasProperty("_Color")) planeMaterial.SetColor("_Color", color);
    }

    /// <summary>Same alpha-blend setup GraphRenderer uses, so plane opacity works at all.</summary>
    private static void MakeTransparent(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public void SetLineColors(Color alongX, Color alongY)
    {
        xLineColor = alongX;
        yLineColor = alongY;

        if (xLine != null) { xLine.startColor = alongX; xLine.endColor = alongX; }
        if (yLine != null) { yLine.startColor = alongY; yLine.endColor = alongY; }
    }

    public void SetTangentLinesVisible(bool visible)
    {
        showTangentLines = visible;

        if (!visible) HideLines();
        else Refresh();
    }

    public void SetPlaneVisible(bool visible)
    {
        showPlane = visible;
        if (tangentPlane != null) tangentPlane.SetActive(visible && hasShown);
    }

    /// <summary>How far the tangent lines reach from the point. Resizes the plane too.</summary>
    public void SetSpan(float halfLength)
    {
        lineHalfLength = halfLength;
        ApplyPlaneShape();
        Refresh();
    }

    /// <summary>Cuts the plane to a different outline: square, circle, triangle...</summary>
    public void SetPlaneShape(PlaneShape shape)
    {
        planeShape = shape;
        ApplyPlaneShape();
    }

    public PlaneShape CurrentShape => planeShape;

    /// <summary>
    /// Rebuilds the plane mesh at the current shape and span.
    ///
    /// The scene ships a Unity Plane primitive squashed to (0.2, 1, 0.2). Once we supply
    /// our own mesh the radius is baked into the vertices, so the transform scale is reset
    /// to one — otherwise every size would come out multiplied by that 0.2.
    /// </summary>
    private void ApplyPlaneShape()
    {
        if (tangentPlane == null) return;

        var filter = tangentPlane.GetComponent<MeshFilter>();
        if (filter == null) return;

        if (planeMesh != null) Destroy(planeMesh);
        planeMesh = PlaneShapes.Build(planeShape, lineHalfLength * Mathf.Max(0.05f, planeRadiusScale));

        filter.sharedMesh = planeMesh;
        tangentPlane.transform.localScale = Vector3.one;

        // Keep the collider in step, if the object has one.
        var collider = tangentPlane.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = null;
            collider.sharedMesh = planeMesh;
        }
    }

    public float GetSpan() => lineHalfLength;
    public bool TangentLinesVisible => showTangentLines;
    public bool PlaneVisible => showPlane;

    /// <summary>Takes the tangent plane, lines, marker and readout back off the graph.</summary>
    public void ClearTangentPlane()
    {
        hasShown = false;
        if (tangentPlane != null) tangentPlane.SetActive(false);
        if (marker != null) marker.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
        HideLines();

        OnTangentPlaneCleared?.Invoke();
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
