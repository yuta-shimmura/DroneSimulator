using System.Collections.Generic;
using UnityEngine;

public static class DroneSpecLoader
{
    static Dictionary<string, DroneSpec> _specs;

    public static IEnumerable<DroneSpec> All
    {
        get { EnsureLoaded(); return _specs.Values; }
    }

    public static DroneSpec Get(string modelId)
    {
        EnsureLoaded();
        return _specs.TryGetValue(modelId, out var spec) ? spec : null;
    }

    public static void Reload()
    {
        _specs = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_specs != null) return;
        _specs = new Dictionary<string, DroneSpec>();
        var assets = Resources.LoadAll<TextAsset>("DroneSpecs");
        foreach (var asset in assets)
        {
            var spec = JsonUtility.FromJson<DroneSpec>(asset.text);
            if (spec != null && !string.IsNullOrEmpty(spec.modelId))
                _specs[spec.modelId] = spec;
        }
        Debug.Log($"[DroneSpecLoader] Loaded {_specs.Count} spec(s).");
    }
}
