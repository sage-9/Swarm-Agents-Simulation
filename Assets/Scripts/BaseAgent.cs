using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public abstract class BaseAgent : MonoBehaviour
{
    [Header("Debug")]
    public Grid PersonalGrid { get; protected set; } // Made public so other agents can access it for merging
    [SerializeField] private bool enableDebugView;

    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float turnSpeed = 120f;
    [SerializeField] protected float arrivalDistance = 0.5f;

    [Header("Obstacle Avoidance Settings")]
    [SerializeField] protected float avoidanceRadius = 3f;
    [SerializeField] protected float avoidanceWeight = 2f;
    [SerializeField] protected float separationRadius = 2f; // Distance they try to keep from each other
    [SerializeField] protected float separationWeight = 1.5f; // How strongly they push away from each other
    [SerializeField] protected float physicsAvoidanceWeight = 3f; // Force to apply when physically near a wall

    // Remove _directions as it's no longer used for reactive movement

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
    private static List<BaseAgent> _allAgents = new List<BaseAgent>();

    public static List<BaseAgent> GetAllAgents() => _allAgents;

    protected Rigidbody _rb;

    protected virtual void Start()
    {
        TargetPosition = transform.position;
        CurrentState = AgentState.Idle;

        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true; // Use kinematic so we can control movement manually but still detect physics overlaps

        Initialize();

        _allAgents.Add(this);

        // Start routines
        StartCoroutine(ScanningRoutine());
        StartCoroutine(CommunicationRoutine());
    }

    protected virtual void OnDestroy()
    {
        _allAgents.Remove(this);
    }

    protected virtual void Update()
    {
        if (CurrentState == AgentState.Idle) return;

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

    protected virtual void HandleMovement()
    {
        float distance = Vector3.Distance(transform.position, TargetPosition);

        if (distance > arrivalDistance)
        {
            // 1. Calculate direction to the target
            Vector3 targetDirection = (TargetPosition - transform.position).normalized;

            // 2. Calculate static obstacle avoidance force based on grid data
            Vector3 obstacleAvoidanceForce = CalculateObstacleAvoidance();

            // 3. Calculate dynamic separation from nearby agents
            Vector3 separationForce = CalculateAgentSeparation();

            // 4. Calculate immediate physics avoidance (in case it sneaks past grid mapping)
            Vector3 physicsAvoidance = CalculateImmediatePhysicsAvoidance();

            // 5. Combine directions and move
            Vector3 finalDirection = (targetDirection +
                                     (obstacleAvoidanceForce * avoidanceWeight) +
                                     (separationForce * separationWeight) +
                                     (physicsAvoidance * physicsAvoidanceWeight)).normalized;

            // Simple Kinematic Move
            transform.position += finalDirection * (moveSpeed * Time.deltaTime);
        }
        else
        {
            OnTargetReached();
        }
    }

    /// <summary>
    /// Does a small physics sphere cast right in front of the drone to push it back if it's about to clip a wall
    /// that its grid hasn't mapped yet.
    /// </summary>
    protected Vector3 CalculateImmediatePhysicsAvoidance()
    {
        Vector3 avoidanceForce = Vector3.zero;
        
        Collider[] hits = new Collider[5]; // Non-allocating buffer
        int numHits = Physics.OverlapSphereNonAlloc(transform.position, avoidanceRadius * 0.5f, hits, obstacleLayerMask);

        for (int i = 0; i < numHits; i++)
        {
            // Push away from the closest point on the collider
            Vector3 closestPoint = hits[i].ClosestPoint(transform.position);
            float dist = Vector3.Distance(transform.position, closestPoint);

            if (dist < 0.01f) dist = 0.01f; // prevent division by zero

            avoidanceForce += (transform.position - closestPoint).normalized / dist;
        }

        return avoidanceForce;
    }

    /// <summary>
    /// Queries the agent's personal voxel grid for nearby occupied nodes to push away from them.
    /// </summary>
    protected Vector3 CalculateObstacleAvoidance()
    {
        Vector3 avoidanceForce = Vector3.zero;

        if (PersonalGrid == null) return avoidanceForce;

        // Find grid index of current position
        if (PersonalGrid.WorldToGrid(transform.position, out Vector3Int currentIdx))
        {
            int checkRange = Mathf.CeilToInt(avoidanceRadius / PersonalGrid.VoxelSize);

            // Iterate local neighborhood
            for (int x = currentIdx.x - checkRange; x <= currentIdx.x + checkRange; x++)
            {
                for (int y = currentIdx.y - checkRange; y <= currentIdx.y + checkRange; y++)
                {
                    for (int z = currentIdx.z - checkRange; z <= currentIdx.z + checkRange; z++)
                    {
                        Vector3Int checkIdx = new Vector3Int(x, y, z);
                        if (PersonalGrid.GetVoxel(checkIdx) == NodeState.Occupied)
                        {
                            Vector3 obstacleWorldPos = PersonalGrid.GridToWorld(checkIdx);
                            float distToObstacle = Vector3.Distance(transform.position, obstacleWorldPos);

                            // Only push if it's within the radius
                            if (distToObstacle < avoidanceRadius && distToObstacle > 0.01f)
                            {
                                // The closer the obstacle, the stronger the push
                                avoidanceForce += (transform.position - obstacleWorldPos).normalized * (1.0f - (distToObstacle / avoidanceRadius));
                            }
                        }
                    }
                }
            }
        }
        return avoidanceForce;
    }

    protected Vector3 CalculateAgentSeparation()
    {
        Vector3 separationForce = Vector3.zero;
        foreach (BaseAgent other in _allAgents)
        {
            if (other == this || other == null) continue;
            float distToOther = Vector3.Distance(transform.position, other.transform.position);
            if (distToOther < separationRadius && distToOther > 0.01f)
            {
                separationForce += (transform.position - other.transform.position).normalized * (1.0f - (distToOther / separationRadius));
            }
        }
        return separationForce;
    }

    protected virtual void HandleRotation()
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
            PersonalGrid.UpdateRay(ray, scanRange, obstacleLayerMask, out _);

            // Secondary check: If we hit something on the Victim Layer, trigger detection
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
        foreach (BaseAgent otherAgent in _allAgents)
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

    private void OnDrawGizmosSelected()
    {
       
        if (!enableDebugView || PersonalGrid == null) return;
        PersonalGrid.DrawDebugGrid();

        // Draw communication range
        Gizmos.color = new Color(0, 0, 1, 0.2f); // Transparent blue
        Gizmos.DrawWireSphere(transform.position, communicationRange);
    }
}
