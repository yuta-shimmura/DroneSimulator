using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WeatherLogger : MonoBehaviour
{
    public WeatherSystem weatherSystem;
    public Transform drone;

    bool _logging;
    StreamWriter _writer;
    string _logPath;
    Rigidbody _droneRb;
    BuildingWeatherEffect _buildingEffect;
    bool _isTCPMode;

    public bool IsLogging   => _logging;
    public string LastLogPath => _logPath;

    void Start()
    {
        _isTCPMode     = SceneManager.GetActiveScene().name.EndsWith("TCP");
        _buildingEffect = FindObjectOfType<BuildingWeatherEffect>();
        if (!_isTCPMode && drone != null)
            _droneRb = drone.GetComponent<Rigidbody>();
    }

    public void StartLogging()
    {
        if (_logging) return;
        string dir = Path.Combine(Application.dataPath, "../WeatherLogs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"weather_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _writer  = new StreamWriter(_logPath, false, Encoding.UTF8);

        string header = _isTCPMode
            ? "drone_id,time,pos_x,pos_y,pos_z,vel_x,vel_y,vel_z,speed,altitude,weather_force_x,weather_force_y,weather_force_z,thermal_y,downdraft_y,active_layers,event,col_normal_x,col_normal_y,col_normal_z,impact_speed"
            :          "time,pos_x,pos_y,pos_z,vel_x,vel_y,vel_z,speed,altitude,weather_force_x,weather_force_y,weather_force_z,thermal_y,downdraft_y,active_layers,event,col_normal_x,col_normal_y,col_normal_z,impact_speed";
        _writer.WriteLine(header);
        _logging = true;
        Debug.Log($"[WeatherLogger] Started: {_logPath}.");
    }

    public void StopLogging()
    {
        if (!_logging) return;
        _writer?.Close();
        _writer  = null;
        _logging = false;
        Debug.Log($"[WeatherLogger] Saved: {_logPath}.");
    }

    void FixedUpdate()
    {
        if (!_logging || _writer == null) return;
        if (_isTCPMode) LogAllDrones();
        else            LogSingleDrone();
    }

    void LogSingleDrone()
    {
        if (drone == null) return;
        Vector3 pos   = drone.position;
        Vector3 vel   = _droneRb != null ? _droneRb.velocity : Vector3.zero;
        Vector3 force = weatherSystem != null ? weatherSystem.GetCurrentForce() : Vector3.zero;
        float thermalY   = _buildingEffect != null ? _buildingEffect.LastThermalForceY   : 0f;
        float downdraftY = _buildingEffect != null ? _buildingEffect.LastDowndraftForceY : 0f;
        string layers    = BuildLayersString();

        _writer.WriteLine(
            $"{Time.time:F3},{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
            $"{vel.x:F3},{vel.y:F3},{vel.z:F3},{vel.magnitude:F3},{pos.y:F3}," +
            $"{force.x:F3},{force.y:F3},{force.z:F3},{thermalY:F3},{downdraftY:F3},{layers},,,,");
    }

    void LogAllDrones()
    {
        var drones = DroneController.AllDrones
            .Where(dc => dc != null)
            .OrderBy(dc => {
                var nc = dc.GetComponent<DroneNetworkController>();
                return nc != null ? nc.spawnIndex : int.MaxValue;
            })
            .ToList();
        if (drones.Count == 0) return;

        Vector3 force  = weatherSystem != null ? weatherSystem.GetCurrentForce() : Vector3.zero;
        string layers  = BuildLayersString();
        string time    = Time.time.ToString("F3");

        foreach (var dc in drones)
        {
            Vector3 pos = dc.transform.position;
            Vector3 vel = dc.Rb != null ? dc.Rb.velocity : Vector3.zero;
            string  id  = dc.gameObject.name;

            float thermalY = 0f, downdraftY = 0f;
            if (_buildingEffect != null)
                _buildingEffect.ComputeForces(pos, out thermalY, out downdraftY);

            _writer.WriteLine(
                $"{id},{time},{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
                $"{vel.x:F3},{vel.y:F3},{vel.z:F3},{vel.magnitude:F3},{pos.y:F3}," +
                $"{force.x:F3},{force.y:F3},{force.z:F3},{thermalY:F3},{downdraftY:F3},{layers},,,,");
        }
    }

    string BuildLayersString()
    {
        if (weatherSystem == null) return "";
        var sb = new StringBuilder();
        foreach (var l in weatherSystem.ActiveLayers)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(l.displayName);
        }
        return sb.ToString();
    }

    public void LogCollision(Vector3 pos, Vector3 normal, float impactSpeed, string droneId = "")
    {
        if (!_logging || _writer == null) return;
        string prefix = _isTCPMode ? $"{droneId}," : "";
        _writer.WriteLine(
            $"{prefix}{Time.time:F3},{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
            $",,,,,,,,,,collision,{normal.x:F3},{normal.y:F3},{normal.z:F3},{impactSpeed:F3}");
    }

    void OnDestroy() => StopLogging();
}
