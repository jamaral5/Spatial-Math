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

    [Tooltip("Starting opacity for every graph. 0 = invisible, 1 = fully solid.")]
    [Range(0f, 1f)]
    public float graphOpacity = 0.65f;

    [Header("Startup")]
    [Tooltip("Leave OFF so the scene opens empty and nothing is plotted until the user " +
             "actually types an equation. Turn ON only for demos or screenshots.")]
    public bool plotOnStart = false;

    [Tooltip("Only used when Plot On Start is ticked.")]
    public string startupEquation = "sin(x) * cos(y)";

    [Header("References")]
    public AxisRenderer axisRenderer;
    public Transform graphContainer;

    // Per-slot state
    private GraphRenderer[] graphSlots = new GraphRenderer[MAX_EQUATIONS];
    private bool[] slotActive = new bool[MAX_EQUATIONS];
    private string[] slotEquations = new string[MAX_EQUATIONS];

    // Events the UI can subscribe to
    public System.Action<int, bool, string> OnSlotChanged; // slot, success, equation
    public System.Action<float> OnOpacityChanged;          // new opacity, 0..1

    void Awake()
    {
        InitSlots();
    }

    void Start()
    {
        // Deliberately empty by default: the user has not asked for a graph yet, so we
        // do not draw one. Flip plotOnStart in the Inspector if you want a demo surface.
        if (plotOnStart && !string.IsNullOrWhiteSpace(startupEquation))
            SetEquation(0, startupEquation);
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
            gr.opacity = graphOpacity;

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
        gr.SetOpacity(graphOpacity);   // a freshly plotted graph joins at the current fade level
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

    /// <summary>
    /// Fades every graph at once. 0 = invisible, 1 = solid. Safe to call from a UI
    /// slider on every frame it moves — it only rewrites a colour on each material.
    /// </summary>
    public void SetGlobalOpacity(float value)
    {
        graphOpacity = Mathf.Clamp01(value);

        for (int i = 0; i < MAX_EQUATIONS; i++)
        {
            if (graphSlots[i] != null) graphSlots[i].SetOpacity(graphOpacity);
        }

        OnOpacityChanged?.Invoke(graphOpacity);
    }

    public float GetGlobalOpacity() => graphOpacity;

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