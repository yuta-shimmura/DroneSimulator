using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HUDSetup : Editor
{
    [MenuItem("Drone/Setup HUD")]
    static void Setup()
    {
        bool isTCPScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.EndsWith("TCP");
        var drone = GameObject.Find("Drone");
        if (drone == null && !isTCPScene)
        {
            EditorUtility.DisplayDialog("Error", "'Drone' object not found in the scene.", "OK");
            return;
        }

        var oldCanvas = GameObject.Find("HUDCanvas");
        if (oldCanvas != null) DestroyImmediate(oldCanvas);

        // Canvas
        var canvasObj = new GameObject("HUDCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;
        canvasObj.AddComponent<GraphicRaycaster>();

        // パネル（左上）: 3行分の高さ
        var panel = new GameObject("HUDPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, 5f);
        panelRect.sizeDelta = new Vector2(140f, 78f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.3f);

        var altObj  = CreateHUDText(panel, "AltText", new Vector2(8f, -7f));
        var spdObj  = CreateHUDText(panel, "SpdText", new Vector2(8f, -30f));
        var batObj  = CreateHUDText(panel, "BatText", new Vector2(8f, -53f));
        Button holdBtn      = null;
        Image  holdBtnImg   = null;
        TextMeshProUGUI holdTmp = null;

        if (!isTCPScene)
        {
            // ── Altitude Hold ボタン ──
            var holdBtnObj  = new GameObject("AltHoldButton");
            holdBtnObj.transform.SetParent(panel.transform, false);
            var holdBtnRect = holdBtnObj.AddComponent<RectTransform>();
            holdBtnRect.anchorMin        = new Vector2(1f, 1f);
            holdBtnRect.anchorMax        = new Vector2(1f, 1f);
            holdBtnRect.pivot            = new Vector2(1f, 1f);
            holdBtnRect.anchoredPosition = new Vector2(0f, -3f);
            holdBtnRect.sizeDelta        = new Vector2(25f, 19f);

            holdBtnImg       = holdBtnObj.AddComponent<Image>();
            holdBtnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            holdBtn = holdBtnObj.AddComponent<Button>();
            holdBtn.targetGraphic = holdBtnImg;
            var cb = holdBtn.colors;
            cb.normalColor      = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.selectedColor    = new Color(1f, 1f, 1f, 1f);
            cb.fadeDuration     = 0.1f;
            holdBtn.colors      = cb;

            var holdTextObj  = new GameObject("AltHoldText");
            holdTextObj.transform.SetParent(holdBtnObj.transform, false);
            var holdTextRect = holdTextObj.AddComponent<RectTransform>();
            holdTextRect.anchorMin        = new Vector2(0.5f, 0.5f);
            holdTextRect.anchorMax        = new Vector2(0.5f, 0.5f);
            holdTextRect.pivot            = new Vector2(0.5f, 0.5f);
            holdTextRect.anchoredPosition = Vector2.zero;
            holdTextRect.sizeDelta        = new Vector2(80f, 52f);
            holdTextObj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

            holdTmp = holdTextObj.AddComponent<TextMeshProUGUI>();
            holdTmp.fontSize     = 52f;
            holdTmp.fontStyle    = FontStyles.Bold;
            holdTmp.color        = new Color(0.55f, 0.55f, 0.55f, 1f);
            holdTmp.outlineWidth = 0.25f;
            holdTmp.outlineColor = new Color32(0, 0, 0, 200);
            holdTmp.alignment    = TextAlignmentOptions.Center;
            holdTmp.overflowMode = TextOverflowModes.Overflow;
            holdTmp.raycastTarget = false;
            holdTmp.text         = "[H]";
        }

        EnsureEventSystem();

        var hud = canvasObj.AddComponent<DroneHUD>();
        hud.drone         = drone != null ? drone.transform : null;
        hud.altText       = altObj.GetComponent<TextMeshProUGUI>();
        hud.spdText       = spdObj.GetComponent<TextMeshProUGUI>();
        hud.batText       = batObj.GetComponent<TextMeshProUGUI>();
        hud.altHoldText   = holdTmp;
        hud.altHoldButton = holdBtn;
        hud.altHoldImage  = holdBtnImg;

        if (isTCPScene)
        {
            var idxObj  = new GameObject("DroneIndexText");
            idxObj.transform.SetParent(panel.transform, false);
            var idxRect = idxObj.AddComponent<RectTransform>();
            idxRect.anchorMin        = new Vector2(1f, 1f);
            idxRect.anchorMax        = new Vector2(1f, 1f);
            idxRect.pivot            = new Vector2(1f, 1f);
            idxRect.anchoredPosition = new Vector2(-5f, -10f);
            idxRect.sizeDelta        = new Vector2(80f, 52f);
            idxObj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

            var idxTmp = idxObj.AddComponent<TextMeshProUGUI>();
            idxTmp.fontSize      = 52f;
            idxTmp.fontStyle     = FontStyles.Bold;
            idxTmp.color         = Color.white;
            idxTmp.alignment     = TextAlignmentOptions.Center;
            idxTmp.overflowMode  = TextOverflowModes.Overflow;
            idxTmp.raycastTarget = false;
            idxTmp.text          = "#1";

            hud.droneIndexText = idxTmp;
        }

        batObj.SetActive(true);

        Debug.Log("[HUDSetup] Done. HUDCanvas added to Hierarchy.");
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    static GameObject CreateHUDText(GameObject parent, string name, Vector2 pos)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;

        // 4倍サイズ→縮小でSDF高画質化
        rect.sizeDelta = new Vector2(640f, 80f);
        obj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 52f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = name.StartsWith("Alt") ? "ALT<pos=135>:<pos=170>0.0 m"
                 : name.StartsWith("Spd") ? "SPD<pos=135>:<pos=170>0.0 m/s"
                                          : "BAT<pos=135>:<pos=170>100.0 %";
        return obj;
    }
}
