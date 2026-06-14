#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 11 — Optimization (Job System + Burst)
//
// Spec Phase 10: misma simulación SPH pero con Job System + Burst activado.
// Soporta 2000 partículas a 50 Hz gracias a Burst SIMD + paralelismo multi-core.
//
// Diferencias con Phase 9/10:
//   - particleCount = 2000
//   - useJobSystem  = true  (SPHDensityJob + SPHForceJob + SPHIntegrateJob)
//   - batchSize     = 100
public static class Phase11SceneSetup
{
    const string ScenePath = "Assets/Scenes/3D/08_3D_Optimized.unity";

    [MenuItem("FluidSandbox/Phase 11 — Optimization (Job System + Burst)")]
    static void Setup()
    {
        CreateScene();
        SetupObjects();
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 11 lista!\n\n" +
            "Job System + Burst activado (useJobSystem = true)\n" +
            "2000 partículas a 50 Hz.\n\n" +
            "Para comparar: desactivar useJobSystem en FluidSolver\n" +
            "y ver el lag → reactivarlo para recuperar el rendimiento.\n\n" +
            "Controls: Space = +100 partículas | R = reset",
            "OK");
    }

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

    static void SetupObjects()
    {
        SetupLight();
        var containerGO = SetupContainer(new Vector3(8f, 6f, 8f));
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
        light.color     = new Color(1f, 0.97f, 0.93f);
    }

    static GameObject SetupContainer(Vector3 size)
    {
        var go = EnsureGO("Container");
        go.transform.position = Vector3.zero;

        var boundary = EnsureComp<Boundary3D>(go);
        boundary.size        = size;
        boundary.restitution = 0.2f;
        boundary.damping     = 0.97f;

        float hw = size.x*0.5f, hh = size.y*0.5f, hd = size.z*0.5f, t = 0.15f;

        CreateWall(go, "Floor",   new Vector3(0, -hh - t*0.5f, 0), new Vector3(size.x, t, size.z),       true);
        CreateWall(go, "Left",    new Vector3(-hw, 0, 0),            new Vector3(t, size.y, size.z),       false);
        CreateWall(go, "Right",   new Vector3( hw, 0, 0),            new Vector3(t, size.y, size.z),       false);
        CreateWall(go, "Front",   new Vector3(0, 0, -hd),            new Vector3(size.x, size.y, t),       false);
        CreateWall(go, "Back",    new Vector3(0, 0,  hd),            new Vector3(size.x, size.y, t),       false);
        CreateWall(go, "Ceiling", new Vector3(0,  hh, 0),           new Vector3(size.x, t, size.z),       false);
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
        mat.color     = new Color(0.15f, 0.18f, 0.28f);
        mr.sharedMaterial = mat;
    }

    static void CreateCageWireframe(GameObject parent, Vector3 size)
    {
        var go = EnsureGO("Cage", parent);
        var lr = EnsureComp<LineRenderer>(go);
        lr.useWorldSpace     = false;
        lr.loop              = false;
        lr.startWidth        = lr.endWidth = 0.025f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material          = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        lr.startColor        = lr.endColor = new Color(0.3f, 0.4f, 0.6f, 0.8f);

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
        // Debug renderer
        var debugGO = EnsureGO("ParticleRenderer_Debug");
        var debug   = EnsureComp<ParticleRenderer3D>(debugGO);
        debug.particleColor = new Color(0.25f, 0.6f, 1f, 1f);

        // Surface renderer (agua)
        var surfaceGO = EnsureGO("SurfaceRenderer");
        var surface   = EnsureComp<FluidSurfaceRenderer3D>(surfaceGO);
        surface.waterColor             = new Color(0.1f, 0.4f, 0.85f, 0.55f);
        surface.smoothness             = 0.88f;
        surface.visualRadiusMultiplier = 1.5f;

        // Spawner — volumen más grande para 2000 partículas
        var spawnerGO = EnsureGO("Spawner");
        spawnerGO.transform.position = Vector3.zero;
        var spawner = EnsureComp<FluidSpawner3D>(spawnerGO);
        spawner.spawnArea    = new Vector3(5f, 4f, 5f);
        spawner.spawnPattern = FluidSpawner3D.Pattern.Grid;

        // Solver — 2000 partículas + Job System habilitado
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver3D>(solverGO);
        solver.spawner             = spawner;
        solver.boundary            = boundary;
        solver.particleRenderer    = debug;
        solver.surfaceRenderer     = surface;
        solver.particleCount       = 2000;
        solver.particleRadius      = 0.07f;
        solver.gravity             = new Vector3(0f, -12f, 0f);
        solver.substeps            = 2;
        solver.maxVelocity         = 20f;
        solver.smoothingRadius     = 0.3f;
        solver.restDensity         = 2f;
        solver.pressureMultiplier  = 300f;
        solver.viscosityStrength   = 0.1f;
        solver.batchSize           = 100;
        solver.useJobSystem        = true;    // ← Burst activado
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
        orbit.distance = 14f;
        orbit.pitch    = 28f;
        orbit.yaw      = 35f;
        var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
        go.transform.position = container.transform.position - rot * Vector3.forward * orbit.distance;
        go.transform.rotation = rot;
    }

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
