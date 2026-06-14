using System.Collections.Generic;
using UnityEngine;

// Phase 9 — Neighbor Search 3D
//
// Extiende SpatialHashGrid2D a tres dimensiones.
// Cada partícula consulta las 27 celdas vecinas (3×3×3) en lugar de las 9 del 2D (3×3).
//
// Complejidad igual que la versión 2D:
//   O(n·k) por frame, donde k = vecinos promedio en el radio de suavizado.
public class SpatialHashGrid3D
{
    private float _cellSize;
    private readonly Dictionary<Vector3Int, List<int>> _cells = new();

    public float CellSize  => _cellSize;
    public int   CellCount => _cells.Count;

    // Insertar todas las partículas en sus celdas. Llamar una vez por frame.
    public void Build(FluidParticle3D[] particles, float cellSize)
    {
        _cellSize = cellSize;
        _cells.Clear();

        for (int i = 0; i < particles.Length; i++)
        {
            var cell = CellOf(particles[i].Position);
            if (!_cells.TryGetValue(cell, out var list))
                _cells[cell] = list = new List<int>(4);
            list.Add(i);
        }
    }

    // Devuelve en results los índices de partículas dentro del radio dado.
    public void Query(Vector3 pos, float radius, List<int> results)
    {
        results.Clear();
        var min = CellOf(new Vector3(pos.x - radius, pos.y - radius, pos.z - radius));
        var max = CellOf(new Vector3(pos.x + radius, pos.y + radius, pos.z + radius));

        for (int cx = min.x; cx <= max.x; cx++)
            for (int cy = min.y; cy <= max.y; cy++)
                for (int cz = min.z; cz <= max.z; cz++)
                    if (_cells.TryGetValue(new Vector3Int(cx, cy, cz), out var list))
                        results.AddRange(list);
    }

    public Vector3Int CellOf(Vector3 pos) =>
        new Vector3Int(Mathf.FloorToInt(pos.x / _cellSize),
                       Mathf.FloorToInt(pos.y / _cellSize),
                       Mathf.FloorToInt(pos.z / _cellSize));

    public IEnumerable<KeyValuePair<Vector3Int, List<int>>> OccupiedCells => _cells;
}
