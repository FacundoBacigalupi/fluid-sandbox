using System.Collections.Generic;
using UnityEngine;

// Phase 1 & 2 — 2D Rigidbody Circle Sandbox
// Key concepts: Rigidbody2D, CircleCollider2D, PhysicsMaterial2D, gravity, drag, bounciness.
[AddComponentMenu("FluidSandbox/Rigidbody Particle Spawner 2D")]
public class RigidbodyParticleSpawner2D : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject particlePrefab;

    [Header("Spawn Settings")]
    public int     particleCount  = 60;
    public float   particleRadius = 0.15f;
    public Vector2 spawnAreaSize  = new Vector2(3f, 1.5f);

    [Header("Mouse Spawn")]
    [Range(0.05f, 1f)] public float spawnScatter = 0.25f;
    [Range(1, 10)]     public int   spawnPerClick = 2;

    [Header("Physics Settings")]
    [Range(0f, 3f)] public float gravityScale = 1f;
    [Range(0f, 1f)] public float bounciness   = 0.2f;
    [Range(0f, 1f)] public float friction      = 0.05f;
    [Range(0f, 2f)] public float linearDrag    = 0.1f;

    [Header("Controls")]
    public KeyCode spawnKey = KeyCode.Space;
    public KeyCode resetKey = KeyCode.R;

    private readonly List<GameObject> _particles = new();
    private PhysicsMaterial2D _material;

    void Awake()
    {
        _material = new PhysicsMaterial2D("ParticleMat");
    }

    void Start()
    {
        SpawnParticles();
    }

    void Update()
    {
        if (Input.GetKeyDown(spawnKey)) SpawnParticles();
        if (Input.GetKeyDown(resetKey)) ResetSimulation();
    }

    // Spawn el lote completo dentro del área definida (tecla Space o botón UI).
    public void SpawnParticles()
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning("[Spawner] particlePrefab not assigned. Run FluidSandbox > Setup Phase 1 Scene first.");
            return;
        }

        SyncMaterial();

        for (int i = 0; i < particleCount; i++)
        {
            float x = Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f);
            float y = Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f);
            CreateParticle(transform.position + new Vector3(x, y, 0f));
        }
    }

    // Spawn unas pocas partículas en una posición del mundo (llamado por el mouse).
    public void SpawnAtPosition(Vector2 worldPos)
    {
        if (particlePrefab == null) return;
        SyncMaterial();

        for (int i = 0; i < spawnPerClick; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnScatter;
            CreateParticle(new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, 0f));
        }
    }

    public void ResetSimulation()
    {
        foreach (var p in _particles)
            if (p != null) Destroy(p);
        _particles.Clear();
    }

    // Propaga cambios de física a todas las partículas existentes.
    public void ApplyPhysicsToAll()
    {
        SyncMaterial();
        foreach (var p in _particles)
        {
            if (p == null) continue;
            var rb  = p.GetComponent<Rigidbody2D>();
            var col = p.GetComponent<CircleCollider2D>();
            if (rb  != null) SetRigidbody(rb);
            if (col != null) col.sharedMaterial = _material;
        }
    }

    void CreateParticle(Vector3 pos)
    {
        var go = Instantiate(particlePrefab, pos, Quaternion.identity);
        // El prefab tiene radio local 0.5 → scale = radius * 2 da el tamaño en el mundo.
        go.transform.localScale = Vector3.one * (particleRadius * 2f);
        var rb  = go.GetComponent<Rigidbody2D>();
        var col = go.GetComponent<CircleCollider2D>();
        if (rb  != null) SetRigidbody(rb);
        if (col != null) col.sharedMaterial = _material;
        _particles.Add(go);
    }

    void SetRigidbody(Rigidbody2D rb)
    {
        rb.gravityScale  = gravityScale;
        rb.linearDamping = linearDrag;
    }

    void SyncMaterial()
    {
        _material.bounciness = bounciness;
        _material.friction   = friction;
    }

    public int ActiveParticleCount => _particles.Count;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
    }
}
