using UnityEngine;

public class WorldGridManager : MonoBehaviour
{
    [Header("World Grid Settings")]
    [SerializeField] private int length;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Transform gridOrigin;
    [SerializeField] private float voxelSize;
    
    public static Grid DefaultGrid {get; private set;}
    public static Grid WorldGrid;
    
    

    void Awake()
    {
        DefaultGrid = new Grid(length, width, height, voxelSize, gridOrigin.position);
        WorldGrid = DefaultGrid;
    }

    void OnDrawGizmosSelected()
    {
        if (WorldGrid == null) return;
        WorldGrid.DrawDebugGrid();
    }
}