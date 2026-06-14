using UnityEngine;

// Renderiza metaballs 2D: una cámara auxiliar acumula sprites gaussianos en una
// RenderTexture; un quad fullscreen umbraliza el resultado para el contorno del fluido.
// M = toggle entre vista de círculos y metaball
[AddComponentMenu("FluidSandbox/Metaball Renderer 2D")]
public class MetaballRenderer2D : MonoBehaviour
{
    [Header("Referencias")]
    public FluidSolver2D      solver;
    public ParticleRenderer2D circleRenderer;

    [Header("Visual")]
    public bool  showMetaballs = true;
    [Range(0.1f, 2f)]     public float blobRadius  = 0.45f;
    [Range(0.1f, 1.4f)]   public float threshold   = 0.5f;
    [Range(0.001f, 0.1f)] public float softness    = 0.03f;
    public Color liquidColor = new Color(0.15f, 0.52f, 1f, 0.97f);

    private Camera           _blobCam;
    private RenderTexture    _blobRT;
    private SpriteRenderer[] _blobPool = System.Array.Empty<SpriteRenderer>();
    private Material         _thresholdMat;
    private Material         _blobMat;
    private Sprite           _blobSprite;
    private MeshRenderer     _overlayMR;
    private int              _blobLayer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _blobLayer = LayerMask.NameToLayer("Blob");
        if (_blobLayer < 0)
        {
            Debug.LogWarning("MetaballRenderer2D: capa 'Blob' no encontrada. " +
                             "Ejecutá FluidSandbox/Phase 6 desde el menú para configurarla.");
            _blobLayer = 0;
        }

        _blobSprite   = CreateBlobSprite();
        _blobMat      = CreateBlobMaterial();
        _blobRT       = new RenderTexture(Screen.width, Screen.height, 24,
                                          RenderTextureFormat.ARGBHalf);
        _blobRT.wrapMode = TextureWrapMode.Clamp;
        _blobRT.name     = "BlobRT";
        _thresholdMat    = CreateThresholdMaterial();

        SetupBlobCamera();
        SetupOverlayQuad();
        ApplyToggle();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            showMetaballs = !showMetaballs;
            ApplyToggle();
        }
    }

    void LateUpdate()
    {
        if (!showMetaballs) return;

        var particles = solver?.Particles;
        if (particles == null) return;

        EnsureBlobPool(particles.Length);

        float d = blobRadius * 2f;
        for (int i = 0; i < _blobPool.Length; i++)
        {
            if (i < particles.Length)
            {
                var p = particles[i].Position;
                _blobPool[i].transform.position  = new Vector3(p.x, p.y, 0f);
                _blobPool[i].transform.localScale = new Vector3(d, d, 1f);
                _blobPool[i].enabled = true;
            }
            else
            {
                _blobPool[i].enabled = false;
            }
        }

        // Renderizar blobs al RT manualmente (cámara disabled = sin auto-render ni preview)
        _blobCam?.Render();

        if (_thresholdMat != null)
        {
            _thresholdMat.SetFloat("_Threshold",   threshold);
            _thresholdMat.SetFloat("_Softness",    softness);
            _thresholdMat.SetColor("_LiquidColor", liquidColor);
        }
    }

    void OnDestroy()
    {
        if (_blobRT != null) { _blobRT.Release(); _blobRT = null; }
        // HideAndDontSave objects need explicit destroy
        if (_blobCam != null) Destroy(_blobCam.gameObject);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupBlobCamera()
    {
        var mainCam = Camera.main;

        var go = new GameObject("BlobCamera");
        // HideAndDontSave: oculta el GO del editor por completo y suprime el preview
        // de cámara en el Game view. Destroy manual requerido en OnDestroy.
        go.hideFlags = HideFlags.HideAndDontSave;

        go.transform.position = mainCam != null
            ? mainCam.transform.position
            : new Vector3(0f, 0f, -10f);

        _blobCam = go.AddComponent<Camera>();
        _blobCam.orthographic     = true;
        _blobCam.orthographicSize = mainCam != null ? mainCam.orthographicSize : 4f;
        _blobCam.clearFlags       = CameraClearFlags.SolidColor;
        _blobCam.backgroundColor  = Color.black;
        _blobCam.targetTexture    = _blobRT;
        _blobCam.cullingMask      = _blobLayer >= 0 ? (1 << _blobLayer) : 0;
        _blobCam.depth            = mainCam != null ? mainCam.depth - 1f : -2f;
        // No se auto-renderiza; se llama Render() manualmente en LateUpdate.
        _blobCam.enabled = false;
    }

    // Quad en world space que cubre exactamente el área visible de la cámara.
    // Evita Canvas + RawImage (sin canvas no hay rectangle outline ni preview box).
    void SetupOverlayQuad()
    {
        var mainCam = Camera.main;
        float camH = mainCam != null ? mainCam.orthographicSize * 2f : 8f;
        float camW = mainCam != null ? camH * mainCam.aspect : camH * (16f / 9f);
        float hw = camW * 0.5f, hh = camH * 0.5f;

        Vector2 origin = mainCam != null
            ? new Vector2(mainCam.transform.position.x, mainCam.transform.position.y)
            : Vector2.zero;

        var go = new GameObject("MetaballOverlay");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(origin.x, origin.y, 0f);

        var mesh = new Mesh { name = "OverlayQuad" };
        mesh.vertices  = new[] { new Vector3(-hw,-hh,0), new Vector3(hw,-hh,0),
                                  new Vector3(-hw, hh,0), new Vector3(hw, hh,0) };
        mesh.uv        = new[] { new Vector2(0,0), new Vector2(1,0),
                                  new Vector2(0,1), new Vector2(1,1) };
        mesh.triangles = new[] { 0,2,1, 2,3,1 };

        go.AddComponent<MeshFilter>().mesh = mesh;

        _overlayMR = go.AddComponent<MeshRenderer>();
        _overlayMR.material          = _thresholdMat;
        _overlayMR.sortingOrder      = 100;          // por encima de todos los sprites
        _overlayMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _overlayMR.receiveShadows    = false;
    }

    void ApplyToggle()
    {
        if (_overlayMR)     _overlayMR.enabled     = showMetaballs;
        if (circleRenderer) circleRenderer.enabled = !showMetaballs;

        for (int i = 0; i < _blobPool.Length; i++)
            if (_blobPool[i] != null) _blobPool[i].enabled = showMetaballs;
    }

    void EnsureBlobPool(int count)
    {
        if (_blobPool.Length >= count) return;
        int old = _blobPool.Length;
        System.Array.Resize(ref _blobPool, count);
        for (int i = old; i < count; i++)
        {
            var go = new GameObject($"Blob{i}");
            go.transform.SetParent(transform);
            go.layer = _blobLayer;
            var sr  = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _blobSprite;
            sr.material     = _blobMat;
            sr.sortingOrder = 0;
            _blobPool[i] = sr;
        }
    }

    // ── Creación de recursos ──────────────────────────────────────────────────

    static Sprite CreateBlobSprite()
    {
        const int sz = 128;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float ctr = sz * 0.5f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float dx  = (x - ctr + 0.5f) / ctr;
                float dy  = (y - ctr + 0.5f) / ctr;
                float val = Mathf.Max(0f, 1f - dx * dx - dy * dy);
                tex.SetPixel(x, y, new Color(val, val, val, val));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), Vector2.one * 0.5f, sz);
    }

    static Material CreateBlobMaterial()
    {
        var shader = Shader.Find("FluidSandbox/BlobParticle");
        if (shader == null)
        {
            Debug.LogError("MetaballRenderer2D: shader 'FluidSandbox/BlobParticle' no encontrado.");
            return new Material(Shader.Find("Sprites/Default"));
        }
        return new Material(shader);
    }

    Material CreateThresholdMaterial()
    {
        var shader = Shader.Find("FluidSandbox/MetaballThreshold");
        if (shader == null)
        {
            Debug.LogError("MetaballRenderer2D: shader 'FluidSandbox/MetaballThreshold' no encontrado.");
            return new Material(Shader.Find("Sprites/Default"));
        }
        var mat = new Material(shader);
        mat.mainTexture = _blobRT;
        mat.SetFloat("_Threshold",   threshold);
        mat.SetFloat("_Softness",    softness);
        mat.SetColor("_LiquidColor", liquidColor);
        return mat;
    }
}
