using UnityEngine;

[CreateAssetMenu(fileName = "FogLayer", menuName = "Weather/Fog")]
public class FogLayer : WeatherLayer
{
    [Range(0f, 0.1f)] public float density = 0.02f;
    public Color fogColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    protected override Vector3 GetBaseForce(Vector3 position, float time)
    {
        return Vector3.zero;
    }
}
