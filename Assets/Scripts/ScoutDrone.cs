using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoutDrone : BaseAgent
{
    [Header("Exploration Settings")]
    [SerializeField] private float frontierSearchRadius = 50f;
    [SerializeField] private int searchStep = 2; // Optimization: don't check every single voxel
    [SerializeField] private float randomVariance = 15f; // Encourages longer sweeps instead of micro-movements
    [SerializeField] private float pathRecalculationInterval = 2f;
    [SerializeField] private float stuckTimeout = 5f;

    private bool _isSearchingForFrontier;
    private List<Vector3> _currentPath = new List<Vector3>();
    private int _currentPathIndex;
    private float _lastPathCalculationTime;
    private float _lastProgressTime;
    private float _minProgressDistance = 0.5f;
    private Vector3 _lastPosition;

    protected override void Start()
    {
        base.Start();
        _lastPosition = transform.position;
        _lastProgressTime = Time.time;
        StartCoroutine(FindNextFrontier());
    }

    protected override void Update()
    {
        base.Update();
        CheckProgress();
    }

    private void CheckProgress()
    {
        if (CurrentState != AgentState.Searching) return;

        if (Vector3.Distance(transform.position, _lastPosition) > _minProgressDistance)
        {
            _lastPosition = transform.position;
            _lastProgressTime = Time.time;
        }
        else if (Time.time - _lastProgressTime > stuckTimeout)
        {
            Debug.LogWarning($"{name} is stuck! Recalculating frontier.");
            _lastProgressTime = Time.time;
            
            // If it's stuck, force a fallback leap immediately to break it out
            Vector3 randomForwardOffset = Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(-90f, 90f), 0) * transform.forward;
            Vector3 escapeVector = randomForwardOffset * (frontierSearchRadius * 0.5f);
            SetTarget(transform.position + escapeVector);
            RecalculatePath();
        }
    }

    /// <summary>
    /// Overrides the base HandleMovement to utilize custom A* pathfinding for 3D aerial movement.
    /// </summary>
    protected override void HandleMovement()
    {
        float distanceToFinalTarget = Vector3.Distance(transform.position, TargetPosition);

        if (distanceToFinalTarget > arrivalDistance)
        {
            // 1. Recalculate path periodically or if we don't have one
            if (Time.time - _lastPathCalculationTime > pathRecalculationInterval || _currentPath == null || _currentPath.Count == 0)
            {
                RecalculatePath();
            }

            Vector3 targetDirection = Vector3.zero;

            // 2. Follow the path
            if (_currentPath != null && _currentPathIndex < _currentPath.Count)
            {
                Vector3 currentWaypoint = _currentPath[_currentPathIndex];
                
                float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);

                if (distanceToWaypoint < arrivalDistance)
                {
                    _currentPathIndex++;
                }
                else
                {
                    targetDirection = (currentWaypoint - transform.position).normalized;
                }
            }
            else
            {
                // Fallback direct movement if no path
                targetDirection = (TargetPosition - transform.position).normalized;
            }

            // 3. We skip BaseAgent static obstacle avoidance since A* handles static obstacles
            // Note: We DO use the new immediate physical avoidance force defined in BaseAgent
            Vector3 physicsAvoidance = CalculateImmediatePhysicsAvoidance();
            
            // 4. Dynamic agent separation
            Vector3 separationForce = CalculateAgentSeparation();

            // 5. Combine and Move
            Vector3 finalDirection = (targetDirection + (separationForce * separationWeight) + (physicsAvoidance * physicsAvoidanceWeight)).normalized;
            
            transform.position += finalDirection * (moveSpeed * Time.deltaTime);
        }
        else
        {
            OnTargetReached();
        }
    }

    protected override void HandleRotation()
    {
        Vector3 targetDirection = Vector3.zero;
        
        if (_currentPath != null && _currentPathIndex < _currentPath.Count)
        {
            targetDirection = _currentPath[_currentPathIndex] - transform.position;
        }
        else if (TargetPosition != transform.position)
        {
            targetDirection = TargetPosition - transform.position;
        }

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }

    private void RecalculatePath()
    {
        if (PersonalGrid == null || CurrentState != AgentState.Searching) return;
        
        // Use a smaller clearance for aerial drones, and ALLOW vertical movement
        _currentPath = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 0.5f, true);
        _currentPathIndex = 0;
        _lastPathCalculationTime = Time.time;
        
        // If A* fails to find a path to the chosen frontier, find a new frontier immediately
        if (_currentPath == null || _currentPath.Count == 0)
        {
             Debug.LogWarning($"{name} could not find path to target. Finding new frontier.");
             StartCoroutine(FindNextFrontier());
        }
    }

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
        if (AgentSpawnManager.Instance != null)
        {
            AgentSpawnManager.Instance.RequestRescuer(victim);
        }
        
        // For your research: You might want the scout to stay 
        // until the rescuer arrives, or keep moving.
    }

    /// <summary>
    /// Analyzes the GridData to find the closest 'Frontier' node.
    /// A Frontier = A 'Free' node adjacent to an 'Unexplored' node.
    /// </summary>
    private IEnumerator FindNextFrontier()
    {
        if (_isSearchingForFrontier) yield break;
        
        _isSearchingForFrontier = true;
        Vector3Int bestNodeIndex = -Vector3Int.one;
        float bestScore = float.MaxValue;

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
        
        // Push the minTargetDist out a bit further to avoid getting stuck in a loop finding the exact same frontier
        float minTargetDist = PersonalGrid.VoxelSize * 3.0f; 

        // Iterate through the grid within the search range
        for (int x = minX; x < maxX; x += searchStep)
        {
            for (int y = minY; y < maxY; y += searchStep)
            {
                for (int z = minZ; z < maxZ; z += searchStep)
                {
                    Vector3Int checkIdx = new Vector3Int(x, y, z);
                    
                    // 1. Check if node is Free
                    if (PersonalGrid.GetVoxel(checkIdx) != NodeState.Free) continue;
                    
                    // 2. Check if it's a frontier (has unexplored neighbors)
                    if (!IsFrontierNode(checkIdx))continue;
                    
                    // Use Vector3.Distance for actual distance, SqrMagnitude makes the randomVariance math weird
                    float dist = Vector3.Distance(transform.position, PersonalGrid.GridToWorld(checkIdx));
                    
                    if (dist < minTargetDist) continue;
                    
                    // Add random variance to the distance score.
                    // This prevents the drone from obsessively hugging the absolute closest wall
                    // and encourages it to push outward in larger sweeps.
                    float score = dist + Random.Range(0f, randomVariance);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestNodeIndex = checkIdx;
                    }
                    
                }
                
            }
            // Yield every X rows to prevent frame spikes in your Unity Editor
            if (x % 5 == 0) yield return null;  
        }

        if (bestNodeIndex != -Vector3Int.one)
        {
            SetTarget(PersonalGrid.GridToWorld(bestNodeIndex));
            RecalculatePath();
        }
        else
        {
            // FALLBACK FIX: The drone has exhausted its local search radius.
            // We must leap FORWARD outside of this "cleared bubble" to find new areas.
            
            // Try to pick a point generally "forward" from where it is facing
            Vector3 randomForwardOffset = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-45f, 45f), 0) * transform.forward;
            Vector3 escapeVector = randomForwardOffset * (frontierSearchRadius * 1.5f);

            SetTarget(transform.position + escapeVector);
            RecalculatePath();
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
    
    public void OnDrawGizmos()
    {
        if (_currentPath != null && _currentPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_currentPath[i], _currentPath[i + 1]);
            }
        }
    }
}
