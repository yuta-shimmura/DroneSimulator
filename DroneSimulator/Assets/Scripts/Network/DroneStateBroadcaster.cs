using System.Collections;
using System.Globalization;
using UnityEngine;

[RequireComponent(typeof(DroneController))]
public class DroneStateBroadcaster : MonoBehaviour
{
    public string    droneId;
    public DroneSpec spec;

    DroneController     _dc;
    BatterySystem       _bat;
    WeatherSystem       _weather;
    Rigidbody           _rb;
    SensorModel         _sensor;
    AutopilotController _ap;

    void Start()
    {
        _dc      = GetComponent<DroneController>();
        _bat     = GetComponent<BatterySystem>();
        _rb      = GetComponent<Rigidbody>();
        _weather = FindObjectOfType<WeatherSystem>();
        _sensor  = GetComponent<SensorModel>();
        _ap      = GetComponent<AutopilotController>();

        int ms = spec?.communication?.broadcastIntervalMs ?? 50;
        StartCoroutine(BroadcastLoop(ms / 1000f));
    }

    IEnumerator BroadcastLoop(float interval)
    {
        var wait = new WaitForSecondsRealtime(interval);
        while (true)
        {
            yield return wait;
            TCPServer.Instance?.SendTo(droneId, BuildJson());
        }
    }

    string BuildJson()
    {
        var pos   = transform.position;
        var vel   = _rb != null ? _rb.velocity : Vector3.zero;
        var rot   = transform.eulerAngles;
        var force = _weather != null ? _weather.GetCurrentForce() : Vector3.zero;

        // センサーノイズを加算
        if (spec?.sensor != null)
        {
            var s = spec.sensor;
            pos += new Vector3(Gauss(s.positionNoiseSigma),
                               Gauss(s.altitudeNoiseSigma),
                               Gauss(s.positionNoiseSigma));
            vel += new Vector3(Gauss(s.velocityNoiseSigma),
                               Gauss(s.velocityNoiseSigma),
                               Gauss(s.velocityNoiseSigma));
        }

        string nearby   = _sensor != null ? _sensor.GetNearbyDronesJson() : "[]";
        bool   airborne = _dc != null && _dc.IsAirborne;
        bool   altHold  = _dc != null && _dc.altitudeHoldEnabled;
        float  battery  = _bat != null ? _bat.Percentage : 100f;

        bool   apActive = _ap != null && _ap.IsActive;
        int    apCur    = _ap != null ? _ap.CurrentIndex  : 0;
        int    apTotal  = _ap != null ? _ap.WaypointCount : 0;

        var ic = CultureInfo.InvariantCulture;
        string F(float v, string fmt = "F3") => v.ToString(fmt, ic);

        return $"{{" +
            $"\"type\":\"state\"," +
            $"\"timestamp\":{F(Time.time)}," +
            $"\"droneId\":\"{droneId}\"," +
            $"\"position\":{{\"x\":{F(pos.x)},\"y\":{F(pos.y)},\"z\":{F(pos.z)}}}," +
            $"\"velocity\":{{\"x\":{F(vel.x)},\"y\":{F(vel.y)},\"z\":{F(vel.z)}}}," +
            $"\"rotation\":{{\"x\":{F(rot.x)},\"y\":{F(rot.y)},\"z\":{F(rot.z)}}}," +
            $"\"altitude\":{F(pos.y)}," +
            $"\"speed\":{F(vel.magnitude)}," +
            $"\"battery\":{F(battery, "F1")}," +
            $"\"isAirborne\":{(airborne ? "true" : "false")}," +
            $"\"altitudeHold\":{(altHold ? "true" : "false")}," +
            $"\"autopilot\":{{\"active\":{(apActive ? "true" : "false")}," +
                $"\"currentWaypoint\":{apCur},\"totalWaypoints\":{apTotal}}}," +
            $"\"weather\":{{\"force\":{{\"x\":{F(force.x)},\"y\":{F(force.y)},\"z\":{F(force.z)}}}}}," +
            $"\"nearbyDrones\":{nearby}" +
            $"}}";
    }

    static float Gauss(float sigma)
    {
        if (sigma <= 0f) return 0f;
        float u1 = Mathf.Max(1e-6f, Random.value);
        return sigma * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * Random.value);
    }
}
