using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DroneController))]
public class AutopilotController : MonoBehaviour
{
    [Header("Autopilot")]
    public float waypointReachDistance = 5f;
    public float maxHorizontalInput    = 0.8f;
    public float maxVerticalInput      = 0.6f;
    public float slowDownDistance      = 12f;
    public float yawGain               = 0.03f;
    public float waypointTimeout       = 15f;

    [Header("Separation")]
    public float separationRadius = 15f;
    public float separationGain   = 0.6f;

    // 巡航高度（DroneSpawner が機体ごとに設定）
    public float cruiseAlt = 30f;

    public bool IsActive      => _active;
    public int  WaypointCount => _waypoints?.Count ?? 0;
    public int  CurrentIndex  => _index;

    public Action         OnArrived;
    public Action<string> OnFailed;

    DroneController _dc;
    SensorModel     _sensor;
    List<Vector3>   _waypoints;
    int             _index;
    bool            _active;
    float           _waypointTimer;
    float           _collisionTimer;

    void Awake()
    {
        _dc     = GetComponent<DroneController>();
        _sensor = GetComponent<SensorModel>();
    }

    public void StartAutopilot(List<Vector3> waypoints)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            OnFailed?.Invoke("no_path_found");
            return;
        }
        _waypoints     = waypoints;
        _index         = 0;
        _active         = true;
        _waypointTimer  = 0f;
        _collisionTimer = 0f;
        _dc.EnableExternalInput(true);
        if (!_dc.IsAirborne) _dc.RequestTakeoff();
        Debug.Log($"[AutopilotController] Autopilot started. Waypoints: {waypoints.Count}.");
    }

    public void StopAutopilot()
    {
        _active = false;
        _dc.SetExternalInput(0f, 0f, 0f, 0f);
        Debug.Log("[AutopilotController] Autopilot stopped.");
    }

    void Update()
    {
        if (!_active) return;
        if (!_dc.IsAirborne) { _dc.RequestTakeoff(); return; }

        if (_index >= _waypoints.Count) { Arrive(); return; }

        var   target   = _waypoints[_index];
        var   toTarget = target - transform.position;
        float dist     = toTarget.magnitude;

        if (dist < waypointReachDistance)
        {
            _index++;
            _waypointTimer  = 0f;
            _collisionTimer = 0f;
            return;
        }

        // 衝突継続タイマー: OnCollisionStay を含む継続接触を検出
        if (_dc.IsInCollision)
        {
            _collisionTimer += Time.deltaTime;
            if (_collisionTimer >= 1.5f)
            {
                Debug.Log($"[AutopilotController] Waypoint {_index} blocked (collision {_collisionTimer:F1}s), skipping.");
                _index++;
                _waypointTimer  = 0f;
                _collisionTimer = 0f;
                return;
            }
        }
        else
        {
            _collisionTimer = 0f;
        }

        _waypointTimer += Time.deltaTime;
        if (_waypointTimer >= waypointTimeout)
        {
            Debug.Log($"[AutopilotController] Waypoint {_index} timed out, skipping.");
            _index++;
            _waypointTimer  = 0f;
            _collisionTimer = 0f;
            return;
        }

        // 天候による水平入力上限の絞り
        float windSpeed      = _dc.GetCurrentWindSpeed();
        float maxOpWind      = _dc.MaxOperatingWindSpeed;
        float weatherScale   = windSpeed > maxOpWind
            ? Mathf.Clamp01(maxOpWind / windSpeed)
            : 1f;
        float hInputLimit    = maxHorizontalInput * weatherScale;

        float speedScale = Mathf.Clamp01(dist / slowDownDistance);

        var hDir     = new Vector3(toTarget.x, 0f, toTarget.z);
        var hDirNorm = hDir.magnitude > 0.01f ? hDir.normalized : Vector3.zero;
        var fwd      = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        var right    = new Vector3(transform.right.x,   0f, transform.right.z).normalized;

        float pitch    = Vector3.Dot(hDirNorm, fwd)  * hInputLimit * speedScale;
        float roll     = Vector3.Dot(hDirNorm, right) * hInputLimit * speedScale;
        float vertical = Mathf.Clamp(toTarget.y / 5f, -1f, 1f) * maxVerticalInput;

        // ヨー: 水平距離が十分あり、かつ角度誤差が閾値超のときのみ回転する（クルクル防止）
        // 水平距離 < 2m（真上から降下中）や角度誤差 < 8° の場合はヨーなし
        bool hasHorizDir = hDir.magnitude > 2f;
        float yawAngle   = hasHorizDir ? Vector3.SignedAngle(fwd, hDirNorm, Vector3.up) : 0f;
        float yaw        = (hasHorizDir && Mathf.Abs(yawAngle) > 8f)
            ? Mathf.Clamp(yawAngle * yawGain, -1f, 1f)
            : 0f;

        // 3D 反発力（近接する他機から押し出す）
        // 最終ウェイポイントへの降下中は分離力を段階的に減衰（ビルへの押し込み防止）
        bool isFinalWaypoint = (_index == _waypoints.Count - 1);
        float sepScale = isFinalWaypoint ? Mathf.Clamp01(dist / 20f) : 1f;
        if (_sensor != null && sepScale > 0.01f)
        {
            var nearby = _sensor.GetNearbyPositions(separationRadius);
            float repPitch = 0f, repRoll = 0f, repVertical = 0f;
            foreach (var otherPos in nearby)
            {
                var   away = transform.position - otherPos;
                float d    = away.magnitude;
                if (d < 0.01f) continue;
                float strength = (1f - d / separationRadius) * separationGain * sepScale;
                var   repulse  = away.normalized * strength;

                repPitch    += Vector3.Dot(repulse, fwd);
                repRoll     += Vector3.Dot(repulse, right);
                repVertical += repulse.y * maxVerticalInput;
            }
            // 反発力合計を hInputLimit の半分に制限（移動入力を打ち消さないよう）
            float repCap = hInputLimit * 0.5f;
            pitch    += Mathf.Clamp(repPitch,    -repCap, repCap);
            roll     += Mathf.Clamp(repRoll,     -repCap, repCap);
            vertical += Mathf.Clamp(repVertical, -maxVerticalInput * 0.5f, maxVerticalInput * 0.5f);
        }

        // 衝突中は下降入力をカット（ビル擦り落ちでウェイポイントを誤通過するのを防止）
        if (_dc.IsInCollision && vertical < 0f) vertical = 0f;

        // 合算後クランプ
        pitch    = Mathf.Clamp(pitch,    -hInputLimit, hInputLimit);
        roll     = Mathf.Clamp(roll,     -hInputLimit, hInputLimit);
        vertical = Mathf.Clamp(vertical, -maxVerticalInput, maxVerticalInput);

        _dc.SetExternalInput(pitch, roll, yaw, vertical);
    }

    void Arrive()
    {
        _active = false;
        _dc.SetExternalInput(0f, 0f, 0f, 0f);
        _dc.EnableExternalInput(false);
        _dc.RequestLand();
        OnArrived?.Invoke();
        Debug.Log("[AutopilotController] Arrived at destination.");
    }
}
