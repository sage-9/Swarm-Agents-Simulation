using System.Collections;
using UnityEngine;

public class RescuerDrone : BaseAgent
{
    [Header("Rescuer Settings")]
    [SerializeField] private float rescueTime = 3f; // Time it takes to "rescue" a victim

    private Vector3 _victimPosition;
    private Vector3 _startPosition;
    private bool _isRescuing = false;

    public enum RescuerState { Idle, MovingToVictim, Rescuing, ReturningToBase }
    public RescuerState CurrentRescuerState { get; private set; }

    protected override void Start()
    {
        base.Start();
        _startPosition = transform.position;
        CurrentRescuerState = RescuerState.Idle;
        CurrentState = AgentState.Idle; // Set base state
    }

    /// <summary>
    /// Assigns a victim to this drone and starts the rescue process.
    /// </summary>
    public void AssignVictim(Vector3 victimPosition)
    {
        if (CurrentRescuerState == RescuerState.Idle)
        {
            _victimPosition = victimPosition;
            SetTarget(_victimPosition);
            CurrentRescuerState = RescuerState.MovingToVictim;
            Debug.Log($"{name} assigned to rescue victim at {_victimPosition}.");
        }
    }

    protected override void OnTargetReached()
    {
        switch (CurrentRescuerState)
        {
            case RescuerState.MovingToVictim:
                StartCoroutine(RescueRoutine());
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
        _isRescuing = false;
    }

    public override void OnVictimFound(GameObject victim)
    {
        // A rescuer's primary job is to go to assigned victims.
        // For now, we'll just log if it finds another one unexpectedly.
        Debug.LogWarning($"{name} (Rescuer) found an unassigned victim at {victim.transform.position}. This could be reported to a swarm manager.");
    }
}
