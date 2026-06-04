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
    
    public static Grid DefaultGrid { get; private set; }
    public static Grid WorldGrid { get; private set; }

    void Awake()
    {
        Vector3 originPos = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        DefaultGrid = new Grid(length, width, height, voxelSize, originPos);
        WorldGrid = new Grid(DefaultGrid);
    }

    void OnDrawGizmos()
    {
        if (WorldGrid == null || drawDebug==false) return;
        WorldGrid.DrawDebugGrid();
    }
}
