using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RescuerDrone : BaseAgent
{
    [Header("Rescuer Settings")]
    [SerializeField] private float rescueTime = 3f; // Time it takes to "rescue" a victim
    [SerializeField] private float groundOffset = 1f;
    [SerializeField] private float pathRecalculationInterval = 1f;

    private Vector3 _victimPosition;
    private Vector3 _startPosition;
    private bool _isRescuing;

    private List<Vector3> _currentPath = new List<Vector3>();
    private int _currentPathIndex;
    private float _lastPathCalculationTime;

    public enum RescuerState { Idle, MovingToVictim, Rescuing, ReturningToBase }
    public RescuerState CurrentRescuerState { get; private set; }

    protected override void Start()
    {
        base.Start();
        _startPosition = transform.position;
        CurrentRescuerState = RescuerState.Idle;
        CurrentState = AgentState.Idle; // Set base state

        // Lock Y position using rigidbody constraints (ground vehicle behavior)
        if (_rb != null)
        {
            _rb.constraints = RigidbodyConstraints.FreezePositionY;
        }
    }

    /// <summary>
    /// Overrides the base HandleMovement to utilize custom A* pathfinding.
    /// Still incorporates the agent separation logic.
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
                currentWaypoint.y = groundOffset; // Ensure waypoint is at ground level

                float distanceToWaypoint = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                            new Vector3(currentWaypoint.x, 0, currentWaypoint.z));

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

            // 4. Dynamic agent separation
            Vector3 separationForce = Vector3.zero;
            foreach (var otherAgent in BaseAgent.GetAllAgents())
            {
                if (otherAgent == this || otherAgent == null) continue;
                float distToOther = Vector3.Distance(transform.position, otherAgent.transform.position);
                if (distToOther < separationRadius && distToOther > 0.01f)
                {
                    separationForce += (transform.position - otherAgent.transform.position).normalized * (1.0f - (distToOther / separationRadius));
                }
            }

            // 5. Combine and Move
            Vector3 finalDirection = (targetDirection + (separationForce * separationWeight)).normalized;

            // Ensure we stay on the ground plane (UGV behavior)
            finalDirection.y = 0;

            transform.position += finalDirection * (moveSpeed * Time.deltaTime);
        }
        else
        {
            OnTargetReached();
        }
    }

    private void RecalculatePath()
    {
        if (PersonalGrid == null) return;

        // Ground vehicle: smaller clearance (0.75f) and no vertical movement
        _currentPath = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 0.75f, false);
        _currentPathIndex = 0;
        _lastPathCalculationTime = Time.time;
    }

    /// <summary>
    /// Overrides rotation to only rotate on the Y axis (yaw) like a ground vehicle.
    /// </summary>
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

        targetDirection.y = 0; // Flat rotation

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }


    /// <summary>
    /// Assigns a victim to this drone and starts the rescue process.
    /// </summary>
    public void AssignVictim(Vector3 victimPosition)
    {
        if (CurrentRescuerState == RescuerState.Idle)
        {
            // Drop the target to the ground if needed
            _victimPosition = victimPosition;
            _victimPosition.y = groundOffset;

            SetTarget(_victimPosition);
            CurrentRescuerState = RescuerState.MovingToVictim;
            RecalculatePath(); // Calculate immediately upon assignment
            Debug.Log($"{name} assigned to rescue victim at {_victimPosition}.");
        }
    }

    protected override void OnTargetReached()
    {
        switch (CurrentRescuerState)
        {
            case RescuerState.MovingToVictim:
                if (!_isRescuing)
                {
                    StartCoroutine(RescueRoutine());
                }
                break;

            case RescuerState.ReturningToBase:
                Debug.Log($"{name} has returned to base.");
                CurrentRescuerState = RescuerState.Idle;
                CurrentState = AgentState.Idle;
                break;
        }
    }

    /// <summary>
    /// A simple timer to simulate the rescue action.
    /// </summary>
    private IEnumerator RescueRoutine()
    {
        if (_isRescuing) yield break;

        _isRescuing = true;
        CurrentRescuerState = RescuerState.Rescuing;
        CurrentState = AgentState.Guarding; // Use Guarding state to signify holding position
        Debug.Log($"{name} has arrived at victim location and is performing rescue.");

        yield return new WaitForSeconds(rescueTime);

        Debug.Log($"{name} has finished rescue. Returning to base.");
        SetTarget(_startPosition);
        CurrentRescuerState = RescuerState.ReturningToBase;
        RecalculatePath(); // Recalculate path for return journey
        _isRescuing = false;
    }

    public override void OnVictimFound(GameObject victim)
    {
        // A rescuer's primary job is to go to assigned victims.
        // For now, we'll just log if it finds another one unexpectedly.
        Debug.LogWarning($"{name} (Rescuer) found an unassigned victim at {victim.transform.position}. This could be reported to a swarm manager.");
    }

    // Optional: Draw the path for debugging
    private void OnDrawGizmos()
    {
        if (_currentPath != null && _currentPath.Count > 0)
        {
            Gizmos.color = Color.green;
            // Start from the drone's current position to the first waypoint
            Gizmos.DrawLine(transform.position, _currentPath[0]);

            for (int i = 0; i < _currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_currentPath[i], _currentPath[i + 1]);
            }
        }
    }
}
