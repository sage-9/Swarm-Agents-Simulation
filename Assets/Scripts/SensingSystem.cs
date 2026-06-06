using System.Collections;
using UnityEngine;

public class SensingSystem : MonoBehaviour
{
    private Grid _personalGrid;

    [Header("Sensor Settings")]
    [SerializeField] private float scanRange = 15f;
    [SerializeField] private int scanResolution = 20;
    [SerializeField] private float scanInterval = 0.2f;
    [SerializeField] private LayerMask obstacleLayerMask;

    public delegate void VictimFoundDelegate(GameObject victim);
    public event VictimFoundDelegate OnVictimFound;

    public void SetPersonalGrid(Grid grid)
    {
        _personalGrid = grid;
    }

    public void StartScanning()
    {
        StartCoroutine(ScanningRoutine());
    }

    private IEnumerator ScanningRoutine()
    {
        while (true)
        {
            if (_personalGrid != null)
            {
                Perform3DScan();
            }
            yield return new WaitForSeconds(scanInterval);
        }
    }

    private void Perform3DScan()
    {
        float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;

        for (int i = 0; i < scanResolution; i++)
        {
            float t = (float)i / scanResolution;
            float inclination = Mathf.Acos(1f - 2f * t);
            float azimuth = 2f * Mathf.PI * goldenRatio * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            Vector3 dir = new Vector3(x, y, z);
            Ray ray = new Ray(transform.position, dir);

            _personalGrid.UpdateRay(ray, scanRange, obstacleLayerMask, out _);
            CheckForVictims(ray);
        }
    }

    private void CheckForVictims(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, scanRange))
        {
            if (hit.collider.CompareTag("Victim"))
            {
                SimulationTelemetry telemetry = FindAnyObjectByType<SimulationTelemetry>();
                if (telemetry != null)
                {
                    telemetry.RecordVictimDiscovered(hit.collider.gameObject, hit.point);
                }

                OnVictimFound?.Invoke(hit.collider.gameObject);
            }
        }
    }
}

