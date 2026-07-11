using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// ネットワーク受信スレッドはJSON文字列だけキューに積む。
// 解析（JsonUtility）はメインスレッド（DroneSpawner）で行う。
public class TCPServer : MonoBehaviour
{
    public static TCPServer Instance { get; private set; }

    public int port = 8080;

    TcpListener _listener;
    Thread _listenerThread;
    readonly List<DroneConnection> _connections = new List<DroneConnection>();
    readonly object _lock = new object();

    public readonly ConcurrentQueue<RawMessage> Incoming = new ConcurrentQueue<RawMessage>();

    // ------------------------------------------------------------------
    public class DroneConnection
    {
        public readonly string connectionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string droneId;
        public TcpClient client;
        public NetworkStream stream;
        public Thread thread;
        public bool isActive = true;

        readonly ConcurrentQueue<string> _sendQ = new ConcurrentQueue<string>();

        public void Enqueue(string json) => _sendQ.Enqueue(json);

        public void FlushSend()
        {
            while (_sendQ.TryDequeue(out var msg))
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(msg + "\n");
                    stream.Write(bytes, 0, bytes.Length);
                }
                catch { isActive = false; }
            }
        }
    }

    public class RawMessage
    {
        public string json;
        public DroneConnection connection;
    }

    // ------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (GameSettings.Instance != null)
            port = GameSettings.Instance.TCPPort;

        _listenerThread = new Thread(ListenLoop) { IsBackground = true };
        _listenerThread.Start();
        Debug.Log($"[TCPServer] Listening on port {port}.");
    }

    void Update()
    {
        lock (_lock)
        {
            _connections.RemoveAll(c => !c.isActive);
            foreach (var c in _connections)
                c.FlushSend();
        }
    }

    // ------------------------------------------------------------------
    void ListenLoop()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            while (true)
            {
                var client = _listener.AcceptTcpClient();
                var conn   = new DroneConnection { client = client, stream = client.GetStream() };
                lock (_lock) _connections.Add(conn);
                conn.thread = new Thread(() => ReadLoop(conn)) { IsBackground = true };
                conn.thread.Start();
                Debug.Log($"[TCPServer] Client connected: {conn.connectionId}");
            }
        }
        catch (Exception e) when (!(e is ThreadAbortException))
        {
            Debug.LogError($"[TCPServer] Listener error: {e.Message}");
        }
    }

    void ReadLoop(DroneConnection conn)
    {
        var buf = new byte[8192];
        var sb  = new StringBuilder();
        try
        {
            while (conn.isActive)
            {
                int n = conn.stream.Read(buf, 0, buf.Length);
                if (n == 0) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, n));

                string data = sb.ToString();
                int nl;
                while ((nl = data.IndexOf('\n')) >= 0)
                {
                    string line = data.Substring(0, nl).Trim();
                    data = data.Substring(nl + 1);
                    if (!string.IsNullOrEmpty(line))
                        Incoming.Enqueue(new RawMessage { json = line, connection = conn });
                }
                sb.Clear();
                sb.Append(data);

                conn.FlushSend();
            }
        }
        catch { /* 切断 */ }
        finally
        {
            conn.isActive = false;
            conn.client?.Close();
            // disconnect 通知用の特殊行
            if (conn.droneId != null)
                Incoming.Enqueue(new RawMessage { json = "{\"type\":\"disconnect\"}", connection = conn });
            Debug.Log($"[TCPServer] Client disconnected: {conn.connectionId}");
        }
    }

    // ------------------------------------------------------------------
    public void SendTo(string droneId, string json)
    {
        lock (_lock)
        {
            foreach (var c in _connections)
            {
                if (c.droneId == droneId && c.isActive)
                { c.Enqueue(json); return; }
            }
        }
    }

    void OnDestroy()
    {
        _listener?.Stop();
        _listenerThread?.Abort();
        lock (_lock)
            foreach (var c in _connections) c.client?.Close();
    }
}
