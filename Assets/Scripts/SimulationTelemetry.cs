using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Security.Cryptography.X509Certificates;
using UnityEngine.Serialization;

/// <summary>
/// Tracks simulation telemetry including exploration, victim discovery, and rescue metrics.
/// Outputs data to CSV for analysis.
/// </summary>
public class SimulationTelemetry : MonoBehaviour
{
    [Header("Output Settings")]
    [SerializeField] private string outputDirectory = "SimulationData";
    [SerializeField] private string sessionName = "Simulation";
    [SerializeField] private bool autoStartTracking = true;
    [SerializeField] private float samplingInterval = 1f;
    
    [Header("Failure test")] 
    [SerializeField]private float timeToActivateSystemFailureInSeconds = 90.0f;
    private bool _failureHasBeenActivated = false;
    public static event Action DisableAgents;

    private float _simulationStartTime;
    private float _lastSampleTime;
    private float _firstVictimFoundTime = -1f;
    private float _firstVictimRescuedTime = -1f;

    private List<VictimEvent> _victimEvents = new List<VictimEvent>();
    private List<SimulationSnapshot> _snapshots = new List<SimulationSnapshot>();

    private HashSet<GameObject> _foundVictims = new HashSet<GameObject>();
    private HashSet<GameObject> _rescuedVictims = new HashSet<GameObject>();

    private struct VictimEvent
    {
        public GameObject victim;
        public float timeToDiscovery;
        public float timeToRescue;
        public Vector3 discoveryLocation;
        public Vector3 rescueLocation;
    }

    private struct SimulationSnapshot
    {
        public float elapsedTime;
        public int activeDrones;
        public int disabledDrones;
        public float mapExplorationPercentage;
        public int victimsDiscovered;
        public int victimsRescued;
        public int totalVictims;
    }

    void Start()
    {
        CreateOutputDirectory();
        _simulationStartTime = Time.time;
        _lastSampleTime = Time.time;

        if (autoStartTracking)
        {
            StartTracking();
        }
    }

    void Update()
    {
        if (Time.time - _lastSampleTime >= samplingInterval)
        {
            RecordSnapshot();
            _lastSampleTime = Time.time;
        }
        ActivateSystemFailure();
    }

    void ActivateSystemFailure()
    {
        if (Time.time - _simulationStartTime <= timeToActivateSystemFailureInSeconds || _failureHasBeenActivated) return;
        _failureHasBeenActivated = true;
        DisableAgents?.Invoke();
    }
    
    

    public void StartTracking()
    {
        _simulationStartTime = Time.time;
        _lastSampleTime = Time.time;
        Debug.Log($"Started simulation tracking at {DateTime.Now}");
    }

    public void StopAndExport()
    {
        ExportToCSV();
        Debug.Log($"Telemetry exported to {outputDirectory}");
    }

    public void RecordVictimDiscovered(GameObject victim, Vector3 discoveryLocation)
    {
        if (_foundVictims.Contains(victim)) return;

        _foundVictims.Add(victim);

        float discoveryTime = Time.time - _simulationStartTime;
        if (_firstVictimFoundTime < 0)
            _firstVictimFoundTime = discoveryTime;

        Debug.Log($"Victim discovered at {discoveryTime:F2}s: {victim.name}");
    }

    public void RecordVictimRescued(GameObject victim, Vector3 rescueLocation)
    {
        if (_rescuedVictims.Contains(victim)) return;

        _rescuedVictims.Add(victim);

        float rescueTime = Time.time - _simulationStartTime;
        if (_firstVictimRescuedTime < 0)
            _firstVictimRescuedTime = rescueTime;

        // Find or create victim event
        VictimEvent evt = new VictimEvent
        {
            victim = victim,
            timeToRescue = rescueTime,
            rescueLocation = rescueLocation
        };

        for (int i = 0; i < _victimEvents.Count; i++)
        {
            if (_victimEvents[i].victim == victim)
            {
                _victimEvents[i] = evt;
                break;
            }
        }

        Debug.Log($"Victim rescued at {rescueTime:F2}s");
    }

    private void RecordSnapshot()
    {
        var allDrones = BaseAgent.GetAllAgents();
        int activeDrones = 0;
        int disabledDrones = 0;

        foreach (var drone in allDrones)
        {
            if (drone.enabled)
                activeDrones++;
            else
                disabledDrones++;
        }

        float mapExploration = CalculateMapExploration();

        SimulationSnapshot snapshot = new SimulationSnapshot
        {
            elapsedTime = Time.time - _simulationStartTime,
            activeDrones = activeDrones,
            disabledDrones = disabledDrones,
            mapExplorationPercentage = mapExploration,
            victimsDiscovered = _foundVictims.Count,
            victimsRescued = _rescuedVictims.Count,
            totalVictims = GameObject.FindGameObjectsWithTag("Victim").Length
        };

        _snapshots.Add(snapshot);
    }

    private float CalculateMapExploration()
    {
        // Get world grid and calculate explored percentage
        Grid worldGrid = WorldGridManager.WorldGrid;
        if (worldGrid == null) return 0f;

        int totalVoxels = worldGrid.Length * worldGrid.Width * worldGrid.Height;
        int exploredVoxels = 0;

        for (int x = 0; x < worldGrid.Length; x++)
        {
            for (int y = 0; y < worldGrid.Height; y++)
            {
                for (int z = 0; z < worldGrid.Width; z++)
                {
                    NodeState state = worldGrid.GetVoxel(new Vector3Int(x, y, z));
                    if (state == NodeState.Free || state == NodeState.Occupied)
                        exploredVoxels++;
                }
            }
        }

        return (exploredVoxels / (float)totalVoxels) * 100f;
    }

    private void ExportToCSV()
    {
        ExportSnapshotsCSV();
        ExportVictimEventsCSV();
        ExportSummaryCSV();
    }

    private void ExportSnapshotsCSV()
    {
        string filePath = Path.Combine(outputDirectory, $"{sessionName}_snapshots_{GetTimestamp()}.csv");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("ElapsedTime,ActiveDrones,DisabledDrones,MapExplorationPercentage,VictimsDiscovered,VictimsRescued,TotalVictims");

            foreach (var snapshot in _snapshots)
            {
                writer.WriteLine($"{snapshot.elapsedTime:F2},{snapshot.activeDrones},{snapshot.disabledDrones}," +
                    $"{snapshot.mapExplorationPercentage:F2},{snapshot.victimsDiscovered},{snapshot.victimsRescued},{snapshot.totalVictims}");
            }
        }

        Debug.Log($"Exported snapshots to {filePath}");
    }

    private void ExportVictimEventsCSV()
    {
        string filePath = Path.Combine(outputDirectory, $"{sessionName}_victims_{GetTimestamp()}.csv");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("VictimName,TimeToDiscovery,TimeToRescue,DiscoveryLocation,RescueLocation");

            foreach (var evt in _victimEvents)
            {
                if (evt.victim == null) continue;

                string discoveryLoc = $"({evt.discoveryLocation.x:F1},{evt.discoveryLocation.y:F1},{evt.discoveryLocation.z:F1})";
                string rescueLoc = $"({evt.rescueLocation.x:F1},{evt.rescueLocation.y:F1},{evt.rescueLocation.z:F1})";

                writer.WriteLine($"{evt.victim.name},{evt.timeToDiscovery:F2},{evt.timeToRescue:F2},\"{discoveryLoc}\",\"{rescueLoc}\"");
            }
        }

        Debug.Log($"Exported victim events to {filePath}");
    }

    private void ExportSummaryCSV()
    {
        string filePath = Path.Combine(outputDirectory, $"{sessionName}_summary_{GetTimestamp()}.csv");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("Metric,Value");

            float elapsedTime = Time.time - _simulationStartTime;
            writer.WriteLine($"SimulationDurationSeconds,{elapsedTime:F2}");
            writer.WriteLine($"FirstVictimDiscoverySeconds,{(_firstVictimFoundTime >= 0 ? _firstVictimFoundTime : -1):F2}");
            writer.WriteLine($"FirstVictimRescueSeconds,{(_firstVictimRescuedTime >= 0 ? _firstVictimRescuedTime : -1):F2}");
            writer.WriteLine($"TotalVictimsDiscovered,{_foundVictims.Count}");
            writer.WriteLine($"TotalVictimsRescued,{_rescuedVictims.Count}");
            writer.WriteLine($"TotalVictims,{GameObject.FindGameObjectsWithTag("Victim").Length}");
            writer.WriteLine($"FinalMapExplorationPercentage,{CalculateMapExploration():F2}");

            var allDrones = BaseAgent.GetAllAgents();
            writer.WriteLine($"TotalDrones,{allDrones.Count}");

            int activeDrones = 0;
            foreach (var drone in allDrones)
            {
                if (drone.enabled) activeDrones++;
            }
            writer.WriteLine($"ActiveDrones,{activeDrones}");
            writer.WriteLine($"DisabledDrones,{allDrones.Count - activeDrones}");

            if (_snapshots.Count > 0)
            {
                float avgExploration = 0;
                foreach (var snap in _snapshots)
                    avgExploration += snap.mapExplorationPercentage;
                writer.WriteLine($"AverageMapExplorationPercentage,{(avgExploration / _snapshots.Count):F2}");
            }

            float rescueSuccessRate = _foundVictims.Count > 0 ? (_rescuedVictims.Count / (float)_foundVictims.Count) * 100f : 0;
            writer.WriteLine($"RescueSuccessRate,{rescueSuccessRate:F2}");
        }

        Debug.Log($"Exported summary to {filePath}");
    }

    private void CreateOutputDirectory()
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            Debug.Log($"Created output directory: {outputDirectory}");
        }
    }

    private string GetTimestamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    private void OnDisable()
    {
        StopAndExport();
    }
}
