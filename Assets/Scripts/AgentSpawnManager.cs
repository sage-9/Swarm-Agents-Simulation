using System.Collections.Generic;
using UnityEngine;

public class AgentSpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private int numberOfScoutDrones;
    [SerializeField] private int numberOfRescueDrones;
    [SerializeField] private int numberOfRelayDrones;

    [Header("Prefabs")]
    [SerializeField] private GameObject scoutDronePrefab;

    private List<GameObject> _scoutDrones;

    void Awake()
    {
        _scoutDrones = new List<GameObject>();
        for (int i = 0; i < numberOfScoutDrones; i++)
        {
            if (scoutDronePrefab != null)
            {
                // Add a random offset so Rigidbodies don't explode apart
                Vector3 randomOffset = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                Vector3 spawnPos = transform.position + randomOffset;
                GameObject go = Instantiate(scoutDronePrefab, spawnPos, transform.rotation);
                _scoutDrones.Add(go);
            }
        }
    }
}
