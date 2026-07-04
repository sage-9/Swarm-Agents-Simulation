using UnityEngine;

/// <summary>
/// Interface for agents that can be assigned a target position.
/// Used by the spawner to dispatch agents without needing type-specific knowledge.
/// </summary>
public interface IAssignable
{
    /// <summary>
    /// Assigns a target position to this agent.
    /// </summary>
    void AssignTarget(Vector3 targetPosition);
    
    void AssignTarget(GameObject targetObject);
}
