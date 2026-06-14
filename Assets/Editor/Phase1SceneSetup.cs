#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the menu item:  FluidSandbox > Setup Phase 1 Scene
//
// What it does:
//   1. Opens 01_Rigidbody_Circles.unity (if not already open).
//   2. Creates a soft circle sprite texture and saves it as a PNG asset.
//   3. Creates Assets/Prefabs/2D/RigidbodyCircleParticle.prefab.
//   4. Adds Container, Spawner, and UIController GameObjects to the scene.
//   5. Saves the scene.
//
// Run once, then press Play.
public static class Phase1SceneSetup
{
    const string ScenePath  = "Assets/Scenes/2D/01_Rigidbody_Circles.unity";
    const string PrefabPath = "Assets/Prefabs/2D/RigidbodyCircleParticle.prefab";
    const string SpritePath = "Assets/Materials/2D/CircleParticle.png";

    [MenuItem("FluidSandbox/Phase 1 — 2D Rigidbody Circle Sandbox")]
    static void Setup()
    {
        OpenScene();
        Sprite circle = EnsureCircleSprite();
        GameObject prefab = EnsureParticlePrefab(circle);
        EnsureSceneObjects(prefab);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[Phase1Setup] Scene ready. Press Play to run the simulation.");
        EditorUtility.DisplayDialog("Fluid Sandbox",
            "Phase 1 scene is ready!\n\nPress Play to start the simulation.\n\nControls:\n  Space — spawn particles\n  R — reset",
            "Got it");
    }

    // ── Step 1: Open scene ────────────────────────────────────────────────────

    static void OpenScene()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);
    }

    // ── Step 2: Circle sprite ────────────────────────────────────────────────

    static Sprite EnsureCircleSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
        File.WriteAllBytes(SpritePath, BuildCircleTexture(64).EncodeToPNG());
        AssetDatabase.ImportAsset(SpritePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode          = FilterMode.Bilinear;
        importer.maxTextureSize      = 64;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    // Procedural soft-edged circle on transparent background.
    static Texture2D BuildCircleTexture(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float ctr  = size * 0.5f;
        float outerR = ctr - 0.5f;
        float edgeW  = outerR * 0.08f; // soft edge width in pixels

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - ctr + 0.5f) * (x - ctr + 0.5f) +
                                        (y - ctr + 0.5f) * (y - ctr + 0.5f));
                float alpha = 1f - Mathf.Clamp01((dist - (outerR - edgeW)) / edgeW);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    // ── Step 3: Particle prefab ───────────────────────────────────────────────

    static GameObject EnsureParticlePrefab(Sprite circle)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        var go = new GameObject("RigidbodyCircleParticle");

        // Visual
        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.color  = new Color(0.25f, 0.55f, 1f, 0.88f); // water-blue

        // Physics collider — radius 0.5 in local space; spawner scales the GO.
        var col    = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        // Rigidbody
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale            = 1f;
        rb.linearDamping           = 0.1f;
        rb.angularDamping          = 0.5f;
        rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;
        rb.interpolation           = RigidbodyInterpolation2D.Interpolate;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── Step 4: Scene objects ────────────────────────────────────────────────

    static void EnsureSceneObjects(GameObject prefab)
    {
        // Container
        if (GameObject.Find("Container") == null)
        {
            var containerGO = new GameObject("Container");
            var walls       = containerGO.AddComponent<ContainerWalls2D>();
            walls.containerSize = new Vector2(8f, 6f);
            Undo.RegisterCreatedObjectUndo(containerGO, "Create Container");
        }

        // Spawner
        RigidbodyParticleSpawner2D spawner = null;
        var spawnerGO = GameObject.Find("Spawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("Spawner");
            spawner   = spawnerGO.AddComponent<RigidbodyParticleSpawner2D>();
            Undo.RegisterCreatedObjectUndo(spawnerGO, "Create Spawner");
        }
        else
        {
            spawner = spawnerGO.GetComponent<RigidbodyParticleSpawner2D>();
        }

        if (spawner != null)
            spawner.particlePrefab = prefab;

        // UI Controller
        if (GameObject.Find("UIController") == null)
        {
            var uiGO = new GameObject("UIController");
            var ui   = uiGO.AddComponent<SimpleUIController>();
            ui.spawner = spawner;

            Undo.RegisterCreatedObjectUndo(uiGO, "Create UIController");
        }

        // Mouse interactor
        var interactorGO = GameObject.Find("MouseInteractor");
        if (interactorGO == null)
        {
            interactorGO = new GameObject("MouseInteractor");
            Undo.RegisterCreatedObjectUndo(interactorGO, "Create MouseInteractor");
        }
        var interactor = interactorGO.GetComponent<MouseInteractor2D>()
                      ?? interactorGO.AddComponent<MouseInteractor2D>();
        interactor.spawner = spawner;
    }
}
#endif
