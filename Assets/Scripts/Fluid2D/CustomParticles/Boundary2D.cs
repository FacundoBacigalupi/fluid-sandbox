using UnityEngine;

// Define un área rectangular. Cuando una partícula sale del área, rebota.
// Restitution: cuánta velocidad conserva al rebotar (1 = perfecta, 0 = queda pegada).
// Damping:     multiplicador aplicado a la velocidad tangencial en el choque (simulá fricción de pared).
[AddComponentMenu("FluidSandbox/Boundary 2D")]
public class Boundary2D : MonoBehaviour
{
    public Vector2 size        = new Vector2(8f, 6f);
    [Range(0f, 1f)] public float restitution = 0.4f;
    [Range(0f, 1f)] public float damping     = 0.98f;

    // Resuelve colisión para una sola partícula. Se llama desde el solver cada paso.
    public void Resolve(ref FluidParticle2D p, float radius)
    {
        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;

        // Pared izquierda / derecha
        if (p.Position.x - radius < -hw)
        {
            p.Position.x =  -hw + radius;
            p.Velocity.x =  Mathf.Abs(p.Velocity.x) * restitution;
            p.Velocity.y *= damping;
        }
        else if (p.Position.x + radius > hw)
        {
            p.Position.x =  hw - radius;
            p.Velocity.x = -Mathf.Abs(p.Velocity.x) * restitution;
            p.Velocity.y *= damping;
        }

        // Piso / techo
        if (p.Position.y - radius < -hh)
        {
            p.Position.y =  -hh + radius;
            p.Velocity.y =  Mathf.Abs(p.Velocity.y) * restitution;
            p.Velocity.x *= damping;
        }
        else if (p.Position.y + radius > hh)
        {
            p.Position.y =  hh - radius;
            p.Velocity.y = -Mathf.Abs(p.Velocity.y) * restitution;
            p.Velocity.x *= damping;
        }
    }

    public Rect GetRect() => new Rect(
        transform.position.x - size.x * 0.5f,
        transform.position.y - size.y * 0.5f,
        size.x, size.y);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.4f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
