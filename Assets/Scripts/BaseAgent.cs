using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public abstract class BaseAgent : MonoBehaviour
{ 
    [Header("References")]
    protected Grid PersonalGrid;
    [SerializeField] private bool enableDebugView;

    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float turnSpeed = 120f;
    [SerializeField] protected float arrivalDistance = 0.5f;
    List<Vector3> _directions = new List<Vector3>();

    [Header("Sensor Settings")]
    [SerializeField] protected float scanRange = 15f;
    [SerializeField] protected int scanResolution = 20; // Number of rays per scan
    [SerializeField] protected float scanInterval = 0.2f; // Time between scans
    
    // Movement State
    protected Vector3 TargetPosition;
    public enum AgentState { Idle, Searching, Guarding, Returning }

    private AgentState _currentState;
    public AgentState CurrentState
    {
        get => _currentState;
        protected set
        {
            _currentState = value;
            Debug.Log($"{_currentState}");
        }
    }
    
    protected virtual void Start()
    {
        Debug.Log("Awake");
        TargetPosition = transform.position;
        CurrentState = AgentState.Idle;
        Initialize();
        // Start the scanning loop
        StartCoroutine(ScanningRoutine());
    }

    /// <summary>
    /// Creates a blank Unexplored Grid with right Dimensions.
    /// </summary>
    public void Initialize()
    {
        Debug.Log("Initialized");
        PersonalGrid = new Grid(WorldGridManager.DefaultGrid);

    }

    protected virtual void Update()
    {
        if (CurrentState == AgentState.Idle || PersonalGrid == null) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        float distance = Vector3.Distance(transform.position, TargetPosition);

        if (distance > arrivalDistance)
        {
            // Simple Kinematic Move
            transform.position = Vector3.MoveTowards(transform.position, TargetPosition, moveSpeed * Time.deltaTime);
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
        Debug.Log("Starting Scan");
        // Generates a spherical burst of rays to map the 3D environment
        // Uses the Fibonacci Sphere algorithm for even distribution
        float goldenRatio = (1 + Mathf.Sqrt(5)) / 2;
        
        for (int i = 0; i < scanResolution; i++)
        {
            float t = (float)i / scanResolution;
            float inclination = Mathf.Acos(1 - 2 * t);
            float azimuth = 2 * Mathf.PI * goldenRatio * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            Vector3 dir = new Vector3(x, y, z);
            Ray ray = new Ray(transform.position, dir);

            // CALLING YOUR GRID CLASS LOGIC
            PersonalGrid.UpdateRay(ray, scanRange, out Vector3 hitPoint);
            // Secondary check: If we hit something on the Victim Layer, trigger detection
            _directions.Add(dir);
            Debug.Log(("Direction Added"));
        }
        Debug.Log("Getting Next Target");
        GetNextDirection();
    }

    private void GetNextDirection()
    {
        float directionCheck = -1;
        Vector3 nextTarget=Vector3.zero;
        foreach (Vector3 dir in _directions)
        {
            Ray ray = new Ray(transform.position, dir);
            if(Physics.Raycast(ray)) continue;
            if (Vector3.Dot(transform.forward, dir) > directionCheck)
            {
                directionCheck = Vector3.Dot(transform.forward, dir);
                nextTarget = ray.GetPoint(scanRange);
                
            }
        }
        SetTarget(nextTarget);
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

    public virtual void SetTarget(Vector3 newTarget)
    {
        Debug.Log("Target set");
        TargetPosition = newTarget;
        CurrentState = AgentState.Searching;
    }

    protected abstract void OnTargetReached();
    public abstract void OnVictimFound(GameObject victim);

    public void OnDrawGizmosSelected()
    {
        if(!enableDebugView || PersonalGrid==null) return;
        PersonalGrid.DrawDebugGrid();
    }
}


