using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HelpSetup : Editor
{
    [MenuItem("Drone/Setup Help UI")]
    static void Setup()
    {
        EnsureEventSystem();

        var old = GameObject.Find("HelpCanvas");
        if (old != null) DestroyImmediate(old);

        var canvasObj = new GameObject("HelpCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // ---- ヒントラベル「H: Help」----
        var hintObj = new GameObject("HintLabel");
        hintObj.transform.SetParent(canvasObj.transform, false);
        var hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 0f);
        hintRect.sizeDelta = new Vector2(90f, 24f);

        var hintBg = hintObj.AddComponent<Image>();
        hintBg.color = new Color(0f, 0f, 0f, 0.3f);
        var hintBtn = hintObj.AddComponent<Button>();
        hintBtn.targetGraphic = hintBg;

        var hintTextObj = new GameObject("Text");
        hintTextObj.transform.SetParent(hintObj.transform, false);
        var hintTextRect = hintTextObj.AddComponent<RectTransform>();
        hintTextRect.anchorMin = Vector2.zero;
        hintTextRect.anchorMax = Vector2.one;
        hintTextRect.offsetMin = new Vector2(6f, 2f);
        hintTextRect.offsetMax = new Vector2(-6f, -2f);
        var hintTmp = hintTextObj.AddComponent<TextMeshProUGUI>();
        hintTmp.text         = "[ Controls ]";
        hintTmp.fontSize     = 13;
        hintTmp.color        = Color.white;
        hintTmp.alignment    = TextAlignmentOptions.Center;
        hintTmp.overflowMode = TextOverflowModes.Overflow;
        hintTmp.outlineWidth = 0.2f;
        hintTmp.outlineColor = new Color32(0, 0, 0, 180);

        // ---- ヘルプパネル ----
        var panelObj = new GameObject("HelpPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 0f);
        panelRect.sizeDelta = new Vector2(252f, 248f); // overwritten after text is determined

        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.7f);

        var textObj = new GameObject("HelpText");
        textObj.transform.SetParent(panelObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 12f);
        textRect.offsetMax = new Vector2(-12f, -12f);
        var helpTmp = textObj.AddComponent<TextMeshProUGUI>();

        bool isTCPScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.EndsWith("TCP");
        helpTmp.text = isTCPScene
            ? "<b><color=#E6E64A>[ Controls ]</color></b>\n<size=7> </size>\n" +
              "Tab<pos=80>: Switch Drone\n" +
              "V<pos=80>: Follow / Split / Overview\n<size=7> </size>\n" +
              "F<pos=80>: Toggle Camera (FPV)\n" +
              "M<pos=80>: Toggle Minimap\n<size=7> </size>\n" +
              "P<pos=80>: Toggle Weather Panel\n" +
              "B<pos=80>: Toggle Battery\n" +
              "L<pos=80>: Toggle Flight Log\n<size=7> </size>\n" +
              "C<pos=80>: Toggle Controls\n" +
              "Esc<pos=80>: Return to Title"
            : "<b><color=#E6E64A>[ Controls ]</color></b>\n<size=7> </size>\n" +
              "Space<pos=80>: Take off / Land\n" +
              "W / S<pos=80>: Forward / Backward\n" +
              "A / D<pos=80>: Left / Right\n" +
              "↑ / ↓<pos=80>: Ascend / Descend\n" +
              "Q / E<pos=80>: Rotate Left / Right\n<size=7> </size>\n" +
              "F<pos=80>: Toggle Camera\n" +
              "M<pos=80>: Toggle Minimap\n<size=7> </size>\n" +
              "P<pos=80>: Toggle Weather Panel\n" +
              "B<pos=80>: Toggle Battery\n" +
              "H<pos=80>: Altitude Hold\n" +
              "L<pos=80>: Toggle Flight Log\n<size=7> </size>\n" +
              "C<pos=80>: Toggle Controls\n" +
              "Esc<pos=80>: Return to Title";
        helpTmp.fontSize     = 13;
        helpTmp.color        = new Color(0.95f, 0.95f, 0.95f, 1f);
        helpTmp.alignment    = TextAlignmentOptions.TopLeft;
        helpTmp.lineSpacing  = 1f;
        helpTmp.overflowMode = TextOverflowModes.Overflow;
        helpTmp.richText     = true;
        helpTmp.outlineWidth = 0.2f;
        helpTmp.outlineColor = new Color32(0, 0, 0, 180);

        panelRect.sizeDelta = new Vector2(isTCPScene ? 260f : 252f, CalcPanelHeight(helpTmp.text));

        // Return to Title button (top-right of panel)
        var retBtnGo = new GameObject("ReturnToTitleButton", typeof(RectTransform));
        retBtnGo.transform.SetParent(panelObj.transform, false);
        var retBtnRect = retBtnGo.GetComponent<RectTransform>();
        retBtnRect.anchorMin        = new Vector2(1f, 1f);
        retBtnRect.anchorMax        = new Vector2(1f, 1f);
        retBtnRect.pivot            = new Vector2(1f, 1f);
        retBtnRect.anchoredPosition = new Vector2(-8f, -9f);
        retBtnRect.sizeDelta        = new Vector2(108f, 18f);
        var retBtnImg = retBtnGo.AddComponent<Image>();
        retBtnImg.color = new Color(0.5f, 0.13f, 0.13f, 0.9f);
        var retBtn = retBtnGo.AddComponent<Button>();
        retBtn.targetGraphic = retBtnImg;
        var retBtnTextGo = new GameObject("Text", typeof(RectTransform));
        retBtnTextGo.transform.SetParent(retBtnGo.transform, false);
        var retBtnTextRect = retBtnTextGo.GetComponent<RectTransform>();
        retBtnTextRect.anchorMin = Vector2.zero;
        retBtnTextRect.anchorMax = Vector2.one;
        retBtnTextRect.sizeDelta = Vector2.zero;
        var retBtnTmp = retBtnTextGo.AddComponent<TextMeshProUGUI>();
        retBtnTmp.text      = "← Return to Title";
        retBtnTmp.fontSize  = 10f;
        retBtnTmp.color     = Color.white;
        retBtnTmp.alignment = TextAlignmentOptions.Center;
        retBtnTmp.overflowMode = TextOverflowModes.Overflow;

        // Fullscreen overlay (behind panel, closes on click)
        var overlayObj = new GameObject("Overlay", typeof(RectTransform));
        overlayObj.transform.SetParent(canvasObj.transform, false);
        overlayObj.transform.SetSiblingIndex(panelObj.transform.GetSiblingIndex());
        var overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        var overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0f);
        overlayObj.AddComponent<Button>();

        var helpUI = canvasObj.AddComponent<HelpUI>();
        helpUI.hintLabel        = hintObj;
        helpUI.helpPanel        = panelObj;
        helpUI.hintButton       = hintBtn;
        helpUI.overlay          = overlayObj;
        helpUI.titleReturnButton = retBtn;

        canvasObj.AddComponent<TitleReturnUI>();

        Debug.Log("[HelpSetup] Done. HelpCanvas added to Hierarchy.");
    }

    static float CalcPanelHeight(string text)
    {
        int totalLines = CountOccurrences(text, "\n") + 1;
        int spacers    = CountOccurrences(text, "<size=7>");
        return 24f + (totalLines - spacers) * 15f + spacers * 8f;
    }

    static int CountOccurrences(string text, string sub)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(sub, i)) >= 0) { count++; i += sub.Length; }
        return count;
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
}
