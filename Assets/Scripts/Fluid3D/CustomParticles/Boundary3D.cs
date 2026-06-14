using UnityEngine;

// Define un volumen cúbico centrado en el origen del GameObject.
// Resuelve colisiones de partículas custom 3D contra las 6 caras.
[AddComponentMenu("FluidSandbox/Boundary 3D")]
public class Boundary3D : MonoBehaviour
{
    public Vector3 size = new Vector3(6f, 5f, 6f);
    [Range(0f, 1f)] public float restitution = 0.4f;
    [Range(0f, 1f)] public float damping     = 0.98f;

    public void Resolve(ref FluidParticle3D p, float radius)
    {
        Vector3 center = transform.position;
        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        float hd = size.z * 0.5f;

        // Eje X
        if (p.Position.x - radius < center.x - hw)
        {
            p.Position.x =  center.x - hw + radius;
            p.Velocity.x =  Mathf.Abs(p.Velocity.x) * restitution;
            p.Velocity.y *= damping; p.Velocity.z *= damping;
        }
        else if (p.Position.x + radius > center.x + hw)
        {
            p.Position.x =  center.x + hw - radius;
            p.Velocity.x = -Mathf.Abs(p.Velocity.x) * restitution;
            p.Velocity.y *= damping; p.Velocity.z *= damping;
        }

        // Eje Y
        if (p.Position.y - radius < center.y - hh)
        {
            p.Position.y =  center.y - hh + radius;
            p.Velocity.y =  Mathf.Abs(p.Velocity.y) * restitution;
            p.Velocity.x *= damping; p.Velocity.z *= damping;
        }
        else if (p.Position.y + radius > center.y + hh)
        {
            p.Position.y =  center.y + hh - radius;
            p.Velocity.y = -Mathf.Abs(p.Velocity.y) * restitution;
            p.Velocity.x *= damping; p.Velocity.z *= damping;
        }

        // Eje Z
        if (p.Position.z - radius < center.z - hd)
        {
            p.Position.z =  center.z - hd + radius;
            p.Velocity.z =  Mathf.Abs(p.Velocity.z) * restitution;
            p.Velocity.x *= damping; p.Velocity.y *= damping;
        }
        else if (p.Position.z + radius > center.z + hd)
        {
            p.Position.z =  center.z + hd - radius;
            p.Velocity.z = -Mathf.Abs(p.Velocity.z) * restitution;
            p.Velocity.x *= damping; p.Velocity.y *= damping;
        }
    }

    public Bounds GetBounds() => new Bounds(transform.position, size);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.35f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
