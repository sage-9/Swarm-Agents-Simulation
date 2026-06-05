using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    private Grid _grid;

    public void SetGrid(Grid grid)
    {
        _grid = grid;
    }

    public void DrawDebugGrid()
    {
        if (_grid == null || _grid.GridData == null) return;

        Vector3 size = Vector3.one * _grid.VoxelSize * 0.9f;

        for (int x = 0; x < _grid.Length; x++)
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int z = 0; z < _grid.Width; z++)
                {
                    NodeState state = _grid.GridData[x, y, z].NodeState;
                    if (state == NodeState.Unknown || state == NodeState.Unexplored)
                        continue;

                    Vector3 center = _grid.GridToWorld(new Vector3Int(x, y, z));

                    Gizmos.color = state switch
                    {
                        NodeState.Free => new Color(0, 1, 0, 0.3f),
                        NodeState.Occupied => new Color(1, 0, 0, 0.5f),
                        _ => new Color(0.5f, 0.5f, 0.5f, 0.25f)
                    };
                    Gizmos.DrawCube(center, size);
                }
            }
        }
    }
}
