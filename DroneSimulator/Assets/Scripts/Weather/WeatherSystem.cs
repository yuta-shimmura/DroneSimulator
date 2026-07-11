using System.Collections.Generic;
using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public Transform drone;
    public List<WeatherLayer> allLayers = new List<WeatherLayer>();

    [Header("Visuals")]
    public ParticleSystem rainParticles;
    public ParticleSystem windParticles;

    readonly HashSet<WeatherLayer> activeLayers = new HashSet<WeatherLayer>();

    public IReadOnlyCollection<WeatherLayer> AllLayers => allLayers;
    public IReadOnlyCollection<WeatherLayer> ActiveLayers => activeLayers;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        RenderSettings.fog = false;
    }

    void Update()
    {
        UpdateRainVisuals();
        UpdateWindVisuals();
        UpdateFogVisuals();
    }

    void UpdateRainVisuals()
    {
        if (rainParticles == null || !rainParticles.isPlaying) return;
        foreach (var layer in activeLayers)
        {
            if (layer is RainLayer rain)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = rain.intensity * rain.strength * 1000f;
                break;
            }
        }
    }

    void UpdateFogVisuals()
    {
        if (!RenderSettings.fog) return;
        foreach (var layer in activeLayers)
        {
            if (layer is FogLayer fog)
            {
                RenderSettings.fogDensity = fog.density * fog.strength * 0.1f;
                break;
            }
        }
    }

    void UpdateWindVisuals()
    {
        if (windParticles == null) return;

        Vector3 windVel = Vector3.zero;
        bool hasWind = false;
        foreach (var layer in activeLayers)
        {
            if (layer is SteadyWindLayer sw)
            {
                windVel += sw.direction.normalized * sw.speed * sw.strength;
                hasWind = true;
            }
            else if (layer is GustLayer gl)
            {
                windVel += gl.baseDirection.normalized * gl.baseSpeed * gl.strength;
                hasWind = true;
            }
        }

        if (hasWind && !windParticles.isPlaying) windParticles.Play();
        else if (!hasWind && windParticles.isPlaying) windParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (hasWind)
        {
            var vel = windParticles.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(windVel.x);
            vel.z = new ParticleSystem.MinMaxCurve(windVel.z);
            var emission = windParticles.emission;
            emission.rateOverTime = Mathf.Min(windVel.magnitude * 5f, 300f);
        }
    }

    // 各 DroneController が自分の FixedUpdate で呼び出す
    public Vector3 GetForceAt(Vector3 pos, float time)
    {
        Vector3 total = Vector3.zero;
        foreach (var layer in activeLayers)
            if (layer != null) total += layer.GetForce(pos, time);
        return total;
    }

    public void SetLayerActive(WeatherLayer layer, bool active)
    {
        if (active) activeLayers.Add(layer);
        else activeLayers.Remove(layer);
        UpdateVisuals();
    }

    public bool IsLayerActive(WeatherLayer layer) => activeLayers.Contains(layer);

    public void ApplyPreset(WeatherPreset preset)
    {
        activeLayers.Clear();
        foreach (var l in preset.layers)
            if (l != null) activeLayers.Add(l);
        UpdateVisuals();
    }

    public Vector3 GetCurrentForce()
    {
        if (drone == null) return Vector3.zero;
        return GetForceAt(drone.position, Time.time);
    }

    void UpdateVisuals()
    {
        bool hasRain = false;
        bool hasFog = false;
        float rainIntensity = 0.5f;
        float fogDensity = 0.02f;
        Color fogColor = new Color(0.8f, 0.8f, 0.8f);

        foreach (var layer in activeLayers)
        {
            if (layer is RainLayer rain)
            {
                hasRain = true;
                rainIntensity = rain.intensity;
            }
            else if (layer is FogLayer fog)
            {
                hasFog = true;
                fogDensity = fog.density * fog.strength * 0.1f;
                fogColor = fog.fogColor;
            }
        }

        if (rainParticles != null)
        {
            if (hasRain && !rainParticles.isPlaying)
                rainParticles.Play();
            else if (!hasRain && rainParticles.isPlaying)
                rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (hasRain)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = rainIntensity * 1000f;
            }
        }

        RenderSettings.fog = hasFog;
        if (hasFog)
        {
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fogDensity;
        }
    }
}
