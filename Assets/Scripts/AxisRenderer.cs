using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AxisRenderer : MonoBehaviour
{
    [Header("Axis Range (should match GraphRenderer.graphRange)")]
    [Tooltip("Half-length of the X and Z axes — the domain the surface is sampled over.")]
    public float axisLength = 5f;

    [Tooltip("Half-length of the vertical axis. GraphManager overwrites this to fit the " +
             "surface when Auto Fit Y Axis is on.")]
    public float yAxisLength = 5f;

    [Header("Label Offset — how far above the graph the labels float")]
    public float labelFloatHeight = 1.2f;

    [Header("Axis Colors")]
    public Color xColor = new Color(1f, 0.3f, 0.3f);   // Red
    public Color yColor = new Color(0.3f, 1f, 0.3f);   // Green
    public Color zColor = new Color(0.3f, 0.5f, 1f);   // Blue

    [Header("Tick Settings")]
    [Tooltip("Roughly how many ticks to aim for per direction. The actual spacing is " +
             "rounded to a readable 1 / 2 / 5 step near this.")]
    public int tickCount = 5;

    [Tooltip("Hard ceiling on ticks per direction. This is the guard that stops a tall " +
             "surface from spawning hundreds of label objects and stalling the frame.")]
    public int maxTicksPerAxis = 8;

    public float tickSize = 0.08f;

    [Tooltip("How far the numeric tick labels sit off their axis. Small keeps them reading " +
             "as rulings on the axis rather than as marks floating in space.")]
    public float tickLabelOffset = 0.28f;

    [Header("Font")]
    public TMP_FontAsset labelFont;

    private Transform cameraTransform;
    private GameObject labelsRoot;

    // Tick label text objects (for billboard updates)
    private TextMeshPro[] allLabels;

    // One material per axis colour, shared by every line that uses it. Building a fresh
    // material (and calling Shader.Find) per line meant ~70 of each on every rebuild.
    private readonly Dictionary<Color, Material> lineMaterials = new Dictionary<Color, Material>();
    private Shader lineShader;

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        BuildAxes();
    }

    void OnDestroy()
    {
        foreach (Material material in lineMaterials.Values)
            if (material != null) Destroy(material);

        lineMaterials.Clear();
    }

    void LateUpdate()
    {
        // Billboard: make all labels face the camera
        if (cameraTransform == null) return;
        if (allLabels == null) return;

        foreach (var label in allLabels)
        {
            if (label == null) continue;
            label.transform.LookAt(label.transform.position + cameraTransform.rotation * Vector3.forward,
                                   cameraTransform.rotation * Vector3.up);
        }
    }

    /// <summary>Sets the horizontal extent, leaving the vertical one alone.</summary>
    public void SetAxisLength(float length)
    {
        axisLength = length;
        RebuildAxes();
    }

    /// <summary>
    /// Sets the horizontal and vertical extents together. GraphManager calls this after a
    /// plot so the vertical ruler actually reaches the surface it is measuring.
    /// </summary>
    public void SetAxisLengths(float horizontal, float vertical)
    {
        axisLength = horizontal;
        yAxisLength = vertical;
        RebuildAxes();
    }

    public void RebuildAxes()
    {
        if (labelsRoot != null) Destroy(labelsRoot);
        BuildAxes();
    }

    private void BuildAxes()
    {
        // Always clear first. GraphManager sizes the axes from its Awake, which builds
        // them once, and Start would otherwise build a second overlapping set that never
        // gets cleaned up — doubling the draw calls and z-fighting every line.
        if (labelsRoot != null) Destroy(labelsRoot);

        labelsRoot = new GameObject("AxesRoot");
        labelsRoot.transform.SetParent(transform, false);

        var labelList = new List<TextMeshPro>();

        float xz = Mathf.Max(0.001f, axisLength);
        float y = Mathf.Max(0.001f, yAxisLength);

        // --- X Axis ---
        CreateAxisLine(Vector3.zero, Vector3.right * xz, xColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.left * xz, xColor, labelsRoot.transform);
        CreateAxisLabel("+X", Vector3.right * (xz + 0.3f) + Vector3.up * labelFloatHeight, xColor, labelsRoot.transform, labelList);

        // --- Y Axis ---
        // Both halves run the full length, so Y is symmetric with X and Z rather than a
        // stub. Surfaces routinely dip well below zero and need the ruler there.
        CreateAxisLine(Vector3.zero, Vector3.up * y, yColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.down * y, yColor, labelsRoot.transform);
        CreateAxisLabel("+Y", Vector3.up * (y + 0.3f), yColor, labelsRoot.transform, labelList);
        CreateAxisLabel("-Y", Vector3.down * (y + 0.3f), yColor, labelsRoot.transform, labelList);

        // --- Z Axis ---
        CreateAxisLine(Vector3.zero, Vector3.forward * xz, zColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.back * xz, zColor, labelsRoot.transform);
        CreateAxisLabel("+Z", Vector3.forward * (xz + 0.3f) + Vector3.up * labelFloatHeight, zColor, labelsRoot.transform, labelList);

        // --- Tick Marks ---
        // X and Z share a step because they share a length; Y gets its own, since it now
        // tracks the height of the surface and can be a very different scale.
        BuildTicks(xz, Vector3.right, Vector3.up, xColor, labelsRoot.transform, labelList);
        BuildTicks(xz, Vector3.forward, Vector3.up, zColor, labelsRoot.transform, labelList);
        BuildTicks(y, Vector3.up, Vector3.right, yColor, labelsRoot.transform, labelList);

        // Origin label
        CreateTickLabel("0", Vector3.up * tickLabelOffset, Color.white, labelsRoot.transform, labelList);

        allLabels = labelList.ToArray();
    }

    /// <summary>
    /// Lays ticks out along one axis, in both directions from the origin.
    /// 'direction' runs along the axis; 'across' is the direction the tick mark and its
    /// label are offset in.
    /// </summary>
    private void BuildTicks(float length, Vector3 direction, Vector3 across, Color color,
                            Transform parent, List<TextMeshPro> labels)
    {
        float step = ChooseTickStep(length);
        int count = Mathf.Min(maxTicksPerAxis, Mathf.FloorToInt(length / step + 0.0001f));
        string format = "F" + DecimalsFor(step);

        for (int i = 1; i <= count; i++)
        {
            float value = i * step;
            Vector3 offset = across * tickLabelOffset;

            CreateTick(direction * value, across, color, parent);
            CreateTick(-direction * value, across, color, parent);

            CreateTickLabel(value.ToString(format), direction * value + offset, color, parent, labels);
            CreateTickLabel("-" + value.ToString(format), -direction * value + offset, color, parent, labels);
        }
    }

    /// <summary>
    /// Picks a readable tick spacing for an axis of the given length.
    ///
    /// Walks the 1 / 2 / 2.5 / 5 / 10 ladder across three decades and keeps whichever
    /// step puts the tick count closest to tickCount without passing maxTicksPerAxis.
    ///
    /// The previous version always rounded the spacing UP to the next rung, which is why
    /// a stretched axis lost its ticks: a 30-unit axis asking for 5 ticks got a step of
    /// 10 and ended up with three marks on it. Choosing the nearest rung instead keeps
    /// the ruler populated at any length.
    /// </summary>
    private float ChooseTickStep(float length)
    {
        int target = Mathf.Clamp(tickCount, 1, Mathf.Max(1, maxTicksPerAxis));
        float rough = length / target;
        if (rough <= 0f || float.IsNaN(rough) || float.IsInfinity(rough)) return 1f;

        float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rough)));
        float[] ladder = { 1f, 2f, 2.5f, 5f, 10f };

        float chosen = 0f;
        int bestDistance = int.MaxValue;

        for (int decade = -1; decade <= 1; decade++)
        {
            float scale = magnitude * Mathf.Pow(10f, decade);

            foreach (float rung in ladder)
            {
                float candidate = rung * scale;
                if (candidate <= 0f) continue;

                int count = Mathf.FloorToInt(length / candidate + 0.0001f);
                if (count < 1 || count > maxTicksPerAxis) continue;

                int distance = Mathf.Abs(count - target);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                chosen = candidate;
            }
        }

        // Nothing on the ladder fitted — a very short or awkwardly sized axis. Divide
        // evenly instead, which always yields exactly the target number of ticks even if
        // the numbers read less neatly.
        return chosen > 0f ? chosen : length / target;
    }

    /// <summary>How many decimal places it takes to write this step exactly.</summary>
    private static int DecimalsFor(float step)
    {
        int decimals = 0;
        float probe = step;

        // Without this a 2.5 step formatted as "F0" and every label rounded to 3.
        while (decimals < 3 && Mathf.Abs(probe - Mathf.Round(probe)) > 0.0001f)
        {
            probe *= 10f;
            decimals++;
        }

        return decimals;
    }

    /// <summary>One shared material per colour, created on first use.</summary>
    private Material LineMaterial(Color color)
    {
        if (lineMaterials.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        if (lineShader == null) lineShader = Shader.Find("Universal Render Pipeline/Unlit");

        var material = new Material(lineShader) { name = $"AxisLine {color}" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

        lineMaterials[color] = material;
        return material;
    }

    private void CreateAxisLine(Vector3 from, Vector3 to, Color color, Transform parent)
    {
        var go = new GameObject("AxisLine");
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { from, to });
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;
        lr.sharedMaterial = LineMaterial(color);
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void CreateTick(Vector3 position, Vector3 direction, Color color, Transform parent)
    {
        CreateAxisLine(position - direction * tickSize,
                       position + direction * tickSize,
                       color, parent);
    }

    private void CreateAxisLabel(string text, Vector3 localPos, Color color, Transform parent,
                                 List<TextMeshPro> list)
    {
        var go = new GameObject($"Label_{text}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 0.4f;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        if (labelFont != null) tmp.font = labelFont;

        list.Add(tmp);
    }

    private void CreateTickLabel(string text, Vector3 localPos, Color color, Transform parent,
                                  List<TextMeshPro> list)
    {
        var go = new GameObject($"TickLabel_{text}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 0.18f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (labelFont != null) tmp.font = labelFont;

        list.Add(tmp);
    }
}
