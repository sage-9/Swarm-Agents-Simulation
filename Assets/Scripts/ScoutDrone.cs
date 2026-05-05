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

        // Iterate through the grid within the search range
        for (int x = currentIdx.x - range; x < currentIdx.x + range; x++)
        {
            for (int y = currentIdx.y - range; y < currentIdx.y + range; y++)
            {
                for (int z = currentIdx.z - range; z < currentIdx.z + range; z++)
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
            SetTarget(transform.position + new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10)));
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
            if (PersonalGrid.GetVoxel(n) == NodeState.Unexplored)
                return true;
        }
        return false;
    }
}