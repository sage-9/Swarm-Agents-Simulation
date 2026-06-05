using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Communication relay drone that positions itself optimally between scouts and base station.
/// Uses IExplorer interface for type-agnostic explorer detection.
/// </summary>
public class RelayDrone : BaseAgent
{
    [Header("Relay Settings")]
    [SerializeField] private float idealDistanceToBase = 40f;
    [SerializeField] private float idealDistanceToOtherAgents = 30f;

    [Header("Base Reference")]
    public Transform baseStation;

    private Vector3 _startPosition;

    protected override void Start()
    {
        base.Start();

        _startPosition = baseStation != null ? baseStation.position : transform.position;
        StartCoroutine(CalculateOptimalPositionRoutine());
    }

    protected override void OnTargetReached()
    {
        CurrentState = AgentState.Guarding;
    }

    public override void OnVictimFound(GameObject victim)
    {
        Debug.Log($"Relay {name} observed victim at {victim.transform.position}.");
    }

    private IEnumerator CalculateOptimalPositionRoutine()
    {
        while (true)
        {
            if (CurrentState == AgentState.Idle || CurrentState == AgentState.Guarding)
            {
                Vector3 newPosition = FindBestRelayPoint();

                if (Vector3.Distance(transform.position, newPosition) > 5f)
                {
                    SetTarget(newPosition);
                    CurrentState = AgentState.Searching;
                }
            }
            yield return new WaitForSeconds(5f);
        }
    }

    private Vector3 FindBestRelayPoint()
    {
        var explorers = BaseAgent.GetAllAgents()
            .FindAll(a => a is IExplorer)
            .ConvertAll(a => (IExplorer)a);

        if (explorers.Count == 0) return transform.position;

        Vector3 averageExplorerPos = Vector3.zero;
        foreach (var explorer in explorers)
        {
            var agent = explorer as BaseAgent;
            if (agent != null)
                averageExplorerPos += agent.transform.position;
        }
        averageExplorerPos /= explorers.Count;

        Vector3 directionToBase = (_startPosition - averageExplorerPos).normalized;

        float distanceToBase = Vector3.Distance(_startPosition, averageExplorerPos);
        float targetDistance = Mathf.Min(distanceToBase * 0.5f, idealDistanceToBase);

        Vector3 optimalPos = _startPosition - directionToBase * targetDistance;
        optimalPos.y = transform.position.y;

        return optimalPos;
    }
}
