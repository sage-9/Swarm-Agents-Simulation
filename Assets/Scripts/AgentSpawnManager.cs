using System.Collections.Generic;
using UnityEngine;

public class AgentSpawnManager : MonoBehaviour
{
    public int numberOfScoutDrones;
    public int numberOfRescueDrones;
    public int numberOfRelayDrones;

    private List<GameObject> _scoutDrones;
    public GameObject scoutDronePrefab;


    void Awake()
    {
        _scoutDrones= new List<GameObject>();
        for (int i = 0; i < numberOfScoutDrones; i++)
        {
            GameObject go = Instantiate(scoutDronePrefab, transform.position, transform.rotation);
            _scoutDrones.Add(go);
        }
    }
    
}