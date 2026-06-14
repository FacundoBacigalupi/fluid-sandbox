#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// FluidSandbox > Phase 12 — Screen-Space Fluid Rendering
//
// Spec Phase 12 (avanzado):
//   - Screen-Space Fluid Rendering para superficie líquida continua
//   - Spatial Hash O(N×k) para soporte de 5000+ partículas
//   - Preset de agua, miel y slime coordinados (física + visual)
//
// Crea: Assets/Scenes/3D/09_3D_SSF.unity
public static class Phase12SceneSetup
{
    const string ScenePath = "Assets/Scenes/3D/09_3D_SSF.unity";

    [MenuItem("FluidSandbox/Phase 12 — Screen-Space Fluid (SSF)")]
    static void Setup()
    {
        CreateScene();
        SetupObjects();
        AddSSFFeatureToRenderer();
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 12 lista!\n\n" +
            "Screen-Space Fluid Rendering activo.\n" +
            "Spatial Hash habilitado (useSpatialHash = true).\n" +
            "5000 partículas soportadas.\n\n" +
            "Si no ves el efecto SSF, verificar:\n" +
            "  - FluidSSFRenderFeature en el URP Renderer\n" +
            "  - FluidSSFController activo en la escena\n" +
            "  - Shader asignado en el Renderer Feature\n\n" +
            "Presets: click derecho en FluidSolver o SSFController.",
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
        var containerGO = SetupContainer(new Vector3(10f, 7f, 10f));
        var boundary    = containerGO.GetComponent<Boundary3D>();
        SetupSimulation(boundary);
        SetupCamera(containerGO);
    }

    static void SetupLight()
    {
        var go    = EnsureGO("Directional Light");
        go.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        var light = EnsureComp<Light>(go);
        light.type      = LightType.Directional;
        light.intensity = 1.6f;
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

        float hw = size.x*0.5f, hh = size.y*0.5f, hd = size.z*0.5f, t = 0.15f;
        CreateWall(go, "Floor",   new Vector3(0, -hh, 0),            new Vector3(size.x, t, size.z), true);
        CreateWall(go, "Left",    new Vector3(-hw, 0, 0),            new Vector3(t, size.y, size.z), false);
        CreateWall(go, "Right",   new Vector3( hw, 0, 0),            new Vector3(t, size.y, size.z), false);
        CreateWall(go, "Front",   new Vector3(0, 0, -hd),            new Vector3(size.x, size.y, t), false);
        CreateWall(go, "Back",    new Vector3(0, 0,  hd),            new Vector3(size.x, size.y, t), false);
        CreateWall(go, "Ceiling", new Vector3(0,  hh, 0),            new Vector3(size.x, t, size.z), false);
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
        var mf        = EnsureComp<MeshFilter>(go);
        mf.sharedMesh = GetCubeMesh();
        var mr        = EnsureComp<MeshRenderer>(go);
        var mat       = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color     = new Color(0.12f, 0.15f, 0.25f);
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
        lr.startColor        = lr.endColor = new Color(0.3f, 0.4f, 0.6f, 0.6f);

        float hw = size.x*0.5f, hh = size.y*0.5f, hd = size.z*0.5f;
        var pts = new Vector3[]
        {
            new(-hw,-hh,-hd), new( hw,-hh,-hd), new( hw,-hh, hd), new(-hw,-hh, hd), new(-hw,-hh,-hd),
            new(-hw, hh,-hd), new( hw, hh,-hd), new( hw, hh, hd), new(-hw, hh, hd), new(-hw, hh,-hd),
            new(-hw, hh, hd), new(-hw,-hh, hd), new( hw,-hh, hd), new( hw, hh, hd),
            new( hw, hh,-hd), new( hw,-hh,-hd),
        };
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
    }

    static void SetupSimulation(Boundary3D boundary)
    {
        // SSF controller (datos de partículas para el render feature)
        var ssfGO  = EnsureGO("SSFController");
        var ssf    = EnsureComp<FluidSSFController>(ssfGO);
        ssf.waterColor  = new Color(0.05f, 0.35f, 0.80f, 0.6f);
        ssf.deepColor   = new Color(0.02f, 0.10f, 0.40f, 1f);
        ssf.smoothness  = 0.92f;
        ssf.fresnelPow  = 3.5f;
        ssf.opacity     = 0.88f;
        ssf.blurRadius  = 10f;
        ssf.blurFalloff = 2.0f;
        ssf.depthScale  = 0.08f;
        ssf.visualRadiusMultiplier = 1.6f;

        // Surface renderer de respaldo — garantiza visibilidad aunque el render feature falle
        var surface = EnsureComp<FluidSurfaceRenderer3D>(ssfGO);
        surface.waterColor             = new Color(0.08f, 0.38f, 0.82f, 0.50f);
        surface.smoothness             = 0.90f;
        surface.visualRadiusMultiplier = 1.5f;

        // Spawner
        var spawnerGO = EnsureGO("Spawner");
        spawnerGO.transform.position = Vector3.zero;
        var spawner = EnsureComp<FluidSpawner3D>(spawnerGO);
        spawner.spawnArea    = new Vector3(7f, 5f, 7f);
        spawner.spawnPattern = FluidSpawner3D.Pattern.Grid;

        // Solver — 5000 partículas, Spatial Hash
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver3D>(solverGO);
        solver.spawner            = spawner;
        solver.boundary           = boundary;
        solver.particleRenderer   = null;
        solver.surfaceRenderer    = surface; // FluidSurfaceRenderer3D en SSFController GO
        solver.particleCount      = 2000;   // empezar con 2000, subir a 5000 si el hardware lo permite
        solver.particleRadius     = 0.07f;
        solver.gravity            = new Vector3(0f, -12f, 0f);
        solver.substeps           = 2;
        solver.maxVelocity        = 20f;
        solver.smoothingRadius    = 0.3f;
        solver.restDensity        = 2f;
        solver.pressureMultiplier = 300f;
        solver.viscosityStrength  = 0.1f;
        solver.batchSize          = 200;
        solver.useJobSystem       = true;
        solver.useSpatialHash     = true;

        // Conectar SSF controller con el solver
        ssf.solver    = solver;
        surface.enabled = true;
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
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        EnsureComp<AudioListener>(go);
        var orbit = EnsureComp<OrbitCamera>(go);
        orbit.target   = container.transform;
        orbit.distance = 18f;
        orbit.pitch    = 25f;
        orbit.yaw      = 40f;
        var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
        go.transform.position = container.transform.position - rot * Vector3.forward * orbit.distance;
        go.transform.rotation = rot;
    }

    // ── Auto-agregar FluidSSFRenderFeature al URP Renderer ───────────────────

    static void AddSSFFeatureToRenderer()
    {
        // Buscar el UniversalRendererData activo
        var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[Phase12] No se encontró UniversalRendererData. " +
                             "Agregar FluidSSFRenderFeature manualmente al URP Renderer.");
            return;
        }

        // Buscar el shader FluidSSF
        var shaderGuids = AssetDatabase.FindAssets("FluidSSF t:Shader");
        Shader ssfShader = null;
        foreach (var sg in shaderGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(sg);
            ssfShader = AssetDatabase.LoadAssetAtPath<Shader>(p);
            if (ssfShader != null) break;
        }

        foreach (var guid in guids)
        {
            var path     = AssetDatabase.GUIDToAssetPath(guid);
            var rendData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rendData == null) continue;

            // Verificar si ya existe
            bool alreadyExists = false;
            foreach (var f in rendData.rendererFeatures)
                if (f is FluidSSFRenderFeature) { alreadyExists = true; break; }

            if (alreadyExists) continue;

            var feature = ScriptableObject.CreateInstance<FluidSSFRenderFeature>();
            feature.name = "Fluid SSF";
            if (ssfShader != null) feature.settings.ssfShader = ssfShader;

            AssetDatabase.AddObjectToAsset(feature, rendData);
            rendData.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(rendData);

            Debug.Log($"[Phase12] FluidSSFRenderFeature agregado a: {path}" +
                      (ssfShader == null ? " (shader no asignado — asignar manualmente)" : ""));
        }

        AssetDatabase.SaveAssets();
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    static Mesh _cubeMesh;
    static Mesh GetCubeMesh()
    {
        if (_cubeMesh != null) return _cubeMesh;
        var tmp   = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
