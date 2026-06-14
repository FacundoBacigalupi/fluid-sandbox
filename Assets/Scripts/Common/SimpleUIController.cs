using UnityEngine;

// Phase 1 & 2 — Presets de material para el spawner de rigidbodies.
// Usá clic derecho en este componente en el Inspector para aplicar un preset.
// Todos los parámetros se controlan directamente en el componente RigidbodyParticleSpawner2D.
[AddComponentMenu("FluidSandbox/Simple UI Controller")]
public class SimpleUIController : MonoBehaviour
{
    public RigidbodyParticleSpawner2D spawner;

    [ContextMenu("Preset / Water")]
    void PresetWater()
    {
        if (spawner == null) return;
        spawner.bounciness   = 0.05f;
        spawner.friction     = 0.02f;
        spawner.linearDrag   = 0.3f;
        spawner.gravityScale = 1f;
        spawner.ApplyPhysicsToAll();
    }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()
    {
        if (spawner == null) return;
        spawner.bounciness   = 0.0f;
        spawner.friction     = 0.3f;
        spawner.linearDrag   = 1.8f;
        spawner.gravityScale = 1f;
        spawner.ApplyPhysicsToAll();
    }

    [ContextMenu("Preset / Bouncy")]
    void PresetBouncy()
    {
        if (spawner == null) return;
        spawner.bounciness   = 0.85f;
        spawner.friction     = 0.01f;
        spawner.linearDrag   = 0.05f;
        spawner.gravityScale = 1f;
        spawner.ApplyPhysicsToAll();
    }
}
