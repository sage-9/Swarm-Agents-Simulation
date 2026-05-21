using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RelayDrone : BaseAgent
{
    [Header("Relay Settings")]
    [SerializeField] private float idealDistanceToBase = 40f;
    [SerializeField] private float idealDistanceToOtherAgents = 30f;

    [Header("Base Reference")]
    public Transform baseStation; // Set this in the inspector, or let it be the spawn point

    private Vector3 _startPosition;

    protected override void Start()
    {
        base.Start();

        // Use the base station location assigned by the Spawner, or default to its current spawn location
        _startPosition = baseStation != null ? baseStation.position : transform.position;

        // Relay drones move strategically, so they need a specialized routine to find optimal positions
        StartCoroutine(CalculateOptimalPositionRoutine());
    }

    protected override void OnTargetReached()
    {
        // When a Relay drone reaches its target, it enters a guarding state to hold position and act as a node
        CurrentState = AgentState.Guarding;
    }

    public override void OnVictimFound(GameObject victim)
    {
        // Relays generally shouldn't be the ones finding victims, but if they do, they can log it.
        Debug.Log($"Relay {name} observed victim at {victim.transform.position}.");
    }

    /// <summary>
    /// Periodically calculates a position that keeps the drone connected to the base station 
    /// while staying close to exploring scouts to maximize communication range.
    /// </summary>
    private IEnumerator CalculateOptimalPositionRoutine()
    {
        while (true)
        {
            // Only recalculate if we are holding a position or idle
            if (CurrentState == AgentState.Idle || CurrentState == AgentState.Guarding)
            {
                Vector3 newPosition = FindBestRelayPoint();

                // Only move if the new position is significantly better/different to prevent jittering
                if (Vector3.Distance(transform.position, newPosition) > 5f)
                {
                    SetTarget(newPosition);
                    CurrentState = AgentState.Searching; // Re-use Searching state to indicate movement
                }
            }
            yield return new WaitForSeconds(5f); // Recalculate every few seconds
        }
    }

    /// <summary>
    /// A heuristic algorithm to find a position that bridges the gap between scouts and the base station.
    /// </summary>
    private Vector3 FindBestRelayPoint()
    {
        // Get all active scout drones from the agent list
        var scouts = BaseAgent.GetAllAgents()
            .Where(a => a is ScoutDrone)
            .Cast<ScoutDrone>()
            .ToArray();

        if (scouts.Length == 0) return transform.position; // No scouts, stay put

        // Find the "center of mass" of all scouts
        Vector3 averageScoutPos = Vector3.zero;
        foreach (ScoutDrone scout in scouts)
        {
            averageScoutPos += scout.transform.position;
        }
        averageScoutPos /= scouts.Length;

        // Determine a point on the line between the base station and the scouts
        Vector3 directionToBase = (_startPosition - averageScoutPos).normalized;

        // We want to be closer to the scouts, but still within range of the base station
        // For simplicity, let's place the relay halfway between the average scout position and the base,
        // but clamped to the maximum communication range from the base.

        float distanceToBase = Vector3.Distance(_startPosition, averageScoutPos);
        float targetDistance = Mathf.Min(distanceToBase * 0.5f, idealDistanceToBase);

        Vector3 optimalPos = _startPosition - directionToBase * targetDistance;
        optimalPos.y = transform.position.y; // Try to maintain current altitude if flying

        return optimalPos;
    }
}
