using UnityEngine;
using UnityEditor;

public class BatterySetup : Editor
{
    [MenuItem("Drone/Setup Battery")]
    static void Setup()
    {
        bool isTCPScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.EndsWith("TCP");

        if (isTCPScene)
            SetupTCP();
        else
            SetupKeyboard();
    }

    static void SetupKeyboard()
    {
        var drone = GameObject.Find("Drone");
        if (drone == null)
        {
            EditorUtility.DisplayDialog("Error", "'Drone' object not found in the scene.", "OK");
            return;
        }

        var battery = drone.GetComponent<BatterySystem>();
        if (battery == null)
            battery = drone.AddComponent<BatterySystem>();

        battery.capacity  = 1800f;
        battery.isEnabled = false;

        EditorUtility.SetDirty(drone);
        Debug.Log("[BatterySetup] Keyboard scene. Battery starts disabled (infinite). Press B to enable.");
    }

    static void SetupTCP()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Drone.prefab");
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Assets/Drone.prefab not found.\nCreate the prefab first (drag Drone from NagoyaCity scene into Assets/).", "OK");
            return;
        }

        var battery = prefab.GetComponent<BatterySystem>();
        if (battery == null)
            battery = prefab.AddComponent<BatterySystem>();

        battery.capacity  = 1800f;
        // isEnabled は BatterySystem.Start() でシーン名から自動設定されるためここでは触らない

        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[BatterySetup] TCP scene. Battery starts enabled (finite) at runtime via BatterySystem.Start().");
    }
}
