using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommunicationSystem : MonoBehaviour
{
    private Grid _personalGrid;

    [Header("Communication Settings")]
    [SerializeField] private float communicationRange = 30f;
    [SerializeField] private float communicationInterval = 1f;
    [SerializeField] private LayerMask obstacleLayerMask;

    public void SetPersonalGrid(Grid grid)
    {
        _personalGrid = grid;
    }

    public void StartCommunication()
    {
        StartCoroutine(CommunicationRoutine());
    }

    private IEnumerator CommunicationRoutine()
    {
        while (true)
        {
            if (_personalGrid != null)
            {
                ShareGridDataWithNearbyAgents();
            }
            yield return new WaitForSeconds(communicationInterval);
        }
    }

    private void ShareGridDataWithNearbyAgents()
    {
        foreach (BaseAgent otherAgent in BaseAgent.GetAllAgents())
        {
            if (otherAgent == GetComponent<BaseAgent>() || otherAgent == null || otherAgent.PersonalGrid == null)
                continue;

            float distance = Vector3.Distance(transform.position, otherAgent.transform.position);
            if (distance <= communicationRange)
            {
                Vector3 direction = (otherAgent.transform.position - transform.position).normalized;

                if (!Physics.Raycast(transform.position, direction, distance, obstacleLayerMask))
                {
                    _personalGrid.MergeGrid(otherAgent.PersonalGrid);
                }
            }
        }
    }
}
