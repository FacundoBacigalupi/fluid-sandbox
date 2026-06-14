using System.Collections.Generic;
using UnityEngine;

// Simulación SPH 2D: integración Euler, spatial hash, densidad, presión, viscosidad.
// R = reset | G = toggle debug grid
[AddComponentMenu("FluidSandbox/Fluid Solver 2D")]
public class FluidSolver2D : MonoBehaviour
{
    [Header("Referencias")]
    public FluidSpawner2D     spawner;
    public Boundary2D         boundary;
    public ParticleRenderer2D particleRenderer;

    [Header("Simulación")]
    public int     particleCount  = 300;
    [Range(0.03f, 0.4f)]  public float particleRadius = 0.08f;
    public Vector2 gravity        = new Vector2(0f, -12f);
    [Range(1, 8)]         public int   substeps       = 3;

    [Header("Neighbor Search")]
    [Range(0.1f, 1.5f)]   public float smoothingRadius = 0.35f;

    [Header("Fluid — SPH")]
    [Range(0f, 15f)]      public float restDensity        = 6f;
    [Range(0f, 600f)]     public float pressureMultiplier = 100f;
    [Range(0f, 2f)]       public float viscosityStrength  = 0.3f;
    [Range(1f, 30f)]      public float maxVelocity        = 15f;

    [Header("Mouse Interaction")]
    [Range(0.2f, 4f)]     public float mouseRadius   = 1.5f;
    [Range(1f, 30f)]      public float pushStrength  = 12f;
    [Range(1, 5)]         public int   spawnCount    = 2;
    [Range(0.04f, 0.5f)]  public float spawnInterval = 0.08f;

    [Header("Debug")]
    public bool showDebug = false;
    [Range(0, 499)] public int debugParticleIndex = 0;

    private FluidParticle2D[] _particles;
    private Vector2[]         _forces = System.Array.Empty<Vector2>();

    public FluidParticle2D[] Particles => _particles;

    private readonly SpatialHashGrid2D _grid      = new SpatialHashGrid2D();
    private readonly List<int>         _neighbors = new List<int>(64);

    private float        _spawnTimer;
    private LineRenderer _ring;
    private Camera       _cam;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake() => SetupRing();

    void Start() => InitParticles();

    void Update()
    {
        _cam ??= Camera.main;

        if (Input.GetKeyDown(KeyCode.R)) InitParticles();
        if (Input.GetKeyDown(KeyCode.G)) showDebug = !showDebug;

        particleRenderer?.Render(_particles, particleRadius);
        HandleMouse();

        if (showDebug && _particles != null && _particles.Length > 0)
        {
            _grid.Build(_particles, smoothingRadius);
            int idx = Mathf.Clamp(debugParticleIndex, 0, _particles.Length - 1);
            _grid.Query(_particles[idx].Position, smoothingRadius, _neighbors);
        }
    }

    void FixedUpdate()
    {
        if (_particles == null) return;
        float dt = Time.fixedDeltaTime / substeps;
        for (int s = 0; s < substeps; s++) Step(dt);
    }

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
            p.Velocity  = Vector2.ClampMagnitude(p.Velocity, maxVelocity);
            p.Position += p.Velocity * dt;
            boundary?.Resolve(ref p, particleRadius);
        }
    }

    // Kernel cúbico simple: W(r,h) = (1 - r/h)³
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
        for (int i = 0; i < _particles.Length; i++) _forces[i] = Vector2.zero;

        for (int i = 0; i < _particles.Length; i++)
        {
            float di = _particles[i].Density;
            float pi = pressureMultiplier * Mathf.Max(0f, di - restDensity);
            _grid.Query(_particles[i].Position, h, _neighbors);

            Vector2 pressureF  = Vector2.zero;
            Vector2 viscosityF = Vector2.zero;

            foreach (int j in _neighbors)
            {
                if (j == i) continue;
                Vector2 rij = _particles[i].Position - _particles[j].Position;
                float   r   = rij.magnitude;
                if (r < 0.0001f || r >= h) continue;

                float dj     = _particles[j].Density;
                float pj     = pressureMultiplier * Mathf.Max(0f, dj - restDensity);
                Vector2 rhat = rij / r;
                float grad   = KGrad(r, h);

                pressureF  += -(pi + pj) * 0.5f / (di * dj) * grad * rhat;
                viscosityF += (_particles[j].Velocity - _particles[i].Velocity) / dj * K(r, h);
            }

            _forces[i] = pressureF + viscosityStrength * viscosityF;
        }
    }

    void InitParticles()
    {
        _particles = new FluidParticle2D[particleCount];
        _forces    = new Vector2[particleCount];
        spawner?.Spawn(_particles);
    }

    // ── Presets (clic derecho en el componente → menú de contexto) ───────────

    [ContextMenu("Preset / Water")]
    void PresetWater() { gravity.y = -12f; restDensity = 6f; pressureMultiplier = 100f; viscosityStrength = 0.1f; }

    [ContextMenu("Preset / Honey")]
    void PresetHoney() { gravity.y = -10f; restDensity = 6f; pressureMultiplier = 70f;  viscosityStrength = 1.0f; }

    [ContextMenu("Preset / Slime")]
    void PresetSlime() { gravity.y = -5f;  restDensity = 5f; pressureMultiplier = 140f; viscosityStrength = 1.8f; }

    // ── Mouse Interaction ────────────────────────────────────────────────────

    void HandleMouse()
    {
        if (_cam == null || _particles == null) return;

        Vector2 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        bool spawning = Input.GetMouseButton(0);
        bool pushing  = Input.GetMouseButton(1);

        Color ringColor = spawning ? new Color(0.3f, 1f, 0.4f, 0.9f)
                        : pushing  ? new Color(0.3f, 0.5f, 1f,  0.9f)
                        :            new Color(1f,   1f,   1f,  0.15f);
        UpdateRing(mouseWorld, ringColor, mouseRadius);

        if (spawning)
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = spawnInterval;
                for (int k = 0; k < spawnCount; k++)
                    AddParticleAt(mouseWorld + Random.insideUnitCircle * 0.15f);
            }
        }

        if (pushing)
            PushAt(mouseWorld, mouseRadius, pushStrength);
    }

    void AddParticleAt(Vector2 pos)
    {
        int n = _particles.Length;
        System.Array.Resize(ref _particles, n + 1);
        System.Array.Resize(ref _forces,    n + 1);
        _particles[n] = new FluidParticle2D { Position = pos, Velocity = Vector2.zero };
    }

    void PushAt(Vector2 center, float radius, float strength)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            Vector2 dir  = _particles[i].Position - center;
            float   dist = dir.magnitude;
            if (dist < radius && dist > 0.001f)
                _particles[i].Velocity += dir.normalized * (1f - dist / radius) * strength;
        }
    }

    void SetupRing()
    {
        var go = new GameObject("MouseRing");
        go.transform.SetParent(transform);
        _ring = go.AddComponent<LineRenderer>();
        _ring.positionCount = 32;
        _ring.loop          = true;
        _ring.useWorldSpace = true;
        _ring.startWidth    = _ring.endWidth = 0.03f;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows    = false;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) _ring.material = new Material(shader);
    }

    void UpdateRing(Vector2 center, Color color, float radius)
    {
        if (_ring == null) return;
        _ring.startColor = _ring.endColor = color;
        const int segs = 32;
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            _ring.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(a) * radius,
                center.y + Mathf.Sin(a) * radius, 0f));
        }
    }

    // ── Debug Gizmos ─────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (!showDebug || _particles == null || _particles.Length == 0) return;

        float cs = _grid.CellSize;
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.12f);
        foreach (var kv in _grid.OccupiedCells)
        {
            var c = kv.Key;
            Gizmos.DrawWireCube(
                new Vector3((c.x + 0.5f) * cs, (c.y + 0.5f) * cs, 0f),
                new Vector3(cs, cs, 0f));
        }

        int idx = Mathf.Clamp(debugParticleIndex, 0, _particles.Length - 1);
        var dp  = _particles[idx];

        Gizmos.color = Color.yellow;
        DrawGizmoCircle(dp.Position, smoothingRadius);
        Gizmos.color = Color.red;
        DrawGizmoCircle(dp.Position, particleRadius);

        Gizmos.color = new Color(0f, 1f, 1f, 0.7f);
        foreach (int ni in _neighbors)
        {
            if (ni == idx) continue;
            if (Vector2.Distance(_particles[ni].Position, dp.Position) < smoothingRadius)
                DrawGizmoCircle(_particles[ni].Position, particleRadius);
        }
    }

    static void DrawGizmoCircle(Vector2 center, float radius, int segs = 24)
    {
        float step = Mathf.PI * 2f / segs;
        var prev = new Vector3(center.x + radius, center.y, 0f);
        for (int i = 1; i <= segs; i++)
        {
            float a = i * step;
            var next = new Vector3(center.x + Mathf.Cos(a) * radius,
                                   center.y + Mathf.Sin(a) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
