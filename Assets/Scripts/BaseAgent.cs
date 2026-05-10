using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public abstract class BaseAgent : MonoBehaviour
{ 
    [Header("Debug")]
    public Grid PersonalGrid { get; protected set; } // Made public so other agents can access it for merging
    [SerializeField] private bool enableDebugView;

    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float turnSpeed = 120f;
    [SerializeField] protected float arrivalDistance = 0.5f;
    [SerializeField] protected float separationRadius = 2f; // Distance they try to keep from each other
    [SerializeField] protected float separationWeight = 1.5f; // How strongly they push away from each other
    private List<Vector3> _directions = new List<Vector3>();

    [Header("Sensor Settings")]
    [SerializeField] protected float scanRange = 15f;
    [SerializeField] protected int scanResolution = 20; // Number of rays per scan
    [SerializeField] protected float scanInterval = 0.2f; // Time between scans

    [Header("Communication Settings")]
    [SerializeField] protected float communicationRange = 30f;
    [SerializeField] protected float communicationInterval = 1f;
    [SerializeField] protected LayerMask obstacleLayerMask; // Set this to the layer your obstacles are on

    // Movement State
    protected Vector3 TargetPosition;
    public enum AgentState { Idle, Searching, Guarding, Returning }
    public AgentState CurrentState { get; protected set; }
    
    // Maintain a list of all active agents to scan against
    private static List<BaseAgent> allAgents = new List<BaseAgent>();

    protected virtual void Start()
    {
        TargetPosition = transform.position;
        CurrentState = AgentState.Idle;
        Initialize();
        
        allAgents.Add(this);

        // Start routines
        StartCoroutine(ScanningRoutine());
        StartCoroutine(CommunicationRoutine());
    }

    protected virtual void OnDestroy()
    {
        allAgents.Remove(this);
    }

    protected virtual void Update()
    {
        if (CurrentState == AgentState.Idle || PersonalGrid == null) return;

        HandleMovement();
        HandleRotation();
    }
    
    /// <summary>
    /// Creates a blank Unexplored Grid with right Dimensions.
    /// </summary>
    public void Initialize()
    {
        PersonalGrid = new Grid(WorldGridManager.DefaultGrid);
    }

    private void HandleMovement()
    {
        float distance = Vector3.Distance(transform.position, TargetPosition);

        if (distance > arrivalDistance)
        {
            // 1. Calculate direction to the target
            Vector3 targetDirection = (TargetPosition - transform.position).normalized;
            
            // 2. Calculate separation from nearby agents
            Vector3 separationForce = Vector3.zero;
            foreach (BaseAgent other in allAgents)
            {
                if (other == this || other == null) continue;
                float distToOther = Vector3.Distance(transform.position, other.transform.position);
                if (distToOther < separationRadius && distToOther > 0.01f)
                {
                    separationForce += (transform.position - other.transform.position).normalized / distToOther;
                }
            }
            
            // 3. Combine directions and move
            Vector3 finalDirection = (targetDirection + (separationForce * separationWeight)).normalized;
            transform.position += finalDirection * moveSpeed * Time.deltaTime;
        }
        else
        {
            OnTargetReached();
        }
    }

    private void HandleRotation()
    {
        Vector3 direction = (TargetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// This routine uses your Grid's UpdateRay method to fill the voxel data.
    /// </summary>
    private IEnumerator ScanningRoutine()
    {
        while (true)
        {
            if (PersonalGrid != null)
            {
                Perform3DScan();
            }
            yield return new WaitForSeconds(scanInterval);
        }
    }

    private void Perform3DScan()
    {
        // Generates a spherical burst of rays to map the 3D environment
        // Uses the Fibonacci Sphere algorithm for even distribution
        float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
        
        _directions.Clear();
        
        for (int i = 0; i < scanResolution; i++)
        {
            float t = (float)i / scanResolution;
            float inclination = Mathf.Acos(1f - 2f * t);
            float azimuth = 2f * Mathf.PI * goldenRatio * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            Vector3 dir = new Vector3(x, y, z);
            Ray ray = new Ray(transform.position, dir);

            // CALLING YOUR GRID CLASS LOGIC
            PersonalGrid.UpdateRay(ray, scanRange, out _);
            // Secondary check: If we hit something on the Victim Layer, trigger detection
            _directions.Add(dir);
            CheckForVictims(ray);
        }
    }
    
    private void CheckForVictims(Ray ray)
    {
        // Using standard Raycast for Victim detection (since NodeState.Occupied 
        // doesn't tell us IF it is a victim, just that it's an obstacle)
        if (Physics.Raycast(ray, out RaycastHit hit, scanRange))
        {
            if (hit.collider.CompareTag("Victim"))
            {
                OnVictimFound(hit.collider.gameObject);
            }
        }
    }

    /// <summary>
    /// Scans for other agents to share grid data.
    /// </summary>
    private IEnumerator CommunicationRoutine()
    {
        while (true)
        {
            if (PersonalGrid != null)
            {
                ShareGridDataWithNearbyAgents();
            }
            yield return new WaitForSeconds(communicationInterval);
        }
    }

    private void ShareGridDataWithNearbyAgents()
    {
        foreach (BaseAgent otherAgent in allAgents)
        {
            if (otherAgent == this || otherAgent == null || otherAgent.PersonalGrid == null) continue;

            float distance = Vector3.Distance(transform.position, otherAgent.transform.position);
            if (distance <= communicationRange)
            {
                // Check Line of Sight
                Vector3 direction = (otherAgent.transform.position - transform.position).normalized;
                
                // Raycast to check if an obstacle is between them
                if (!Physics.Raycast(transform.position, direction, distance, obstacleLayerMask))
                {
                    // No obstacles, merge data!
                    PersonalGrid.MergeGrid(otherAgent.PersonalGrid);
                }
            }
        }
    }

    public virtual void SetTarget(Vector3 newTarget)
    {
        TargetPosition = newTarget;
        CurrentState = AgentState.Searching;
    }

    protected abstract void OnTargetReached();
    public abstract void OnVictimFound(GameObject victim);

    public void OnDrawGizmosSelected()
    {
        if (!enableDebugView || PersonalGrid == null) return;
        PersonalGrid.DrawDebugGrid();

        // Draw communication range
        Gizmos.color = new Color(0, 0, 1, 0.2f); // Transparent blue
        Gizmos.DrawWireSphere(transform.position, communicationRange);
    }
}
