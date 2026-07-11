using UnityEngine;

[CreateAssetMenu(fileName = "RainLayer", menuName = "Weather/Rain")]
public class RainLayer : WeatherLayer
{
    [Range(0f, 1f)] public float intensity = 0.5f;
    [Tooltip("Additional downward force from rain impact")]
    [Range(0f, 5f)] public float downForce = 1f;

    protected override Vector3 GetBaseForce(Vector3 position, float time)
    {
        return Vector3.down * downForce * intensity;
    }
}
