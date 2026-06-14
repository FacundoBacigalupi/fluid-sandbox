using UnityEngine;

// Inicializa el array 3D con posiciones de inicio.
// Grid = cuadrícula 3D ordenada.  Random = posiciones al azar en el volumen.
[AddComponentMenu("FluidSandbox/Fluid Spawner 3D")]
public class FluidSpawner3D : MonoBehaviour
{
    public enum Pattern { Grid, Random }

    public Pattern spawnPattern = Pattern.Grid;
    public Vector3 spawnArea    = new Vector3(4f, 2f, 4f);

    public void Spawn(FluidParticle3D[] particles)
    {
        if (spawnPattern == Pattern.Grid)
            SpawnGrid(particles);
        else
            SpawnRandom(particles);
    }

    void SpawnGrid(FluidParticle3D[] particles)
    {
        int n = particles.Length;
        // Distribuir en cubo: resolver dimensiones enteras
        int nx = Mathf.CeilToInt(Mathf.Pow(n, 1f / 3f));
        int ny = nx;
        int nz = Mathf.CeilToInt((float)n / (nx * ny));

        float dx = nx > 1 ? spawnArea.x / (nx - 1) : 0f;
        float dy = ny > 1 ? spawnArea.y / (ny - 1) : 0f;
        float dz = nz > 1 ? spawnArea.z / (nz - 1) : 0f;

        Vector3 origin = (Vector3)transform.position - spawnArea * 0.5f;

        int idx = 0;
        for (int z = 0; z < nz && idx < n; z++)
            for (int y = 0; y < ny && idx < n; y++)
                for (int x = 0; x < nx && idx < n; x++, idx++)
                    particles[idx] = new FluidParticle3D
                    {
                        Position = origin + new Vector3(x * dx, y * dy, z * dz),
                        Velocity = Vector3.zero
                    };
    }

    void SpawnRandom(FluidParticle3D[] particles)
    {
        Vector3 center = transform.position;
        for (int i = 0; i < particles.Length; i++)
            particles[i] = new FluidParticle3D
            {
                Position = center + new Vector3(
                    Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
                    Random.Range(0f,                   spawnArea.y),
                    Random.Range(-spawnArea.z * 0.5f, spawnArea.z * 0.5f)),
                Velocity = Vector3.zero
            };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, spawnArea);
    }
}
