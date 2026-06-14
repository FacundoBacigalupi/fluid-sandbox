using UnityEngine;

// Simulación SPH enteramente en GPU via Compute Shaders.
// Expone Positions[] para que un renderer externo (GPUFluidRenderer) dibuje las partículas.
[AddComponentMenu("FluidSandbox/Fluid Compute Solver (GPU)")]
public class FluidComputeSolver : MonoBehaviour
{
    [Header("Shaders")]
    public ComputeShader sphCompute;

    [Header("Simulación")]
    public int     particleCount  = 2000;
    [Range(0.01f, 0.2f)]  public float particleRadius  = 0.07f;
    public Vector3         gravity        = new Vector3(0f, -12f, 0f);
    [Range(1, 4)]          public int    substeps        = 2;
    [Range(5f, 30f)]       public float  maxVelocity     = 20f;

    [Header("SPH")]
    [Range(0.05f, 0.8f)]  public float smoothingRadius = 0.28f;
    [Range(0f, 10f)]      public float restDensity      = 2f;
    [Range(0f, 600f)]     public float pressureMult     = 300f;
    [Range(0f, 3f)]       public float viscosity        = 0.1f;

    [Header("Boundary")]
    public Boundary3D boundary;

    // Posiciones legibles por GPUFluidRenderer
    public Vector3[] Positions { get; private set; }

    ComputeBuffer _posBuffer, _velBuffer, _denBuffer, _forBuffer;
    Vector4[]     _cpuPos;

    bool _ready;
    int  _kDensity, _kForce, _kIntegrate;
    const int THREADS = 64;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (sphCompute == null) { Debug.LogError("[FluidComputeSolver] Asignar Sph Compute.", this); return; }

        int stride4 = 4 * sizeof(float);
        _posBuffer = new ComputeBuffer(particleCount, stride4);
        _velBuffer = new ComputeBuffer(particleCount, stride4);
        _denBuffer = new ComputeBuffer(particleCount, sizeof(float));
        _forBuffer = new ComputeBuffer(particleCount, stride4);

        _cpuPos   = new Vector4[particleCount];
        Positions = new Vector3[particleCount];

        _kDensity   = sphCompute.FindKernel("CSDensity");
        _kForce     = sphCompute.FindKernel("CSForce");
        _kIntegrate = sphCompute.FindKernel("CSIntegrate");

        foreach (int k in new[] { _kDensity, _kForce, _kIntegrate })
        {
            sphCompute.SetBuffer(k, "_Positions",  _posBuffer);
            sphCompute.SetBuffer(k, "_Velocities", _velBuffer);
            sphCompute.SetBuffer(k, "_Densities",  _denBuffer);
            sphCompute.SetBuffer(k, "_Forces",     _forBuffer);
        }

        InitParticles();
        _ready = true;
    }

    void Update()
    {
        if (_ready && Input.GetKeyDown(KeyCode.Space))
            InitParticles();
    }

    void FixedUpdate()
    {
        if (!_ready) return;

        float dt     = Time.fixedDeltaTime / substeps;
        int   groups = Mathf.CeilToInt(particleCount / (float)THREADS);

        SetConstants(dt);

        for (int s = 0; s < substeps; s++)
        {
            sphCompute.Dispatch(_kDensity,   groups, 1, 1);
            sphCompute.Dispatch(_kForce,     groups, 1, 1);
            sphCompute.Dispatch(_kIntegrate, groups, 1, 1);
        }

        _posBuffer.GetData(_cpuPos);

        for (int i = 0; i < particleCount; i++)
            Positions[i] = new Vector3(_cpuPos[i].x, _cpuPos[i].y, _cpuPos[i].z);
    }

    void OnDestroy()
    {
        _posBuffer?.Release(); _velBuffer?.Release();
        _denBuffer?.Release(); _forBuffer?.Release();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void InitParticles()
    {
        int     gridW   = Mathf.CeilToInt(Mathf.Pow(particleCount, 1f / 3f));
        float   sp      = particleRadius * 2.2f;
        Vector3 center  = boundary != null ? boundary.transform.position : Vector3.zero;
        Vector3 halfExt = boundary != null ? boundary.size * 0.5f        : Vector3.one * 5f;

        float blockH = gridW * sp;
        float startY = center.y + halfExt.y - blockH - sp * 2f;
        startY = Mathf.Max(startY, center.y - halfExt.y + sp);

        Vector3 origin = new Vector3(
            center.x - gridW * sp * 0.5f,
            startY,
            center.z - gridW * sp * 0.5f);

        var positions  = new Vector4[particleCount];
        var velocities = new Vector4[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            int ix = i % gridW, iy = (i / gridW) % gridW, iz = i / (gridW * gridW);
            positions[i] = new Vector4(origin.x + ix * sp, origin.y + iy * sp, origin.z + iz * sp, 0);
        }
        _posBuffer.SetData(positions);
        _velBuffer.SetData(velocities);

        _posBuffer.GetData(_cpuPos);
        for (int i = 0; i < particleCount; i++)
            Positions[i] = new Vector3(_cpuPos[i].x, _cpuPos[i].y, _cpuPos[i].z);
    }

    void SetConstants(float dt)
    {
        Vector3 bc = boundary != null ? boundary.transform.position : Vector3.zero;
        Vector3 bs = boundary != null ? boundary.size               : Vector3.one * 10f;

        sphCompute.SetInt("_ParticleCount",    particleCount);
        sphCompute.SetFloat("_H",              smoothingRadius);
        sphCompute.SetFloat("_RestDensity",    restDensity);
        sphCompute.SetFloat("_PressureMult",   pressureMult);
        sphCompute.SetFloat("_Viscosity",      viscosity);
        sphCompute.SetVector("_Gravity",       gravity);
        sphCompute.SetFloat("_DeltaTime",      dt);
        sphCompute.SetFloat("_MaxVelocity",    maxVelocity);
        sphCompute.SetVector("_BoundsMin",     bc - bs * 0.5f);
        sphCompute.SetVector("_BoundsMax",     bc + bs * 0.5f);
        sphCompute.SetFloat("_ParticleRadius", particleRadius);
        sphCompute.SetFloat("_Restitution",    boundary != null ? boundary.restitution : 0.3f);
        sphCompute.SetFloat("_Damping",        boundary != null ? boundary.damping     : 0.98f);
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater() { gravity.y=-12f; restDensity=2f; pressureMult=300f; viscosity=0.1f; }

    [ContextMenu("Preset / Honey")]
    void PresetHoney() { gravity.y=-8f;  restDensity=2f; pressureMult=200f; viscosity=1.2f; }

    [ContextMenu("Preset / Slime")]
    void PresetSlime() { gravity.y=-4f;  restDensity=2f; pressureMult=400f; viscosity=2.5f; }

    [ContextMenu("Reiniciar")]
    void Restart() { if (_ready) InitParticles(); }
}
