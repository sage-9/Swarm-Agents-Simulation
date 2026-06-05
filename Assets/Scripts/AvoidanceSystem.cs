using System.Collections.Generic;
using UnityEngine;

public class AvoidanceSystem : MonoBehaviour
{
    private Grid _personalGrid;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float avoidanceRadius = 3f;
    [SerializeField] private float avoidanceWeight = 2f;

    [Header("Agent Separation")]
    [SerializeField] private float separationRadius = 2f;
    [SerializeField] private float separationWeight = 1.5f;

    [Header("Physics Avoidance")]
    [SerializeField] private float physicsAvoidanceWeight = 3f;
    [SerializeField] private LayerMask obstacleLayerMask;

    public float AvoidanceRadius => avoidanceRadius;
    public float SeparationRadius => separationRadius;
    public float AvoidanceWeight => avoidanceWeight;
    public float SeparationWeight => separationWeight;
    public float PhysicsAvoidanceWeight => physicsAvoidanceWeight;

    public void SetPersonalGrid(Grid grid)
    {
        _personalGrid = grid;
    }

    public Vector3 GetAvoidanceForces(Vector3 position, List<BaseAgent> allAgents)
    {
        Vector3 obstacleForce = CalculateObstacleAvoidance(position);
        Vector3 separationForce = AgentBehaviors.CalculateSeparation(position, separationRadius, allAgents);
        Vector3 physicsForce = CalculateImmediatePhysicsAvoidance(position);

        return (obstacleForce * avoidanceWeight) +
               (separationForce * separationWeight) +
               (physicsForce * physicsAvoidanceWeight);
    }

    private Vector3 CalculateObstacleAvoidance(Vector3 position)
    {
        Vector3 avoidanceForce = Vector3.zero;

        if (_personalGrid == null) return avoidanceForce;

        if (_personalGrid.WorldToGrid(position, out Vector3Int currentIdx))
        {
            int checkRange = Mathf.CeilToInt(avoidanceRadius / _personalGrid.VoxelSize);

            for (int x = currentIdx.x - checkRange; x <= currentIdx.x + checkRange; x++)
            {
                for (int y = currentIdx.y - checkRange; y <= currentIdx.y + checkRange; y++)
                {
                    for (int z = currentIdx.z - checkRange; z <= currentIdx.z + checkRange; z++)
                    {
                        Vector3Int checkIdx = new Vector3Int(x, y, z);
                        if (_personalGrid.GetVoxel(checkIdx) == NodeState.Occupied)
                        {
                            Vector3 obstacleWorldPos = _personalGrid.GridToWorld(checkIdx);
                            float distToObstacle = Vector3.Distance(position, obstacleWorldPos);

                            if (distToObstacle < avoidanceRadius && distToObstacle > 0.01f)
                            {
                                avoidanceForce += (position - obstacleWorldPos).normalized * (1.0f - (distToObstacle / avoidanceRadius));
                            }
                        }
                    }
                }
            }
        }
        return avoidanceForce;
    }

    private Vector3 CalculateImmediatePhysicsAvoidance(Vector3 position)
    {
        Vector3 avoidanceForce = Vector3.zero;

        Collider[] hits = new Collider[5];
        int numHits = Physics.OverlapSphereNonAlloc(position, avoidanceRadius * 0.5f, hits, obstacleLayerMask);

        for (int i = 0; i < numHits; i++)
        {
            Vector3 closestPoint = hits[i].ClosestPoint(position);
            float dist = Vector3.Distance(position, closestPoint);

            if (dist < 0.01f) dist = 0.01f;

            avoidanceForce += (position - closestPoint).normalized / dist;
        }

        return avoidanceForce;
    }
}
