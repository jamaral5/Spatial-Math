using UnityEngine;

/// <summary>
/// Turns a right-click on the graph into a point selection.
///
/// Right-click fires a ray into the scene; if it lands on a graph surface, the marker
/// moves there and a small menu opens offering to render the tangent plane. Nothing is
/// drawn until the user picks that entry — left-click is left entirely to the camera, so
/// orbiting the view no longer scatters tangent planes across the surface.
/// </summary>
public class SurfacePointSelector : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject marker;
    public TangentPlaneRenderer tangentPlaneRenderer;

    [Tooltip("Found automatically if left empty.")]
    public TangentContextMenu contextMenu;

    // The point the user right-clicked, held until they choose an action.
    private Vector3 candidatePoint;
    private GraphRenderer candidateGraph;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (contextMenu == null) contextMenu = FindFirstObjectByType<TangentContextMenu>();
    }

    void Update()
    {
        // Only act on the frame the right mouse button is first pressed down.
        if (!Input.GetMouseButtonDown(1)) return;

        // Ignore clicks that landed on the UI (the equation box, the opacity slider,
        // the context menu itself).
        if (PointerOverUI.AtMouse()) return;
        if (mainCamera == null) return;

        // Build a ray from the camera through the mouse position on screen.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Raycast returns true if the ray struck a collider; 'hit' is filled with
        // details (the world point, and which object/collider we touched).
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            if (contextMenu != null) contextMenu.Close();
            return;
        }

        // Ask the object we hit whether it is a graph surface. If you ever show several
        // equations at once, this guarantees we read slopes from the SAME surface the
        // user actually clicked, not just the first one found.
        GraphRenderer hitGraph = hit.collider != null
            ? hit.collider.GetComponent<GraphRenderer>()
            : null;

        if (hitGraph == null)
        {
            if (contextMenu != null) contextMenu.Close();
            return;
        }

        candidatePoint = hit.point;
        candidateGraph = hitGraph;

        // Show the marker straight away so it is obvious which point the menu refers to.
        if (marker != null)
        {
            marker.SetActive(true);
            marker.transform.position = candidatePoint;
        }

        if (contextMenu != null)
            contextMenu.Open(Input.mousePosition, RenderTangentPlane, ClearTangentPlane);
        else
            RenderTangentPlane();   // no menu wired up: fall back to the old behaviour
    }

    private void RenderTangentPlane()
    {
        if (tangentPlaneRenderer != null)
            tangentPlaneRenderer.ShowTangentPlaneAt(candidatePoint, candidateGraph);
    }

    private void ClearTangentPlane()
    {
        if (tangentPlaneRenderer != null)
            tangentPlaneRenderer.ClearTangentPlane();
    }
}
