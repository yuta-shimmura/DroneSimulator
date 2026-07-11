using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeatherUI : MonoBehaviour
{
    public WeatherSystem weatherSystem;
    public WeatherLogger weatherLogger;
    public List<WeatherPreset> presets = new List<WeatherPreset>();

    [Header("UI Refs")]
    public Button openButton;
    public GameObject overlay;
    public GameObject panel;
    public List<Toggle> presetToggles = new List<Toggle>();
    public TextMeshProUGUI forceText;
    public TextMeshProUGUI logStatusText;
    public Button logButton;
    public TextMeshProUGUI logButtonLabel;

    [Header("Slider Panel")]
    public GameObject sliderPanel;
    public RectTransform sliderContainer;

    int _currentPreset = 0;
    readonly List<GameObject> _sliderRows = new List<GameObject>();
    readonly List<Slider> _sliders = new List<Slider>();
    readonly List<TextMeshProUGUI> _sliderLabels = new List<TextMeshProUGUI>();
    int _selectedSlider = -1;
    CameraFollow    _cameraFollow;

    bool IsSplitMode()
    {
        if (_cameraFollow == null) _cameraFollow = FindObjectOfType<CameraFollow>();
        return _cameraFollow != null && _cameraFollow.Mode == CameraFollow.CameraMode.Split;
    }

    static readonly Color LabelDefault  = Color.white;
    static readonly Color LabelSelected = new Color(0.9f, 0.9f, 0.4f);

    void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(TogglePanel);
        if (logButton != null) logButton.onClick.AddListener(ToggleLogging);
        if (overlay != null) overlay.GetComponent<Button>()?.onClick.AddListener(ClosePanel);

        for (int i = 0; i < presetToggles.Count; i++)
        {
            int idx = i;
            presetToggles[i].onValueChanged.AddListener(on => { if (on) ApplyPreset(idx); });
        }

        panel.SetActive(false);
        if (sliderPanel != null) sliderPanel.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        RefreshLogUI();

        LoadStrengths();
        ApplyPreset(0);
    }

    static string StrengthKey(WeatherLayer layer) => "WeatherStrength_" + layer.displayName;

    void LoadStrengths()
    {
        if (weatherSystem == null) return;
        foreach (var layer in weatherSystem.allLayers)
            if (layer != null && PlayerPrefs.HasKey(StrengthKey(layer)))
                layer.strength = PlayerPrefs.GetFloat(StrengthKey(layer));
    }

    void SaveStrength(WeatherLayer layer)
    {
        PlayerPrefs.SetFloat(StrengthKey(layer), layer.strength);
        PlayerPrefs.Save();
    }

    public void TogglePanel()
    {
        bool next = !panel.activeSelf;
        panel.SetActive(next);
        if (overlay != null) overlay.SetActive(next);
        if (openButton != null) openButton.gameObject.SetActive(!next && !IsSplitMode());
        if (sliderPanel != null) sliderPanel.SetActive(next && HasSliders());
        if (!next) _selectedSlider = -1;
        PauseAllDrones(next);
    }

    void ClosePanel()
    {
        panel.SetActive(false);
        if (sliderPanel != null) sliderPanel.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        if (openButton != null) openButton.gameObject.SetActive(!IsSplitMode());
        _selectedSlider = -1;
        PauseAllDrones(false);
    }

    static void PauseAllDrones(bool pause)
    {
        foreach (var d in DroneController.AllDrones)
            d.PausePhysics(pause);
    }

    public void ApplyPreset(int index)
    {
        if (index < 0 || index >= presets.Count) return;
        _currentPreset = index;
        weatherSystem.ApplyPreset(presets[index]);
        for (int i = 0; i < presetToggles.Count; i++)
            presetToggles[i].SetIsOnWithoutNotify(i == index);
        RebuildSliders(presets[index]);
    }

    void RebuildSliders(WeatherPreset preset)
    {
        foreach (var row in _sliderRows)
            if (row != null) Destroy(row);
        _sliderRows.Clear();
        _sliders.Clear();
        _sliderLabels.Clear();
        _selectedSlider = -1;

        if (sliderContainer == null || sliderPanel == null) return;

        bool hasLayers = preset.layers != null && preset.layers.Count > 0;
        sliderPanel.SetActive(hasLayers && panel.activeSelf);

        if (!hasLayers) return;

        foreach (var layer in preset.layers)
        {
            if (layer == null) continue;
            var row = CreateSliderRow(layer);
            _sliderRows.Add(row);
        }
    }

    GameObject CreateSliderRow(WeatherLayer layer)
    {
        var row = new GameObject("Row_" + layer.displayName, typeof(RectTransform));
        row.transform.SetParent(sliderContainer, false);
        var vlg = row.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 36f;
        rowLE.preferredHeight = 36f;

        // Layer name label
        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(row.transform, false);
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text         = layer.displayName;
        lbl.fontSize     = 11f;
        lbl.color        = LabelDefault;
        lbl.alignment    = TextAlignmentOptions.MidlineLeft;
        lbl.enableWordWrapping = false;
        var lblLE = lblGo.AddComponent<LayoutElement>();
        lblLE.minHeight = 16f;
        lblLE.preferredHeight = 16f;
        _sliderLabels.Add(lbl);

        // Slider + value label row
        var sliderRow = new GameObject("SliderRow", typeof(RectTransform));
        sliderRow.transform.SetParent(row.transform, false);
        var hlg = sliderRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        var sliderRowLE = sliderRow.AddComponent<LayoutElement>();
        sliderRowLE.minHeight = 22f;
        sliderRowLE.preferredHeight = 22f;

        // Slider
        var sliderGo = BuildSlider(sliderRow.transform, layer.strength);
        sliderGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var slider = sliderGo.GetComponent<Slider>();
        _sliders.Add(slider);

        // Value label
        var valGo = new GameObject("Val", typeof(RectTransform));
        valGo.transform.SetParent(sliderRow.transform, false);
        var valLE = valGo.AddComponent<LayoutElement>();
        valLE.minWidth = 28f;
        valLE.preferredWidth = 28f;
        var valTmp = valGo.AddComponent<TextMeshProUGUI>();
        valTmp.text         = layer.strength.ToString("F1");
        valTmp.fontSize     = 11f;
        valTmp.color        = new Color(0.4f, 0.9f, 0.5f);
        valTmp.alignment    = TextAlignmentOptions.MidlineRight;

        var capturedLayer = layer;
        var capturedVal = valTmp;
        slider.onValueChanged.AddListener(v =>
        {
            capturedLayer.strength = v;
            capturedVal.text = v.ToString("F1");
            SaveStrength(capturedLayer);
        });

        return row;
    }

    GameObject BuildSlider(Transform parent, float value)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>();

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.25f);
        faRect.anchorMax = new Vector2(1f, 0.75f);
        faRect.offsetMin = new Vector2(7f, 0f);
        faRect.offsetMax = new Vector2(-7f, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.65f, 0.9f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRect = handleArea.GetComponent<RectTransform>();
        haRect.anchorMin = new Vector2(0f, 0f);
        haRect.anchorMax = new Vector2(1f, 1f);
        haRect.offsetMin = new Vector2(7f, 0f);
        haRect.offsetMax = new Vector2(-7f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 0f);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 10f;
        slider.value = value;

        return go;
    }

    bool HasSliders()
    {
        if (_currentPreset < 0 || _currentPreset >= presets.Count) return false;
        var p = presets[_currentPreset];
        return p.layers != null && p.layers.Count > 0;
    }

    void SelectSlider(int index)
    {
        for (int i = 0; i < _sliderLabels.Count; i++)
            if (_sliderLabels[i] != null)
                _sliderLabels[i].color = (i == index) ? LabelSelected : LabelDefault;
        _selectedSlider = index;
    }

    public void ToggleLogging()
    {
        if (weatherLogger == null) return;
        if (weatherLogger.IsLogging) weatherLogger.StopLogging();
        else weatherLogger.StartLogging();
        RefreshLogUI();
    }

    void RefreshLogUI()
    {
        bool rec = weatherLogger != null && weatherLogger.IsLogging;
        if (logStatusText != null)
            logStatusText.text = rec ? "<color=red>● REC</color>" : "○ STOP";
        if (logButtonLabel != null)
            logButtonLabel.text = rec ? "Stop Log" : "Start Log";
    }

    void Update()
    {
        HandleKeyboard();
        if (!panel.activeSelf) return;
        if (forceText != null && weatherSystem != null)
        {
            Vector3 f = weatherSystem.GetCurrentForce();
            forceText.text = $"Force: ({f.x:F1}, {f.y:F1}, {f.z:F1})";
        }
        RefreshLogUI();
    }

    void HandleKeyboard()
    {
        if (TitleReturnUI.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.P)) TogglePanel();
        if (Input.GetKeyDown(KeyCode.L)) ToggleLogging();
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyPreset(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyPreset(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyPreset(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyPreset(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ApplyPreset(4);

        if (!panel.activeSelf || _sliders.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            int next = (_selectedSlider + 1) % _sliders.Count;
            SelectSlider(next);
        }

        if (_selectedSlider < 0 || _selectedSlider >= _sliders.Count) return;

        if (Input.GetKey(KeyCode.RightArrow))
            _sliders[_selectedSlider].value =
                Mathf.Clamp(_sliders[_selectedSlider].value + 0.1f * Time.deltaTime * 10f, 0f, 10f);

        if (Input.GetKey(KeyCode.LeftArrow))
            _sliders[_selectedSlider].value =
                Mathf.Clamp(_sliders[_selectedSlider].value - 0.1f * Time.deltaTime * 10f, 0f, 10f);
    }
}
