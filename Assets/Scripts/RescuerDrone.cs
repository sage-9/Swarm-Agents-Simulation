using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground-based rescue drone that uses A* pathfinding.
/// Maintains a state machine for rescue operations.
/// </summary>
public class RescuerDrone : BaseAgent, IAssignable
{
    [Header("Rescuer Settings")]
    [SerializeField] private float rescueTime = 3f;
    [SerializeField] private float groundOffset = 1f;

    private Vector3 _victimPosition;
    private Vector3 _startPosition;
    private bool _isRescuing;

    private PathFollower _pathFollower;
    private StateMachine<RescuerState> _stateMachine;

    public enum RescuerState { Idle, MovingToVictim, Rescuing, ReturningToBase }
    public RescuerState CurrentRescuerState => _stateMachine.CurrentState;

    protected override void Start()
    {
        base.Start();
        _startPosition = transform.position;
        CurrentState = AgentState.Idle;

        _pathFollower = GetComponent<PathFollower>();
        if (_pathFollower == null)
            _pathFollower = gameObject.AddComponent<PathFollower>();

        _stateMachine = new StateMachine<RescuerState>(RescuerState.Idle);
        _stateMachine.OnStateChanged += OnRescuerStateChanged;

        if (_rb != null)
        {
            _rb.constraints = RigidbodyConstraints.FreezePositionY;
        }
    }

    protected override void HandleMovement()
    {
        float distanceToFinalTarget = Vector3.Distance(transform.position, TargetPosition);

        if (distanceToFinalTarget >= arrivalDistance)
        {
            if (_pathFollower.ShouldRecalculate())
            {
                RecalculatePath();
            }

            Vector3 targetDirection = Vector3.zero;

            if (_pathFollower.HasPath)
            {
                Vector3 currentWaypoint = _pathFollower.GetCurrentWaypoint();
                currentWaypoint.y = groundOffset;

                float distanceToWaypoint = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                            new Vector3(currentWaypoint.x, 0, currentWaypoint.z));

                if (distanceToWaypoint < 0.1)
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

            Vector3 separationForce = AgentBehaviors.CalculateSeparation(transform.position, separationRadius, BaseAgent.GetAllAgents());

            Vector3 finalDirection = (targetDirection + (separationForce * separationWeight)).normalized;
            finalDirection.y = 0;

            GetComponent<MovementSystem>().Move(finalDirection, moveSpeed);
        }
        else
        {
            Debug.Log("Victim reached");
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

        targetDirection.y = 0;

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            GetComponent<MovementSystem>().Rotate(targetDirection);
        }
    }

    private void RecalculatePath()
    {
        if (PersonalGrid == null) return;

        List<Vector3> path = AStarPathfinder.FindPath(transform.position, TargetPosition, PersonalGrid, 0.75f, false);
        _pathFollower.SetPath(path);
    }

    public void AssignVictim(Vector3 victimPosition)
    {
        Debug.Log("Assigning drone");
        if (!_stateMachine.IsInState(RescuerState.Idle))
        {
            Debug.Log("Drone wasn't assigned");
        }

        _victimPosition = victimPosition;
        _victimPosition.y = groundOffset;

        SetTarget(_victimPosition);
        _stateMachine.TransitionTo(RescuerState.MovingToVictim);
        RecalculatePath();
        Debug.Log($"{name} assigned to rescue victim at {_victimPosition}.");
    }

    public void AssignTarget(Vector3 targetPosition)
    {
        AssignVictim(targetPosition);
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
                _stateMachine.TransitionTo(RescuerState.Idle);
                break;
        }
    }

    private void OnRescuerStateChanged(RescuerState previousState, RescuerState newState)
    {
        switch (newState)
        {
            case RescuerState.Idle:
                CurrentState = AgentState.Idle;
                break;
            case RescuerState.MovingToVictim:
                CurrentState = AgentState.Searching;
                break;
            case RescuerState.Rescuing:
                CurrentState = AgentState.Guarding;
                break;
            case RescuerState.ReturningToBase:
                CurrentState = AgentState.Returning;
                break;
        }
    }

    private IEnumerator RescueRoutine()
    {
        if (_isRescuing) yield break;

        _isRescuing = true;
        _stateMachine.TransitionTo(RescuerState.Rescuing);
        Debug.Log($"{name} has arrived at victim location and is performing rescue.");

        yield return new WaitForSeconds(rescueTime);

        SimulationTelemetry telemetry = FindObjectOfType<SimulationTelemetry>();
        if (telemetry != null)
        {
            telemetry.RecordVictimRescued(null, transform.position);
        }

        Debug.Log($"{name} has finished rescue. Returning to base.");
        SetTarget(_startPosition);
        _stateMachine.TransitionTo(RescuerState.ReturningToBase);
        RecalculatePath();
        _isRescuing = false;
    }

    public override void OnVictimFound(GameObject victim)
    {
        Debug.LogWarning($"{name} (Rescuer) found an unassigned victim at {victim.transform.position}.");
    }

    private void OnDrawGizmos()
    {
        if (_pathFollower == null) _pathFollower = GetComponent<PathFollower>();
        if (_pathFollower == null || !_pathFollower.HasPath) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, _pathFollower.GetCurrentWaypoint());

        var path = _pathFollower.Path;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }
}
