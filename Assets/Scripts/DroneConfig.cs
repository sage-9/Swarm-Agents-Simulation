using UnityEngine;

[CreateAssetMenu(menuName = "Drone Config", fileName = "New Drone Config")]
public class DroneConfig : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 120f;
    public float arrivalDistance = 0.5f;

    [Header("Avoidance")]
    public float avoidanceRadius = 3f;
    public float avoidanceWeight = 2f;
    public float separationRadius = 2f;
    public float separationWeight = 1.5f;
    public float physicsAvoidanceWeight = 3f;

    [Header("Sensing")]
    public float scanRange = 15f;
    public int scanResolution = 20;
    public float scanInterval = 0.2f;

    [Header("Communication")]
    public float communicationRange = 30f;
    public float communicationInterval = 1f;
}
