using UnityEngine;
using UnityEngine.Rendering;

// Renderiza las partículas del FluidComputeSolver usando un pool de GameObjects.
// Mismo patrón que FluidSurfaceRenderer3D — una esfera URP/Lit por partícula.
[AddComponentMenu("FluidSandbox/GPU Fluid Renderer")]
public class GPUFluidRenderer : MonoBehaviour
{
    [Header("Solver")]
    public FluidComputeSolver solver;

    [Header("Apariencia")]
    public Color waterColor = new Color(0.1f, 0.4f, 0.85f, 0.55f);
    [Range(0f, 1f)] public float smoothness = 0.88f;
    [Range(1f, 3f)] public float visualRadiusMultiplier = 1.5f;

    MeshRenderer[] _pool   = System.Array.Empty<MeshRenderer>();
    Material       _mat;
    Mesh           _sphere;

    void Start()
    {
        _sphere = GetSphereMesh();
        _mat    = CreateMaterial();
    }

    void LateUpdate()
    {
        if (solver == null || solver.Positions == null || _mat == null) return;

        var     pos   = solver.Positions;
        int     count = solver.particleCount;
        float   d     = solver.particleRadius * visualRadiusMultiplier * 2f;
        Vector3 scale = new Vector3(d, d, d);

        EnsurePool(count);

        for (int i = 0; i < _pool.Length; i++)
        {
            if (i < count)
            {
                _pool[i].transform.position   = pos[i];
                _pool[i].transform.localScale  = scale;
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
            var go            = new GameObject($"P{i}");
            go.transform.SetParent(transform, false);
            var mf            = go.AddComponent<MeshFilter>();
            mf.sharedMesh     = _sphere;
            var mr            = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            _pool[i] = mr;
        }
    }

    Material CreateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) { Debug.LogError("[GPUFluidRenderer] Shader no encontrado."); return null; }

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
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        mat.SetColor("_BaseColor",  waterColor);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic",   0f);

        return mat;
    }

    static Mesh GetSphereMesh()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var m  = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return m;
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater()   { waterColor=new Color(0.1f,0.4f,0.85f,0.55f); smoothness=0.88f; UpdateMat(); }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()   { waterColor=new Color(0.82f,0.52f,0.04f,0.78f); smoothness=0.55f; UpdateMat(); }

    [ContextMenu("Preset / Slime")]
    void PresetSlime()   { waterColor=new Color(0.15f,0.7f,0.1f,0.65f); smoothness=0.65f; UpdateMat(); }

    [ContextMenu("Preset / Mercury")]
    void PresetMercury() { waterColor=new Color(0.6f,0.6f,0.65f,0.85f); smoothness=0.98f; UpdateMat(); }

    void UpdateMat()
    {
        if (_mat == null) return;
        _mat.SetColor("_BaseColor",  waterColor);
        _mat.SetFloat("_Smoothness", smoothness);
    }
}
