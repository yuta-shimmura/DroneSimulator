using UnityEngine;

[CreateAssetMenu(fileName = "SteadyWindLayer", menuName = "Weather/Steady Wind")]
public class SteadyWindLayer : WeatherLayer
{
    [Tooltip("Wind direction (normalized automatically)")]
    public Vector3 direction = new Vector3(1f, 0f, 0f);
    [Range(0f, 30f)] public float speed = 5f;

    protected override Vector3 GetBaseForce(Vector3 position, float time)
    {
        return direction.normalized * speed * 0.25f;
    }
}
