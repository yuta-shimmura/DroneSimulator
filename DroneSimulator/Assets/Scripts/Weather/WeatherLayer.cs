using UnityEngine;

public abstract class WeatherLayer : ScriptableObject
{
    public string displayName = "Layer";
    [TextArea(1, 2)] public string description;
    [Range(0f, 10f)] public float strength = 1f;

    public Vector3 GetForce(Vector3 position, float time) =>
        GetBaseForce(position, time) * strength;

    protected abstract Vector3 GetBaseForce(Vector3 position, float time);
}
