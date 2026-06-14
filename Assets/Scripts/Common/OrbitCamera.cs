using UnityEngine;

// Cámara orbital para escenas 3D.
// RMB drag = orbitar   |   Scroll = zoom   |   MMB drag = pan
[AddComponentMenu("FluidSandbox/Orbit Camera")]
public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Órbita")]
    public float yaw         = 30f;
    public float pitch       = 25f;
    public float distance    = 10f;

    [Header("Velocidad")]
    [Range(50f,  400f)] public float orbitSpeed = 200f;
    [Range(1f,   20f)]  public float zoomSpeed  = 5f;
    [Range(0.1f, 5f)]   public float panSpeed   = 2f;

    [Header("Límites")]
    [Range(1f,  30f)]  public float minDistance = 2f;
    [Range(5f,  60f)]  public float maxDistance = 25f;
    [Range(-80f, 0f)]  public float minPitch    = -10f;
    [Range(5f,  89f)]  public float maxPitch    = 80f;

    private Vector3 _panOffset;

    void LateUpdate()
    {
        // Órbita — botón derecho
        if (Input.GetMouseButton(1))
        {
            yaw   += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Pan — botón central
        if (Input.GetMouseButton(2))
        {
            _panOffset -= transform.right   * Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime;
            _panOffset -= transform.up      * Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;
        }

        // Zoom — rueda del mouse
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

        // Aplicar
        Vector3 pivot = (target != null ? target.position : Vector3.zero) + _panOffset;
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = pivot - rot * Vector3.forward * distance;
        transform.rotation = rot;
    }

    [ContextMenu("Reset Pan")]
    void ResetPan() => _panOffset = Vector3.zero;
}
