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

        // Unity's mesh raycasts skip back faces by default, and that quietly broke
        // picking on any surface that curves away from you.
        //
        // The graph is a height field, so RecalculateNormals points every normal broadly
        // upward. On a bowl like x^2 + y^2 that means the wall NEAREST the camera has its
        // normal facing away from you — it is a back face — so the ray passed straight
        // through it and reported the far wall instead. Clicking the front of the bowl
        // gave you a tangent plane on the back.
        //
        // Letting queries hit back faces makes the closest surface win, which is what a
        // click on a surface is supposed to mean.
        Physics.queriesHitBackfaces = true;
    }

    void Update()
    {
        // Nothing is interactive until the user has left the title screen.
        if (StartScreen.Exists && !StartScreen.Dismissed) return;

        // Only act on the frame the right mouse button is first pressed down.
        if (!Input.GetMouseButtonDown(1)) return;

        // Ignore clicks that landed on the UI (the equation box, the opacity slider,
        // the context menu itself).
        if (PointerOverUI.AtMouse()) return;
        if (mainCamera == null) return;

        // Build a ray from the camera through the mouse position on screen.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // RaycastAll rather than Raycast, because the graph is not the only collider in
        // the scene: the tangent plane and the marker ball both have one. A plain
        // Raycast returns whichever is nearest, so once a tangent plane was on screen it
        // sat between the camera and the surface and swallowed every later click.
        //
        // Collect everything, then take the nearest hit that is actually a graph.
        RaycastHit[] hits = Physics.RaycastAll(ray);

        GraphRenderer hitGraph = null;
        float nearest = float.MaxValue;
        Vector3 nearestPoint = Vector3.zero;

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null) continue;

            // Asking the collider whether it is a graph surface also keeps this correct
            // when several equations are on screen: we read slopes from the SAME surface
            // the user clicked, not just the first one found.
            var graph = candidate.collider.GetComponent<GraphRenderer>();
            if (graph == null || candidate.distance >= nearest) continue;

            nearest = candidate.distance;
            nearestPoint = candidate.point;
            hitGraph = graph;
        }

        if (hitGraph == null)
        {
            if (contextMenu != null) contextMenu.Close();
            return;
        }

        candidatePoint = nearestPoint;
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
