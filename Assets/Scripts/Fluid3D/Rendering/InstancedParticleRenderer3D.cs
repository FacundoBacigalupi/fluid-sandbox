using UnityEngine;
using UnityEngine.Rendering;

// Renderiza partículas 3D con GPU Instancing usando Graphics.RenderMeshInstanced
// (API moderna SRP/URP — reemplaza DrawMeshInstanced que tiene bugs en Unity 6/URP).
// Limit: 1023 instancias por draw call — batching automático para N > 1023.
[AddComponentMenu("FluidSandbox/Instanced Particle Renderer 3D")]
public class InstancedParticleRenderer3D : MonoBehaviour
{
    public Color particleColor = new Color(0.3f, 0.6f, 1f, 1f);

    private Mesh         _mesh;
    private Material     _material;
    private RenderParams _rp;
    private Matrix4x4[]  _batch = new Matrix4x4[1023];

    void Start()
    {
        _mesh     = GetSphereMesh();
        _material = CreateMaterial();
        _rp = new RenderParams(_material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows    = false,
            worldBounds       = new Bounds(Vector3.zero, Vector3.one * 1000f),
        };
    }

    public void Render(FluidParticle3D[] particles, float radius)
    {
        if (!enabled || particles == null || _mesh == null || _material == null) return;

        float   d     = radius * 2f;
        Vector3 scale = new Vector3(d, d, d);
        int     total = particles.Length;
        int     offset = 0;

        while (offset < total)
        {
            int count = Mathf.Min(1023, total - offset);
            for (int i = 0; i < count; i++)
                _batch[i] = Matrix4x4.TRS(particles[offset + i].Position, Quaternion.identity, scale);
            Graphics.RenderMeshInstanced(_rp, _mesh, 0, _batch, count);
            offset += count;
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
        mat.renderQueue      = (int)RenderQueue.Geometry;
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.enableInstancing = true;
        return mat;
    }
}
