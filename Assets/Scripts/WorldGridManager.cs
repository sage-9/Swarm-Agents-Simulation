using UnityEngine;

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
        InvokeRepeating(nameof(UpdateMergedWorldGrid), 0f, 0.9f);
    }

    /// <summary>
    /// Pulls the personal grids from all active drones in the scene and merges them into the global WorldGrid.
    /// </summary>
    public void UpdateMergedWorldGrid()
    {
        if (WorldGrid == null) return;

        // Find all active agents in the scene
        BaseAgent[] allAgents = AgentSpawnManager.Instance.GetDronesByType("Scout").ToArray();
        foreach (ScoutDrone agent in allAgents)
        {
            if (agent.PersonalGrid != null)
            {
                WorldGrid.MergeGrid(agent.PersonalGrid);
            }
        }
    }

    /// <summary>
    /// Calculates the percentage of the map that has been explored by the swarm.
    /// </summary>
    public float CalculateExplorationPercentage()
    {
        if (WorldGrid == null || WorldGrid.GridData == null) return 0f;

        int totalVoxels = WorldGrid.GridData.Length;
        int exploredVoxels = 0;

        // A foreach loop on a multi-dimensional array automatically flattens the iteration, 
        // which is much faster than running 3 nested for-loops.
        foreach (var node in WorldGrid.GridData)
        {
            if (node.NodeState != NodeState.Unexplored)
            {
                exploredVoxels++;
            }
        }

        return totalVoxels > 0 ? ((float)exploredVoxels / totalVoxels) * 100f : 0f;
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
