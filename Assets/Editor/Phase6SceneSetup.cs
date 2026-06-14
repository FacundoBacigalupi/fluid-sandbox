#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FluidSandbox > Phase 6 — 2D Metaballs
//
// Crea la escena 03_2D_Metaballs.unity, que reutiliza la simulación SPH de Phase 5
// y agrega MetaballRenderer2D para el efecto de fluido visual.
//
// Cambios respecto a Phase 3:
//   - Agrega la layer "Blob" al proyecto (usada por el BlobCamera).
//   - La Main Camera excluye la layer "Blob" (la BlobCamera la renderiza sola).
//   - MetaballRenderer2D gestiona la BlobCamera, el pool de blobs y el Canvas.
public static class Phase6SceneSetup
{
    const string ScenePath  = "Assets/Scenes/2D/03_2D_Metaballs.unity";
    const string SpritePath = "Assets/Materials/2D/CircleParticle.png";

    [MenuItem("FluidSandbox/Phase 6 — 2D Metaballs")]
    static void Setup()
    {
        int blobLayer = EnsureBlobLayer();
        CreateScene();
        var sprite = EnsureCircleSprite();
        SetupSceneObjects(sprite, blobLayer);
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 6 lista!\n\n" +
            "Press Play para ver el efecto metaball.\n\n" +
            "Controls:\n" +
            "  M — toggle metaball / círculos\n" +
            "  R — reset simulación",
            "OK");
    }

    // ── Layer ─────────────────────────────────────────────────────────────────

    static int EnsureBlobLayer()
    {
        int existing = LayerMask.NameToLayer("Blob");
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            var prop = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = "Blob";
                tagManager.ApplyModifiedProperties();
                AssetDatabase.Refresh();
                Debug.Log($"MetaballRenderer2D: layer 'Blob' agregada en slot {i}.");
                return i;
            }
        }

        Debug.LogWarning("Phase 6: no se encontró un slot libre para la layer 'Blob'.");
        return -1;
    }

    // ── Escena ────────────────────────────────────────────────────────────────

    static void CreateScene()
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(),
                                  ScenePath.Replace('/', '\\'));
        if (File.Exists(abs)) { EditorSceneManager.OpenScene(ScenePath); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                NewSceneMode.Single);
        var camGO  = new GameObject("Main Camera");
        camGO.tag  = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        var cam    = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = Color.black;
        cam.orthographic     = true;
        cam.orthographicSize = 4f;
        camGO.AddComponent<AudioListener>();

        Directory.CreateDirectory("Assets/Scenes/2D");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    // ── Objetos de escena ─────────────────────────────────────────────────────

    static void SetupSceneObjects(Sprite sprite, int blobLayer)
    {
        // Boundary
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

        // ParticleRenderer (círculos — se oculta cuando metaballs está activo)
        var rendererGO = EnsureGO("ParticleRenderer");
        var pRenderer  = EnsureComp<ParticleRenderer2D>(rendererGO);
        pRenderer.circleSprite = sprite;

        // FluidSolver
        var solverGO = EnsureGO("FluidSolver");
        var solver   = EnsureComp<FluidSolver2D>(solverGO);
        solver.spawner          = spawner;
        solver.boundary         = boundary;
        solver.particleRenderer = pRenderer;
        solver.particleCount    = 300;
        solver.particleRadius   = 0.08f;

        // MetaballRenderer
        var metaGO   = EnsureGO("MetaballRenderer");
        var metaRend = EnsureComp<MetaballRenderer2D>(metaGO);
        metaRend.solver         = solver;
        metaRend.circleRenderer = pRenderer;

        // Main camera: excluir layer Blob para que no renderice los blobs directamente
        var mainCam = Object.FindFirstObjectByType<Camera>();
        if (mainCam != null && blobLayer >= 0)
            mainCam.cullingMask &= ~(1 << blobLayer);
    }

    // ── Sprite (mismo helper que Phase 3) ─────────────────────────────────────

    static Sprite EnsureCircleSprite()
    {
        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(),
                                     SpritePath.Replace('/', '\\'))))
        {
            EnsureSpriteImport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float ctr = size * 0.5f, r = ctr - 0.5f, edge = r * 0.08f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x-ctr+.5f)*(x-ctr+.5f)+(y-ctr+.5f)*(y-ctr+.5f));
                float a = 1f - Mathf.Clamp01((d-(r-edge))/edge);
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
        if (imp == null || (imp.textureType == TextureImporterType.Sprite
                            && imp.alphaIsTransparency)) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
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
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void EnsureWalls(GameObject parent, Vector2 containerSize)
    {
        if (parent.transform.Find("Bottom") != null) return;
        float hw = containerSize.x * 0.5f, hh = containerSize.y * 0.5f, wt = 0.2f;
        CreateWall(parent, "Bottom", new Vector2(0, -hh - wt*.5f), new Vector2(containerSize.x + wt*2, wt));
        CreateWall(parent, "Top",    new Vector2(0,  hh + wt*.5f), new Vector2(containerSize.x + wt*2, wt));
        CreateWall(parent, "Left",   new Vector2(-hw - wt*.5f, 0), new Vector2(wt, containerSize.y));
        CreateWall(parent, "Right",  new Vector2( hw + wt*.5f, 0), new Vector2(wt, containerSize.y));
    }

    static void CreateWall(GameObject parent, string wallName, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
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
        _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
        return _square;
    }
}
#endif
