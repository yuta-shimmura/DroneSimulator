using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SensorModel : MonoBehaviour
{
    public string    droneId;
    public DroneSpec spec;

    static readonly List<SensorModel> All = new List<SensorModel>();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    public string GetNearbyDronesJson()
    {
        var sensor = spec?.sensor;
        if (sensor == null) return "[]";

        float range   = sensor.detectionRangeM;
        bool  full360 = sensor.detectionFOVDeg >= 360f;
        float fovCos  = Mathf.Cos(sensor.detectionFOVDeg * 0.5f * Mathf.Deg2Rad);
        bool  useLOS  = sensor.useLineOfSight;
        float sigma   = sensor.positionNoiseSigma;

        var sb    = new StringBuilder("[");
        bool first = true;

        foreach (var other in All)
        {
            if (other == this || !other.isActiveAndEnabled) continue;

            Vector3 toOther = other.transform.position - transform.position;
            float   dist    = toOther.magnitude;
            if (dist > range) continue;

            if (!full360 && Vector3.Dot(transform.forward, toOther.normalized) < fovCos)
                continue;

            if (useLOS && Physics.Linecast(transform.position, other.transform.position))
                continue;

            Vector3 npos = other.transform.position + new Vector3(
                Gauss(sigma), Gauss(sigma * 0.5f), Gauss(sigma));

            if (!first) sb.Append(',');
            sb.Append($"{{\"droneId\":\"{other.droneId}\"," +
                      $"\"position\":{{\"x\":{npos.x:F3},\"y\":{npos.y:F3},\"z\":{npos.z:F3}}}," +
                      $"\"distance\":{dist:F3}}}");
            first = false;
        }
        sb.Append(']');
        return sb.ToString();
    }

    // AutopilotController が反発力計算に使用（ノイズなし・Vector3 直返し）
    public List<Vector3> GetNearbyPositions(float radius)
    {
        var result = new List<Vector3>();
        foreach (var other in All)
        {
            if (other == this || !other.isActiveAndEnabled) continue;
            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist <= radius)
                result.Add(other.transform.position);
        }
        return result;
    }

    static float Gauss(float sigma)
    {
        if (sigma <= 0f) return 0f;
        float u1 = Mathf.Max(1e-6f, Random.value);
        return sigma * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * Random.value);
    }
}
