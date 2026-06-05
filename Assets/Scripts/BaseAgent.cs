using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all drone agents. Uses composition via system components.
/// Handles lifecycle, coordination, and abstract methods for derived classes.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(MovementSystem))]
[RequireComponent(typeof(AvoidanceSystem))]
[RequireComponent(typeof(SensingSystem))]
[RequireComponent(typeof(CommunicationSystem))]
public abstract class BaseAgent : MonoBehaviour
{
    [Header("Debug")]
    public Grid PersonalGrid { get; protected set; }
    [SerializeField] private bool enableDebugView;

    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float turnSpeed = 120f;
    [SerializeField] protected float arrivalDistance = 0.5f;

    [Header("Obstacle Avoidance Settings")]
    [SerializeField] protected float avoidanceRadius = 3f;
    [SerializeField] protected float avoidanceWeight = 2f;
    [SerializeField] protected float separationRadius = 2f;
    [SerializeField] protected float separationWeight = 1.5f;
    [SerializeField] protected float physicsAvoidanceWeight = 3f;

    [Header("Sensor Settings")]
    [SerializeField] protected float scanRange = 15f;
    [SerializeField] protected int scanResolution = 20;
    [SerializeField] protected float scanInterval = 0.2f;

    [Header("Communication Settings")]
    [SerializeField] protected float communicationRange = 30f;
    [SerializeField] protected float communicationInterval = 1f;
    [SerializeField] protected LayerMask obstacleLayerMask;

    protected Vector3 TargetPosition;

    public enum AgentState { Idle, Searching, Guarding, Returning }
    public AgentState CurrentState { get; protected set; }

    private static List<BaseAgent> _allAgents = new List<BaseAgent>();
    public static List<BaseAgent> GetAllAgents() => _allAgents;

    protected Rigidbody _rb;
    protected MovementSystem _movement;
    protected AvoidanceSystem _avoidance;
    protected SensingSystem _sensing;
    protected CommunicationSystem _communication;

    protected virtual void Start()
    {
        TargetPosition = transform.position;
        CurrentState = AgentState.Idle;

        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;

        _movement = GetComponent<MovementSystem>();
        _avoidance = GetComponent<AvoidanceSystem>();
        _sensing = GetComponent<SensingSystem>();
        _communication = GetComponent<CommunicationSystem>();

        Initialize();

        _allAgents.Add(this);

        _avoidance.SetPersonalGrid(PersonalGrid);
        _sensing.SetPersonalGrid(PersonalGrid);
        _communication.SetPersonalGrid(PersonalGrid);

        _sensing.OnVictimFound += OnVictimFound;

        _sensing.StartScanning();
        _communication.StartCommunication();
    }

    protected virtual void OnDestroy()
    {
        _allAgents.Remove(this);
        if (_sensing != null)
            _sensing.OnVictimFound -= OnVictimFound;
    }

    protected virtual void Update()
    {
        if (CurrentState == AgentState.Idle) return;

        HandleMovement();
        HandleRotation();
    }

    /// <summary>
    /// Initializes the agent's personal grid based on world grid template.
    /// </summary>
    public void Initialize()
    {
        PersonalGrid = new Grid(WorldGridManager.DefaultGrid);
    }

    /// <summary>
    /// Handles movement toward target with avoidance forces applied.
    /// </summary>
    protected virtual void HandleMovement()
    {
        float distance = Vector3.Distance(transform.position, TargetPosition);

        if (distance > arrivalDistance)
        {
            Vector3 targetDirection = (TargetPosition - transform.position).normalized;
            Vector3 avoidanceForces = _avoidance.GetAvoidanceForces(transform.position, _allAgents);

            Vector3 finalDirection = (targetDirection + avoidanceForces).normalized;

            _movement.Move(finalDirection, moveSpeed);
        }
        else
        {
            OnTargetReached();
        }
    }

    /// <summary>
    /// Handles rotation toward target.
    /// </summary>
    protected virtual void HandleRotation()
    {
        Vector3 direction = (TargetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            _movement.Rotate(direction);
        }
    }

    /// <summary>
    /// Sets a new target position for the agent.
    /// </summary>
    public virtual void SetTarget(Vector3 newTarget)
    {
        TargetPosition = newTarget;
        CurrentState = AgentState.Searching;
    }

    /// <summary>
    /// Called when the agent reaches its target. Override in derived classes.
    /// </summary>
    protected abstract void OnTargetReached();

    /// <summary>
    /// Called when a victim is detected. Override in derived classes.
    /// </summary>
    public abstract void OnVictimFound(GameObject victim);

    private void OnDrawGizmosSelected()
    {
        if (!enableDebugView || PersonalGrid == null) return;

        GridVisualizer visualizer = GetComponent<GridVisualizer>();
        if (visualizer == null)
            visualizer = gameObject.AddComponent<GridVisualizer>();

        visualizer.SetGrid(PersonalGrid);
        visualizer.DrawDebugGrid();

        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, communicationRange);
    }
}
