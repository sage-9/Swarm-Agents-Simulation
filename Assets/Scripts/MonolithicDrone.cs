using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a highly advanced, expensive single drone utilizing simulated DRL.
/// Uses global Information Gain heuristics to fake deep learning spatial understanding
/// and features an over-clocked sensor array.
/// </summary>
public class MonolithicDrone : BaseAgent, IExplorer
{
    [Header("Deep Learning Simulation Settings")]
    [Tooltip("How far the agent looks ahead to calculate information gain")]
    [SerializeField] private int informationGainRadius = 5;
    [SerializeField] private float weightDistance = 1.0f;
    [SerializeField] private float weightInformation = 5.0f; // Strongly prefers areas with massive unmapped voids
    
    [Header("Hardware Simulation")]
    [SerializeField] private float upgradedScanRange = 40f;
    [SerializeField] private int upgradedScanResolution = 200; // Simulating high-end 128-channel LiDAR

    [Header("Semantic AI Navigation")]
    private SemanticDoorway activeDoorway = null;
    private bool isApproachingDoor = false;
    
    private bool _isProcessingGlobalState;
    private PathFollower _pathFollower;
    private float _lastProgressTime;
    private Vector3 _lastPosition;
    private HashSet<Vector3Int> _blacklistedFrontiers = new HashSet<Vector3Int>();

    protected override void Start()
    {
        base.Start();
        
        _lastPosition = transform.position;
        _lastProgressTime = Time.time;

        _pathFollower = GetComponent<PathFollower>();
        if (_pathFollower == null)
            _pathFollower = gameObject.AddComponent<PathFollower>();

        // FAKE THE HARDWARE: Over-clock the existing sensing and movement systems
        moveSpeed *= 1.5f; // Simulating superior propulsion
        turnSpeed *= 1.5f;
        
        // Inject expensive sensor parameters via reflection or direct access if fields are made public.
        // Assuming we are updating the sensing system's capability directly:
        StartCoroutine(SimulateAdvancedSensingRoutine());

        StartCoroutine(CalculateOptimalDeepLearningPath());
        Invoke(nameof(StartDLRoutine), 1.0f);
    }

    protected override void Update()
    {
        base.Update();
        CheckProgress();
    }
    
    private void StartDLRoutine()
    {
        StartCoroutine(CalculateOptimalDeepLearningPath());
    }

    private void CheckProgress()
    {
        if (CurrentState != AgentState.Searching) return;

        if (Vector3.Distance(transform.position, _lastPosition) > 0.5f)
        {
            _lastPosition = transform.position;
            _lastProgressTime = Time.time;
        }
        else if (Time.time - _lastProgressTime > 4f)
        {
            Debug.LogWarning($"{name} is physically stuck! Using Semantic AI to escape.");
            _lastProgressTime = Time.time;

            if (PersonalGrid.WorldToGrid(TargetPosition, out Vector3Int badIdx))
            {
                _blacklistedFrontiers.Add(badIdx);
            }

            // Try to find a logical door. If all doors are already explored, fallback to random wander.
            if (!TrySemanticEscape())
            {
                SetTarget(GetRandomNodeNearby());
                List<Vector3> escapePath = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 2, true);
                _pathFollower.SetPath(escapePath);
            }
        }
    }

    protected override void HandleMovement()
    {
        float distanceToFinalTarget = Vector3.Distance(transform.position, TargetPosition);

        if (distanceToFinalTarget > arrivalDistance)
        {
            if (_pathFollower.ShouldRecalculate())
            {
                RecalculatePath();
            }

            Vector3 targetDirection = Vector3.zero;

            if (_pathFollower.HasPath)
            {
                Vector3 currentWaypoint = _pathFollower.GetCurrentWaypoint();
    
                // FIX: Check !_pathFollower.IsAtEnd to prevent an infinite loop 
                // when consuming the final waypoint in the array.
                while (!_pathFollower.IsAtEnd && Vector3.Distance(transform.position, currentWaypoint) < arrivalDistance)
                {
                    _pathFollower.AdvanceToNextWaypoint();
                    currentWaypoint = _pathFollower.GetCurrentWaypoint();
                }

                if (!_pathFollower.IsAtEnd)
                {
                    targetDirection = (currentWaypoint - transform.position).normalized;
                }
            }
            // THE FIX: If A* completely fails while we are lined up with a door, 
            // override the grid and fly blindly forward into the room!
            else if (activeDoorway != null && !isApproachingDoor)
            {
                targetDirection = (TargetPosition - transform.position).normalized;
            }
            else 
            {
                // FIX: If A* completely fails because the clearance intersects the floor at spawn, 
                // blindly push towards the target to escape the tight space.
                targetDirection = (TargetPosition - transform.position).normalized;
            }

            if (targetDirection != Vector3.zero)
            {
                Vector3 physicsAvoidance = GetComponent<AvoidanceSystem>().GetAvoidanceForces(transform.position, GetAllAgents());
                
                // THE FIX: Suppress the Avoidance System by 90% when threading the needle 
                // so the door frames don't act like a repelling magnet.
                if (activeDoorway != null && !isApproachingDoor)
                {
                    physicsAvoidance *= 0.01f; 
                }

                Vector3 finalDirection = targetDirection + physicsAvoidance;
                GetComponent<MovementSystem>().Move(finalDirection.normalized, moveSpeed);
            }
        }
        else
        {
            OnTargetReached();
        }
    }

    protected override void HandleRotation()
    {
        Vector3 targetDirection = Vector3.zero;
        if (_pathFollower.HasPath)
        {
            targetDirection = _pathFollower.GetCurrentWaypoint() - transform.position;
        }
        else if (TargetPosition != transform.position)
        {
            targetDirection = TargetPosition - transform.position;
        }

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            GetComponent<MovementSystem>().Rotate(targetDirection);
        }
    }

    private void RecalculatePath()
    {
        if (PersonalGrid == null || CurrentState != AgentState.Searching) return;

        // THE FIX: Dynamically adjust the A* clearance. 
        // Use a clearance of 1 ONLY when actively pushing through the door, otherwise use 2.
        int clearance = (activeDoorway != null && !isApproachingDoor) ? 1 : 2;

        List<Vector3> path = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, clearance, true);
        _pathFollower.SetPath(path);

        // If A* fails, only blacklist the target if we are NOT doing a semantic door maneuver
        if (!_pathFollower.HasPath && activeDoorway == null)
        {
            if (PersonalGrid.WorldToGrid(TargetPosition, out Vector3Int badIdx))
            {
                _blacklistedFrontiers.Add(badIdx);
                Debug.LogWarning($"{name} blacklisted unreachable target at {badIdx}.");
            }
            StartCoroutine(CalculateOptimalDeepLearningPath());
        }
    }

    protected override void OnTargetReached()
    {
        // If we are currently executing a Semantic Doorway maneuver
        if (activeDoorway != null)
        {
            if (isApproachingDoor)
            {
                // We reached the outside of the door. Now force it INSIDE the room.
                isApproachingDoor = false;
                SetTarget(activeDoorway.GetEntryPoint());
                
                // IMPORTANT: Lower clearance to 1 so the A* algorithm squeezes through the tight door frame
                List<Vector3> path = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 1, true);
                _pathFollower.SetPath(path);
            }
            else
            {
                // We successfully entered the room!
                Debug.Log($"<color=magenta>{name} successfully entered room via {activeDoorway.name}</color>");
                activeDoorway = null;
                StartCoroutine(CalculateOptimalDeepLearningPath()); // Resume normal voxel exploration
            }
        }
        else if (!_isProcessingGlobalState)
        {
            StartCoroutine(CalculateOptimalDeepLearningPath());
        }
    }

    public override void OnVictimFound(GameObject victim)
    {
        Debug.Log($"<color=cyan>Monolithic DL Drone localized victim at {victim.transform.position}!</color>");
        
        // The DL Drone acts as its own rescuer/reporter since it's a monolithic unit
        if (AgentSpawnManager.Instance != null)
        {
            AgentSpawnManager.Instance.RequestRescuer(victim);
        }
    }

    public Vector3 GetExplorationTarget()
    {
        return TargetPosition;
    }

    /// <summary>
    /// Fakes a Deep Reinforcement Learning policy.
    /// Instead of wandering, it evaluates the entire known grid to calculate the mathematical
    /// Information Gain of every possible frontier, heavily weighting massive unexplored voids.
    /// </summary>
    private IEnumerator CalculateOptimalDeepLearningPath()
        {
            if (_isProcessingGlobalState) yield break;
            _isProcessingGlobalState = true;
    
            Vector3Int bestNodeIndex = -Vector3Int.one;
            float highestUtilityScore = float.MinValue;
    
            PersonalGrid.WorldToGrid(transform.position, out Vector3Int currentIdx);
    
            for (int x = 0; x < PersonalGrid.Length; x += 3)
            {
                for (int y = 0; y < PersonalGrid.Height; y += 3)
                {
                    for (int z = 0; z < PersonalGrid.Width; z += 3)
                    {
                        Vector3Int checkIdx = new Vector3Int(x, y, z);
    
                        if (PersonalGrid.GetVoxel(checkIdx) != NodeState.Free) continue;
                        if (!IsFrontierNode(checkIdx)) continue;
                        
                        if (_blacklistedFrontiers.Contains(checkIdx)) continue;
    
                        float dist = Vector3.Distance(transform.position, PersonalGrid.GridToWorld(checkIdx));
                        if (dist < PersonalGrid.VoxelSize * 3.0f) continue;
    
                        float informationGain = CalculateInformationGain(checkIdx);
                        float utilityScore = (informationGain * weightInformation) - (dist * weightDistance);
    
                        if (utilityScore > highestUtilityScore)
                        {
                            highestUtilityScore = utilityScore;
                            bestNodeIndex = checkIdx;
                        }
                    }
                }
                if (x % 3 == 0) yield return null; 
            }
    
            if (bestNodeIndex != -Vector3Int.one)
            {
                SetTarget(PersonalGrid.GridToWorld(bestNodeIndex));
                RecalculatePath();
            }
            else
            {
                // If all frontiers are blacklisted or none exist, clear the blacklist and wander
                _blacklistedFrontiers.Clear();
                
                // Safely wander by picking a random known FREE node nearby, not just a blind forward vector
                SetTarget(GetRandomNodeNearby());
            }
    
            _isProcessingGlobalState = false;
        }
    
        // Helper method to ensure safe fallback wandering
        private Vector3 GetRandomNodeNearby()
        {
            PersonalGrid.WorldToGrid(transform.position, out Vector3Int center);
            for (int i = 0; i < 20; i++) // Try 20 times to find a safe spot
            {
                Vector3Int randomIdx = center + new Vector3Int(Random.Range(-7, 7), Random.Range(-5, 5), Random.Range(-7, 7));
                if (PersonalGrid.GetVoxel(randomIdx) == NodeState.Free||PersonalGrid.GetVoxel(randomIdx)== NodeState.Unexplored)
                {
                    return PersonalGrid.GridToWorld(randomIdx);
                }
            }

            return transform.position + (transform.forward * 5f);
        }

    /// <summary>
    /// Calculates how many Unexplored voxels are behind a specific frontier.
    /// This gives the drone a "semantic understanding" of where the biggest rooms are.
    /// </summary>
    private float CalculateInformationGain(Vector3Int node)
    {
        int unexploredCount = 0;
        
        for (int x = -informationGainRadius; x <= informationGainRadius; x++)
        {
            for (int y = -informationGainRadius; y <= informationGainRadius; y++)
            {
                for (int z = -informationGainRadius; z <= informationGainRadius; z++)
                {
                    Vector3Int check = new Vector3Int(node.x + x, node.y + y, node.z + z);
                    if (PersonalGrid.GetVoxel(check) == NodeState.Unexplored)
                    {
                        unexploredCount++;
                    }
                }
            }
        }
        return unexploredCount;
    }

    private bool IsFrontierNode(Vector3Int idx)
    {
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

    private IEnumerator SimulateAdvancedSensingRoutine()
    {
        float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
        
        while (true)
        {
            if (PersonalGrid != null)
            {
                for (int i = 0; i < upgradedScanResolution; i++)
                {
                    float t = (float)i / upgradedScanResolution;
                    float inclination = Mathf.Acos(1f - 2f * t);
                    float azimuth = 2f * Mathf.PI * goldenRatio * i;

                    float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
                    float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
                    float z = Mathf.Cos(inclination);

                    Vector3 dir = new Vector3(x, y, z);
                    Ray ray = new Ray(transform.position, dir);

                    // Pushes rays up to 40 meters out to mimic high-end hardware
                    PersonalGrid.UpdateRay(ray, upgradedScanRange, obstacleLayerMask, out _);
                }
            }
            yield return new WaitForSeconds(0.1f); // 10Hz scan rate
        }
    }
    private bool TrySemanticEscape()
    {
        // Find all semantic doorways in the scene
        SemanticDoorway[] allDoorways = FindObjectsOfType<SemanticDoorway>();
            SemanticDoorway bestDoor = null;
            float closestDist = float.MaxValue;
        
            foreach (var door in allDoorways)
            {
                // FIX: Skip explored doors AND the door we are currently stuck trying to enter
                if (door.isExplored || door == activeDoorway) continue; 
        
                float dist = Vector3.Distance(transform.position, door.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestDoor = door;
                }
            }

        if (bestDoor != null)
        {
            activeDoorway = bestDoor;
            isApproachingDoor = true;

            // Step 1: Route A* pathfinding to the YELLOW approach point outside the door
            SetTarget(activeDoorway.GetApproachPoint());
            List<Vector3> path = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 2, true);
            _pathFollower.SetPath(path);
            
            Debug.Log($"<color=magenta>{name} engaging Semantic AI: Routing to doorway {bestDoor.name}</color>");
            return true;
        }
        return false;
    }
}