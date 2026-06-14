#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 7 — 3D Sphere Prototype
//
// Crea la escena 04_3D_Spheres.unity con:
//   - Directional Light
//   - Contenedor 3D (piso visible + paredes con BoxCollider)
//   - RigidbodyParticleSpawner3D
//   - Cámara perspectiva con OrbitCamera
public static class Phase7SceneSetup
{
    const string ScenePath = "Assets/Scenes/3D/04_3D_Spheres.unity";

    [MenuItem("FluidSandbox/Phase 7 — 3D Sphere Prototype")]
    static void Setup()
    {
        CreateScene();
        SetupObjects();
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 7 lista!\n\n" +
            "Controls:\n" +
            "  Space        — spawn partículas\n" +
            "  R            — reset\n" +
            "  RMB drag     — orbitar cámara\n" +
            "  Scroll       — zoom\n" +
            "  MMB drag     — pan\n\n" +
            "Presets: clic derecho en RigidbodyParticleSpawner3D",
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
        SetupSpawner(containerGO);
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

        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        float hd = size.z * 0.5f;
        float t  = 0.15f;

        // Piso visible (sólido)
        CreateWall(go, "Floor",  new Vector3(0, -hh, 0), new Vector3(size.x, t, size.z), visible: true,  alpha: 1f);
        // Paredes laterales (solo collider, transparentes)
        CreateWall(go, "Left",   new Vector3(-hw, 0, 0),  new Vector3(t, size.y, size.z), visible: false, alpha: 0f);
        CreateWall(go, "Right",  new Vector3( hw, 0, 0),  new Vector3(t, size.y, size.z), visible: false, alpha: 0f);
        CreateWall(go, "Front",  new Vector3(0, 0, -hd),  new Vector3(size.x, size.y, t), visible: false, alpha: 0f);
        CreateWall(go, "Back",   new Vector3(0, 0,  hd),  new Vector3(size.x, size.y, t), visible: false, alpha: 0f);
        // Techo (solo collider)
        CreateWall(go, "Ceiling",new Vector3(0,  hh, 0), new Vector3(size.x, t, size.z), visible: false, alpha: 0f);

        // Cage wireframe visual usando LineRenderer
        CreateCageWireframe(go, size);

        return go;
    }

    static void CreateWall(GameObject parent, string wallName, Vector3 localPos, Vector3 scale,
                           bool visible, float alpha)
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
        mat.color = new Color(0.18f, 0.22f, 0.32f, alpha);
        mr.sharedMaterial = mat;
    }

    // Dibuja las aristas del contenedor con un LineRenderer
    static void CreateCageWireframe(GameObject parent, Vector3 size)
    {
        var go = EnsureGO("Cage", parent);
        var lr = EnsureComp<LineRenderer>(go);

        lr.useWorldSpace = false;
        lr.loop          = false;
        lr.startWidth    = lr.endWidth = 0.025f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        lr.material = new Material(shader);
        lr.startColor = lr.endColor = new Color(0.35f, 0.42f, 0.6f, 0.9f);

        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        float hd = size.z * 0.5f;

        // Recorre las 12 aristas del cubo como una línea continua (con saltos)
        Vector3[] pts = new Vector3[]
        {
            // Borde inferior
            new(-hw,-hh,-hd), new( hw,-hh,-hd),
            new( hw,-hh, hd), new(-hw,-hh, hd), new(-hw,-hh,-hd),
            // Subir arista frontal-izquierda
            new(-hw, hh,-hd),
            // Borde superior
            new( hw, hh,-hd), new( hw, hh, hd), new(-hw, hh, hd), new(-hw, hh,-hd),
            // Pilares restantes
            new(-hw, hh, hd), new(-hw,-hh, hd),
            new( hw,-hh, hd), new( hw, hh, hd),
            new( hw, hh,-hd), new( hw,-hh,-hd),
        };

        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
    }

    static void SetupSpawner(GameObject container)
    {
        var go = EnsureGO("Spawner");
        // Posición en el centro del contenedor, un poco arriba del fondo
        go.transform.position = new Vector3(0f, 0.5f, 0f);
        var spawner = EnsureComp<RigidbodyParticleSpawner3D>(go);
        spawner.particleCount = 80;
        spawner.particleRadius = 0.15f;
        spawner.spawnAreaSize  = new Vector3(4f, 2f, 4f);
    }

    static void SetupCamera(GameObject container)
    {
        var go = EnsureGO("Main Camera");
        go.tag = "MainCamera";

        var cam = EnsureComp<Camera>(go);
        cam.fieldOfView   = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane  = 200f;
        cam.clearFlags    = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);

        EnsureComp<AudioListener>(go);

        var orbit = EnsureComp<OrbitCamera>(go);
        orbit.target   = container.transform;
        orbit.distance = 11f;
        orbit.pitch    = 28f;
        orbit.yaw      = 35f;

        // Posicionar la cámara según los parámetros de órbita iniciales
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
