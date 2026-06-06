using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages agent spawning with a generic, extensible system.
/// Decoupled from specific drone types - supports any IAssignable drone.
/// </summary>
public class AgentSpawnManager : MonoBehaviour
{
    public static AgentSpawnManager Instance { get; private set; }

    [System.Serializable]
    private class DroneSpawnConfig
    {
        public GameObject prefab;
        public int count = 1;
        public float yOffset = 0f;
        public string typeKey = "Drone";
    }

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 0.3f;
    [SerializeField] private DroneSpawnConfig[] droneConfigs = new DroneSpawnConfig[3];

    [Header("Base Station")]
    [SerializeField] private Transform baseStationLocation;

    private Dictionary<string, List<BaseAgent>> _dronesByType = new Dictionary<string, List<BaseAgent>>();
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

        if (baseStationLocation == null)
            baseStationLocation = transform;

        InitializeDefaultConfigs();
    }

    void Start()
    {
        StartCoroutine(SpawnAgentsRoutine());
    }

    private void InitializeDefaultConfigs()
    {
        if (droneConfigs == null || droneConfigs.Length == 0)
        {
            droneConfigs = new DroneSpawnConfig[3];
            droneConfigs[0] = new DroneSpawnConfig { typeKey = "Scout", count = 3 };
            droneConfigs[1] = new DroneSpawnConfig { typeKey = "Rescuer", count = 2, yOffset = 0f };
            droneConfigs[2] = new DroneSpawnConfig { typeKey = "Relay", count = 1 };
        }
    }

    private IEnumerator SpawnAgentsRoutine()
    {
        if (droneConfigs == null || droneConfigs.Length == 0)
        {
            Debug.LogWarning("No drone configurations provided!");
            yield break;
        }

        foreach (var config in droneConfigs)
        {
            if (config == null || config.prefab == null)
                continue;

            if (string.IsNullOrEmpty(config.typeKey))
                config.typeKey = config.prefab.name;

            if (!_dronesByType.ContainsKey(config.typeKey))
                _dronesByType[config.typeKey] = new List<BaseAgent>();

            yield return StartCoroutine(SpawnDroneType(config));
        }

        Debug.Log($"Spawning complete. Total drones: {GetAllDrones().Count}");
    }

    private IEnumerator SpawnDroneType(DroneSpawnConfig config)
    {
        for (int i = 0; i < config.count; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();
            spawnPos.y += config.yOffset;

            GameObject go = Instantiate(config.prefab, spawnPos, baseStationLocation.rotation);
            BaseAgent agent = go.GetComponent<BaseAgent>();

            if (agent != null)
            {
                _dronesByType[config.typeKey].Add(agent);

                if (agent is RelayDrone relay && baseStationLocation != null)
                    relay.baseStation = baseStationLocation;
            }
            else
            {
                Debug.LogError($"{config.prefab.name} is missing BaseAgent component!");
                Destroy(go);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
        return baseStationLocation.position + randomOffset;
    }

    /// <summary>
    /// Requests an available rescuer to be assigned to a victim.
    /// Uses interface-based dispatch for type agnosticism.
    /// </summary>
    public void RequestRescuer(GameObject victim)
    {
       
        if (_reportedVictims.Contains(victim))
            return;
        
        Debug.Log("Checking for available drones");
        if (_dronesByType.TryGetValue("Rescuer", out var rescuers))
        {
            Debug.Log("Rescuers found");
            foreach (var drone in rescuers)
            {
                if (drone is IAssignable assignable && drone.CurrentState == BaseAgent.AgentState.Idle)
                {
                    assignable.AssignTarget(victim.transform.position);
                    Debug.Log("Assigned to Rescue");
                    _reportedVictims.Add(victim);
                    return;
                }
            }
            Debug.Log("No drones found");
        }

        Debug.LogWarning("No idle rescuer drones available to dispatch!");
    }

    public List<BaseAgent> GetDronesByType(string typeKey)
    {
        return _dronesByType.TryGetValue(typeKey, out var list) ? list : new List<BaseAgent>();
    }

    public List<BaseAgent> GetAllDrones()
    {
        return _dronesByType.Values.SelectMany(l => l).ToList();
    }
}
