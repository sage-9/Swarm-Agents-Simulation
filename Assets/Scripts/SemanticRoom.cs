using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SemanticRoom : MonoBehaviour
{
    [Tooltip("Is the drone finished exploring this room?")]
    public bool isExplored = false;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }
    
    // In the future, you can link this to your Voxel grid to calculate 
    // exactly what percentage of THIS specific room has been mapped.
    public void MarkRoomExplored()
    {
        isExplored = true;
        Debug.Log($"<color=cyan>AI recognized room {gameObject.name} as fully explored.</color>");
    }
}