#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 10 — 3D Liquid Rendering
//
// Spec Phase 9: hacer que las partículas 3D parezcan un fluido continuo.
//
// Técnica visual:
//   - Radio visual > radio de simulación (x1.6) → esferas solapadas
//   - Shader WaterParticle: Fresnel + Blinn-Phong especular + soft edges
//   - Soft edges (alpha 0 en silhouette): las esferas solapadas se fusionan como metaballs
//   - Semi-transparente: acumulación de alpha → densidad visual ≈ densidad del fluido
//
// El componente FluidSurfaceRenderer3D puede deshabilitarse para ver debug spheres.
// Presets (clic derecho en componente): Water / Lava / Slime / Mercury
public static class Phase10SceneSetup
{
    const string ScenePath = "Assets/Scenes/3D/07_3D_Liquid.unity";

    [MenuItem("FluidSandbox/Phase 10 — 3D Liquid Rendering")]
    static void Setup()
    {
        CreateScene();
        SetupObjects();
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 10 lista!\n\n" +
            "Renderer visual activo: FluidSurfaceRenderer3D\n" +
            "  → Deshabilitar componente → debug spheres\n\n" +
            "Presets (clic derecho en FluidSurfaceRenderer3D):\n" +
            "  Preset/Water · Lava · Slime · Mercury\n\n" +
            "Controls:\n" +
            "  Space        — spawn batch\n" +
            "  R            — reset\n" +
            "  LMB          — spawn en cursor\n" +
            "  RMB drag     — mover fluido\n" +
            "  RMB drag     — orbitar cámara (OrbitCamera)\n" +
            "  Scroll       — zoom",
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
        var go    = EnsureGO("Directional Light");
        go.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        var light = EnsureComp<Light>(go);
        light.type      = LightType.Directional;
        light.intensity = 1.4f;
        light.color     = new Color(1f, 0.98f, 0.95f);
    }

    static GameObject SetupContainer(Vector3 size)
    {
        var go = EnsureGO("Container");
        go.transform.position = Vector3.zero;

        var boundary = EnsureComp<Boundary3D>(go);
        boundary.size        = size;
        boundary.restitution = 0.2f;
        boundary.damping     = 0.97f;

        float hw = size.x * 0.5f, hh = size.y * 0.5f, hd = size.z * 0.5f, t = 0.15f;

        CreateWall(go, "Floor",   new Vector3(0, -hh - t * 0.5f, 0), new Vector3(size.x, t, size.z),       visible: true);
        CreateWall(go, "Left",    new Vector3(-hw, 0, 0),              new Vector3(t, size.y, size.z),       visible: false);
        CreateWall(go, "Right",   new Vector3( hw, 0, 0),              new Vector3(t, size.y, size.z),       visible: false);
        CreateWall(go, "Front",   new Vector3(0, 0, -hd),              new Vector3(size.x, size.y, t),       visible: false);
        CreateWall(go, "Back",    new Vector3(0, 0,  hd),              new Vector3(size.x, size.y, t),       visible: false);
        CreateWall(go, "Ceiling", new Vector3(0,  hh, 0),             new Vector3(size.x, t, size.z),       visible: false);
        CreateCageWireframe(go, size);
        return go;
    }

    static void CreateWall(GameObject parent, string wallName,
                           Vector3 localPos, Vector3 scale, bool visible)
    {
        var go = EnsureGO(wallName, parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        EnsureComp<BoxCollider>(go);
        if (!visible) return;

        var mf        = EnsureComp<MeshFilter>(go);
        mf.sharedMesh = GetCubeMesh();
        var mr        = EnsureComp<MeshRenderer>(go);
        var mat       = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color     = new Color(0.15f, 0.18f, 0.28f, 1f);
        mr.sharedMaterial = mat;
    }

    static void CreateCageWireframe(GameObject parent, Vector3 size)
    {
        var go = EnsureGO("Cage", parent);
        var lr = EnsureComp<LineRenderer>(go);
        lr.useWorldSpace     = false;
        lr.loop              = false;
        lr.startWidth        = lr.endWidth = 0.02f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material          = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        lr.startColor        = lr.endColor = new Color(0.3f, 0.4f, 0.6f, 0.7f);

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
        // Debug renderer — esferas opacas, habilitadas por defecto como fallback visible.
        // Para ver solo el look de agua: deshabilitar este componente en el Inspector.
        var debugGO = EnsureGO("ParticleRenderer_Debug");
        var debug   = EnsureComp<ParticleRenderer3D>(debugGO);
        debug.particleColor = new Color(0.25f, 0.6f, 1f, 1f);

        // Surface renderer — esferas transparentes solapadas, aspecto de fluido
        var surfaceGO = EnsureGO("SurfaceRenderer");
        var surface   = EnsureComp<FluidSurfaceRenderer3D>(surfaceGO);
        surface.waterColor             = new Color(0.1f, 0.4f, 0.85f, 0.55f);
        surface.smoothness             = 0.88f;
        surface.visualRadiusMultiplier = 1.5f;

        // Spawner
        var spawnerGO = EnsureGO("Spawner");
        spawnerGO.transform.position = Vector3.zero;
        var spawner = EnsureComp<FluidSpawner3D>(spawnerGO);
        spawner.spawnArea    = new Vector3(4f, 3f, 4f);
        spawner.spawnPattern = FluidSpawner3D.Pattern.Grid;

        // Solver — mismos parámetros SPH que Phase 9
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver3D>(solverGO);
        solver.spawner          = spawner;
        solver.boundary         = boundary;
        solver.particleRenderer = debug;
        solver.surfaceRenderer  = surface;
        solver.particleCount       = 600;
        solver.particleRadius      = 0.08f;
        solver.gravity             = new Vector3(0f, -12f, 0f);
        solver.substeps            = 3;
        solver.maxVelocity         = 20f;
        solver.smoothingRadius     = 0.35f;
        solver.restDensity         = 2f;
        solver.pressureMultiplier  = 300f;
        solver.viscosityStrength   = 0.1f;
        solver.batchSize           = 50;
    }

    static void SetupCamera(GameObject container)
    {
        var go = EnsureGO("Main Camera");
        go.tag = "MainCamera";
        var cam = EnsureComp<Camera>(go);
        cam.fieldOfView     = 60f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 200f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.07f);
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
