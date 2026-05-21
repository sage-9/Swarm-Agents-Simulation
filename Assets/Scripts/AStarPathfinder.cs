using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    private class PathNode
    {
        public Vector3Int GridIndex;
        public Vector3 WorldPosition;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public PathNode Parent;

        public PathNode(Vector3Int gridIndex, Vector3 worldPos)
        {
            GridIndex = gridIndex;
            WorldPosition = worldPos;
        }
    }

    /// <summary>
    /// Finds a path through the provided voxel grid using A* with priority queue optimization.
    /// </summary>
    public static List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, Grid droneGrid, float clearanceRadius = 1f, bool allowVerticalMovement = false, int maxIterations = 5000)
    {
        if (!droneGrid.WorldToGrid(startPos, out Vector3Int startIndex) ||
            !droneGrid.WorldToGrid(targetPos, out Vector3Int targetIndex))
        {
            return new List<Vector3>(); // Start or target out of bounds
        }

        // Determine how many grid cells the drone's physical radius occupies
        int clearanceCells = Mathf.CeilToInt(clearanceRadius / droneGrid.VoxelSize);

        PathNode startNode = new PathNode(startIndex, startPos);
        PathNode targetNode = new PathNode(targetIndex, targetPos);

        // Use priority queue for efficient minimum node extraction
        var openSet = new PriorityQueue<PathNode, int>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, PathNode> allNodes = new Dictionary<Vector3Int, PathNode>();

        openSet.Enqueue(startNode, startNode.FCost);
        allNodes.Add(startIndex, startNode);

        int iterations = 0;

        while (openSet.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                Debug.LogWarning($"A* Pathfinding aborted after {maxIterations} iterations. Target was unreachable or too far.");
                allNodes.Clear();
                return new List<Vector3>(); // Prevent infinite loop / freezing
            }

            PathNode currentNode = openSet.Dequeue();
            closedSet.Add(currentNode.GridIndex);

            // If we are close enough to the target, finish
            // (we check distance instead of exact match in case the target is surrounded by obstacles)
            if (GetDistance(currentNode.GridIndex, targetIndex, allowVerticalMovement) <= clearanceCells)
            {
                allNodes.Clear();
                return RetracePath(startNode, currentNode);
            }

            foreach (Vector3Int neighborIndex in GetNeighbors(currentNode.GridIndex, allowVerticalMovement))
            {
                if (closedSet.Contains(neighborIndex)) continue;

                // Check if the node AND its surrounding clearance cells are free
                if (!IsNodeWalkable(neighborIndex, droneGrid, clearanceCells, allowVerticalMovement)) continue;

                int moveCost = currentNode.GCost + GetDistance(currentNode.GridIndex, neighborIndex, allowVerticalMovement);

                if (!allNodes.TryGetValue(neighborIndex, out PathNode neighborNode))
                {
                    Vector3 neighborWorldPos = droneGrid.GridToWorld(neighborIndex);
                    neighborNode = new PathNode(neighborIndex, neighborWorldPos);
                    allNodes.Add(neighborIndex, neighborNode);
                }

                if (moveCost < neighborNode.GCost || !openSet.Contains(neighborNode))
                {
                    neighborNode.GCost = moveCost;
                    neighborNode.HCost = GetDistance(neighborIndex, targetIndex, allowVerticalMovement);
                    neighborNode.Parent = currentNode;

                    if (!openSet.Contains(neighborNode))
                        openSet.Enqueue(neighborNode, neighborNode.FCost);
                }
            }
        }

        // No path found
        allNodes.Clear();
        return new List<Vector3>();
    }

    /// <summary>
    /// Checks if a node is walkable, ensuring that it and its neighboring nodes (within clearance radius) are not occupied.
    /// This gives the drone a "buffer" so it doesn't clip walls.
    /// </summary>
    private static bool IsNodeWalkable(Vector3Int centerNode, Grid droneGrid, int clearanceCells, bool allowVerticalMovement)
    {
        for (int x = -clearanceCells; x <= clearanceCells; x++)
        {
            for (int z = -clearanceCells; z <= clearanceCells; z++)
            {
                if (allowVerticalMovement)
                {
                    for (int y = -clearanceCells; y <= clearanceCells; y++)
                    {
                        Vector3Int checkNode = new Vector3Int(centerNode.x + x, centerNode.y + y, centerNode.z + z);
                        if (droneGrid.GetVoxel(checkNode) == NodeState.Occupied) return false;
                    }
                }
                else
                {
                    // We check a small square around the node. 
                    // We skip checking the Y axis since the UGV stays on the ground.
                    Vector3Int checkNode = new Vector3Int(centerNode.x + x, centerNode.y, centerNode.z + z);
                    if (droneGrid.GetVoxel(checkNode) == NodeState.Occupied) return false;
                }
            }
        }

        // Ensure the central node itself isn't out of bounds
        if (droneGrid.GetVoxel(centerNode) == NodeState.Unknown) return false;

        return true;
    }

    private static List<Vector3> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        PathNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.WorldPosition);
            currentNode = currentNode.Parent;
        }
        path.Reverse();
        return path;
    }

    private static int GetDistance(Vector3Int nodeA, Vector3Int nodeB, bool allowVerticalMovement)
    {
        int dstX = Mathf.Abs(nodeA.x - nodeB.x);
        int dstZ = Mathf.Abs(nodeA.z - nodeB.z);

        if (allowVerticalMovement)
        {
            int dstY = Mathf.Abs(nodeA.y - nodeB.y);
            return dstX + dstY + dstZ;
        }

        // Manhattan distance for UGV
        return dstX + dstZ;
    }

    private static List<Vector3Int> GetNeighbors(Vector3Int center, bool allowVerticalMovement)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();
        // 4-way movement
        neighbors.Add(new Vector3Int(center.x + 1, center.y, center.z));
        neighbors.Add(new Vector3Int(center.x - 1, center.y, center.z));
        neighbors.Add(new Vector3Int(center.x, center.y, center.z + 1));
        neighbors.Add(new Vector3Int(center.x, center.y, center.z - 1));

        if (allowVerticalMovement)
        {
            // 6-way movement for aerial drones
            neighbors.Add(new Vector3Int(center.x, center.y + 1, center.z));
            neighbors.Add(new Vector3Int(center.x, center.y - 1, center.z));
        }

        return neighbors;
    }
}
