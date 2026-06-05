using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages path following behavior for agents using A* pathfinding.
/// Provides a clean interface for setting paths and navigating through waypoints.
/// </summary>
public class PathFollower : MonoBehaviour
{
    private List<Vector3> _path = new List<Vector3>();
    private int _currentWaypointIndex;
    private float _lastPathCalculationTime;

    [SerializeField] private float pathRecalculationInterval = 1f;

    public List<Vector3> Path => _path;
    public int CurrentWaypointIndex => _currentWaypointIndex;
    public bool HasPath => _path != null && _path.Count > 0;
    public bool IsAtEnd => !HasPath || _currentWaypointIndex >= _path.Count;
    public float LastPathCalculationTime => _lastPathCalculationTime;

    /// <summary>
    /// Sets a new path for the agent to follow.
    /// </summary>
    public void SetPath(List<Vector3> path)
    {
        _path = path ?? new List<Vector3>();
        _currentWaypointIndex = 0;
        _lastPathCalculationTime = Time.time;
    }

    /// <summary>
    /// Gets the current waypoint the agent should move toward.
    /// </summary>
    public Vector3 GetCurrentWaypoint()
    {
        if (!HasPath || _currentWaypointIndex >= _path.Count)
            return transform.position;
        return _path[_currentWaypointIndex];
    }

    /// <summary>
    /// Advances to the next waypoint in the path.
    /// </summary>
    /// <returns>True if there are more waypoints, false if at the end of the path</returns>
    public bool AdvanceToNextWaypoint()
    {
        if (!HasPath) return false;
        _currentWaypointIndex++;
        return _currentWaypointIndex < _path.Count;
    }

    /// <summary>
    /// Clears the current path.
    /// </summary>
    public void ClearPath()
    {
        _path.Clear();
        _currentWaypointIndex = 0;
    }

    /// <summary>
    /// Checks if the path should be recalculated based on interval.
    /// </summary>
    public bool ShouldRecalculate()
    {
        return Time.time - _lastPathCalculationTime > pathRecalculationInterval || !HasPath;
    }
}
