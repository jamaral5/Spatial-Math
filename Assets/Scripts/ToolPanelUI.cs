using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shared shell for the corner tool buttons and the lists they open.
///
/// Handles the button, the panel, placement, hiding until the title screen is dismissed,
/// and making sure only one list is open at a time. Subclasses only say what the button
/// is called and what goes in the list.
/// </summary>
public abstract class ToolPanelUI : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    public Canvas targetCanvas;

    [Header("Button")]
    [Tooltip("Which corner of the screen the button hangs off. (0,1) is top-left.")]
    public Vector2 anchor = new Vector2(0f, 1f);

    public Vector2 buttonSize = new Vector2(300f, 52f);
    public float buttonFontSize = 20f;

    [Header("Stacking")]
    [Tooltip("Lay every tool button out in one column automatically, so two panels can " +
             "never land on the same spot. Turn off to position this one by hand.")]
    public bool autoStack = true;

    [Tooltip("Lower numbers sit higher in the column. Ties break on component name.")]
    public int stackOrder = 0;

    [Tooltip("Where the top button in the column starts, from the anchored corner.")]
    public Vector2 stackOrigin = new Vector2(16f, -212f);

    [Tooltip("Gap between stacked buttons, and between the column and the open list.")]
    public float stackSpacing = 8f;

    [Tooltip("Give every button in the column the same size — the largest any of them " +
             "asks for — so one long label does not leave the others looking clipped.")]
    public bool uniformButtonSize = true;

    [Header("Manual Placement (used when Auto Stack is off)")]
    public Vector2 buttonOffset = new Vector2(16f, -212f);
    public Vector2 panelOffset = new Vector2(16f, -336f);

    [Header("Panel")]
    public Vector2 panelSize = new Vector2(300f, 400f);
    public float entryFontSize = 17f;

    // Every open panel registers here so opening one closes the others.
    private static readonly List<ToolPanelUI> OpenPanels = new List<ToolPanelUI>();

    protected RectTransform panel;
    private RectTransform button;

    /// <summary>Text on the corner button.</summary>
    protected abstract string ButtonLabel { get; }

    /// <summary>Resolve references. Return false to disable the component.</summary>
    protected abstract bool Initialise();

    /// <summary>Fill the list. Called once, after Initialise.</summary>
    protected abstract void Populate(RectTransform column);

    private void Start()
    {
        // Statics survive a play session when Domain Reload is off, and would otherwise
        // hold destroyed panels from the previous run.
        OpenPanels.Clear();

        // A second copy of the same panel would build a second identical button on top
        // of the first, which is exactly what it looks like: one button, twice.
        if (UIKit.IsDuplicate(this))
        {
            enabled = false;
            return;
        }

        if (targetCanvas == null) targetCanvas = UIKit.FindSceneCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning($"[{GetType().Name}] No Canvas in the scene — disabling.");
            enabled = false;
            return;
        }

        if (!Initialise())
        {
            enabled = false;
            return;
        }

        Build();

        // Hidden until the user leaves the title screen, like the rest of the controls.
        button.gameObject.SetActive(false);
        StartScreen.WhenDismissed(() =>
        {
            if (button != null) button.gameObject.SetActive(true);
        });
    }

    private void OnDestroy()
    {
        OpenPanels.Remove(this);
    }

    /// <summary>Live layout tweaking during Play mode. See GraphOpacityUI.OnValidate.</summary>
    private void OnValidate()
    {
        if (!Application.isPlaying || button == null || panel == null) return;
        PlaceWidgets();
    }

    private void PlaceWidgets()
    {
        if (!autoStack)
        {
            UIKit.Corner(button, anchor, buttonSize, buttonOffset);
            UIKit.Corner(panel, anchor, panelSize, panelOffset);
            return;
        }

        ToolPanelUI[] column = StackOrder();

        // One size for the whole column, so "Customize Tangent Plane" and the shorter
        // labels beside it read as a matched set rather than as ragged boxes.
        Vector2 size = buttonSize;
        if (uniformButtonSize)
        {
            foreach (ToolPanelUI entry in column)
                size = Vector2.Max(size, entry.buttonSize);
        }

        // Walk down the column adding up the buttons above this one. Every instance runs
        // the same sum independently and arrives at the same layout, so no coordination
        // between them is needed and nothing can end up sharing a slot.
        float y = stackOrigin.y;
        float bottom = stackOrigin.y;

        foreach (ToolPanelUI entry in column)
        {
            if (entry == this) y = bottom;
            bottom -= (uniformButtonSize ? size.y : entry.buttonSize.y) + stackSpacing;
        }

        UIKit.Corner(button, anchor, size, new Vector2(stackOrigin.x, y));

        // The list opens below the WHOLE column, so an open panel never covers a button.
        UIKit.Corner(panel, anchor, panelSize, new Vector2(stackOrigin.x, bottom - stackSpacing));
    }

    /// <summary>
    /// Every tool panel in the scene, in a stable order: stackOrder first, then component
    /// name so the result never depends on scene load order.
    /// </summary>
    private static ToolPanelUI[] StackOrder()
    {
        ToolPanelUI[] panels = FindObjectsByType<ToolPanelUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        System.Array.Sort(panels, (a, b) =>
        {
            int byOrder = a.stackOrder.CompareTo(b.stackOrder);
            if (byOrder != 0) return byOrder;
            return string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
        });

        return panels;
    }

    private void Build()
    {
        Button toggle = UIKit.TextButton(GetType().Name + "Button", targetCanvas.transform,
                                         ButtonLabel, buttonFontSize, UIKit.NeonSoft);
        button = toggle.GetComponent<RectTransform>();
        toggle.onClick.AddListener(TogglePanel);

        panel = UIKit.NeonPanel(GetType().Name + "Panel", targetCanvas.transform,
                                UIKit.PanelDark, UIKit.NeonSoft);

        PlaceWidgets();

        RectTransform column = UIKit.Stretch(UIKit.NewRect("Column", panel), 12f);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;

        Populate(column);

        panel.gameObject.SetActive(false);
    }

    /// <summary>Adds a heading row to a list.</summary>
    protected void AddHeading(Transform parent, string text)
    {
        TextMeshProUGUI heading = UIKit.Label("Heading", parent, text.ToUpperInvariant(),
                                              11f, UIKit.Neon, TextAlignmentOptions.Left);
        heading.characterSpacing = 6f;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
    }

    /// <summary>
    /// Adds a clickable row. Returns its label so callers can rewrite it — which is how
    /// the on/off rows show their current state.
    /// </summary>
    protected TMP_Text AddEntry(Transform parent, string label, System.Action action)
    {
        Button entry = UIKit.TextButton(label, parent, label, entryFontSize,
                                        new Color(1f, 1f, 1f, 0.10f));
        entry.gameObject.AddComponent<LayoutElement>().preferredHeight = entryFontSize + 16f;
        entry.onClick.AddListener(() => action?.Invoke());

        return entry.GetComponentInChildren<TMP_Text>();
    }

    private void TogglePanel()
    {
        if (panel == null) return;
        SetOpen(!panel.gameObject.activeSelf);
    }

    protected void SetOpen(bool open)
    {
        if (panel == null) return;

        if (open)
        {
            // Close every other tool list first: they share one slot on screen, so two
            // open at once would sit directly on top of each other.
            for (int i = OpenPanels.Count - 1; i >= 0; i--)
            {
                ToolPanelUI other = OpenPanels[i];
                if (other == null || other == this) continue;
                other.SetOpen(false);
            }

            if (!OpenPanels.Contains(this)) OpenPanels.Add(this);
        }
        else
        {
            OpenPanels.Remove(this);
        }

        panel.gameObject.SetActive(open);
    }

    public void ClosePanel() => SetOpen(false);
}
