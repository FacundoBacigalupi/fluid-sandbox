using UnityEngine;

// Phase 3 — Custom 2D Particle Simulation
// Cada partícula es puro dato, sin GameObject. El solver actualiza el array completo.
// Density y Pressure están reservados para las fases 4 y 5 (SPH).
public struct FluidParticle2D
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float   Density;   // calculado en fase 5
    public float   Pressure;  // calculado en fase 5
}
