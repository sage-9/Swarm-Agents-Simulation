using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aerial exploration drone that searches for frontiers in the explored map.
/// Implements IExplorer for type-agnostic relay coordination.
/// </summary>
public class ScoutDrone : BaseAgent, IExplorer
{
    [Header("Exploration Settings")]
    [SerializeField] private float frontierSearchRadius = 50f;
    [SerializeField] private int searchStep = 2;
    [SerializeField] private float randomVariance = 15f;
    [SerializeField] private float stuckTimeout = 5f;

    private bool _isSearchingForFrontier;
    private PathFollower _pathFollower;
    private float _lastProgressTime;
    private float _minProgressDistance = 0.5f;
    private Vector3 _lastPosition;

    protected override void Start()
    {
        base.Start();
        _lastPosition = transform.position;
        _lastProgressTime = Time.time;

        _pathFollower = GetComponent<PathFollower>();
        if (_pathFollower == null)
            _pathFollower = gameObject.AddComponent<PathFollower>();

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

            Vector3 randomForwardOffset = Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(-90f, 90f), 0) * transform.forward;
            Vector3 escapeVector = randomForwardOffset * (frontierSearchRadius * 0.5f);
            SetTarget(transform.position + escapeVector);
            RecalculatePath();
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

                float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);

                if (distanceToWaypoint < arrivalDistance)
                {
                    _pathFollower.AdvanceToNextWaypoint();
                }
                else
                {
                    targetDirection = (currentWaypoint - transform.position).normalized;
                }
            }
            else
            {
                targetDirection = (TargetPosition - transform.position).normalized;
            }

            Vector3 physicsAvoidance = GetComponent<AvoidanceSystem>().GetAvoidanceForces(transform.position, GetAllAgents());

            Vector3 finalDirection = (targetDirection + (physicsAvoidance)).normalized;

            GetComponent<MovementSystem>().Move(finalDirection, moveSpeed);
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

        List<Vector3> path = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 0.5f, true);
        _pathFollower.SetPath(path);

        if (!_pathFollower.HasPath)
        {
            Debug.LogWarning($"{name} could not find path to target. Finding new frontier.");
            StartCoroutine(FindNextFrontier());
        }
    }

    protected override void OnTargetReached()
    {
        if (!_isSearchingForFrontier)
        {
            StartCoroutine(FindNextFrontier());
        }
    }

    public override void OnVictimFound(GameObject victim)
    {
        Debug.Log($"Scout {name} found victim at {victim.transform.position}!");

        if (AgentSpawnManager.Instance != null)
        {
            AgentSpawnManager.Instance.RequestRescuer(victim);
        }
    }

    public Vector3 GetExplorationTarget()
    {
        return TargetPosition;
    }

    private IEnumerator FindNextFrontier()
    {
        if (_isSearchingForFrontier) yield break;

        _isSearchingForFrontier = true;
        Vector3Int bestNodeIndex = -Vector3Int.one;
        float bestScore = float.MaxValue;

        PersonalGrid.WorldToGrid(transform.position, out Vector3Int currentIdx);

        int range = Mathf.RoundToInt(frontierSearchRadius / PersonalGrid.VoxelSize);

        int minX = Mathf.Max(0, currentIdx.x - range);
        int maxX = Mathf.Min(PersonalGrid.Length, currentIdx.x + range);
        int minY = Mathf.Max(0, currentIdx.y - range);
        int maxY = Mathf.Min(PersonalGrid.Height, currentIdx.y + range);
        int minZ = Mathf.Max(0, currentIdx.z - range);
        int maxZ = Mathf.Min(PersonalGrid.Width, currentIdx.z + range);

        float minTargetDist = PersonalGrid.VoxelSize * 3.0f;

        for (int x = minX; x < maxX; x += searchStep)
        {
            for (int y = minY; y < maxY; y += searchStep)
            {
                for (int z = minZ; z < maxZ; z += searchStep)
                {
                    Vector3Int checkIdx = new Vector3Int(x, y, z);

                    if (PersonalGrid.GetVoxel(checkIdx) != NodeState.Free) continue;

                    if (!IsFrontierNode(checkIdx)) continue;

                    float dist = Vector3.Distance(transform.position, PersonalGrid.GridToWorld(checkIdx));

                    if (dist < minTargetDist) continue;

                    float score = dist + Random.Range(0f, randomVariance);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestNodeIndex = checkIdx;
                    }
                }

                if (x % 5 == 0) yield return null;
            }
        }

        if (bestNodeIndex != -Vector3Int.one)
        {
            SetTarget(PersonalGrid.GridToWorld(bestNodeIndex));
            RecalculatePath();
        }
        else
        {
            Vector3 randomForwardOffset = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-45f, 45f), 0) * transform.forward;
            Vector3 escapeVector = randomForwardOffset * (frontierSearchRadius * 1.5f);

            SetTarget(transform.position + escapeVector);
            RecalculatePath();
        }

        _isSearchingForFrontier = false;
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

    public void OnDrawGizmos()
    {
        if (_pathFollower == null) _pathFollower = GetComponent<PathFollower>();
        if (_pathFollower == null || !_pathFollower.HasPath) return;

        Gizmos.color = Color.yellow;
        var path = _pathFollower.Path;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }
}
