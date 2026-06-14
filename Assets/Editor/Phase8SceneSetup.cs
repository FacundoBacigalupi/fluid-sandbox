#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 8 — 3D Custom Particles
//
// Crea la escena 05_3D_Custom_Particles.unity con:
//   - Contenedor 3D (misma jaula de la Phase 7: piso sólido + wireframe + BoxColliders)
//   - FluidSolver3D con integración de Euler y boundary resolution
//   - GPU Instanced renderer (un draw call por lote de 1023 partículas)
//   - OrbitCamera (igual que Phase 7)
public static class Phase8SceneSetup
{
    const string ScenePath = "Assets/Scenes/3D/05_3D_Custom_Particles.unity";

    [MenuItem("FluidSandbox/Phase 8 — 3D Custom Particles")]
    static void Setup()
    {
        CreateScene();
        SetupObjects();
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 8 lista!\n\n" +
            "Controls:\n" +
            "  R            — reset simulación\n" +
            "  LMB          — spawn partículas\n" +
            "  RMB drag     — orbitar cámara\n" +
            "  Scroll       — zoom\n" +
            "  MMB drag     — pan",
            "OK");
    }

    // ── Escena ────────────────────────────────────────────────────────────────

    static void CreateScene()
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), ScenePath.Replace('/', '\\'));
        if (File.Exists(abs)) { EditorSceneManager.OpenScene(ScenePath); return; }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.Refresh();
    }

    // ── Objetos ───────────────────────────────────────────────────────────────

    static void SetupObjects()
    {
        SetupLight();
        var containerGO = SetupContainer(new Vector3(6f, 5f, 6f));
        var boundary    = containerGO.GetComponent<Boundary3D>();
        SetupSimulation(boundary);
        SetupCamera(containerGO);
    }

    static void SetupLight()
    {
        var go = EnsureGO("Directional Light");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = EnsureComp<Light>(go);
        light.type      = LightType.Directional;
        light.intensity = 1.2f;
        light.color     = new Color(1f, 0.97f, 0.9f);
    }

    static GameObject SetupContainer(Vector3 size)
    {
        var go = EnsureGO("Container");
        go.transform.position = Vector3.zero;

        var boundary = EnsureComp<Boundary3D>(go);
        boundary.size        = size;
        boundary.restitution = 0.4f;
        boundary.damping     = 0.98f;

        float hw = size.x * 0.5f, hh = size.y * 0.5f, hd = size.z * 0.5f, t = 0.15f;

        // Piso visible
        CreateWall(go, "Floor",   new Vector3(0,-hh - t*0.5f,0), new Vector3(size.x, t, size.z), visible: true);
        // Paredes solo como colliders (transparentes)
        CreateWall(go, "Left",    new Vector3(-hw,0,0),   new Vector3(t, size.y, size.z), visible: false);
        CreateWall(go, "Right",   new Vector3( hw,0,0),   new Vector3(t, size.y, size.z), visible: false);
        CreateWall(go, "Front",   new Vector3(0,0,-hd),   new Vector3(size.x, size.y, t), visible: false);
        CreateWall(go, "Back",    new Vector3(0,0, hd),   new Vector3(size.x, size.y, t), visible: false);
        CreateWall(go, "Ceiling", new Vector3(0, hh,0),  new Vector3(size.x, t, size.z), visible: false);

        CreateCageWireframe(go, size);
        return go;
    }

    static void CreateWall(GameObject parent, string wallName, Vector3 localPos, Vector3 scale, bool visible)
    {
        var go = EnsureGO(wallName, parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        EnsureComp<BoxCollider>(go);

        if (!visible) return;
        var mf = EnsureComp<MeshFilter>(go);
        mf.sharedMesh = GetCubeMesh();
        var mr  = EnsureComp<MeshRenderer>(go);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.18f, 0.22f, 0.32f, 1f);
        mr.sharedMaterial = mat;
    }

    static void CreateCageWireframe(GameObject parent, Vector3 size)
    {
        var go = EnsureGO("Cage", parent);
        var lr = EnsureComp<LineRenderer>(go);
        lr.useWorldSpace    = false;
        lr.loop             = false;
        lr.startWidth       = lr.endWidth = 0.025f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        lr.material    = mat;
        lr.startColor  = lr.endColor = new Color(0.35f, 0.42f, 0.6f, 0.9f);

        float hw = size.x*0.5f, hh = size.y*0.5f, hd = size.z*0.5f;
        var pts = new Vector3[]
        {
            new(-hw,-hh,-hd), new( hw,-hh,-hd),
            new( hw,-hh, hd), new(-hw,-hh, hd), new(-hw,-hh,-hd),
            new(-hw, hh,-hd),
            new( hw, hh,-hd), new( hw, hh, hd), new(-hw, hh, hd), new(-hw, hh,-hd),
            new(-hw, hh, hd), new(-hw,-hh, hd),
            new( hw,-hh, hd), new( hw, hh, hd),
            new( hw, hh,-hd), new( hw,-hh,-hd),
        };
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
    }

    static void SetupSimulation(Boundary3D boundary)
    {
        // Renderer
        var rendererGO = EnsureGO("ParticleRenderer");
        var pRenderer  = EnsureComp<ParticleRenderer3D>(rendererGO);
        pRenderer.particleColor = new Color(0.3f, 0.6f, 1f, 1f);

        // Spawner — en el tercio superior del contenedor para que caigan
        var spawnerGO = EnsureGO("Spawner");
        spawnerGO.transform.position = new Vector3(0f, 0.5f, 0f);
        var spawner = EnsureComp<FluidSpawner3D>(spawnerGO);
        spawner.spawnArea    = new Vector3(4f, 2f, 4f);
        spawner.spawnPattern = FluidSpawner3D.Pattern.Random;

        // Solver
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver3D>(solverGO);
        solver.spawner          = spawner;
        solver.boundary         = boundary;
        solver.particleRenderer = pRenderer;
        solver.particleCount    = 500;
        solver.particleRadius   = 0.1f;
        solver.gravity          = new Vector3(0f, -12f, 0f);
        solver.substeps         = 3;
    }

    static void SetupCamera(GameObject container)
    {
        var go = EnsureGO("Main Camera");
        go.tag = "MainCamera";

        var cam = EnsureComp<Camera>(go);
        cam.fieldOfView    = 60f;
        cam.nearClipPlane  = 0.1f;
        cam.farClipPlane   = 200f;
        cam.clearFlags     = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);

        EnsureComp<AudioListener>(go);

        var orbit = EnsureComp<OrbitCamera>(go);
        orbit.target   = container.transform;
        orbit.distance = 11f;
        orbit.pitch    = 28f;
        orbit.yaw      = 35f;

        var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
        go.transform.position = container.transform.position - rot * Vector3.forward * orbit.distance;
        go.transform.rotation = rot;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Mesh _cubeMesh;
    static Mesh GetCubeMesh()
    {
        if (_cubeMesh != null) return _cubeMesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return _cubeMesh;
    }

    static GameObject EnsureGO(string name, GameObject parent = null)
    {
        Transform found = parent != null
            ? parent.transform.Find(name)
            : GameObject.Find(name)?.transform;
        if (found != null) return found.gameObject;
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
#endif
