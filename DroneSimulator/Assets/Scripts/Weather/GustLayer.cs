using UnityEngine;

[CreateAssetMenu(fileName = "GustLayer", menuName = "Weather/Gust")]
public class GustLayer : WeatherLayer
{
    public Vector3 baseDirection = new Vector3(1f, 0f, 0f);
    [Range(0f, 20f)] public float baseSpeed = 3f;
    [Range(0f, 20f)] public float gustStrength = 8f;
    [Range(0.1f, 5f)] public float frequency = 1f;

    protected override Vector3 GetBaseForce(Vector3 position, float time)
    {
        float t = time * frequency;
        float noise = Mathf.PerlinNoise(t, 0.5f) * 2f - 1f;
        float gx = (Mathf.PerlinNoise(t * 0.7f, 1.0f) * 2f - 1f) * gustStrength * Mathf.Max(0f, noise);
        float gz = (Mathf.PerlinNoise(t * 0.7f, 2.0f) * 2f - 1f) * gustStrength * Mathf.Max(0f, noise);
        return (baseDirection.normalized * baseSpeed + new Vector3(gx, 0f, gz)) * 0.25f;
    }
}
