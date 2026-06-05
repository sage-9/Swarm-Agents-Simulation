using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class containing shared behavior calculations used by all agents.
/// </summary>
public static class AgentBehaviors
{
    /// <summary>
    /// Calculates a separation force that pushes an agent away from nearby agents.
    /// Used for swarm cohesion and avoiding collisions between drones.
    /// </summary>
    /// <param name="position">Position of the agent</param>
    /// <param name="separationRadius">Radius within which to consider other agents</param>
    /// <param name="allAgents">List of all active agents in the scene</param>
    /// <returns>Normalized separation force vector</returns>
    public static Vector3 CalculateSeparation(Vector3 position, float separationRadius, List<BaseAgent> allAgents)
    {
        Vector3 separationForce = Vector3.zero;
        foreach (BaseAgent other in allAgents)
        {
            if (other == null) continue;
            float distToOther = Vector3.Distance(position, other.transform.position);
            if (distToOther < separationRadius && distToOther > 0.01f)
            {
                separationForce += (position - other.transform.position).normalized * (1.0f - (distToOther / separationRadius));
            }
        }
        return separationForce;
    }
}
