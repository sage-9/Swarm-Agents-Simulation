using UnityEngine;

/// <summary>
/// Interface for drones that perform exploration/scouting.
/// Allows other systems to interact with explorers without type coupling.
/// </summary>
public interface IExplorer
{
    /// <summary>
    /// Gets the current exploration target position.
    /// </summary>
    Vector3 GetExplorationTarget();
}

/// <summary>
/// Interface for drones that perform rescue operations.
/// </summary>
public interface IRescuer
{
    /// <summary>
    /// Gets the current rescue target position.
    /// </summary>
    Vector3 GetRescueTarget();
}

/// <summary>
/// Interface for communication relay drones.
/// </summary>
public interface IRelay
{
    /// <summary>
    /// Gets the optimal relay position for communication bridging.
    /// </summary>
    Vector3 GetRelayPosition();
}
