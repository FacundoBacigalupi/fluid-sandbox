using UnityEngine;
using UnityEngine.Rendering;

// Renderiza partículas como esferas URP/Lit transparentes solapadas.
// El radio visual mayor al de simulación hace que las esferas se mezclen visualmente.
[AddComponentMenu("FluidSandbox/Fluid Surface Renderer 3D")]
public class FluidSurfaceRenderer3D : MonoBehaviour
{
    [Header("Apariencia")]
    public Color waterColor = new Color(0.1f, 0.4f, 0.85f, 0.55f);
    [Range(0f, 1f)] public float smoothness = 0.88f;

    [Header("Geometría visual")]
    [Tooltip("Radio visual = simRadius × este factor. Esferas solapadas → aspecto de fluido.")]
    [Range(1f, 3f)] public float visualRadiusMultiplier = 1.5f;

    private MeshRenderer[] _pool   = System.Array.Empty<MeshRenderer>();
    private Material        _mat;
    private Mesh            _sphere;

    void Start()
    {
        _sphere = GetSphereMesh();
        _mat    = CreateWaterMaterial();
    }

    public void Render(FluidParticle3D[] particles, float simRadius)
    {
        if (!enabled || particles == null) return;
        if (_sphere == null) _sphere = GetSphereMesh();
        if (_mat    == null) _mat    = CreateWaterMaterial();
        if (_mat    == null) return;

        float   d     = simRadius * visualRadiusMultiplier * 2f;
        Vector3 scale = new Vector3(d, d, d);

        EnsurePool(particles.Length);

        for (int i = 0; i < _pool.Length; i++)
        {
            if (i < particles.Length)
            {
                _pool[i].transform.position  = particles[i].Position;
                _pool[i].transform.localScale = scale;
                _pool[i].enabled = true;
            }
            else
            {
                _pool[i].enabled = false;
            }
        }
    }

    void OnDisable()
    {
        foreach (var mr in _pool)
            if (mr != null) mr.enabled = false;
    }

    void EnsurePool(int count)
    {
        if (_pool.Length >= count) return;

        int old = _pool.Length;
        System.Array.Resize(ref _pool, count);

        for (int i = old; i < count; i++)
        {
            var go = new GameObject($"Surf{i}");
            go.transform.SetParent(transform, false);
            var mf        = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _sphere;
            var mr              = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial   = _mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows   = false;
            _pool[i] = mr;
        }
    }

    // URP/Lit configurado como transparente — el mismo shader que ya funciona
    // en ParticleRenderer3D pero con Surface Type = Transparent y alpha < 1.
    Material CreateWaterMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("[FluidSurfaceRenderer3D] No se encontró URP/Lit ni Standard shader.");
            return null;
        }

        var mat = new Material(shader) { enableInstancing = true };

        // Configurar como transparente (Alpha blend)
        mat.SetFloat("_Surface",      1f);   // 1 = Transparent
        mat.SetFloat("_Blend",        0f);   // 0 = Alpha
        mat.SetFloat("_AlphaClip",    0f);
        mat.SetInt("_SrcBlend",       (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",       (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha",  (int)BlendMode.One);
        mat.SetInt("_DstBlendAlpha",  (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",         0);
        mat.renderQueue = 3000;

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Color agua + alta reflectividad
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

    // ── Presets ───────────────────────────────────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater()
    {
        waterColor = new Color(0.1f, 0.4f, 0.85f, 0.55f);
        smoothness = 0.88f; visualRadiusMultiplier = 1.5f;
        if (_mat != null) UpdateMat();
    }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()
    {
        waterColor = new Color(0.82f, 0.52f, 0.04f, 0.78f);
        smoothness = 0.55f; visualRadiusMultiplier = 1.35f;
        if (_mat != null) UpdateMat();
    }

    [ContextMenu("Preset / Lava")]
    void PresetLava()
    {
        waterColor = new Color(0.85f, 0.2f, 0.05f, 0.7f);
        smoothness = 0.4f; visualRadiusMultiplier = 1.3f;
        if (_mat != null) UpdateMat();
    }

    [ContextMenu("Preset / Slime")]
    void PresetSlime()
    {
        waterColor = new Color(0.15f, 0.7f, 0.1f, 0.65f);
        smoothness = 0.65f; visualRadiusMultiplier = 1.7f;
        if (_mat != null) UpdateMat();
    }

    [ContextMenu("Preset / Mercury")]
    void PresetMercury()
    {
        waterColor = new Color(0.6f, 0.6f, 0.65f, 0.85f);
        smoothness = 0.98f; visualRadiusMultiplier = 1.25f;
        if (_mat != null) UpdateMat();
    }

    public void RefreshMaterial()
    {
        if (_mat != null) UpdateMat();
    }

    void UpdateMat()
    {
        _mat.SetColor("_BaseColor",  waterColor);
        _mat.SetFloat("_Smoothness", smoothness);
    }
}
