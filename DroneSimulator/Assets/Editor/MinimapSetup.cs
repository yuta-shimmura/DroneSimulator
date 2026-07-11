using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class MinimapSetup : Editor
{
    [MenuItem("Drone/Setup Minimap")]
    static void Setup()
    {
        bool isTCPScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.EndsWith("TCP");
        var drone = GameObject.Find("Drone");
        if (drone == null && !isTCPScene)
        {
            EditorUtility.DisplayDialog("Error", "'Drone' object not found in the scene.", "OK");
            return;
        }

        // 既存のミニマップを削除
        var oldCanvas = GameObject.Find("MinimapCanvas");
        if (oldCanvas != null) DestroyImmediate(oldCanvas);
        var oldCam = GameObject.Find("MinimapCamera");
        if (oldCam != null) DestroyImmediate(oldCam);

        // RenderTexture作成（既存があれば再利用）
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/MinimapRenderTexture.renderTexture");
        if (rt == null)
        {
            rt = new RenderTexture(256, 256, 16);
            rt.name = "MinimapRenderTexture";
            AssetDatabase.CreateAsset(rt, "Assets/MinimapRenderTexture.renderTexture");
        }

        // ミニマップカメラ作成
        var camObj = new GameObject("MinimapCamera");
        var cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 150f;
        cam.transform.position = new Vector3(0f, 200f, 0f);
        cam.transform.eulerAngles = new Vector3(90f, 0f, 0f);
        cam.targetTexture = rt;
        cam.cullingMask = ~0;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.15f, 0.1f);
        var minimapCamScript = camObj.AddComponent<MinimapCamera>();
        minimapCamScript.target = drone != null ? drone.transform : null;

        // Canvas作成
        var canvasObj = new GameObject("MinimapCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 丸型マスク用パネル（円形スプライトでマスク）
        var panelObj = new GameObject("MinimapPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-15f, 15f);
        panelRect.sizeDelta = new Vector2(100f, 100f);

        // ミニマップ映像（カスタムシェーダーで滑らかな円形）
        var mapObj = new GameObject("MinimapView");
        mapObj.transform.SetParent(panelObj.transform, false);
        var mapRect = mapObj.AddComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = Vector2.zero;
        mapRect.offsetMax = Vector2.zero;
        var rawImage = mapObj.AddComponent<RawImage>();
        rawImage.texture = rt;

        // カスタムシェーダーマテリアルを適用
        var shader = Shader.Find("Custom/UI/MinimapCircle");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BorderColor", new Color(0f, 0f, 0f, 1f));
            mat.SetFloat("_BorderWidth", 1f / 100f); // 初期サイズ100pxで1px固定
            rawImage.material = mat;
        }
        else
        {
            Debug.LogWarning("[MinimapSetup] MinimapCircle shader not found. Check Assets/Shaders/MinimapCircle.shader.");
        }

        // ドローンマーカー
        var markerObj = new GameObject("DroneMarker");
        markerObj.transform.SetParent(panelObj.transform, false);
        var markerRect = markerObj.AddComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(10f, 10f);
        markerRect.anchoredPosition = Vector2.zero;
        var markerImg = markerObj.AddComponent<RawImage>();
        markerImg.color = Color.yellow;

        // MinimapMarkerスクリプト
        var marker = canvasObj.AddComponent<MinimapMarker>();
        marker.minimapCamera = cam;
        marker.drone = drone != null ? drone.transform : null;
        marker.markerImage = markerImg;

        // 方角ラベル用コンテナ（マップと逆回転して常に正しい方角を指す）
        var labelsContainerObj = new GameObject("LabelsContainer");
        labelsContainerObj.transform.SetParent(panelObj.transform, false);
        var labelsContainerRect = labelsContainerObj.AddComponent<RectTransform>();
        labelsContainerRect.anchorMin = Vector2.zero;
        labelsContainerRect.anchorMax = Vector2.one;
        labelsContainerRect.offsetMin = Vector2.zero;
        labelsContainerRect.offsetMax = Vector2.zero;

        AddDirectionLabel(labelsContainerObj, "N", new Vector2(0.5f, 0.88f));
        AddDirectionLabel(labelsContainerObj, "S", new Vector2(0.5f, 0.12f));
        AddDirectionLabel(labelsContainerObj, "E", new Vector2(0.88f, 0.5f));
        AddDirectionLabel(labelsContainerObj, "W", new Vector2(0.12f, 0.5f));

        // DirectionLabels スクリプト
        var dirLabels = canvasObj.AddComponent<DirectionLabels>();
        dirLabels.drone = drone != null ? drone.transform : null;
        dirLabels.labelsContainer = labelsContainerRect;

        // UIManagerのMinimapToggle参照を自動更新（Setup再実行時に参照がnullになるのを防ぐ）
        var toggle = Object.FindObjectOfType<MinimapToggle>();
        if (toggle != null)
        {
            toggle.minimapCanvas = canvasObj;
            toggle.minimapCamera = cam;
            EditorUtility.SetDirty(toggle);
        }

        AssetDatabase.SaveAssets();
        string mode = isTCPScene ? "TCP scene: drone target auto-followed at runtime." : "Keyboard scene.";
        Debug.Log($"[MinimapSetup] Done. {mode}");
    }

    static void AddDirectionLabel(GameObject parent, string text, Vector2 anchor)
    {
        var obj = new GameObject("Dir_" + text);
        obj.transform.SetParent(parent.transform, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        // 3倍サイズでレンダリングしてlocalScaleで縮小→SDF精度が上がり高画質になる
        rect.sizeDelta = new Vector2(60f, 48f);
        obj.transform.localScale = new Vector3(1f / 3f, 1f / 3f, 1f);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = text == "N" ? 42f : 30f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineWidth = text == "N" ? 0.45f : 0.3f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }
}
