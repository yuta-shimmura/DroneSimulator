using UnityEngine;
using UnityEngine.InputSystem;

public class BatterySystem : MonoBehaviour
{
    [Header("Battery")]
    public bool  isEnabled = false;
    public float capacity  = 1800f;

    [Range(0f, 1f)]
    public float remaining = 1f;

    // spec から設定される消費率（Inspector でも上書き可）
    public float idleConsumptionRate         = 0.3f;
    public float fullThrottleConsumptionRate = 1f;
    public float maxThrust                   = 20f;   // 天候ドレイン計算に使用

    // TCP モードでは B キーを無効化
    public bool allowKeyboardToggle = true;

    DroneController _drone;

    public bool  IsDepleted  => isEnabled && remaining <= 0f;
    public float Percentage  => remaining * 100f;

    public void ApplySpec(DroneSpec.BatterySpec batterySpec, DroneSpec.PhysicsSpec physicsSpec = null)
    {
        capacity                    = batterySpec.capacitySeconds;
        idleConsumptionRate         = batterySpec.idleConsumptionRate;
        fullThrottleConsumptionRate = batterySpec.fullThrottleConsumptionRate;
        if (physicsSpec != null) maxThrust = physicsSpec.maxThrust;
    }

    void Start()
    {
        _drone = GetComponent<DroneController>();

        bool isTCPScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.EndsWith("TCP");
        if (isTCPScene)
        {
            isEnabled = true;
            remaining = 1f;
            Debug.Log("[BatterySystem] TCP scene. Battery starts enabled (finite).");
        }
    }

    void Update()
    {
        if (!allowKeyboardToggle) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
        {
            isEnabled = !isEnabled;
            if (isEnabled) remaining = 1f;
            Debug.Log($"[BatterySystem] {(isEnabled ? "Enabled. Full charge." : "Disabled.")}");
        }
    }

    void FixedUpdate()
    {
        if (!isEnabled || _drone == null || remaining <= 0f) return;

        float drain = Mathf.Lerp(idleConsumptionRate, fullThrottleConsumptionRate, _drone.Throttle);

        // 天候負荷ドレイン（風力が大きいほどバッテリー消費増加）
        if (WeatherSystem.Instance != null && maxThrust > 0f)
        {
            float windLoad  = WeatherSystem.Instance.GetForceAt(transform.position, Time.time).magnitude;
            float weatherExtra = (windLoad / maxThrust) * 0.5f;
            drain += weatherExtra;
        }

        remaining -= drain / capacity * Time.fixedDeltaTime;
        remaining  = Mathf.Max(0f, remaining);

        if (remaining <= 0f)
            Debug.Log("[BatterySystem] Depleted. Hover force disabled.");
    }
}
