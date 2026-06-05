using UnityEngine;

/// <summary>
/// Singleton manager for the world-wide voxel grid.
/// Provides default grid template for all agents.
/// </summary>
public class WorldGridManager : MonoBehaviour
{
    [Header("World Grid Settings")]
    [SerializeField] private int length = 50;
    [SerializeField] private int width = 50;
    [SerializeField] private int height = 20;
    [SerializeField] private Transform gridOrigin;
    [SerializeField] private float voxelSize = 1f;
    [SerializeField] private bool drawDebug;

    private GridVisualizer _visualizer;

    public static Grid DefaultGrid { get; private set; }
    public static Grid WorldGrid { get; private set; }

    void Awake()
    {
        Vector3 originPos = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        DefaultGrid = new Grid(length, width, height, voxelSize, originPos);
        WorldGrid = new Grid(DefaultGrid);

        _visualizer = GetComponent<GridVisualizer>();
        if (_visualizer == null)
            _visualizer = gameObject.AddComponent<GridVisualizer>();

        _visualizer.SetGrid(WorldGrid);
    }

    void OnDrawGizmos()
    {
        if (WorldGrid == null || !drawDebug) return;

        if (_visualizer == null)
        {
            _visualizer = GetComponent<GridVisualizer>();
            if (_visualizer == null)
                _visualizer = gameObject.AddComponent<GridVisualizer>();
        }

        _visualizer.SetGrid(WorldGrid);
        _visualizer.DrawDebugGrid();
    }
}
