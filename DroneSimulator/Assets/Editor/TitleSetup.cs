using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class TitleSetup
{
    const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    const string KeyboardScene  = "NagoyaCity";
    const string TCPScene       = "NagoyaCityTCP";

    [MenuItem("Drone/Setup Title Scene")]
    static void Setup()
    {
        if (!Directory.Exists("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas
        var canvasGo = new GameObject("TitleCanvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        var mgr = canvasGo.AddComponent<TitleSceneManager>();

        // 背景
        var bg   = CreatePanel(canvasGo.transform, "Background", new Color(0.04f, 0.06f, 0.12f, 1f));
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // グリッドオーバーレイ
        var gridColor = new Color(0.35f, 0.55f, 1f, 0.055f);
        for (int i = 1; i <= 9; i++)
        {
            float y    = i / 10f;
            var line   = CreatePanel(bg.transform, $"HLine{i}", gridColor);
            var rt     = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, y);
            rt.anchorMax = new Vector2(1f, y);
            rt.sizeDelta = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }
        for (int i = 1; i <= 7; i++)
        {
            float x    = i / 8f;
            var line   = CreatePanel(bg.transform, $"VLine{i}", gridColor);
            var rt     = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x, 0f);
            rt.anchorMax = new Vector2(x, 1f);
            rt.sizeDelta = new Vector2(1f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        // ドローンアイコン (斜め4アーム + プロペラ)
        var iconRoot = new GameObject("DroneIcon");
        iconRoot.transform.SetParent(bg.transform, false);
        var iconRt = iconRoot.AddComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.884f);
        iconRt.pivot          = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta      = new Vector2(110f, 110f);
        iconRt.anchoredPosition = Vector2.zero;

        var armColor  = new Color(0.50f, 0.75f, 1f, 0.80f);
        var propColor = new Color(0.50f, 0.75f, 1f, 0.45f);
        float[]   armAngles  = { 45f, 135f, 225f, 315f };
        Vector2[] propOffset = {
            new Vector2( 36f,  36f),
            new Vector2(-36f,  36f),
            new Vector2(-36f, -36f),
            new Vector2( 36f, -36f),
        };
        for (int i = 0; i < 4; i++)
        {
            var arm   = CreatePanel(iconRoot.transform, "Arm", armColor);
            var armRt = arm.GetComponent<RectTransform>();
            armRt.anchorMin = armRt.anchorMax = new Vector2(0.5f, 0.5f);
            armRt.pivot             = new Vector2(0f, 0.5f);
            armRt.sizeDelta         = new Vector2(50f, 3f);
            armRt.anchoredPosition  = Vector2.zero;
            armRt.localEulerAngles  = new Vector3(0f, 0f, armAngles[i]);

            var prop   = CreatePanel(iconRoot.transform, "Prop", propColor);
            var propRt = prop.GetComponent<RectTransform>();
            propRt.anchorMin = propRt.anchorMax = new Vector2(0.5f, 0.5f);
            propRt.pivot            = new Vector2(0.5f, 0.5f);
            propRt.sizeDelta        = new Vector2(24f, 24f);
            propRt.anchoredPosition = propOffset[i];
        }
        var body   = CreatePanel(iconRoot.transform, "Body", armColor);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot            = new Vector2(0.5f, 0.5f);
        bodyRt.sizeDelta        = new Vector2(16f, 16f);
        bodyRt.anchoredPosition = Vector2.zero;

        // タイトル
        var titleGo  = CreateTMPText(bg.transform, "TitleText", "DRONE SIMULATOR", 86, Color.white);
        SetAnchored(titleGo, new Vector2(0.5f, 0.741f), new Vector2(900f, 110f));
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.fontStyle        = FontStyles.Bold;
        titleTmp.characterSpacing = 8f;

        // セパレーター
        var sep   = CreatePanel(bg.transform, "Separator", new Color(1f, 1f, 1f, 0.12f));
        var sepRt = sep.GetComponent<RectTransform>();
        sepRt.anchorMin = sepRt.anchorMax = new Vector2(0.5f, 0.640f);
        sepRt.pivot             = new Vector2(0.5f, 0.5f);
        sepRt.sizeDelta         = new Vector2(870f, 1f);
        sepRt.anchoredPosition  = Vector2.zero;

        // Keyboard Mode ボタン (左)
        var kbBtn = CreateButton(bg.transform, "KeyboardModeButton",
            "Keyboard Mode", new Color(0.2f, 0.5f, 0.9f));
        SetAnchored(kbBtn, new Vector2(0.383f, 0.49f), new Vector2(420f, 100f));
        UnityEventTools.AddPersistentListener(
            kbBtn.GetComponent<Button>().onClick, mgr.OnKeyboardModeSelected);

        // TCP Mode ボタン (右)
        var tcpBtn = CreateButton(bg.transform, "TCPModeButton",
            "TCP Mode", new Color(0.2f, 0.7f, 0.4f));
        SetAnchored(tcpBtn, new Vector2(0.617f, 0.49f), new Vector2(420f, 100f));
        UnityEventTools.AddPersistentListener(
            tcpBtn.GetComponent<Button>().onClick, mgr.OnTCPModeSelected);

        // TCP オプションパネル
        var optPanel = CreatePanel(bg.transform, "TCPOptionsPanel", new Color(0f, 0f, 0f, 0.7f));
        SetAnchored(optPanel, new Vector2(0.5f, 0.35f), new Vector2(540f, 120f));
        optPanel.SetActive(false);
        mgr.tcpOptionsPanel = optPanel;

        var portLabel = CreateTMPText(optPanel.transform, "PortLabel", "Port:", 16, Color.white);
        SetAnchored(portLabel, new Vector2(0.3f, 0.65f), new Vector2(80f, 30f));

        var portField = CreateInputField(optPanel.transform, "PortInputField", "8080");
        SetAnchored(portField, new Vector2(0.65f, 0.65f), new Vector2(160f, 34f));
        mgr.portInputField = portField.GetComponent<TMP_InputField>();

        var startBtn = CreateButton(optPanel.transform, "StartTCPButton",
            "Start", new Color(0.9f, 0.6f, 0.1f));
        SetAnchored(startBtn, new Vector2(0.5f, 0.2f), new Vector2(160f, 34f));
        UnityEventTools.AddPersistentListener(
            startBtn.GetComponent<Button>().onClick, mgr.OnStartTCP);

        // 下部情報テキスト
        var bottomGo = CreateTMPText(bg.transform, "BottomInfo",
            "PLATEAU SDK for Unity  ×  Unity 2022.3 LTS  |  Research Build",
            18, new Color(0.35f, 0.45f, 0.60f, 0.80f));
        SetAnchored(bottomGo, new Vector2(0.5f, 0.04f), new Vector2(800f, 30f));

        // コーナーブラケット装飾
        AddCornerBrackets(bg.transform);

        // EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        EditorSceneManager.SaveScene(scene, TitleScenePath);
        AddSceneToBuildSettings(TitleScenePath);
        AddSceneToBuildSettings($"Assets/Scenes/{KeyboardScene}.unity");
        AddSceneToBuildSettings($"Assets/Scenes/{TCPScene}.unity");

        Debug.Log("[TitleSetup] TitleScene created and saved.");
        Debug.Log("[TitleSetup] TitleScene added to Build Settings.");
        Debug.Log($"[TitleSetup] Make sure {KeyboardScene}.unity and {TCPScene}.unity exist in Assets/Scenes/.");
    }

    static void AddCornerBrackets(Transform parent)
    {
        var c = new Color(0.40f, 0.65f, 1f, 0.25f);
        const float len = 28f, thick = 1.5f, margin = 10f;

        CreateBracket(parent, c, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2( margin, -margin), len, thick); // TL
        CreateBracket(parent, c, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-margin, -margin), len, thick); // TR
        CreateBracket(parent, c, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2( margin,  margin), len, thick); // BL
        CreateBracket(parent, c, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-margin,  margin), len, thick); // BR
    }

    static void CreateBracket(Transform parent, Color c, Vector2 anchor, Vector2 pivot, Vector2 pos, float len, float thick)
    {
        var h   = CreatePanel(parent, "Bracket", c);
        var hrt = h.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = anchor;
        hrt.pivot           = pivot;
        hrt.sizeDelta       = new Vector2(len, thick);
        hrt.anchoredPosition = pos;

        var v   = CreatePanel(parent, "Bracket", c);
        var vrt = v.GetComponent<RectTransform>();
        vrt.anchorMin = vrt.anchorMax = anchor;
        vrt.pivot           = pivot;
        vrt.sizeDelta       = new Vector2(thick, len);
        vrt.anchoredPosition = pos;
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == scenePath) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------
    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject CreateTMPText(Transform parent, string name, string text, float size, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static GameObject CreateButton(Transform parent, string name, string label, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>();

        var lblGo     = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        var tmp       = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 36;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt        = lblGo.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.sizeDelta  = Vector2.zero;
        return go;
    }

    static GameObject CreateInputField(Transform parent, string name, string placeholder)
    {
        var go    = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var field = go.AddComponent<TMP_InputField>();

        var textGo  = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var tmp     = textGo.AddComponent<TextMeshProUGUI>();
        tmp.color   = Color.white;
        tmp.fontSize = 18;
        var trt     = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);
        field.textComponent = tmp;

        var phGo    = new GameObject("Placeholder");
        phGo.transform.SetParent(go.transform, false);
        var phTmp   = phGo.AddComponent<TextMeshProUGUI>();
        phTmp.text     = placeholder;
        phTmp.color    = new Color(0.5f, 0.5f, 0.5f);
        phTmp.fontSize = 18;
        var phrt    = phGo.GetComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero;
        phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(8f, 0f);
        phrt.offsetMax = new Vector2(-8f, 0f);
        field.placeholder = phTmp;
        field.text = placeholder;
        return go;
    }

    static void SetAnchored(GameObject go, Vector2 anchorCenter, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorCenter;
        rt.anchorMax        = anchorCenter;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = Vector2.zero;
    }
}
