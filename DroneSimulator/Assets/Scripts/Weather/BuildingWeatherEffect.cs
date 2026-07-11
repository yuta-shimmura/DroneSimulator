using System.Collections.Generic;
using UnityEngine;

public class BuildingWeatherEffect : MonoBehaviour
{
    [Header("Thermal")]
    public bool thermalEnabled = true;
    public float thermalStrength = 0.05f;

    [Header("Downdraft")]
    public bool downdraftEnabled = true;
    public float downdraftStrength = 0.1f;

    [Header("Detection")]
    [Tooltip("この高さ未満のオブジェクトは建物とみなさない")]
    public float minBuildingHeight = 5f;

    public Transform drone;

    struct BuildingZone
    {
        public Vector3 center;
        public float radiusXZ;
        public float topY;
    }

    readonly List<BuildingZone> _zones = new List<BuildingZone>();
    Rigidbody _droneRb;
    WeatherSystem _weatherSystem;

    public float LastThermalForceY   { get; private set; }
    public float LastDowndraftForceY { get; private set; }

    void Start()
    {
        if (drone != null)
            _droneRb = drone.GetComponent<Rigidbody>();
        _weatherSystem = GetComponent<WeatherSystem>();
        ScanBuildings();
    }

    public void ScanBuildings()
    {
        _zones.Clear();
        var renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var r in renderers)
        {
            if (drone != null && r.transform.IsChildOf(drone)) continue;

            var bounds = r.bounds;
            if (bounds.size.y < minBuildingHeight) continue;

            _zones.Add(new BuildingZone
            {
                center   = bounds.center,
                radiusXZ = Mathf.Max(bounds.extents.x, bounds.extents.z),
                topY     = bounds.max.y
            });
        }
        Debug.Log($"[BuildingWeatherEffect] Scanned {_zones.Count} building zones.");
    }

    public void ComputeForces(Vector3 dronePos, out float thermalY, out float downdraftY)
    {
        Vector3 windDir = GetWindDir();
        float tScale    = GetActiveLayerStrength<ThermalLayer>();
        float dScale    = GetActiveLayerStrength<DowndraftLayer>();

        float thermalTotal   = 0f;
        float downdraftTotal = 0f;

        foreach (var zone in _zones)
        {
            float dx     = dronePos.x - zone.center.x;
            float dz     = dronePos.z - zone.center.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);

            if (distXZ > zone.radiusXZ + 50f) continue;

            if (thermalEnabled && tScale > 0f && dronePos.y > zone.topY && distXZ < zone.radiusXZ)
            {
                float t = 1f - distXZ / zone.radiusXZ;
                thermalTotal += thermalStrength * tScale * t;
            }

            if (downdraftEnabled && dScale > 0f && windDir != Vector3.zero)
            {
                Vector3 leeCenter = zone.center + windDir * (zone.radiusXZ + 8f);
                float   ldx       = dronePos.x - leeCenter.x;
                float   ldz       = dronePos.z - leeCenter.z;
                float   leeDist   = Mathf.Sqrt(ldx * ldx + ldz * ldz);
                float   leeRadius = zone.radiusXZ * 1.5f;

                if (leeDist < leeRadius && dronePos.y < zone.topY + 20f && dronePos.y > zone.center.y - zone.radiusXZ)
                {
                    float t = 1f - leeDist / leeRadius;
                    downdraftTotal += downdraftStrength * dScale * t;
                }
            }
        }

        thermalY   = thermalTotal;
        downdraftY = -downdraftTotal;
    }

    void FixedUpdate()
    {
        if (_droneRb == null || drone == null || _droneRb.isKinematic) return;

        ComputeForces(drone.position, out float tY, out float dY);
        LastThermalForceY   = tY;
        LastDowndraftForceY = dY;

        Vector3 total = Vector3.up * (tY + dY);
        if (total.sqrMagnitude > 0f)
            _droneRb.AddForce(total, ForceMode.Acceleration);
    }

    float GetActiveLayerStrength<T>() where T : WeatherLayer
    {
        if (_weatherSystem == null) return 1f;
        foreach (var layer in _weatherSystem.ActiveLayers)
            if (layer is T l) return l.strength;
        return 0f;
    }

    Vector3 GetWindDir()
    {
        if (_weatherSystem == null) return Vector3.zero;
        foreach (var layer in _weatherSystem.ActiveLayers)
        {
            if (layer is SteadyWindLayer sw)
                return new Vector3(sw.direction.x, 0f, sw.direction.z).normalized;
            if (layer is GustLayer gl)
                return new Vector3(gl.baseDirection.x, 0f, gl.baseDirection.z).normalized;
        }
        return Vector3.zero;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_zones.Count == 0) ScanBuildings();
        if (_zones.Count == 0) return;

        Vector3 windDir = Application.isPlaying ? GetWindDir() : Vector3.zero;

        foreach (var zone in _zones)
        {
            if (thermalEnabled)
            {
                UnityEditor.Handles.color = new Color(1f, 0.9f, 0f, 0.25f);
                UnityEditor.Handles.DrawSolidDisc(new Vector3(zone.center.x, zone.topY, zone.center.z), Vector3.up, zone.radiusXZ);
                UnityEditor.Handles.color = new Color(1f, 0.9f, 0f, 0.8f);
                UnityEditor.Handles.DrawWireDisc(new Vector3(zone.center.x, zone.topY, zone.center.z), Vector3.up, zone.radiusXZ);
            }

            if (downdraftEnabled && windDir != Vector3.zero)
            {
                Vector3 leeCenter = zone.center + windDir * (zone.radiusXZ + 8f);
                float   leeRadius = zone.radiusXZ * 1.5f;
                float   leeY      = zone.center.y + (zone.topY - zone.center.y) * 0.5f;
                UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 0.2f);
                UnityEditor.Handles.DrawSolidDisc(new Vector3(leeCenter.x, leeY, leeCenter.z), Vector3.up, leeRadius);
                UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 0.8f);
                UnityEditor.Handles.DrawWireDisc(new Vector3(leeCenter.x, leeY, leeCenter.z), Vector3.up, leeRadius);
            }
        }
    }
#endif
}
