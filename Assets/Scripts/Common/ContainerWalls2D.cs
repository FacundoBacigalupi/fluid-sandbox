using UnityEngine;

// Creates 4 visible BoxCollider2D walls that form a rectangular container.
// Walls are generated at runtime in Awake, so no manual child objects are needed.
[AddComponentMenu("FluidSandbox/Container Walls 2D")]
public class ContainerWalls2D : MonoBehaviour
{
    public Vector2 containerSize  = new Vector2(8f, 6f);
    [Range(0.05f, 1f)] public float wallThickness = 0.2f;
    public Color wallColor = new Color(0.25f, 0.28f, 0.35f, 1f);

    void Awake()
    {
        float hw = containerSize.x * 0.5f;
        float hh = containerSize.y * 0.5f;
        float wt = wallThickness;

        // Each wall: (name, local center, world size)
        CreateWall("Bottom", new Vector2(0,  -hh - wt * 0.5f), new Vector2(containerSize.x + wt * 2f, wt));
        CreateWall("Top",    new Vector2(0,   hh + wt * 0.5f), new Vector2(containerSize.x + wt * 2f, wt));
        CreateWall("Left",   new Vector2(-hw - wt * 0.5f, 0),  new Vector2(wt, containerSize.y));
        CreateWall("Right",  new Vector2( hw + wt * 0.5f, 0),  new Vector2(wt, containerSize.y));
    }

    void CreateWall(string wallName, Vector2 localCenter, Vector2 size)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(transform);
        go.transform.localPosition = localCenter;
        // Scale so a unit sprite and unit collider cover the desired world size.
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one; // collider is 1x1 in local space; transform.scale does the rest

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSquareSprite();
        sr.color  = wallColor;
        sr.sortingOrder = -1; // render behind particles
    }

    // Lazily create a minimal 1x1 white sprite used for all walls.
    static Sprite _square;
    static Sprite GetWhiteSquareSprite()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        return _square;
    }

    // Draw the container outline in the Scene view even before play mode.
    void OnDrawGizmos()
    {
        Gizmos.color = wallColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(containerSize.x, containerSize.y, 0f));
    }
}
