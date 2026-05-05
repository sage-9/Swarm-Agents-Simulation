using UnityEngine;

public class Grid
{
	public int Length { get; private set; }
	public int Width { get; private set; }
	public int Height { get; private set; }
	public Vector3 GridOrigin { get; private set; }
	public float VoxelSize { get; private set; }
	public Node[,,] GridData { get; private set; }
	
	public Grid(int length, int width, int height, float voxelSize, Vector3 gridOrigin)
	{
		Length = length;
		Width = width;
		Height = height;
		GridOrigin = gridOrigin;
		VoxelSize = voxelSize;
		GridData = new Node[length, height, width];

		for (int i = 0; i < length; i++)
		{
			for (int j = 0; j < height; j++)
			{
				for (int k = 0; k < width; k++)
				{
					GridData[i, j, k] = new Node(NodeState.Unexplored);
				}
			}
		}
	}

	public Grid(Grid grid) : this(grid.Length, grid.Width, grid.Height, grid.VoxelSize, grid.GridOrigin)
	{
	}
	
	/// <summary>
	/// Check whether an index lies inside the grid boundaries.
	/// </summary>
	private bool IsInBounds(Vector3Int index)
    {
    	return index.x >= 0 && index.x < Length &&
    	       index.y >= 0 && index.y < Height &&
    	       index.z >= 0 && index.z < Width;
    }
	
	/// <summary>
	/// Convert a world position to the corresponding integer grid index.
	/// Returns false if the point lies outside the grid.
	/// </summary>
	public bool WorldToGrid(Vector3 worldPos, out Vector3Int index)
	{
		Vector3 local = worldPos - GridOrigin;
		index = new Vector3Int(
			Mathf.FloorToInt(local.x / VoxelSize),
			Mathf.FloorToInt(local.y / VoxelSize),
			Mathf.FloorToInt(local.z / VoxelSize)
		);
		return IsInBounds(index);
	}
	
	/// <summary>
	/// Convert an integer grid index to the world position of the voxel's center.
	/// </summary>
	public Vector3 GridToWorld(Vector3Int index)
	{
		return GridOrigin + new Vector3(
			(index.x + 0.5f) * VoxelSize,
			(index.y + 0.5f) * VoxelSize,
			(index.z + 0.5f) * VoxelSize
		);
	}
	
	/// <summary>
	/// Set the state of a voxel by its index.
	/// </summary>
	public void SetVoxel(Vector3Int index, NodeState state)
	{
		if (!IsInBounds(index)) return;
		GridData[index.x, index.y, index.z].NodeState = state;
	}
	
	/// <summary>
	/// Get the state of a voxel by its index.
	/// Returns Unknown if the index is out of bounds.
	/// </summary>
	public NodeState GetVoxel(Vector3Int index)
	{
		if (!IsInBounds(index)) return NodeState.Unknown;
		return GridData[index.x, index.y, index.z].NodeState;
	}
	
	/// <summary>
	/// Overload to set a voxel using a world position.
	/// </summary>
	public void SetVoxelFromWorld(Vector3 worldPos, NodeState state)
	{
		if (WorldToGrid(worldPos, out Vector3Int idx))
			SetVoxel(idx, state);
	}
	
	/// <summary>
	/// Marks all voxels along a ray as Free, and the voxel at the hit point as Occupied.
	/// This simulates a typical LiDAR beam.
	/// </summary>
	/// <param name="ray">The sensor ray (origin and direction).</param>
	/// <param name="maxDistance">Maximum range of the sensor.</param>
	/// <param name="hitPoint">Output the hit point if something was hit, otherwise max range point.</param>
	public void UpdateRay(Ray ray, float maxDistance, out Vector3 hitPoint)
	{
		// Perform a raycast against a simple collider or assume open space.
		bool somethingHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance);
		hitPoint = somethingHit ? hitInfo.point : ray.GetPoint(maxDistance);

		Vector3 origin = ray.origin;
		Vector3 direction = ray.direction;
		float step = VoxelSize * 0.5f; // marching step (smaller than voxel for coverage)
		float travelled = 0f;

		while (travelled < maxDistance)
		{
			Vector3 currentPos = origin + direction * travelled;
			if (!WorldToGrid(currentPos, out Vector3Int idx))
				break;

			float distToHit = Vector3.Distance(origin, hitPoint);
			if (travelled + step >= distToHit)
			{
				// This is the final voxel before the hit point: mark as Occupied.
				SetVoxel(idx, somethingHit ? NodeState.Occupied : NodeState.Free);
				break;
			}
			else
			{
				// Voxel is traversed freely.
				SetVoxel(idx, NodeState.Free);
			}

			travelled += step;
		}
	}
	
	///<summary>
	/// Draws a Debug view of 3d grid showing which voxels are Explored or Occupied
	///</summary>
	public void DrawDebugGrid()
	{
		if (GridData == null) return;
		// Cache lossy scale for performance
		Vector3 size = Vector3.one * VoxelSize * 0.9f; // slightly smaller than voxel to avoid overlap lines

		for (int x = 0; x < Length; x++)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int z = 0; z < Width; z++)
				{
					NodeState state = GridData[x, y, z].NodeState;
					if (state == NodeState.Unknown)
						continue; // optionally draw unknown as transparent

					Vector3 center = GridToWorld(new Vector3Int(x, y, z));

					Gizmos.color = state switch
					{
						NodeState.Free => new Color(0, 1, 0, 0.3f),
						NodeState.Occupied => new Color(1, 0, 0, 0.5f),
						NodeState.Unexplored => new Color(0.5f, 0.5f, 0.5f, 0.1f),
						_ => new Color(0, 0, 0, 0)
					};
					
					Gizmos.DrawCube(center, size);
				}
			}
		}
	 }
}

public struct Node
{
	public NodeState NodeState;

	public Node(NodeState nodeState)
	{
		NodeState = nodeState;
	}
}

public enum NodeState
{
	Unknown,
	Unexplored,
	Occupied,
	Free
}
