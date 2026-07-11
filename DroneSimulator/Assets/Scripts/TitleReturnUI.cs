using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class TitleReturnUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    GameObject _overlay;
    Image      _yesImg;
    Image      _noImg;
    bool       _selectYes = true;

    static readonly Color ColNormal = new Color(0.2f,  0.2f,  0.2f,  1f);
    static readonly Color ColYes    = new Color(0.15f, 0.55f, 0.25f, 1f);
    static readonly Color ColNo     = new Color(0.55f, 0.15f, 0.15f, 1f);

    void Awake()
    {
        // Root-level canvas so sortingOrder=50 is respected (nested canvas inherits parent order)
        var canvasGo = new GameObject("TitleReturnCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        BuildDialog(canvasGo.transform);
    }

    void BuildDialog(Transform parent)
    {
        _overlay = new GameObject("Overlay", typeof(RectTransform));
        _overlay.transform.SetParent(parent, false);
        var ovRect = _overlay.GetComponent<RectTransform>();
        ovRect.anchorMin = Vector2.zero;
        ovRect.anchorMax = Vector2.one;
        ovRect.sizeDelta = Vector2.zero;
        var ovImg = _overlay.AddComponent<Image>();
        ovImg.color = new Color(0f, 0f, 0f, 0.55f);

        var dlg = new GameObject("Dialog", typeof(RectTransform));
        dlg.transform.SetParent(_overlay.transform, false);
        var dlgRect = dlg.GetComponent<RectTransform>();
        dlgRect.anchorMin        = new Vector2(0.5f, 0.5f);
        dlgRect.anchorMax        = new Vector2(0.5f, 0.5f);
        dlgRect.pivot            = new Vector2(0.5f, 0.5f);
        dlgRect.anchoredPosition = Vector2.zero;
        dlgRect.sizeDelta        = new Vector2(320f, 150f);
        var dlgImg = dlg.AddComponent<Image>();
        dlgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.97f);

        var qGo = new GameObject("Question", typeof(RectTransform));
        qGo.transform.SetParent(dlg.transform, false);
        var qRect = qGo.GetComponent<RectTransform>();
        qRect.anchorMin = new Vector2(0f, 0.62f);
        qRect.anchorMax = new Vector2(1f, 1f);
        qRect.offsetMin = new Vector2(16f, 0f);
        qRect.offsetMax = new Vector2(-16f, -10f);
        var qTmp = qGo.AddComponent<TextMeshProUGUI>();
        qTmp.text      = "Return to Title?";
        qTmp.fontSize  = 18f;
        qTmp.color     = Color.white;
        qTmp.fontStyle = FontStyles.Bold;
        qTmp.alignment = TextAlignmentOptions.Center;

        var row = new GameObject("BtnRow", typeof(RectTransform));
        row.transform.SetParent(dlg.transform, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0.25f);
        rowRect.anchorMax = new Vector2(1f, 0.62f);
        rowRect.offsetMin = new Vector2(40f, 0f);
        rowRect.offsetMax = new Vector2(-40f, 0f);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 24f;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment       = TextAnchor.MiddleCenter;

        _yesImg = CreateBtn(row.transform, "YES", () => Confirm(true));
        _noImg  = CreateBtn(row.transform, "NO",  () => Confirm(false));

        var hGo = new GameObject("Hint", typeof(RectTransform));
        hGo.transform.SetParent(dlg.transform, false);
        var hRect = hGo.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 0f);
        hRect.anchorMax = new Vector2(1f, 0.25f);
        hRect.offsetMin = new Vector2(16f, 4f);
        hRect.offsetMax = new Vector2(-16f, 0f);
        var hTmp = hGo.AddComponent<TextMeshProUGUI>();
        hTmp.text      = "← → to select   Enter to confirm";
        hTmp.fontSize  = 11f;
        hTmp.color     = new Color(0.65f, 0.65f, 0.65f, 1f);
        hTmp.alignment = TextAlignmentOptions.Center;

        _overlay.SetActive(false);
    }

    Image CreateBtn(Transform parent, string label, System.Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = ColNormal;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var tGo = new GameObject("Text", typeof(RectTransform));
        tGo.transform.SetParent(go.transform, false);
        var tRect = tGo.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 16f;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        return img;
    }

    public void ShowDialog()
    {
        if (IsOpen) return;
        IsOpen     = true;
        _selectYes = true;
        RefreshHighlights();
        _overlay.SetActive(true);
        PauseAll(true);
    }

    void Confirm(bool yes)
    {
        if (!IsOpen) return;
        IsOpen = false;
        _overlay.SetActive(false);
        PauseAll(false);
        if (yes) SceneManager.LoadScene("TitleScene");
    }

    void PauseAll(bool pause)
    {
        foreach (var d in DroneController.AllDrones)
            d.PausePhysics(pause);
    }

    void RefreshHighlights()
    {
        _yesImg.color = _selectYes ? ColYes   : ColNormal;
        _noImg.color  = _selectYes ? ColNormal : ColNo;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOpen) Confirm(false);
            else        ShowDialog();
            return;
        }

        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            _selectYes = !_selectYes;
            RefreshHighlights();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Confirm(_selectYes);
    }
}
