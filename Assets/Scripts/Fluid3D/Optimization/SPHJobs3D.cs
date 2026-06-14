// Jobs SPH con Burst Compiler para paralelismo multi-core + SIMD.
//
// Brute-force O(N²): SPHDensityJob, SPHForceJob, SPHIntegrateJob.
// Spatial Hash O(N×k): SPHBuildHashMapJob, SPHDensityJobSH, SPHForceJobSH + SPHIntegrateJob.
//   NativeParallelMultiHashMap limita la búsqueda a las 27 celdas vecinas (~80% menos trabajo).

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// ── Densidad ─────────────────────────────────────────────────────────────────

[BurstCompile]
public struct SPHDensityJob : IJobParallelFor
{
    [ReadOnly]  public NativeArray<float3> positions;
    [WriteOnly] public NativeArray<float>  densities;
    public float h;

    public void Execute(int i)
    {
        float3 pi = positions[i];
        float  d  = 0f;
        for (int j = 0; j < positions.Length; j++)
        {
            float r = math.length(positions[j] - pi);
            if (r < h) { float t = 1f - r / h; d += t * t * t; }
        }
        densities[i] = math.max(d, 0.001f);
    }
}

// ── Fuerzas SPH ──────────────────────────────────────────────────────────────

[BurstCompile]
public struct SPHForceJob : IJobParallelFor
{
    [ReadOnly]  public NativeArray<float3> positions;
    [ReadOnly]  public NativeArray<float3> velocities;
    [ReadOnly]  public NativeArray<float>  densities;
    [WriteOnly] public NativeArray<float3> forces;

    public float h;
    public float restDensity;
    public float pressureMult;
    public float viscosity;

    public void Execute(int i)
    {
        float3 pi  = positions[i];
        float3 vi  = velocities[i];
        float  di  = densities[i];
        float  prI = pressureMult * math.max(0f, di - restDensity);

        float3 pf = float3.zero;
        float3 vf = float3.zero;

        for (int j = 0; j < positions.Length; j++)
        {
            if (j == i) continue;
            float3 rij = pi - positions[j];
            float  r   = math.length(rij);
            if (r < 0.0001f || r >= h) continue;

            float  dj    = densities[j];
            float  prJ   = pressureMult * math.max(0f, dj - restDensity);
            float  t     = 1f - r / h;
            float3 rhat  = rij / r;
            float  grad  = -3f * t * t / h;   // gradiente kernel cúbico

            pf += -(prI + prJ) * 0.5f / (di * dj) * grad * rhat;
            vf += (velocities[j] - vi) / dj * (t * t * t);
        }

        forces[i] = pf + viscosity * vf;
    }
}

// ── Integración + Boundary ───────────────────────────────────────────────────

[BurstCompile]
public struct SPHIntegrateJob : IJobParallelFor
{
    public NativeArray<float3> positions;
    public NativeArray<float3> velocities;
    [ReadOnly] public NativeArray<float3> forces;

    public float3 gravity;
    public float  dt;
    public float  maxVel;
    public float3 bMin;       // boundary mínimo (world space)
    public float3 bMax;       // boundary máximo (world space)
    public float  radius;
    public float  restitution;
    public float  damping;

    public void Execute(int i)
    {
        float3 v = velocities[i] + (gravity + forces[i]) * dt;
        float spd = math.length(v);
        if (spd > maxVel) v = v / spd * maxVel;

        float3 p = positions[i] + v * dt;
        float  r = radius;

        // Resolver colisión con cada pared
        if (p.x < bMin.x + r) { p.x = bMin.x + r; v.x =  math.abs(v.x) * restitution; v *= damping; }
        if (p.x > bMax.x - r) { p.x = bMax.x - r; v.x = -math.abs(v.x) * restitution; v *= damping; }
        if (p.y < bMin.y + r) { p.y = bMin.y + r; v.y =  math.abs(v.y) * restitution; v *= damping; }
        if (p.y > bMax.y - r) { p.y = bMax.y - r; v.y = -math.abs(v.y) * restitution; v *= damping; }
        if (p.z < bMin.z + r) { p.z = bMin.z + r; v.z =  math.abs(v.z) * restitution; v *= damping; }
        if (p.z > bMax.z - r) { p.z = bMax.z - r; v.z = -math.abs(v.z) * restitution; v *= damping; }

        if (math.any(math.isnan(p))) { p = float3.zero; v = float3.zero; }

        positions[i]  = p;
        velocities[i] = v;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Versión B — Spatial Hash O(N×k)
// ═══════════════════════════════════════════════════════════════════════════

// ── Build spatial hash ────────────────────────────────────────────────────

[BurstCompile]
public struct SPHBuildHashMapJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    public NativeParallelMultiHashMap<int, int>.ParallelWriter hashMap;
    public float h;

    public void Execute(int i) => hashMap.Add(CellHash(GetCell(positions[i], h)), i);

    internal static int3 GetCell(float3 pos, float h) => new int3(math.floor(pos / h));
    internal static int  CellHash(int3 c)             => math.abs(c.x * 73856093 ^ c.y * 19349663 ^ c.z * 83492791);
}

// ── Densidad con Spatial Hash ─────────────────────────────────────────────

[BurstCompile]
public struct SPHDensityJobSH : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [WriteOnly] public NativeArray<float> densities;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> hashMap;
    public float h;

    public void Execute(int i)
    {
        float3 pi   = positions[i];
        int3   cell = SPHBuildHashMapJob.GetCell(pi, h);
        float  d    = 0f;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            int key = SPHBuildHashMapJob.CellHash(new int3(cell.x + dx, cell.y + dy, cell.z + dz));
            if (!hashMap.TryGetFirstValue(key, out int j, out var it)) continue;
            do
            {
                float r = math.length(positions[j] - pi);
                if (r < h) { float t = 1f - r / h; d += t * t * t; }
            }
            while (hashMap.TryGetNextValue(out j, ref it));
        }

        densities[i] = math.max(d, 0.001f);
    }
}

// ── Fuerzas con Spatial Hash ──────────────────────────────────────────────

[BurstCompile]
public struct SPHForceJobSH : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;
    [ReadOnly] public NativeArray<float>  densities;
    [WriteOnly] public NativeArray<float3> forces;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> hashMap;
    public float h, restDensity, pressureMult, viscosity;

    public void Execute(int i)
    {
        float3 pi  = positions[i];
        float3 vi  = velocities[i];
        float  di  = densities[i];
        float  prI = pressureMult * math.max(0f, di - restDensity);
        int3   cell = SPHBuildHashMapJob.GetCell(pi, h);

        float3 pf = float3.zero, vf = float3.zero;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            int key = SPHBuildHashMapJob.CellHash(new int3(cell.x + dx, cell.y + dy, cell.z + dz));
            if (!hashMap.TryGetFirstValue(key, out int j, out var it)) continue;
            do
            {
                if (j == i) continue;
                float3 rij = pi - positions[j];
                float  r   = math.length(rij);
                if (r < 0.0001f || r >= h) continue;

                float  dj   = densities[j];
                float  prJ  = pressureMult * math.max(0f, dj - restDensity);
                float  t    = 1f - r / h;
                float3 rhat = rij / r;
                float  grad = -3f * t * t / h;

                pf += -(prI + prJ) * 0.5f / (di * dj) * grad * rhat;
                vf += (velocities[j] - vi) / dj * (t * t * t);
            }
            while (hashMap.TryGetNextValue(out j, ref it));
        }

        forces[i] = pf + viscosity * vf;
    }
}
