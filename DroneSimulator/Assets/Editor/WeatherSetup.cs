using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class WeatherSetup : EditorWindow
{
    [MenuItem("Drone/Setup Weather System")]
    static void Run()
    {
        var drone = GameObject.Find("Drone");
        if (drone == null) { Debug.LogError("[WeatherSetup] 'Drone' not found."); return; }

        EnsureEventSystem();

        CreateLayerAssets(out var layers);
        CreatePresetAssets(layers, out var presets);
        var ws = CreateWeatherSystemObject(drone, layers);
        var wl = CreateWeatherLoggerObject(drone, ws);
        CreateWeatherUI(ws, wl, presets);

        Debug.Log("[WeatherSetup] Done. Check WeatherSystem and WeatherCanvas in Hierarchy.");
    }

    // ── ScriptableObject assets ──────────────────────────────────────────

    static void CreateLayerAssets(out List<WeatherLayer> layers)
    {
        const string dir = "Assets/Resources/WeatherLayers";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources", "WeatherLayers");

        layers = new List<WeatherLayer>
        {
            GetOrCreate<SteadyWindLayer>(dir, "SteadyWind", l =>
            {
                l.displayName = "Steady Wind";
                l.description = "Constant wind in a fixed direction";
                l.direction   = new Vector3(1f, 0f, 0f);
                l.speed       = 5f;
            }),
            GetOrCreate<GustLayer>(dir, "Gust", l =>
            {
                l.displayName   = "Gust";
                l.description   = "Turbulent gusts using Perlin noise";
                l.baseDirection = new Vector3(1f, 0f, 0f);
                l.baseSpeed     = 3f;
                l.gustStrength  = 8f;
                l.frequency     = 1f;
            }),
            GetOrCreate<ThermalLayer>(dir, "Thermal", l =>
            {
                l.displayName  = "Thermal";
                l.description  = "Rising warm air column";
                l.centerXZ     = Vector2.zero;
                l.radius       = 20f;
                l.liftStrength = 5f;
            }),
            GetOrCreate<DowndraftLayer>(dir, "Downdraft", l =>
            {
                l.displayName        = "Downdraft";
                l.description        = "Descending air (e.g. building wake)";
                l.centerXZ           = new Vector2(10f, 0f);
                l.radius             = 10f;
                l.downdraftStrength  = 4f;
            }),
            GetOrCreate<RainLayer>(dir, "Rain", l =>
            {
                l.displayName = "Rain";
                l.description = "Rainfall with downward force and visual particles";
                l.intensity   = 0.5f;
                l.downForce   = 1f;
            }),
            GetOrCreate<FogLayer>(dir, "Fog", l =>
            {
                l.displayName = "Fog";
                l.description = "Visibility reduction (visual only)";
                l.density     = 0.02f;
                l.fogColor    = new Color(0.8f, 0.8f, 0.8f);
            }),
        };
    }

    static void CreatePresetAssets(List<WeatherLayer> layers, out List<WeatherPreset> presets)
    {
        const string dir = "Assets/Resources/WeatherPresets";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources", "WeatherPresets");

        var thermal   = layers[2];
        var downdraft = layers[3];

        var clear = GetOrCreate<WeatherPreset>(dir, "Clear", p =>
        {
            p.presetName  = "Clear";
            p.description = "No weather effects";
        });
        clear.layers = new List<WeatherLayer> { thermal, downdraft };
        EditorUtility.SetDirty(clear);

        var lightWind = GetOrCreate<WeatherPreset>(dir, "LightWind", p =>
        {
            p.presetName  = "Light Wind";
            p.description = "Gentle steady breeze";
        });
        lightWind.layers = new List<WeatherLayer> { thermal, downdraft, layers[0] };
        EditorUtility.SetDirty(lightWind);

        var gusty = GetOrCreate<WeatherPreset>(dir, "Gusty", p =>
        {
            p.presetName  = "Gusty";
            p.description = "Turbulent gusts";
        });
        gusty.layers = new List<WeatherLayer> { thermal, downdraft, layers[0], layers[1] };
        EditorUtility.SetDirty(gusty);

        var rainy = GetOrCreate<WeatherPreset>(dir, "Rainy", p =>
        {
            p.presetName  = "Rainy";
            p.description = "Rain and fog";
        });
        rainy.layers = new List<WeatherLayer> { thermal, downdraft, layers[4], layers[5] };
        EditorUtility.SetDirty(rainy);

        var storm = GetOrCreate<WeatherPreset>(dir, "Storm", p =>
        {
            p.presetName  = "Storm";
            p.description = "Strong wind, gusts, rain, and fog";
        });
        storm.layers = new List<WeatherLayer> { thermal, downdraft, layers[0], layers[1], layers[4], layers[5] };
        EditorUtility.SetDirty(storm);

        presets = new List<WeatherPreset> { clear, lightWind, gusty, rainy, storm };
        AssetDatabase.SaveAssets();
    }

    // ── Scene objects ────────────────────────────────────────────────────

    static WeatherSystem CreateWeatherSystemObject(GameObject drone, List<WeatherLayer> layers)
    {
        var existing = Object.FindObjectOfType<WeatherSystem>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject("WeatherSystem");
        var ws = go.AddComponent<WeatherSystem>();
        ws.drone = drone.transform;
        ws.allLayers = new List<WeatherLayer>(layers);

        // Building weather effect (auto thermal/downdraft)
        var bwe = go.GetComponent<BuildingWeatherEffect>() ?? go.AddComponent<BuildingWeatherEffect>();
        bwe.drone = drone.transform;

        // Rain particles
        var rainGo = new GameObject("RainParticles");
        rainGo.transform.SetParent(go.transform);
        var ps = rainGo.AddComponent<ParticleSystem>();
        ConfigureRainParticles(ps, drone.transform);
        ws.rainParticles = ps;

        // Wind particles
        var windGo = new GameObject("WindParticles");
        windGo.transform.SetParent(go.transform);
        var wps = windGo.AddComponent<ParticleSystem>();
        ConfigureWindParticles(wps, drone.transform);
        ws.windParticles = wps;

        return ws;
    }

    static void ConfigureRainParticles(ParticleSystem ps, Transform droneRef)
    {
        // Material
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (mat != null) psr.material = mat;
        psr.renderMode    = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0.05f;
        psr.lengthScale   = 2f;

        var main = ps.main;
        main.loop        = true;
        main.playOnAwake = false;
        main.startLifetime  = 1.2f;
        main.startSpeed     = 15f;
        main.startSize      = 0.06f;
        main.startColor     = new Color(0.75f, 0.85f, 1f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles   = 3000;

        var emission = ps.emission;
        emission.rateOverTime = 500f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(40f, 0.1f, 40f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y       = new ParticleSystem.MinMaxCurve(-15f);

        // Follow drone
        ps.gameObject.AddComponent<RainFollowDrone>().target = droneRef;

        ps.Stop();
    }

    static void ConfigureWindParticles(ParticleSystem ps, Transform droneRef)
    {
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (mat != null) psr.material = mat;

        var main = ps.main;
        main.loop           = true;
        main.playOnAwake    = false;
        main.startLifetime  = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed     = 0f;
        main.startSize      = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor     = new Color(0.85f, 0.82f, 0.72f, 0.25f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles   = 500;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(20f, 5f, 20f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        ps.gameObject.AddComponent<RainFollowDrone>().target = droneRef;

        ps.Stop();
    }

    static WeatherLogger CreateWeatherLoggerObject(GameObject drone, WeatherSystem ws)
    {
        var existing = Object.FindObjectOfType<WeatherLogger>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject("WeatherLogger");
        var wl = go.AddComponent<WeatherLogger>();
        wl.drone         = drone.transform;
        wl.weatherSystem = ws;
        return wl;
    }

    // ── UI ───────────────────────────────────────────────────────────────

    static void CreateWeatherUI(WeatherSystem ws, WeatherLogger wl, List<WeatherPreset> presets)
    {
        var existing = GameObject.Find("WeatherCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        // Canvas
        var canvasGo = new GameObject("WeatherCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Open button (top-right, flush to corner)
        var btnGo = CreateButton(canvasGo.transform, "WeatherBtn", "[ Weather Panel ]",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(130f, 30f), new Color(0f, 0f, 0f, 0.3f));

        // Panel
        var panelGo = new GameObject("WeatherPanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(1f, 1f);
        panelRect.anchorMax        = new Vector2(1f, 1f);
        panelRect.pivot            = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, 0f);
        panelRect.sizeDelta        = new Vector2(200f, 0f);
        panelGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(10, 10, 10, 10);
        vlg.spacing              = 4f;
        vlg.childAlignment       = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight     = true;

        var csf = panelGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Slider panel (left of preset panel)
        var sliderPanelGo = new GameObject("SliderPanel", typeof(RectTransform));
        sliderPanelGo.transform.SetParent(canvasGo.transform, false);
        var spRect = sliderPanelGo.GetComponent<RectTransform>();
        spRect.anchorMin        = new Vector2(1f, 1f);
        spRect.anchorMax        = new Vector2(1f, 1f);
        spRect.pivot            = new Vector2(1f, 1f);
        spRect.anchoredPosition = new Vector2(-200f, 0f);
        spRect.sizeDelta        = new Vector2(300f, 0f);
        sliderPanelGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var spVLG = sliderPanelGo.AddComponent<VerticalLayoutGroup>();
        spVLG.padding              = new RectOffset(10, 10, 10, 10);
        spVLG.spacing              = 6f;
        spVLG.childAlignment       = TextAnchor.UpperLeft;
        spVLG.childForceExpandWidth  = true;
        spVLG.childForceExpandHeight = false;
        spVLG.childControlWidth      = true;
        spVLG.childControlHeight     = true;

        var spCSF = sliderPanelGo.AddComponent<ContentSizeFitter>();
        spCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        spCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var titleLbl = AddLabel(sliderPanelGo.transform, "[ Intensity ]", 13f, FontStyles.Bold,
            new Color(0.9f, 0.9f, 0.4f));
        titleLbl.text = "[ Intensity ]     <size=11><color=white>Tab: Select   ← →: Adjust</color></size>";
        titleLbl.enableWordWrapping = false;
        titleLbl.overflowMode = TextOverflowModes.Overflow;
        AddDivider(sliderPanelGo.transform);

        // Container that WeatherUI populates at runtime
        var sliderContainerGo = new GameObject("SliderContainer", typeof(RectTransform));
        sliderContainerGo.transform.SetParent(sliderPanelGo.transform, false);
        var scVLG = sliderContainerGo.AddComponent<VerticalLayoutGroup>();
        scVLG.spacing              = 4f;
        scVLG.childForceExpandWidth  = true;
        scVLG.childForceExpandHeight = false;
        scVLG.childControlWidth      = true;
        scVLG.childControlHeight     = true;
        sliderContainerGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // WeatherUI component
        var ui = canvasGo.AddComponent<WeatherUI>();
        ui.weatherSystem   = ws;
        ui.weatherLogger   = wl;
        ui.presets         = new List<WeatherPreset>(presets);
        ui.panel           = panelGo;
        ui.sliderPanel     = sliderPanelGo;
        ui.sliderContainer = sliderContainerGo.GetComponent<RectTransform>();

        // Overlay (fullscreen, behind panel)
        var overlayGo = new GameObject("Overlay", typeof(RectTransform));
        overlayGo.transform.SetParent(canvasGo.transform, false);
        overlayGo.transform.SetSiblingIndex(panelGo.transform.GetSiblingIndex());
        var or = overlayGo.GetComponent<RectTransform>();
        or.anchorMin = Vector2.zero; or.anchorMax = Vector2.one; or.sizeDelta = Vector2.zero;
        overlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        overlayGo.AddComponent<Button>();
        ui.overlay = overlayGo;

        // ── Title ──
        AddLabel(panelGo.transform, "[ Weather Panel ]", 13f, FontStyles.Bold, new Color(0.9f, 0.9f, 0.4f));
        AddDivider(panelGo.transform);

        // ── Preset list (radio checkboxes) ──
        var tg = panelGo.AddComponent<ToggleGroup>();
        tg.allowSwitchOff = false;

        string[] numbers = { "1", "2", "3", "4", "5" };
        for (int i = 0; i < presets.Count; i++)
        {
            var row = new GameObject("Preset_" + i, typeof(RectTransform));
            row.transform.SetParent(panelGo.transform, false);
            var rowHLG = row.AddComponent<HorizontalLayoutGroup>();
            rowHLG.spacing              = 8f;
            rowHLG.childAlignment       = TextAnchor.MiddleLeft;
            rowHLG.childControlWidth      = true;
            rowHLG.childControlHeight     = true;
            rowHLG.childForceExpandWidth  = false;
            rowHLG.childForceExpandHeight = true;

            // Checkbox
            var cbGo = new GameObject("CB", typeof(RectTransform));
            cbGo.transform.SetParent(row.transform, false);
            var cbLE = cbGo.AddComponent<LayoutElement>();
            cbLE.minWidth = 18f; cbLE.preferredWidth = 18f;
            cbLE.minHeight = 18f; cbLE.preferredHeight = 18f;
            var cbImg = cbGo.AddComponent<Image>();
            cbImg.color = new Color(0.3f, 0.3f, 0.3f);
            var toggle = cbGo.AddComponent<Toggle>();
            toggle.group = tg;

            var checkGo = new GameObject("Check", typeof(RectTransform));
            checkGo.transform.SetParent(cbGo.transform, false);
            var cr = checkGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0.5f);
            cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot     = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(14f, 14f);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.9f, 0.4f);
            toggle.graphic       = checkImg;
            toggle.targetGraphic = cbImg;
            toggle.isOn          = (i == 0);

            // Label "1. Clear" etc.
            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lblLE = lblGo.AddComponent<LayoutElement>();
            lblLE.flexibleWidth = 1f;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text               = $"{numbers[i]}. {presets[i].presetName}";
            lbl.fontSize           = 13f;
            lbl.color              = Color.white;
            lbl.alignment          = TextAlignmentOptions.MidlineLeft;
            lbl.enableWordWrapping = false;
            lbl.overflowMode       = TextOverflowModes.Overflow;
            lbl.outlineWidth       = 0.2f;
            lbl.outlineColor       = new Color32(0, 0, 0, 180);

            ui.presetToggles.Add(toggle);
        }

        AddDivider(panelGo.transform);

        // ── Force display ──
        var forceLabel = AddLabel(panelGo.transform, "Force: (0.0, 0.0, 0.0)", 11f,
            FontStyles.Normal, new Color(0.5f, 0.9f, 0.5f));
        ui.forceText = forceLabel;

        AddDivider(panelGo.transform);

        // ── Log row ──
        AddLabel(panelGo.transform, "Flight Log (CSV)", 11f, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f));

        var logRow = new GameObject("LogRow", typeof(RectTransform));
        logRow.transform.SetParent(panelGo.transform, false);
        var logLE = logRow.AddComponent<LayoutElement>();
        logLE.minHeight = 26f; logLE.preferredHeight = 26f;
        var logHLG = logRow.AddComponent<HorizontalLayoutGroup>();
        logHLG.spacing              = 8f;
        logHLG.childForceExpandWidth  = false;
        logHLG.childForceExpandHeight = true;
        logHLG.childControlHeight     = true;

        var logBtn = CreateSmallButton(logRow.transform, "Start Log", new Color(0.2f, 0.4f, 0.2f));
        logBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 0f);
        ui.logButton      = logBtn;
        ui.logButtonLabel = logBtn.GetComponentInChildren<TextMeshProUGUI>();

        var statusGo = new GameObject("LogStatus", typeof(RectTransform));
        statusGo.transform.SetParent(logRow.transform, false);
        statusGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
        statusTmp.text         = "○ STOP";
        statusTmp.fontSize     = 12f;
        statusTmp.color        = Color.white;
        statusTmp.alignment    = TextAlignmentOptions.MidlineLeft;
        statusTmp.outlineWidth = 0.2f;
        statusTmp.outlineColor = new Color32(0, 0, 0, 180);
        ui.logStatusText = statusTmp;

        ui.openButton = btnGo.GetComponent<Button>();
    }

    // ── UI helpers ───────────────────────────────────────────────────────

    static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin       = anchorMin;
        rect.anchorMax       = anchorMax;
        rect.pivot           = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta       = size;

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        go.AddComponent<Button>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin  = Vector2.zero;
        txtRect.anchorMax  = Vector2.one;
        txtRect.sizeDelta  = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text         = label;
        tmp.fontSize     = 13f;
        tmp.color        = Color.white;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);

        return go;
    }

    static Button CreateSmallButton(Transform parent, string label, Color bgColor)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 26f);

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var tr = txtGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text         = label;
        tmp.fontSize     = 11f;
        tmp.color        = Color.white;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);

        return btn;
    }

    static TextMeshProUGUI AddLabel(Transform parent, string text, float size,
        FontStyles style, Color color)
    {
        var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(text.Length, 10)),
            typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text         = text;
        tmp.fontSize     = size;
        tmp.fontStyle    = style;
        tmp.color        = color;
        tmp.alignment    = TextAlignmentOptions.MidlineLeft;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);
        return tmp;
    }

    static void AddDivider(Transform parent)
    {
        var go = new GameObject("Divider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 1f;
        le.preferredHeight = 1f;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    }

    // ── Utility ──────────────────────────────────────────────────────────

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    // ── Asset helper ─────────────────────────────────────────────────────

    static T GetOrCreate<T>(string dir, string fileName, System.Action<T> init)
        where T : ScriptableObject
    {
        string path = $"{dir}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
