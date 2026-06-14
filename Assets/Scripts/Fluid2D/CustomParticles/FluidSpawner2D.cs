using UnityEngine;

// Inicializa el array de partículas con posiciones de inicio.
// Dos patrones: Grid (cuadrícula ordenada) y Random (posiciones al azar).
// El solver llama a Spawn() al inicio y al resetear.
[AddComponentMenu("FluidSandbox/Fluid Spawner 2D")]
public class FluidSpawner2D : MonoBehaviour
{
    public enum Pattern { Grid, Random }

    public Pattern spawnPattern = Pattern.Grid;
    public Vector2 spawnArea    = new Vector2(5f, 4f);

    // Rellena el array con posiciones iniciales según el patrón elegido.
    public void Spawn(FluidParticle2D[] particles)
    {
        if (spawnPattern == Pattern.Grid)
            SpawnGrid(particles);
        else
            SpawnRandom(particles);
    }

    void SpawnGrid(FluidParticle2D[] particles)
    {
        int n    = particles.Length;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
        int rows = Mathf.CeilToInt((float)n / cols);

        float dx     = spawnArea.x / cols;
        float dy     = spawnArea.y / rows;
        Vector2 origin = (Vector2)transform.position - spawnArea * 0.5f;

        for (int i = 0; i < n; i++)
        {
            int col = i % cols;
            int row = i / cols;
            particles[i] = new FluidParticle2D
            {
                Position = origin + new Vector2(col * dx + dx * 0.5f, row * dy + dy * 0.5f),
                Velocity = Vector2.zero
            };
        }
    }

    void SpawnRandom(FluidParticle2D[] particles)
    {
        Vector2 origin = transform.position;
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i] = new FluidParticle2D
            {
                Position = origin + new Vector2(
                    Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
                    Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f)),
                Velocity = Vector2.zero
            };
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
    }
}
