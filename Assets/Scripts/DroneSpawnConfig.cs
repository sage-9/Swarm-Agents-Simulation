using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DroneSpawnConfig
{
    public GameObject prefab;
    public int count = 1;
    public float yOffset = 0f;
    public string typeKey = "Drone";
}
