using UnityEditor;
using UnityEngine;

// NagoyaCityTCP シーンで実行。TCPServer と DroneSpawner を追加する。
public class TCPSetup
{
    [MenuItem("Drone/Add TCP Manager")]
    static void Setup()
    {
        if (Object.FindObjectOfType<TCPServer>() != null)
        {
            EditorUtility.DisplayDialog("Add TCP Manager", "TCPManager already exists in this scene.", "OK");
            return;
        }

        var go = new GameObject("TCPManager");
        go.AddComponent<TCPServer>();
        go.AddComponent<DroneSpawner>();

        Debug.Log("[TCPSetup] TCPManager (TCPServer + DroneSpawner) added to scene.");
        Debug.Log("[TCPSetup] Steps:");
        Debug.Log("  1. Save this scene as NagoyaCityTCP.");
        Debug.Log("  2. Set DroneSpawner.DronePrefab (or leave null to use default).");
        Debug.Log("  3. Add TitleScene / NagoyaCity / NagoyaCityTCP to Build Settings.");
    }
}
