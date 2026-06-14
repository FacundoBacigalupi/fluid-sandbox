using UnityEngine;
using UnityEngine.Rendering;

// Renderiza esferas de partículas con Graphics.DrawMeshInstanced y provee
// matrices de instancia al FluidSSFRenderFeature para el efecto de blur/composite.
[AddComponentMenu("FluidSandbox/Fluid SSF Controller")]
public class FluidSSFController : MonoBehaviour
{
    public static FluidSSFController Instance { get; private set; }

    [Header("Fuente")]
    public FluidSolver3D solver;

    [Header("Apariencia")]
    public Color waterColor  = new Color(0.05f, 0.35f, 0.80f, 0.6f);
    public Color deepColor   = new Color(0.02f, 0.10f, 0.40f, 1f);
    [Range(0f, 1f)] public float smoothness  = 0.92f;
    [Range(1f, 8f)] public float fresnelPow  = 3.5f;
    [Range(0f, 1f)] public float opacity     = 0.88f;

    [Header("Blur bilateral (render feature)")]
    [Range(1f, 24f)]  public float blurRadius  = 10f;
    [Range(0.1f, 10f)] public float blurFalloff = 2.0f;

    [Header("Normal (render feature)")]
    [Range(0.01f, 0.5f)] public float depthScale = 0.08f;

    [Header("Geometría")]
    [Range(1f, 3f)] public float visualRadiusMultiplier = 1.6f;

    // Datos compartidos con el RenderFeature
    internal Matrix4x4[] instanceMatrices = System.Array.Empty<Matrix4x4>();
    internal int         particleCount;
    internal Mesh        sphereMesh;

    // Renderizado directo (fallback siempre activo)
    Material _directMat;

    void Awake()
    {
        Instance   = this;
        sphereMesh = GetSphereMesh();
        _directMat = CreateMaterial();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_directMat != null) Destroy(_directMat);
    }

    void LateUpdate()
    {
        if (solver == null) solver = FindFirstObjectByType<FluidSolver3D>();
        var particles = solver?.Particles;
        if (particles == null || particles.Length == 0) { particleCount = 0; return; }

        particleCount = particles.Length;
        if (instanceMatrices.Length < particleCount)
            instanceMatrices = new Matrix4x4[particleCount];

        float d = solver.particleRadius * visualRadiusMultiplier * 2f;
        var   s = new Vector3(d, d, d);

        for (int i = 0; i < particleCount; i++)
            instanceMatrices[i] = Matrix4x4.TRS(particles[i].Position, Quaternion.identity, s);

        // Renderizado directo — funciona independientemente del render feature
        if (!enabled || sphereMesh == null || _directMat == null) return;
        _directMat.SetColor("_BaseColor", waterColor);
        for (int start = 0; start < particleCount; start += 1023)
        {
            int count = Mathf.Min(1023, particleCount - start);
            var slice = new Matrix4x4[count];
            System.Array.Copy(instanceMatrices, start, slice, 0, count);
            Graphics.DrawMeshInstanced(sphereMesh, 0, _directMat, slice, count,
                null, ShadowCastingMode.Off, false);
        }
    }

    Material CreateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        var mat = new Material(shader) { enableInstancing = true };
        mat.SetFloat("_Surface",     1f);
        mat.SetFloat("_Blend",       0f);
        mat.SetFloat("_AlphaClip",   0f);
        mat.SetInt("_SrcBlend",      (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",      (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",        0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetColor("_BaseColor",  waterColor);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic",   0f);
        return mat;
    }

    static Mesh GetSphereMesh()
    {
        var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return mesh;
    }

    // ── Context-menu presets ──────────────────────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater()   { waterColor = new Color(0.05f,0.35f,0.80f,0.6f); deepColor = new Color(0.02f,0.10f,0.40f,1); smoothness=0.92f; fresnelPow=3.5f; opacity=0.88f; visualRadiusMultiplier=1.6f; }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()   { waterColor = new Color(0.82f,0.52f,0.04f,0.7f); deepColor = new Color(0.45f,0.20f,0.01f,1); smoothness=0.55f; fresnelPow=2.5f; opacity=0.92f; visualRadiusMultiplier=1.4f; }

    [ContextMenu("Preset / Slime")]
    void PresetSlime()   { waterColor = new Color(0.15f,0.70f,0.10f,0.65f); deepColor = new Color(0.05f,0.30f,0.04f,1); smoothness=0.60f; fresnelPow=2.0f; opacity=0.90f; visualRadiusMultiplier=1.8f; }

    [ContextMenu("Preset / Mercury")]
    void PresetMercury() { waterColor = new Color(0.65f,0.65f,0.70f,0.9f); deepColor = new Color(0.25f,0.25f,0.30f,1); smoothness=0.98f; fresnelPow=5.0f; opacity=0.95f; visualRadiusMultiplier=1.3f; }
}
