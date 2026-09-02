using UnityEngine;
using System.Collections.Generic;


public class GraphManager : MonoBehaviour
{
    public const int MAX_EQUATIONS = 3;

    [Header("Graph Slots")]
    [Tooltip("One material per possible graph slot (supports vertex colors).")]
    public Material[] graphMaterials;

    [Header("Graph Visual Settings")]
    public int meshResolution = 60;
    public float graphRange = 5f;
    public float maxYClamp = 10f;

    [Header("References")]
    public AxisRenderer axisRenderer;
    public Transform graphContainer;

    // Per-slot state
    private GraphRenderer[] graphSlots = new GraphRenderer[MAX_EQUATIONS];
    private bool[] slotActive = new bool[MAX_EQUATIONS];
    private string[] slotEquations = new string[MAX_EQUATIONS];

    // Events the UI can subscribe to
    public System.Action<int, bool, string> OnSlotChanged; // slot, success, equation

    void Awake()
    {
        InitSlots();
    }

    void Start()
    {
        // Load a default equation in slot 0
        SetEquation(0, "sin(x) * cos(y)");
    }

    private void InitSlots()
    {
        for (int i = 0; i < MAX_EQUATIONS; i++)
        {
            var go = new GameObject($"Graph_Slot_{i}");
            go.transform.SetParent(graphContainer != null ? graphContainer : transform, false);

            var gr = go.AddComponent<GraphRenderer>();
            gr.resolution = meshResolution;
            gr.graphRange = graphRange;
            gr.maxYClamp = maxYClamp;

            if (graphMaterials != null && i < graphMaterials.Length)
                gr.graphMaterial = graphMaterials[i];

            graphSlots[i] = gr;
            slotActive[i] = false;
            slotEquations[i] = "";
            go.SetActive(false);
        }

        // Sync axis renderer with range
        if (axisRenderer != null)
            axisRenderer.SetAxisLength(graphRange);
    }

    // ─── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Set equation for a given slot (0–MAX_EQUATIONS-1). Empty string clears the slot.
    /// Returns true if the equation parsed successfully.
    /// </summary>
    public bool SetEquation(int slot, string equation)
    {
        if (slot < 0 || slot >= MAX_EQUATIONS) return false;

        if (string.IsNullOrWhiteSpace(equation))
        {
            ClearSlot(slot);
            return true;
        }

        var gr = graphSlots[slot];
        gr.gameObject.SetActive(true);
        bool ok = gr.SetEquation(equation);

        slotActive[slot] = ok;
        slotEquations[slot] = ok ? equation : "";

        if (!ok) gr.gameObject.SetActive(false);

        OnSlotChanged?.Invoke(slot, ok, equation);
        return ok;
    }

    /// <summary>Hides and clears a slot.</summary>
    public void ClearSlot(int slot)
    {
        if (slot < 0 || slot >= MAX_EQUATIONS) return;
        graphSlots[slot].gameObject.SetActive(false);
        slotActive[slot] = false;
        slotEquations[slot] = "";
        OnSlotChanged?.Invoke(slot, true, "");
    }

    public void ClearAll()
    {
        for (int i = 0; i < MAX_EQUATIONS; i++) ClearSlot(i);
    }

    /// <summary>Toggle visibility of a slot without clearing its equation.</summary>
    public void ToggleSlotVisibility(int slot)
    {
        if (slot < 0 || slot >= MAX_EQUATIONS) return;
        if (!slotActive[slot]) return;
        graphSlots[slot].gameObject.SetActive(!graphSlots[slot].gameObject.activeSelf);
    }

    public string GetEquation(int slot) =>
        (slot >= 0 && slot < MAX_EQUATIONS) ? slotEquations[slot] : "";

    public bool IsSlotActive(int slot) =>
        (slot >= 0 && slot < MAX_EQUATIONS) && slotActive[slot];

    /// <summary>
    /// Update the graph range at runtime and rebuild all active graphs.
    /// </summary>
    public void SetGraphRange(float range)
    {
        graphRange = Mathf.Clamp(range, 1f, 20f);
        for (int i = 0; i < MAX_EQUATIONS; i++)
        {
            graphSlots[i].graphRange = graphRange;
            if (slotActive[i]) graphSlots[i].RebuildMesh();
        }
        if (axisRenderer != null)
            axisRenderer.SetAxisLength(graphRange);
    }

    public void SetResolution(int res)
    {
        meshResolution = Mathf.Clamp(res, 10, 150);
        for (int i = 0; i < MAX_EQUATIONS; i++)
        {
            graphSlots[i].resolution = meshResolution;
            if (slotActive[i]) graphSlots[i].RebuildMesh();
        }
    }
}