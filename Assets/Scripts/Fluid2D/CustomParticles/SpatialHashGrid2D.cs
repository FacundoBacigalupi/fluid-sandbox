using System.Collections.Generic;
using UnityEngine;

// Phase 4 — Neighbor Search / Spatial Hash Grid
//
// Divide el espacio en celdas cuadradas de tamaño = smoothingRadius.
// Cada partícula solo consulta las celdas vecinas inmediatas (3x3 = 9 celdas max)
// en lugar de compararse contra todas las demás.
//
// Complejidad:
//   O(n²) naive  → comparar todas las partículas entre sí
//   O(n·k)  grid → n partículas × k vecinos promedio, donde k << n
//
// Uso típico por frame:
//   1. Build()  — insertar todas las partículas en sus celdas
//   2. Query()  — para cada partícula, pedir sus vecinos
public class SpatialHashGrid2D
{
    private float _cellSize;
    private readonly Dictionary<Vector2Int, List<int>> _cells =
        new Dictionary<Vector2Int, List<int>>();

    public float CellSize  => _cellSize;
    public int   CellCount => _cells.Count;

    // Inserta todas las partículas en la grilla según su posición actual.
    // Llamar una vez por frame, antes de cualquier Query.
    public void Build(FluidParticle2D[] particles, float cellSize)
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
    // La partícula de consulta también se incluye; filtrala si necesitás excluirla.
    public void Query(Vector2 pos, float radius, List<int> results)
    {
        results.Clear();
        var min = CellOf(new Vector2(pos.x - radius, pos.y - radius));
        var max = CellOf(new Vector2(pos.x + radius, pos.y + radius));

        for (int cx = min.x; cx <= max.x; cx++)
            for (int cy = min.y; cy <= max.y; cy++)
                if (_cells.TryGetValue(new Vector2Int(cx, cy), out var list))
                    results.AddRange(list);
    }

    // Coordenada de celda que contiene el punto world-space pos.
    public Vector2Int CellOf(Vector2 pos) =>
        new Vector2Int(Mathf.FloorToInt(pos.x / _cellSize),
                       Mathf.FloorToInt(pos.y / _cellSize));

    // Para debug y gizmos: iterar todas las celdas que tienen al menos una partícula.
    public IEnumerable<KeyValuePair<Vector2Int, List<int>>> OccupiedCells => _cells;
}
