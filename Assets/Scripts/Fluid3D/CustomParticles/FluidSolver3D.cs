using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = UnityEngine.Random;

// Simulación SPH 3D: integración Euler, boundary, densidad, presión, viscosidad.
// Space = agregar batch de partículas | R = resetear
[AddComponentMenu("FluidSandbox/Fluid Solver 3D")]
public class FluidSolver3D : MonoBehaviour
{
    [Header("Referencias")]
    public FluidSpawner3D         spawner;
    public Boundary3D             boundary;
    public ParticleRenderer3D     particleRenderer;   // esferas sólidas (debug)
    public FluidSurfaceRenderer3D surfaceRenderer;    // look de agua (Phase 9 visual)

    [Header("Simulación")]
    public int     particleCount  = 500;
    [Range(0.03f, 0.4f)] public float   particleRadius = 0.08f;
    public Vector3 gravity        = new Vector3(0f, -12f, 0f);
    [Range(1, 8)]        public int     substeps        = 3;
    [Range(1f, 40f)]     public float   maxVelocity     = 20f;

    [Header("Neighbor Search")]
    [Range(0.1f, 1.5f)]  public float smoothingRadius = 0.35f;

    [Header("Fluid — SPH")]
    [Range(0f, 15f)]     public float restDensity        = 6f;
    [Range(0f, 600f)]    public float pressureMultiplier = 100f;
    [Range(0f, 2f)]      public float viscosityStrength  = 0.3f;

    [Header("Space — Batch Spawn")]
    [Range(10, 200)]     public int   batchSize     = 50;

    [Header("Mouse — Spawn")]
    [Range(1, 10)]       public int   spawnCount    = 3;
    [Range(0.04f, 0.5f)] public float spawnInterval = 0.08f;
    [Range(0f, 5f)]      public float spawnDepth    = 2f;

    [Header("Mouse — Push")]
    [Range(0.2f, 5f)]    public float mouseRadius  = 1.5f;
    [Range(1f, 30f)]     public float pushStrength = 15f;

    [Header("Optimización — Job System + Burst")]
    [Tooltip("Usa Unity Job System + Burst Compiler para SPH paralelo. Permite 2000+ partículas.")]
    public bool useJobSystem = false;
    [Tooltip("O(N×k) Spatial Hash en lugar de O(N²) brute-force. Activo solo si useJobSystem=true. Permite 5000-10000 partículas.")]
    public bool useSpatialHash = false;

    private FluidParticle3D[] _particles;
    private Vector3[]         _forces = System.Array.Empty<Vector3>();

    public FluidParticle3D[] Particles => _particles;

    private readonly SpatialHashGrid3D _grid      = new SpatialHashGrid3D();
    private readonly List<int>         _neighbors = new List<int>(64);

    private float  _spawnTimer;
    private Camera _cam;

    // NativeArrays para el Job System (allocados cuando useJobSystem = true)
    NativeArray<float3> _nPos;
    NativeArray<float3> _nVel;
    NativeArray<float>  _nDen;
    NativeArray<float3> _nFor;
    NativeParallelMultiHashMap<int, int> _hashMap;
    int _nativeCapacity = 0;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        // Auto-find references si el Inspector las perdió (problema de serialización de escena)
        if (spawner          == null) spawner          = FindFirstObjectByType<FluidSpawner3D>();
        if (boundary         == null) boundary         = FindFirstObjectByType<Boundary3D>();
        if (particleRenderer == null) particleRenderer = FindFirstObjectByType<ParticleRenderer3D>();
        if (surfaceRenderer  == null) surfaceRenderer  = FindFirstObjectByType<FluidSurfaceRenderer3D>();

        if (spawner          == null) Debug.LogWarning("[FluidSolver3D] spawner no encontrado", this);
        if (boundary         == null) Debug.LogWarning("[FluidSolver3D] boundary no encontrado", this);

        InitParticles();
    }

    void Update()
    {
        _cam ??= Camera.main;

        if (Input.GetKeyDown(KeyCode.Space)) SpawnBatch(batchSize);
        if (Input.GetKeyDown(KeyCode.R))     InitParticles();

        surfaceRenderer?.Render(_particles, particleRadius);
        particleRenderer?.Render(_particles, particleRadius);

        HandleMouse();
    }

    void FixedUpdate()
    {
        if (_particles == null) return;
        float dt = Time.fixedDeltaTime / substeps;
        for (int s = 0; s < substeps; s++)
        {
            if (useJobSystem) StepJobs(dt);
            else              Step(dt);
        }
    }

    void OnDestroy() => DisposeNativeArrays();

    // ── Simulación SPH ───────────────────────────────────────────────────────

    void Step(float dt)
    {
        float h = smoothingRadius;
        _grid.Build(_particles, h);
        ComputeDensities(h);
        ComputeForces(h);

        for (int i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            p.Velocity += (gravity + _forces[i]) * dt;
            p.Velocity  = Vector3.ClampMagnitude(p.Velocity, maxVelocity);
            p.Position += p.Velocity * dt;
            boundary?.Resolve(ref p, particleRadius);
            // Sanidad: si SPH produce NaN, resetear al origen para no perder la partícula
            if (float.IsNaN(p.Position.x) || float.IsNaN(p.Position.y) || float.IsNaN(p.Position.z))
            { p.Position = Vector3.zero; p.Velocity = Vector3.zero; }
        }
    }

    // Kernel cúbico: W(r,h) = (1 - r/h)³  — idéntico al 2D, radialmente simétrico
    static float K(float r, float h)
    {
        if (r >= h) return 0f;
        float t = 1f - r / h;
        return t * t * t;
    }
    static float KGrad(float r, float h)
    {
        if (r >= h) return 0f;
        float t = 1f - r / h;
        return -3f * t * t / h;
    }

    void ComputeDensities(float h)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            _grid.Query(_particles[i].Position, h, _neighbors);
            float d = 0f;
            foreach (int j in _neighbors)
                d += K((_particles[i].Position - _particles[j].Position).magnitude, h);
            _particles[i].Density = Mathf.Max(d, 0.001f);
        }
    }

    void ComputeForces(float h)
    {
        for (int i = 0; i < _particles.Length; i++) _forces[i] = Vector3.zero;

        for (int i = 0; i < _particles.Length; i++)
        {
            float   di = _particles[i].Density;
            float   pi = pressureMultiplier * Mathf.Max(0f, di - restDensity);
            _grid.Query(_particles[i].Position, h, _neighbors);

            Vector3 pressureF  = Vector3.zero;
            Vector3 viscosityF = Vector3.zero;

            foreach (int j in _neighbors)
            {
                if (j == i) continue;
                Vector3 rij = _particles[i].Position - _particles[j].Position;
                float   r   = rij.magnitude;
                if (r < 0.0001f || r >= h) continue;

                float   dj   = _particles[j].Density;
                float   pj   = pressureMultiplier * Mathf.Max(0f, dj - restDensity);
                Vector3 rhat = rij / r;
                float   grad = KGrad(r, h);

                pressureF  += -(pi + pj) * 0.5f / (di * dj) * grad * rhat;
                viscosityF += (_particles[j].Velocity - _particles[i].Velocity) / dj * K(r, h);
            }

            _forces[i] = pressureF + viscosityStrength * viscosityF;
        }
    }

    void InitParticles()
    {
        _particles = new FluidParticle3D[particleCount];
        _forces    = new Vector3[particleCount];
        spawner?.Spawn(_particles);
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater()
    {
        gravity.y = -12f; restDensity = 2f; pressureMultiplier = 300f; viscosityStrength = 0.1f;
        if (surfaceRenderer != null) { surfaceRenderer.waterColor = new Color(0.1f, 0.4f, 0.85f, 0.55f); surfaceRenderer.smoothness = 0.88f; surfaceRenderer.visualRadiusMultiplier = 1.5f; surfaceRenderer.RefreshMaterial(); }
    }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()
    {
        gravity.y = -8f; restDensity = 2f; pressureMultiplier = 200f; viscosityStrength = 1.2f;
        if (surfaceRenderer != null) { surfaceRenderer.waterColor = new Color(0.82f, 0.52f, 0.04f, 0.78f); surfaceRenderer.smoothness = 0.55f; surfaceRenderer.visualRadiusMultiplier = 1.35f; surfaceRenderer.RefreshMaterial(); }
    }

    [ContextMenu("Preset / Slime")]
    void PresetSlime()
    {
        gravity.y = -4f; restDensity = 2f; pressureMultiplier = 400f; viscosityStrength = 2.5f;
        if (surfaceRenderer != null) { surfaceRenderer.waterColor = new Color(0.12f, 0.65f, 0.1f, 0.65f); surfaceRenderer.smoothness = 0.65f; surfaceRenderer.visualRadiusMultiplier = 1.7f; surfaceRenderer.RefreshMaterial(); }
    }

    [ContextMenu("Spawn — Grid")]
    void UseGrid()   { if (spawner) { spawner.spawnPattern = FluidSpawner3D.Pattern.Grid;   InitParticles(); } }

    [ContextMenu("Spawn — Random")]
    void UseRandom() { if (spawner) { spawner.spawnPattern = FluidSpawner3D.Pattern.Random; InitParticles(); } }

    // ── Mouse Interaction ────────────────────────────────────────────────────

    void HandleMouse()
    {
        if (_cam == null || _particles == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Input.GetMouseButton(0))
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = spawnInterval;
                Vector3 pos = ray.origin + ray.direction * spawnDepth;
                for (int k = 0; k < spawnCount; k++)
                    AddParticleAt(pos + Random.insideUnitSphere * 0.15f);
            }
        }

        if (Input.GetMouseButton(1))
            PushFrom(ray.origin + ray.direction * spawnDepth, mouseRadius, pushStrength);
    }

    void SpawnBatch(int count)
    {
        int old = _particles.Length;
        System.Array.Resize(ref _particles, old + count);
        System.Array.Resize(ref _forces,    old + count);

        // Spawn en la parte superior para que caigan sin solaparse con partículas ya asentadas
        float hw, hd, yTop, yRange;
        if (boundary != null)
        {
            Vector3 bc = boundary.transform.position;
            hw     = boundary.size.x * 0.42f;
            hd     = boundary.size.z * 0.42f;
            yTop   = bc.y + boundary.size.y * 0.5f - particleRadius * 2f;
            yRange = boundary.size.y * 0.25f;
        }
        else
        {
            hw = hd = 2f; yTop = 2f; yRange = 1f;
        }

        for (int i = old; i < _particles.Length; i++)
            _particles[i] = new FluidParticle3D
            {
                Position = new Vector3(
                    Random.Range(-hw, hw),
                    Random.Range(yTop - yRange, yTop),
                    Random.Range(-hd, hd)),
                Velocity = Vector3.zero
            };
    }

    [ContextMenu("Spawn — Batch")]
    void SpawnBatchMenu() => SpawnBatch(batchSize);

    void AddParticleAt(Vector3 pos)
    {
        int n = _particles.Length;
        System.Array.Resize(ref _particles, n + 1);
        System.Array.Resize(ref _forces,    n + 1);
        _particles[n] = new FluidParticle3D { Position = pos, Velocity = Vector3.zero };
    }

    void PushFrom(Vector3 center, float radius, float strength)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            Vector3 dir  = _particles[i].Position - center;
            float   dist = dir.magnitude;
            if (dist < radius && dist > 0.001f)
                _particles[i].Velocity += dir.normalized * (1f - dist / radius) * strength;
        }
    }

    // ── Job System ────────────────────────────────────────────────────────────

    void StepJobs(float dt)
    {
        int n = _particles.Length;
        EnsureNativeArrays(n);

        // Copiar datos managed → native
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = _particles[i].Position;
            Vector3 vel = _particles[i].Velocity;
            _nPos[i] = new float3(pos.x, pos.y, pos.z);
            _nVel[i] = new float3(vel.x, vel.y, vel.z);
        }

        // Calcular límites del boundary en world space
        Vector3 bCenter = boundary != null ? boundary.transform.position : Vector3.zero;
        Vector3 bSize   = boundary != null ? boundary.size : Vector3.one * 8f;
        float3 bMin = new float3(bCenter.x - bSize.x * 0.5f,
                                 bCenter.y - bSize.y * 0.5f,
                                 bCenter.z - bSize.z * 0.5f);
        float3 bMax = new float3(bCenter.x + bSize.x * 0.5f,
                                 bCenter.y + bSize.y * 0.5f,
                                 bCenter.z + bSize.z * 0.5f);

        float rest  = boundary != null ? boundary.restitution : 0.3f;
        float damp  = boundary != null ? boundary.damping     : 0.98f;

        // Planificar jobs encadenados: densidad → fuerzas → integración
        JobHandle densityHandle, forceHandle;

        if (useSpatialHash)
        {
            _hashMap.Clear();
            var buildHandle = new SPHBuildHashMapJob
            {
                positions = _nPos,
                hashMap   = _hashMap.AsParallelWriter(),
                h         = smoothingRadius,
            }.Schedule(n, 64);

            densityHandle = new SPHDensityJobSH
            {
                positions = _nPos,
                densities = _nDen,
                hashMap   = _hashMap,
                h         = smoothingRadius,
            }.Schedule(n, 64, buildHandle);

            forceHandle = new SPHForceJobSH
            {
                positions    = _nPos,
                velocities   = _nVel,
                densities    = _nDen,
                forces       = _nFor,
                hashMap      = _hashMap,
                h            = smoothingRadius,
                restDensity  = restDensity,
                pressureMult = pressureMultiplier,
                viscosity    = viscosityStrength,
            }.Schedule(n, 32, densityHandle);
        }
        else
        {
            densityHandle = new SPHDensityJob
            {
                positions = _nPos,
                densities = _nDen,
                h         = smoothingRadius,
            }.Schedule(n, 64);

            forceHandle = new SPHForceJob
            {
                positions    = _nPos,
                velocities   = _nVel,
                densities    = _nDen,
                forces       = _nFor,
                h            = smoothingRadius,
                restDensity  = restDensity,
                pressureMult = pressureMultiplier,
                viscosity    = viscosityStrength,
            }.Schedule(n, 32, densityHandle);
        }

        var intJob = new SPHIntegrateJob
        {
            positions  = _nPos,
            velocities = _nVel,
            forces     = _nFor,
            gravity    = new float3(gravity.x, gravity.y, gravity.z),
            dt         = dt,
            maxVel     = maxVelocity,
            bMin       = bMin,
            bMax       = bMax,
            radius     = particleRadius,
            restitution = rest,
            damping    = damp,
        };
        intJob.Schedule(n, 64, forceHandle).Complete();

        // Copiar resultados native → managed
        for (int i = 0; i < n; i++)
        {
            _particles[i].Position = new Vector3(_nPos[i].x, _nPos[i].y, _nPos[i].z);
            _particles[i].Velocity = new Vector3(_nVel[i].x, _nVel[i].y, _nVel[i].z);
            _particles[i].Density  = _nDen[i];
        }
    }

    void EnsureNativeArrays(int count)
    {
        if (_nativeCapacity == count) return;
        DisposeNativeArrays();
        _nPos    = new NativeArray<float3>(count, Allocator.Persistent);
        _nVel    = new NativeArray<float3>(count, Allocator.Persistent);
        _nDen    = new NativeArray<float> (count, Allocator.Persistent);
        _nFor    = new NativeArray<float3>(count, Allocator.Persistent);
        _hashMap = new NativeParallelMultiHashMap<int, int>(count * 4, Allocator.Persistent);
        _nativeCapacity = count;
    }

    void DisposeNativeArrays()
    {
        if (_nativeCapacity == 0) return;
        if (_nPos.IsCreated)    _nPos.Dispose();
        if (_nVel.IsCreated)    _nVel.Dispose();
        if (_nDen.IsCreated)    _nDen.Dispose();
        if (_nFor.IsCreated)    _nFor.Dispose();
        if (_hashMap.IsCreated) _hashMap.Dispose();
        _nativeCapacity = 0;
    }

}
