using UnityEngine;
using TMPro; 


public class AxisRenderer : MonoBehaviour
{
    [Header("Axis Range (should match GraphRenderer.graphRange)")]
    public float axisLength = 5f;

    [Header("Label Offset — how far above the graph the labels float")]
    public float labelFloatHeight = 1.2f;

    [Header("Axis Colors")]
    public Color xColor = new Color(1f, 0.3f, 0.3f);   // Red
    public Color yColor = new Color(0.3f, 1f, 0.3f);   // Green
    public Color zColor = new Color(0.3f, 0.5f, 1f);   // Blue

    [Header("Tick Settings")]
    public int tickCount = 5;
    public float tickSize = 0.08f;

    [Header("Font")]
    public TMP_FontAsset labelFont;

    private Transform cameraTransform;
    private GameObject labelsRoot;

    // Tick label text objects (for billboard updates)
    private TextMeshPro[] allLabels;

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        BuildAxes();
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

    public void SetAxisLength(float length)
    {
        axisLength = length;
        RebuildAxes();
    }

    public void RebuildAxes()
    {
        if (labelsRoot != null) Destroy(labelsRoot);
        BuildAxes();
    }

    private void BuildAxes()
    {
        labelsRoot = new GameObject("AxesRoot");
        labelsRoot.transform.SetParent(transform, false);

        var labelList = new System.Collections.Generic.List<TextMeshPro>();

        // --- X Axis ---
        CreateAxisLine(Vector3.zero, Vector3.right * axisLength, xColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.left * axisLength, xColor, labelsRoot.transform);
        CreateAxisLabel("+X", Vector3.right * (axisLength + 0.3f) + Vector3.up * labelFloatHeight, xColor, labelsRoot.transform, labelList);

        // --- Y Axis ---
        CreateAxisLine(Vector3.zero, Vector3.up * axisLength, yColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.down * axisLength * 0.5f, yColor, labelsRoot.transform);
        CreateAxisLabel("+Y", Vector3.up * (axisLength + 0.3f), yColor, labelsRoot.transform, labelList);

        // --- Z Axis ---
        CreateAxisLine(Vector3.zero, Vector3.forward * axisLength, zColor, labelsRoot.transform);
        CreateAxisLine(Vector3.zero, Vector3.back * axisLength, zColor, labelsRoot.transform);
        CreateAxisLabel("+Z", Vector3.forward * (axisLength + 0.3f) + Vector3.up * labelFloatHeight, zColor, labelsRoot.transform, labelList);

        // --- Tick Marks ---
        float tickSpacing = axisLength / tickCount;

        for (int i = 1; i <= tickCount; i++)
        {
            float val = i * tickSpacing;

            // X ticks
            CreateTick(Vector3.right * val, Vector3.up, xColor, labelsRoot.transform);
            CreateTick(Vector3.left * val, Vector3.up, xColor, labelsRoot.transform);
            CreateTickLabel($"{val:F1}", Vector3.right * val + Vector3.up * labelFloatHeight * 0.6f, xColor, labelsRoot.transform, labelList);
            CreateTickLabel($"-{val:F1}", Vector3.left * val + Vector3.up * labelFloatHeight * 0.6f, xColor, labelsRoot.transform, labelList);

            // Y ticks
            CreateTick(Vector3.up * val, Vector3.right, yColor, labelsRoot.transform);
            CreateTickLabel($"{val:F1}", Vector3.up * val + Vector3.right * 0.3f, yColor, labelsRoot.transform, labelList);

            // Z ticks
            CreateTick(Vector3.forward * val, Vector3.up, zColor, labelsRoot.transform);
            CreateTick(Vector3.back * val, Vector3.up, zColor, labelsRoot.transform);
            CreateTickLabel($"{val:F1}", Vector3.forward * val + Vector3.up * labelFloatHeight * 0.6f, zColor, labelsRoot.transform, labelList);
            CreateTickLabel($"-{val:F1}", Vector3.back * val + Vector3.up * labelFloatHeight * 0.6f, zColor, labelsRoot.transform, labelList);
        }

        // Origin label
        CreateTickLabel("0", Vector3.up * labelFloatHeight * 0.5f, Color.white, labelsRoot.transform, labelList);

        allLabels = labelList.ToArray();
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
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
                                 System.Collections.Generic.List<TextMeshPro> list)
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
                                  System.Collections.Generic.List<TextMeshPro> list)
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