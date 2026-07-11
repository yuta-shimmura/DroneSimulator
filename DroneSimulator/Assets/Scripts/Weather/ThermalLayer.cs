using UnityEngine;

[CreateAssetMenu(fileName = "ThermalLayer", menuName = "Weather/Thermal")]
public class ThermalLayer : WeatherLayer
{
    [Tooltip("Center position on XZ plane")]
    public Vector2 centerXZ = Vector2.zero;
    [Range(1f, 100f)] public float radius = 20f;
    [Range(0f, 20f)] public float liftStrength = 5f;

    protected override Vector3 GetBaseForce(Vector3 position, float time)
    {
        float dx = position.x - centerXZ.x;
        float dz = position.z - centerXZ.y;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist >= radius) return Vector3.zero;
        return Vector3.up * liftStrength * (1f - dist / radius);
    }
}
