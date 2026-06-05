using System.Collections.Generic;
using UnityEngine;
using static SimulationTelemetry;
using Random = UnityEngine.Random;

/// <summary>
/// Randomly disables a percentage of drones in the scene.
/// Useful for testing swarm behavior with reduced resources.
/// </summary>
public class DroneDisabler : MonoBehaviour
{
    [Header("Disable Settings")]
    [SerializeField] [Range(0, 100)] private float disablePercentage;
    [SerializeField] private bool randomizeSelection = true;
    [SerializeField] private List<string> targetDroneTypes = new List<string> { "Scout", "Rescuer", "Relay" };

    private void Start()
    {
        DisableAgents += InitiateKillSwitch;
    }

    void InitiateKillSwitch()
    {
        if (disablePercentage <= 0 || disablePercentage >= 100) return;
        DisableDronesPercentage();
        Debug.Log("<color=red>KillSwitch initiated!</color>");
        Debug.Log($"<color=red>disabling {disablePercentage}% of drones.</color>");
    }

    private void DisableDronesPercentage()
    {
        var allDrones = GetAllTargetDrones();
        int dronesToDisable = Mathf.CeilToInt(allDrones.Count * (disablePercentage / 100f));

        if (randomizeSelection)
        {
            for (int i = 0; i < dronesToDisable; i++)
            {
                int randomIndex = Random.Range(0, allDrones.Count);
                allDrones[randomIndex].enabled = false;
                allDrones.RemoveAt(randomIndex);
            }
        }
        else
        {
            for (int i = 0; i < dronesToDisable; i++)
            {
                allDrones[i].enabled = false;
            }
        }

        Debug.Log($"Disabled {dronesToDisable} drones ({disablePercentage}%)");
    }

    private List<BaseAgent> GetAllTargetDrones()
    {
        List<BaseAgent> targetDrones = new List<BaseAgent>();

        if (AgentSpawnManager.Instance == null)
            return targetDrones;

        foreach (var droneType in targetDroneTypes)
        {
            targetDrones.AddRange(AgentSpawnManager.Instance.GetDronesByType(droneType));
        }

        return targetDrones;
    }
}
