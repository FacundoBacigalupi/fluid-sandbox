using UnityEngine;
using UnityEngine.Rendering;

// Renderiza partículas 3D usando MeshRenderers poolados — igual que ParticleRenderer2D
// pero con esferas en vez de sprites. Unity batchea automáticamente los GameObjects
// que comparten mesh + material, sin necesidad de DrawMeshInstanced.
[AddComponentMenu("FluidSandbox/Particle Renderer 3D")]
public class ParticleRenderer3D : MonoBehaviour
{
    public Color particleColor = new Color(0.3f, 0.6f, 1f, 1f);

    private MeshRenderer[] _pool   = System.Array.Empty<MeshRenderer>();
    private Material        _mat;
    private Mesh            _sphere;

    void Start()
    {
        _sphere = GetSphereMesh();
        _mat    = CreateMaterial();
    }

    public void Render(FluidParticle3D[] particles, float radius)
    {
        if (!enabled || particles == null) return;

        EnsurePool(particles.Length);

        float   d     = radius * 2f;
        Vector3 scale = new Vector3(d, d, d);

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
            var go = new GameObject($"P{i}");
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

    static Mesh GetSphereMesh()
    {
        var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return mesh;
    }

    Material CreateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetFloat("_Surface", 0f);
        mat.SetColor("_BaseColor", particleColor);
        mat.color            = particleColor;
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.enableInstancing = true;
        return mat;
    }
}
