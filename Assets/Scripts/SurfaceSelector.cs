using UnityEngine;

/// <summary>
/// Fires a ray from the mouse into the scene on left-click. If it hits a graph
/// surface, it hands that exact point — and the specific graph that was hit — to
/// the TangentPlaneRenderer, which draws the tangent plane, lines, and equation.
/// </summary>
public class SurfacePointSelector : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject marker;
    public TangentPlaneRenderer tangentPlaneRenderer;

    void Update()
    {
        // Only act on the frame the left mouse button is first pressed down.
        if (Input.GetMouseButtonDown(0))
        {
            // Build a ray from the camera through the mouse position on screen.
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Raycast returns true if the ray struck a collider; 'hit' is filled with
            // details (the world point, and which object/collider we touched).
            if (Physics.Raycast(ray, out hit))
            {
                if (marker != null)
                {
                    marker.SetActive(true);
                    marker.transform.position = hit.point;
                }

                // Ask the object we hit whether it is a graph surface. If you ever show
                // several equations at once, this guarantees we read slopes from the
                // SAME surface the user actually clicked, not just the first one found.
                GraphRenderer hitGraph = hit.collider != null
                    ? hit.collider.GetComponent<GraphRenderer>()
                    : null;

                if (tangentPlaneRenderer != null)
                {
                    tangentPlaneRenderer.ShowTangentPlaneAt(hit.point, hitGraph);
                }
            }
        }
    }
}
