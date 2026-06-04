using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentSpawnManager : MonoBehaviour
{
    // A simple singleton pattern so other scripts can access the spawner easily if needed
    public static AgentSpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private int numberOfScoutDrones = 3;
    [SerializeField] private int numberOfRescueDrones = 2;
    [SerializeField] private int numberOfRelayDrones = 1;
    [SerializeField] private float spawnInterval = 3f; // Time between each drone spawn

    [Header("Prefabs")]
    [SerializeField] private GameObject scoutDronePrefab;
    [SerializeField] private GameObject rescuerDronePrefab;
    [SerializeField] private GameObject relayDronePrefab;
    
    [Header("Base Station")]
    [SerializeField] private Transform baseStationLocation;

    private List<ScoutDrone> _scoutDrones = new List<ScoutDrone>();
    private List<RescuerDrone> _rescuerDrones = new List<RescuerDrone>();
    private List<RelayDrone> _relayDrones = new List<RelayDrone>();
    
    private HashSet<GameObject> _reportedVictims = new HashSet<GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (baseStationLocation == null) baseStationLocation = transform;
    }

    void Start()
    {
        StartCoroutine(SpawnAgentsRoutine());
    }

    private IEnumerator SpawnAgentsRoutine()
    {
        // 1. Spawn Scouts
        for (int i = 0; i < numberOfScoutDrones; i++)
        {
            if (scoutDronePrefab != null)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                GameObject go = Instantiate(scoutDronePrefab, spawnPos, transform.rotation);
                ScoutDrone scout = go.GetComponent<ScoutDrone>();
                if (scout != null)
                {
                    _scoutDrones.Add(scout);
                }
                else
                {
                    Debug.LogError("Scout prefab is missing ScoutDrone component!");
                    Destroy(go);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // 2. Spawn Rescuers
        for (int i = 0; i < numberOfRescueDrones; i++)
        {
            if (rescuerDronePrefab != null)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                // Ensure rescuers spawn on the ground (y=0 or whatever your ground level is)
                spawnPos.y = baseStationLocation.position.y;
                GameObject go = Instantiate(rescuerDronePrefab, spawnPos, transform.rotation);
                RescuerDrone rescuer = go.GetComponent<RescuerDrone>();
                if (rescuer != null)
                {
                    _rescuerDrones.Add(rescuer);
                }
                else
                {
                    Debug.LogError("Rescuer prefab is missing RescuerDrone component!");
                    Destroy(go);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // 3. Spawn Relays
        for (int i = 0; i < numberOfRelayDrones; i++)
        {
            if (relayDronePrefab != null)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                GameObject go = Instantiate(relayDronePrefab, spawnPos, transform.rotation);
                RelayDrone relay = go.GetComponent<RelayDrone>();
                if (relay != null)
                {
                    relay.baseStation = baseStationLocation; // Give Relay reference to base
                    _relayDrones.Add(relay);
                }
                else
                {
                    Debug.LogError("Relay prefab is missing RelayDrone component!");
                    Destroy(go);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        // Add a random offset so Rigidbodies don't explode apart upon spawning
        Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
        return baseStationLocation.position + randomOffset;
    }

    /// <summary>
    /// Swarm Interaction: When a scout finds a victim, it calls this to dispatch an idle rescuer.
    /// </summary>
    public void RequestRescuer(GameObject victim)
    {
        // If we have already reported this victim, do nothing.
        if (_reportedVictims.Contains(victim))
        {
            return;
        }
        
        _reportedVictims.Add(victim);
        
        foreach (var rescuer in _rescuerDrones)
        {
            if (rescuer.CurrentRescuerState == RescuerDrone.RescuerState.Idle)
            {
                rescuer.AssignVictim(victim.transform.position);
                return; // Only assign one rescuer per request
            }
        }
        Debug.LogWarning("No idle rescuer drones available to dispatch!");
    }
}
