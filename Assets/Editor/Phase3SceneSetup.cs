#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 3 — Custom 2D Particle Simulation
//
// Crea la escena 02_Custom_2D_Particles.unity y la configura:
//   1. Cámara ortográfica con fondo negro.
//   2. Boundary2D con paredes visuales.
//   3. FluidSpawner2D, FluidSolver2D, ParticleRenderer2D conectados entre sí.
//   4. Sprite de círculo asignado al renderer (reutiliza el PNG de fase 1).
public static class Phase3SceneSetup
{
    const string ScenePath  = "Assets/Scenes/2D/02_Custom_2D_Particles.unity";
    const string SpritePath = "Assets/Materials/2D/CircleParticle.png";

    [MenuItem("FluidSandbox/Phase 3 — Custom 2D Particle Simulation")]
    static void Setup()
    {
        CreateScene();
        var sprite = EnsureCircleSprite();
        SetupSceneObjects(sprite);
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 3 lista!\n\nPress Play para ver la simulación custom.\n\nControls:\n  R — reset",
            "OK");
    }

    // ── Escena ────────────────────────────────────────────────────────────────

    static void CreateScene()
    {
        string absPath = Path.Combine(Directory.GetCurrentDirectory(), ScenePath.Replace('/', '\\'));
        if (File.Exists(absPath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = Color.black;
        cam.orthographic     = true;
        cam.orthographicSize = 4f;
        camGO.AddComponent<AudioListener>();

        Directory.CreateDirectory("Assets/Scenes/2D");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    // ── Sprite de círculo ─────────────────────────────────────────────────────

    static Sprite EnsureCircleSprite()
    {
        // Reimportar como Sprite si ya existe el PNG
        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SpritePath.Replace('/', '\\'))))
        {
            EnsureSpriteImport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // Crear el PNG desde cero
        const int size = 64;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float ctr = size * 0.5f;
        float r   = ctr - 0.5f;
        float edge = r * 0.08f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - ctr + 0.5f) * (x - ctr + 0.5f) +
                                     (y - ctr + 0.5f) * (y - ctr + 0.5f));
                float a = 1f - Mathf.Clamp01((d - (r - edge)) / edge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        Directory.CreateDirectory("Assets/Materials/2D");
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(SpritePath);

        EnsureSpriteImport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static void EnsureSpriteImport()
    {
        var imp = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (imp == null) return;
        if (imp.textureType == TextureImporterType.Sprite && imp.alphaIsTransparency) return;

        imp.textureType         = TextureImporterType.Sprite;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
    }

    // ── Objetos de escena ─────────────────────────────────────────────────────

    static void SetupSceneObjects(Sprite sprite)
    {
        // Boundary (paredes físicas)
        var boundaryGO = EnsureGO("Boundary");
        var boundary   = EnsureComp<Boundary2D>(boundaryGO);
        boundary.size  = new Vector2(8f, 6f);
        EnsureWalls(boundaryGO, boundary.size);

        // Spawner
        var spawnerGO = EnsureGO("Spawner");
        spawnerGO.transform.position = Vector3.up * 1f;
        var spawner = EnsureComp<FluidSpawner2D>(spawnerGO);
        spawner.spawnArea    = new Vector2(5f, 3f);
        spawner.spawnPattern = FluidSpawner2D.Pattern.Random;

        // Renderer — usa pooled SpriteRenderers, requiere un Sprite
        var rendererGO = EnsureGO("ParticleRenderer");
        var pRenderer  = EnsureComp<ParticleRenderer2D>(rendererGO);
        pRenderer.circleSprite = sprite;

        // Solver — conecta todo
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver2D>(solverGO);
        solver.spawner          = spawner;
        solver.boundary         = boundary;
        solver.particleRenderer = pRenderer;
        solver.particleCount    = 300;
        solver.particleRadius   = 0.08f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject EnsureGO(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static T EnsureComp<T>(GameObject go) where T : Component
        => go.GetComponent<T>() ?? go.AddComponent<T>();

    static void EnsureWalls(GameObject parent, Vector2 containerSize)
    {
        if (parent.transform.Find("Bottom") != null) return;

        float hw = containerSize.x * 0.5f;
        float hh = containerSize.y * 0.5f;
        float wt = 0.2f;

        CreateWall(parent, "Bottom", new Vector2(0, -hh - wt * 0.5f), new Vector2(containerSize.x + wt * 2f, wt));
        CreateWall(parent, "Top",    new Vector2(0,  hh + wt * 0.5f), new Vector2(containerSize.x + wt * 2f, wt));
        CreateWall(parent, "Left",   new Vector2(-hw - wt * 0.5f, 0), new Vector2(wt, containerSize.y));
        CreateWall(parent, "Right",  new Vector2( hw + wt * 0.5f, 0), new Vector2(wt, containerSize.y));
    }

    static void CreateWall(GameObject parent, string wallName, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = new Vector3(size.x, size.y, 1f);

        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color  = new Color(0.25f, 0.28f, 0.35f, 1f);
        sr.sortingOrder = -1;
    }

    static Sprite _square;
    static Sprite GetWhiteSprite()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        return _square;
    }
}
#endif
