using System;
using System.Collections.Generic;
using UnityEngine;

// TCPServer の Incoming キューをメインスレッドで処理し、
// ドローンの生成 / 削除とコマンド転送を行う。
public class DroneSpawner : MonoBehaviour
{
    public GameObject dronePrefab;
    public Transform  spawnRoot;           // グリッド中心 Transform（任意）
    public float      gridSpacingX = 3f;   // 2×4グリッド: 列間隔
    public float      gridSpacingZ = 3f;   // 2×4グリッド: 行間隔
    public float      baseCruiseAlt = 30f; // 機体0の巡航高度
    public float      cruiseAltStep = 2f;  // 機体ごとの巡航高度オフセット

    const int MaxDrones = 8;

    readonly Dictionary<string, DroneNetworkController> _drones
        = new Dictionary<string, DroneNetworkController>();

    [Serializable] class ConnectMsg    { public string type; public string modelId; }
    [Serializable] class DisconnectMsg { public string type; }
    [Serializable] class ControlMsg
    {
        public string type;
        public float  pitch, roll, yaw, vertical;
        public bool   takeoff, land;
    }
    [Serializable] class AutopilotMsg
    {
        public string type;
        public string droneId;
        public Vec3Msg goal;
    }
    [Serializable] class Vec3Msg { public float x, y, z; }

    void Update()
    {
        if (TCPServer.Instance == null) return;

        while (TCPServer.Instance.Incoming.TryDequeue(out var raw))
            HandleMessage(raw);
    }

    void HandleMessage(TCPServer.RawMessage raw)
    {
        // type だけ先に読む
        var probe = JsonUtility.FromJson<ConnectMsg>(raw.json);
        if (probe == null) return;

        switch (probe.type)
        {
            case "connect":    OnConnect(raw);    break;
            case "control":    OnControl(raw);    break;
            case "autopilot":  OnAutopilot(raw);  break;
            case "disconnect": OnDisconnect(raw); break;
        }
    }

    void OnConnect(TCPServer.RawMessage raw)
    {
        if (_drones.Count >= MaxDrones)
        {
            Debug.LogWarning($"[DroneSpawner] Max drone limit ({MaxDrones}) reached. Connection rejected.");
            return;
        }

        var msg     = JsonUtility.FromJson<ConnectMsg>(raw.json);
        var spec    = DroneSpecLoader.Get(msg.modelId) ?? DroneSpecLoader.Get("Drone_TypeA");
        var droneId = $"drone_{raw.connection.connectionId}";

        raw.connection.droneId = droneId;

        // 2×4 グリッド配置（4列×2行、中心を spawnRoot に合わせる）
        int   idx  = _drones.Count;  // 0〜7
        int   col  = idx % 4;        // 0〜3
        int   row  = idx / 4;        // 0〜1
        float offX = (col - 1.5f) * gridSpacingX;
        float offZ = (row - 0.5f) * gridSpacingZ;
        Vector3 center = spawnRoot != null ? spawnRoot.position : new Vector3(0f, 0f, 0f);
        Vector3 pos    = center + new Vector3(offX, 10f, offZ);

        var go = dronePrefab != null
            ? Instantiate(dronePrefab, pos, Quaternion.identity)
            : CreateDefaultDrone(pos);

        go.name = droneId;

        // カメラを先頭機（idx==0）にのみ追従させる
        if (idx == 0)
        {
            var cf = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cf == null) cf = FindObjectOfType<CameraFollow>();
            if (cf != null) cf.target = go.transform;
        }

        // ネットワーク制御コンポーネントを追加
        var nc = go.AddComponent<DroneNetworkController>();
        nc.droneId    = droneId;
        nc.spec       = spec;
        nc.spawnIndex = idx;

        var sb = go.AddComponent<DroneStateBroadcaster>();
        sb.droneId = droneId;
        sb.spec    = spec;

        var sm = go.AddComponent<SensorModel>();
        sm.droneId = droneId;
        sm.spec    = spec;

        // AutopilotController に巡航高度を事前設定
        var ap = go.AddComponent<AutopilotController>();
        ap.cruiseAlt = baseCruiseAlt + idx * cruiseAltStep;

        _drones[droneId] = nc;

        // ACK を送信
        var specJson = JsonUtility.ToJson(spec);
        TCPServer.Instance.SendTo(droneId,
            $"{{\"type\":\"connected\",\"droneId\":\"{droneId}\",\"spawnIndex\":{idx},\"specs\":{specJson}}}");

        Debug.Log($"[DroneSpawner] Spawned {droneId} (model={spec.modelId}, slot={idx}, cruiseAlt={ap.cruiseAlt}m).");
    }

    void OnControl(TCPServer.RawMessage raw)
    {
        if (raw.connection.droneId == null) return;
        if (!_drones.TryGetValue(raw.connection.droneId, out var nc)) return;

        var msg = JsonUtility.FromJson<ControlMsg>(raw.json);
        nc.ApplyCommand(msg.pitch, msg.roll, msg.yaw, msg.vertical, msg.takeoff, msg.land);
    }

    void OnAutopilot(TCPServer.RawMessage raw)
    {
        var msg = JsonUtility.FromJson<AutopilotMsg>(raw.json);
        if (msg?.goal == null) return;

        string id = msg.droneId ?? raw.connection.droneId;
        if (id == null || !_drones.TryGetValue(id, out var nc)) return;

        var goal  = new Vector3(msg.goal.x, msg.goal.y, msg.goal.z);

        // 機体ごとの巡航高度を PathPlanner に渡す
        var ap0 = nc.gameObject.GetComponent<AutopilotController>();
        float thisCruiseAlt = ap0 != null ? ap0.cruiseAlt : baseCruiseAlt;

        var start = nc.transform.position;
        // ドローンは autopilot 開始後に離陸(+10m)してから経路追従を開始する。
        // スポーン高度(y=10)から計画すると離陸後に最初のウェイポイントへ向かって
        // 降下し、建物に衝突するケースがある。離陸後の実際の高度から計画する。
        start.y = Mathf.Max(start.y + 10f, thisCruiseAlt);

        TCPServer.Instance.SendTo(id,
            $"{{\"type\":\"autopilot_status\",\"droneId\":\"{id}\",\"status\":\"planning\"}}");

        StartCoroutine(PathPlanner.PlanAsync(start, goal, path =>
        {
            if (path == null)
            {
                TCPServer.Instance?.SendTo(id,
                    $"{{\"type\":\"autopilot_status\",\"droneId\":\"{id}\"," +
                    $"\"status\":\"failed\",\"reason\":\"no_path_found\"}}");
                return;
            }

            var ap = nc.gameObject.GetComponent<AutopilotController>();
            if (ap == null) ap = nc.gameObject.AddComponent<AutopilotController>();

            ap.OnArrived = () =>
                TCPServer.Instance?.SendTo(id,
                    $"{{\"type\":\"autopilot_status\",\"droneId\":\"{id}\",\"status\":\"arrived\"}}");

            ap.OnFailed = reason =>
                TCPServer.Instance?.SendTo(id,
                    $"{{\"type\":\"autopilot_status\",\"droneId\":\"{id}\"," +
                    $"\"status\":\"failed\",\"reason\":\"{reason}\"}}");

            float delay = nc.spawnIndex * 3f;
            StartCoroutine(DelayedStartAutopilot(ap, path, id, goal, delay));
        }, thisCruiseAlt));
    }

    System.Collections.IEnumerator DelayedStartAutopilot(
        AutopilotController ap, List<Vector3> path, string id, Vector3 goal, float delay)
    {
        if (delay > 0f)
        {
            Debug.Log($"[DroneSpawner] {id} departure delayed {delay:F0}s (UTM slot).");
            yield return new WaitForSeconds(delay);
        }
        ap.StartAutopilot(path);
        TCPServer.Instance?.SendTo(id,
            $"{{\"type\":\"autopilot_status\",\"droneId\":\"{id}\"," +
            $"\"status\":\"flying\",\"waypointCount\":{path.Count}}}");
        Debug.Log($"[DroneSpawner] Autopilot started for {id}. Goal=({goal.x:F1},{goal.y:F1},{goal.z:F1}).");
    }

    void OnDisconnect(TCPServer.RawMessage raw)
    {
        string id = raw.connection.droneId;
        if (id == null || !_drones.TryGetValue(id, out var nc)) return;

        _drones.Remove(id);
        if (nc != null) Destroy(nc.gameObject);
        RenumberDrones();
        Debug.Log($"[DroneSpawner] Removed {id}.");
    }

    void RenumberDrones()
    {
        var sorted = new List<DroneNetworkController>(_drones.Values);
        sorted.Sort((a, b) => a.spawnIndex.CompareTo(b.spawnIndex));
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].spawnIndex = i;
    }

    // dronePrefab が未設定のときにデフォルト GameObject を生成
    static GameObject CreateDefaultDrone(Vector3 pos)
    {
        var go = new GameObject("drone_default");
        go.transform.position = pos;
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity  = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        // SphereCollider を先に追加することで RequireComponent の警告を抑制
        var sc = go.AddComponent<SphereCollider>();
        sc.center = new Vector3(0f, 0.05f, 0f);
        sc.radius = 0.55f;
        go.AddComponent<DroneController>();
        go.AddComponent<BatterySystem>();

        // 視認できるよう簡易メッシュを追加（プレハブ未設定時のフォールバック）
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
        UnityEngine.Object.Destroy(body.GetComponent<Collider>());

        return go;
    }
}
