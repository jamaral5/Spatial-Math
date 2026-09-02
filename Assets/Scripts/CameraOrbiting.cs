using UnityEngine;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Distance")]
    public float distance = 10f;
    public float minDistance = 3f;
    public float maxDistance = 20f;
    public float zoomSpeed = 5f;

    [Header("Rotation")]
    public float xSpeed = 120f;
    public float ySpeed = 120f;
    public float minYAngle = -20f;
    public float maxYAngle = 80f;

    private float xAngle = 0f;
    private float yAngle = 20f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        xAngle = angles.y;
        yAngle = angles.x;

        if (target == null)
        {
            Debug.LogWarning("OrbitCameraController: No target assigned.");
        }

        UpdateCameraPosition();
    }

    void Update()
    {
        if (target == null) return;

        // Left mouse drag to rotate
        if (Input.GetMouseButton(0))
        {
            xAngle += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            yAngle -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            yAngle = Mathf.Clamp(yAngle, minYAngle, maxYAngle);
        }

        // Scroll wheel to zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(yAngle, xAngle, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}