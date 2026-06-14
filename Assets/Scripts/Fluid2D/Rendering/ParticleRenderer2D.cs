using UnityEngine;

// Renderiza el array de FluidParticle2D usando SpriteRenderers poolados.
// Los SpriteRenderers son hijos de este GameObject — se crean una sola vez
// y se reposicionan cada frame. Sin instanciar/destruir nada por frame.
// (DrawMeshInstanced requiere setup de shader URP complejo; esto es más simple y confiable.)
[AddComponentMenu("FluidSandbox/Particle Renderer 2D")]
public class ParticleRenderer2D : MonoBehaviour
{
    public Sprite circleSprite;
    public Color  particleColor = new Color(0.25f, 0.55f, 1f, 0.88f);

    private SpriteRenderer[] _pool = System.Array.Empty<SpriteRenderer>();

    // Llamado por FluidSolver2D cada Update con el array de partículas actualizado.
    void OnDisable()
    {
        foreach (var sr in _pool)
            if (sr != null) sr.enabled = false;
    }

    public void Render(FluidParticle2D[] particles, float radius)
    {
        if (!enabled || particles == null) return;

        EnsurePool(particles.Length);

        float d = radius * 2f;

        for (int i = 0; i < _pool.Length; i++)
        {
            if (i < particles.Length)
            {
                var p = particles[i];
                _pool[i].transform.position   = new Vector3(p.Position.x, p.Position.y, 0f);
                _pool[i].transform.localScale  = new Vector3(d, d, 1f);
                _pool[i].enabled = true;
            }
            else
            {
                _pool[i].enabled = false;
            }
        }
    }

    // Crece el pool si el número de partículas aumenta. Nunca encoge.
    void EnsurePool(int requiredCount)
    {
        if (_pool.Length >= requiredCount) return;

        Sprite s = circleSprite ?? MakeCircleSprite();

        int oldSize = _pool.Length;
        System.Array.Resize(ref _pool, requiredCount);

        for (int i = oldSize; i < requiredCount; i++)
        {
            var go = new GameObject($"P{i}");
            go.transform.SetParent(transform);
            var sr    = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color  = particleColor;
            _pool[i]  = sr;
        }
    }

    static Sprite MakeCircleSprite()
    {
        const int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        float ctr = sz * 0.5f, r = ctr - 1f, edge = r * 0.08f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d = Mathf.Sqrt((x - ctr + .5f) * (x - ctr + .5f) + (y - ctr + .5f) * (y - ctr + .5f));
                float a = 1f - Mathf.Clamp01((d - (r - edge)) / edge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), Vector2.one * 0.5f, sz);
    }
}
