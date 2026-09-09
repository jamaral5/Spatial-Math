using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The small menu that appears where you right-click the surface.
///
/// Splitting "pick a point" from "render the plane" means a stray click while orbiting no
/// longer litters the graph with tangent planes — the user has to ask for one.
/// </summary>
[AddComponentMenu("Spatial Math/Tangent Context Menu")]
public class TangentContextMenu : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    public Canvas targetCanvas;

    [Header("Layout")]
    public Vector2 menuSize = new Vector2(190f, 76f);

    private RectTransform menu;
    private System.Action onRender;
    private System.Action onClear;

    // The frame Open() was called on. Script execution order between this component and
    // SurfacePointSelector is undefined, so without this a right-click that reopens the
    // menu somewhere else could be dismissed by the very click that opened it.
    private int openedFrame = -1;

    public bool IsOpen => menu != null && menu.gameObject.activeSelf;

    void Start()
    {
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[TangentContextMenu] No Canvas in the scene — disabling.");
            enabled = false;
            return;
        }

        Build();
        Close();
    }

    void Update()
    {
        if (!IsOpen) return;
        if (openedFrame == Time.frameCount) return;   // opened this very frame

        // Any click that is not on the menu itself dismisses it, which is what every
        // context menu does and saves needing a cancel button.
        bool clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        if (clicked && !PointerOverUI.AtMouse())
            Close();
    }

    /// <summary>Opens the menu at a screen position, wiring up what each entry does.</summary>
    public void Open(Vector2 screenPosition, System.Action renderAction, System.Action clearAction)
    {
        if (menu == null) return;

        onRender = renderAction;
        onClear = clearAction;

        menu.gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        PositionAt(screenPosition);
    }

    public void Close()
    {
        if (menu != null) menu.gameObject.SetActive(false);
    }

    /// <summary>
    /// Places the menu at the cursor, nudged back inside the screen if it would hang off
    /// the right or bottom edge.
    /// </summary>
    private void PositionAt(Vector2 screenPosition)
    {
        var canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null) return;

        Camera cam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, cam, out Vector2 local))
            return;

        // Pivot is top-left, so the menu grows right and down from the cursor.
        float maxX = canvasRect.rect.xMax - menuSize.x;
        float minY = canvasRect.rect.yMin + menuSize.y;

        local.x = Mathf.Min(local.x, maxX);
        local.y = Mathf.Max(local.y, minY);

        menu.anchoredPosition = local;
    }

    private void Build()
    {
        menu = UIKit.NeonPanel("TangentContextMenu", targetCanvas.transform,
                               UIKit.PanelDark, UIKit.NeonSoft);

        // Anchored to the canvas centre so ScreenPointToLocalPointInRectangle results
        // can be assigned to anchoredPosition directly.
        menu.anchorMin = menu.anchorMax = new Vector2(0.5f, 0.5f);
        menu.pivot = new Vector2(0f, 1f);
        menu.sizeDelta = menuSize;

        RectTransform column = UIKit.Stretch(UIKit.NewRect("Column", menu), 8f);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4f;

        AddEntry(column, "Render tangent plane", () => onRender?.Invoke());
        AddEntry(column, "Clear tangent plane", () => onClear?.Invoke());
    }

    private void AddEntry(Transform parent, string label, System.Action action)
    {
        Button button = UIKit.TextButton(label, parent, label, 14f, new Color(1f, 1f, 1f, 0.10f));
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

        button.onClick.AddListener(() =>
        {
            action?.Invoke();
            Close();
        });
    }
}
