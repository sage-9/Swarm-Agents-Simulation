using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SemanticDoorway : MonoBehaviour
{
    [Tooltip("Has the drone successfully passed through this door?")]
    public bool isExplored = false;

    [Tooltip("How far inside the room should the drone aim for?")]
    public float roomEntryDepth = 3.0f;

    [Tooltip("How far outside the door should the drone line up?")]
    public float approachDistance = 2.0f;

    private void Start()
    {
        // Ensure the collider is a trigger so the drone doesn't crash into it
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // When the drone's collider passes through the trigger, mark this door as explored
        if (other.GetComponentInParent<BaseAgent>() != null)
        {
            isExplored = true;
        }
    }

    // Calculates a point outside the room for the drone to line up with
    public Vector3 GetApproachPoint()
    {
        // Since your Z-axis (transform.forward) points INTO the room, subtracting it pushes the point OUTSIDE
        return transform.position - (transform.forward * approachDistance);
    }

    // Calculates a point inside the room to ensure the drone fully enters
    public Vector3 GetEntryPoint()
    {
        return transform.position + (transform.forward * roomEntryDepth);
    }

    // Draws visual helpers in the Unity Editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetApproachPoint(), 0.3f); // Approach Point (Yellow)
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetEntryPoint(), 0.3f);   // Entry Point (Green)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(GetApproachPoint(), GetEntryPoint()); // Flight Path
    }
}