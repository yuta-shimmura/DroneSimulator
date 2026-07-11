using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeatherPreset", menuName = "Weather/Preset")]
public class WeatherPreset : ScriptableObject
{
    public string presetName = "New Preset";
    [TextArea(1, 2)] public string description;
    public List<WeatherLayer> layers = new List<WeatherLayer>();
}
