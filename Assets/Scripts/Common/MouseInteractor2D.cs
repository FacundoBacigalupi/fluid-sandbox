using UnityEngine;

// Interacción del mouse con las partículas en el mundo 2D.
//
//   Click izquierdo  → spawna partículas en la posición del cursor (verde)
//   Click derecho    → empuja partículas hacia afuera (azul)
//   Click del medio  → atrae partículas hacia el cursor (naranja)
//
[AddComponentMenu("FluidSandbox/Mouse Interactor 2D")]
public class MouseInteractor2D : MonoBehaviour
{
    [Header("Referencias")]
    public RigidbodyParticleSpawner2D spawner;

    [Header("Spawn")]
    [Range(0.05f, 0.5f)] public float emitInterval = 0.08f; // segundos entre cada emisión

    [Header("Fuerza")]
    [Range(0.2f, 5f)]  public float radius    = 1.2f;
    [Range(0f, 100f)]  public float pushForce = 25f;
    [Range(0f, 100f)]  public float pullForce = 15f;

    private Transform    _ring;
    private LineRenderer _lr;
    private float        _emitTimer;
    private readonly Collider2D[] _hits = new Collider2D[256];

    void Awake() => BuildRing();

    void BuildRing()
    {
        var go = new GameObject("MouseRing");
        _ring  = go.transform;

        _lr = go.AddComponent<LineRenderer>();
        _lr.useWorldSpace    = false;
        _lr.loop             = true;
        _lr.widthMultiplier  = 0.04f;
        _lr.material         = new Material(Shader.Find("Sprites/Default"));

        // Círculo de 48 segmentos en espacio local (radio 0.5 → scale lo lleva al mundo)
        const int segs = 48;
        _lr.positionCount = segs;
        for (int i = 0; i < segs; i++)
        {
            float a = i * 2f * Mathf.PI / segs;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
        }

        SetColor(State.Idle);
    }

    void Update()
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        _ring.position   = worldPos;
        _ring.localScale = Vector3.one * (radius * 2f);

        bool spawning = Input.GetMouseButton(0);
        bool pushing  = Input.GetMouseButton(1);
        bool pulling  = Input.GetMouseButton(2);

        SetColor(spawning ? State.Spawn : pushing ? State.Push : pulling ? State.Pull : State.Idle);

        // Spawn continuo con throttle para no inundar la escena
        if (spawning && spawner != null)
        {
            _emitTimer -= Time.deltaTime;
            if (_emitTimer <= 0f)
            {
                spawner.SpawnAtPosition(worldPos);
                _emitTimer = emitInterval;
            }
        }
        else
        {
            _emitTimer = 0f; // al soltar, el próximo click spawna de inmediato
        }

        if (pushing) ApplyForce(worldPos, outward: true,  pushForce);
        if (pulling) ApplyForce(worldPos, outward: false, pullForce);
    }

    void ApplyForce(Vector2 center, bool outward, float strength)
    {
        int count = Physics2D.OverlapCircle(center, radius, new ContactFilter2D().NoFilter(), _hits);
        for (int i = 0; i < count; i++)
        {
            var rb = _hits[i].attachedRigidbody;
            if (rb == null) continue;

            Vector2 dir  = rb.position - center;
            float   dist = dir.magnitude;

            Vector2 normDir = dist < 0.001f
                ? Random.insideUnitCircle.normalized
                : dir / dist;

            if (!outward) normDir = -normDir;

            float falloff = 1f - Mathf.Clamp01(dist / radius);
            rb.AddForce(normDir * strength * falloff, ForceMode2D.Force);
        }
    }

    enum State { Idle, Spawn, Push, Pull }

    void SetColor(State state)
    {
        var c = state switch
        {
            State.Spawn => new Color(0.2f, 1f,  0.3f, 0.9f),  // verde al spawnear
            State.Push  => new Color(0.3f, 0.7f, 1f,  0.9f),  // azul al empujar
            State.Pull  => new Color(1f,   0.4f, 0.2f, 0.9f), // naranja al atraer
            _           => new Color(1f,   1f,   1f,   0.3f), // blanco tenue en reposo
        };
        _lr.startColor = _lr.endColor = c;
    }
}
