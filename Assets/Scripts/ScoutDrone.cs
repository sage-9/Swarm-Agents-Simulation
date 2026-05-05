using System.Collections;
using UnityEngine;

public class ScoutDrone : BaseAgent
{
    [Header("Exploration Settings")]
    [SerializeField] private float frontierSearchRadius = 20f;
    [SerializeField] private int searchStep = 2; // Optimization: don't check every single voxel
    
    private bool _isSearchingForFrontier = false;


    protected override void OnTargetReached()
    {
        // When we reach a frontier, find the next one
        if (!_isSearchingForFrontier)
        {
            StartCoroutine(FindNextFrontier());
        }
    }

    public override void OnVictimFound(GameObject victim)
    {
        Debug.Log($"Scout {name} found victim at {victim.transform.position}!");
        
        // INTERFACE: Trigger the Swarm Manager to send a Rescuer
        // SwarmManager.Instance.RequestRescuer(victim.transform.position, this);
        
        // For your research: You might want the scout to stay 
        // until the rescuer arrives, or keep moving.
    }

    /// <summary>
    /// Analyzes the GridData to find the closest 'Frontier' node.
    /// A Frontier = A 'Free' node adjacent to an 'Unexplored' node.
    /// </summary>
    private IEnumerator FindNextFrontier()
    {
        _isSearchingForFrontier = true;
        Vector3Int bestNodeIndex = -Vector3Int.one;
        float closestDistance = float.MaxValue;

        // Optimization: We only search within a local bounds to save CPU
        PersonalGrid.WorldToGrid(transform.position, out Vector3Int currentIdx);
        
        int range = Mathf.RoundToInt(frontierSearchRadius / PersonalGrid.VoxelSize);

        // Calculate clamped bounds to avoid looping outside the grid
        int minX = Mathf.Max(0, currentIdx.x - range);
        int maxX = Mathf.Min(PersonalGrid.Length, currentIdx.x + range);
        int minY = Mathf.Max(0, currentIdx.y - range);
        int maxY = Mathf.Min(PersonalGrid.Height, currentIdx.y + range);
        int minZ = Mathf.Max(0, currentIdx.z - range);
        int maxZ = Mathf.Min(PersonalGrid.Width, currentIdx.z + range);

        // Iterate through the grid within the search range
        for (int x = minX; x < maxX; x += searchStep)
        {
            for (int y = minY; y < maxY; y += searchStep)
            {
                for (int z = minZ; z < maxZ; z += searchStep)
                {
                    Vector3Int checkIdx = new Vector3Int(x, y, z);
                    
                    // 1. Check if node is Free
                    if (PersonalGrid.GetVoxel(checkIdx) == NodeState.Free)
                    {
                        // 2. Check if it's a frontier (has unexplored neighbors)
                        if (IsFrontierNode(checkIdx))
                        {
                            float dist = Vector3.SqrMagnitude(transform.position - PersonalGrid.GridToWorld(checkIdx));
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                bestNodeIndex = checkIdx;
                            }
                        }
                    }
                }
            }
            // Yield every X rows to prevent frame spikes in your Unity Editor
            if (x % 5 == 0) yield return null;
        }

        if (bestNodeIndex != -Vector3Int.one)
        {
            SetTarget(PersonalGrid.GridToWorld(bestNodeIndex));
        }
        else
        {
            // If no local frontier, move to a random far-away Unexplored point 
            // or return to base to avoid getting stuck.
            SetTarget(transform.position + new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)));
        }

        _isSearchingForFrontier = false;
    }

    private bool IsFrontierNode(Vector3Int idx)
    {
        // Check 6 cardinal neighbors (Up, Down, Left, Right, Forward, Back)
        Vector3Int[] neighbors = {
            idx + Vector3Int.up, idx + Vector3Int.down,
            idx + Vector3Int.left, idx + Vector3Int.right,
            idx + Vector3Int.forward, idx + Vector3Int.back
        };

        foreach (var n in neighbors)
        {
            // Ensure neighbor is within grid bounds before checking state
            if (n.x >= 0 && n.x < PersonalGrid.Length &&
                n.y >= 0 && n.y < PersonalGrid.Height &&
                n.z >= 0 && n.z < PersonalGrid.Width)
            {
                if (PersonalGrid.GetVoxel(n) == NodeState.Unexplored)
                    return true;
            }
        }
        return false;
    }
}
