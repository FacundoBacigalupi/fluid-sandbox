using System.Collections.Generic;
using UnityEngine;

// Phase 7 — 3D Sphere Particle Prototype
// Esferas con Rigidbody + SphereCollider dentro de un contenedor 3D.
// Análogo a RigidbodyParticleSpawner2D pero en 3D, sin prefab (usa CreatePrimitive).
[AddComponentMenu("FluidSandbox/Rigidbody Particle Spawner 3D")]
public class RigidbodyParticleSpawner3D : MonoBehaviour
{
    [Header("Spawn")]
    public int     particleCount  = 80;
    [Range(0.05f, 0.5f)] public float   particleRadius = 0.15f;
    public Vector3 spawnAreaSize  = new Vector3(3f, 2f, 3f);
    public Color   particleColor  = new Color(0.3f, 0.6f, 1f, 1f);

    [Header("Física")]
    [Range(0f, 1f)] public float bounciness   = 0.2f;
    [Range(0f, 1f)] public float friction      = 0.05f;
    [Range(0f, 2f)] public float linearDrag    = 0.1f;
    [Range(0f, 2f)] public float angularDrag   = 0.05f;

    [Header("Controles")]
    public KeyCode spawnKey = KeyCode.Space;
    public KeyCode resetKey = KeyCode.R;

    private readonly List<GameObject> _particles = new();
    private PhysicsMaterial _physicsMat;
    private Material        _sphereMat;

    void Awake()
    {
        _physicsMat = new PhysicsMaterial("SphereMat3D");
        SyncPhysicsMaterial();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        _sphereMat = new Material(shader ?? Shader.Find("Standard"));
        _sphereMat.color = particleColor;
    }

    void Start() => SpawnParticles();

    void Update()
    {
        if (Input.GetKeyDown(spawnKey)) SpawnParticles();
        if (Input.GetKeyDown(resetKey)) ResetSimulation();
    }

    // ── Acciones ──────────────────────────────────────────────────────────────

    public void SpawnParticles()
    {
        SyncPhysicsMaterial();
        for (int i = 0; i < particleCount; i++)
        {
            float x = Random.Range(-spawnAreaSize.x * 0.5f,  spawnAreaSize.x * 0.5f);
            float y = Random.Range(0f,                        spawnAreaSize.y);
            float z = Random.Range(-spawnAreaSize.z * 0.5f,  spawnAreaSize.z * 0.5f);
            CreateParticle(transform.position + new Vector3(x, y, z));
        }
    }

    public void ResetSimulation()
    {
        foreach (var p in _particles)
            if (p != null) Destroy(p);
        _particles.Clear();
    }

    public void ApplyPhysicsToAll()
    {
        SyncPhysicsMaterial();
        foreach (var p in _particles)
        {
            if (p == null) continue;
            var rb  = p.GetComponent<Rigidbody>();
            var col = p.GetComponent<SphereCollider>();
            if (rb  != null) ApplyRigidbodySettings(rb);
            if (col != null) col.material = _physicsMat;
        }
    }

    // ── Presets (clic derecho en el Inspector) ────────────────────────────────

    [ContextMenu("Preset / Water")]
    void PresetWater()  { bounciness = 0.05f; friction = 0.02f; linearDrag = 0.3f;  angularDrag = 0.1f;  ApplyPhysicsToAll(); }

    [ContextMenu("Preset / Honey")]
    void PresetHoney()  { bounciness = 0.0f;  friction = 0.3f;  linearDrag = 1.8f;  angularDrag = 1.0f;  ApplyPhysicsToAll(); }

    [ContextMenu("Preset / Bouncy")]
    void PresetBouncy() { bounciness = 0.85f; friction = 0.01f; linearDrag = 0.05f; angularDrag = 0.02f; ApplyPhysicsToAll(); }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void CreateParticle(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Particle3D";
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * (particleRadius * 2f);

        go.GetComponent<MeshRenderer>().sharedMaterial = _sphereMat;

        var rb  = go.AddComponent<Rigidbody>();
        ApplyRigidbodySettings(rb);

        var col = go.GetComponent<SphereCollider>();
        col.material = _physicsMat;

        _particles.Add(go);
    }

    void ApplyRigidbodySettings(Rigidbody rb)
    {
        rb.linearDamping  = linearDrag;
        rb.angularDamping = angularDrag;
    }

    void SyncPhysicsMaterial()
    {
        if (_physicsMat == null) return;
        _physicsMat.bounciness      = bounciness;
        _physicsMat.staticFriction  = friction;
        _physicsMat.dynamicFriction = friction;
    }

    public int ActiveParticleCount => _particles.Count;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * spawnAreaSize.y * 0.5f, spawnAreaSize);
    }
}
