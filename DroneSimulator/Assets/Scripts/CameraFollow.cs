using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CameraFollow : MonoBehaviour
{
    public enum CameraMode { Follow, Split, Overview }

    public Transform target;
    public Vector3 offset      = new Vector3(0f, 5f, -10f);
    public float smoothSpeed   = 8f;
    public float fpvOffsetY    = 0.2f;

    bool        isFPV;
    Transform   _prevTarget;
    float       _autoFindTimer;

    CameraMode  _mode;
    public CameraMode Mode => _mode;
    int         _targetIdx;
    Camera      _mainCam;
    Camera[]                   _splitCameras;
    Transform[]                _splitTargets;
    Rigidbody[]                _splitRigidbodies;
    BatterySystem[]            _splitBatteries;
    TextMeshProUGUI[]          _splitHUDTexts;
    DroneNetworkController[]   _splitNetworkControllers;
    DroneHUD      _droneHUD;
    WeatherUI     _weatherUI;
    HelpUI        _helpUI;
    MinimapToggle _minimapToggle;

    void Awake()
    {
        _mainCam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.fKey.wasPressedThisFrame) isFPV = !isFPV;
            if (IsTCPScene())
            {
                if (kb.tabKey.wasPressedThisFrame && !IsWeatherPanelOpen()) CycleTarget();
                if (kb.vKey.wasPressedThisFrame) CycleMode();
            }
        }

        switch (_mode)
        {
            case CameraMode.Follow:   UpdateFollow();   break;
            case CameraMode.Split:    UpdateSplit();    break;
            case CameraMode.Overview: UpdateOverview(); break;
        }
    }

    static bool IsTCPScene() =>
        (GameSettings.Instance != null && GameSettings.Instance.IsTCPMode)
        || SceneManager.GetActiveScene().name.EndsWith("TCP");

    bool IsWeatherPanelOpen()
    {
        if (_weatherUI == null) _weatherUI = FindObjectOfType<WeatherUI>();
        return _weatherUI != null && _weatherUI.panel != null && _weatherUI.panel.activeSelf;
    }

    void CycleTarget()
    {
        var drones = FindAllDrones();
        if (drones.Length == 0) return;
        int current = Array.FindIndex(drones, d => d.transform == target);
        _targetIdx = (current + 1) % drones.Length;
        target = drones[_targetIdx].transform;
    }

    void CycleMode()
    {
        SetMode((CameraMode)(((int)_mode + 1) % 3));
    }

    void SetMode(CameraMode next)
    {
        if (_mode == CameraMode.Split) DestroySplitCameras();
        _mode = next;

        if (_mode == CameraMode.Split)
        {
            var drones = FindAllDrones();
            if (drones.Length == 0) { _mode = CameraMode.Follow; }
            else BuildSplitCameras(drones);
        }

        _mainCam.enabled = (_mode != CameraMode.Split);

        if (_droneHUD == null) _droneHUD = FindObjectOfType<DroneHUD>();
        if (_droneHUD != null) _droneHUD.gameObject.SetActive(_mode != CameraMode.Split);

        bool isSplit = _mode == CameraMode.Split;

        if (_weatherUI == null) _weatherUI = FindObjectOfType<WeatherUI>();
        if (_weatherUI != null)
        {
            if (isSplit && _weatherUI.panel != null && _weatherUI.panel.activeSelf)
                _weatherUI.panel.SetActive(false);
            if (_weatherUI.openButton != null)
                _weatherUI.openButton.gameObject.SetActive(!isSplit);
        }

        if (_helpUI == null) _helpUI = FindObjectOfType<HelpUI>();
        if (_helpUI != null)
        {
            if (isSplit) _helpUI.ForceClose();
            _helpUI.hintLabel.SetActive(!isSplit);
        }

        if (_minimapToggle == null) _minimapToggle = FindObjectOfType<MinimapToggle>();
        if (_minimapToggle != null)
        {
            if (isSplit)
                _minimapToggle.minimapCanvas.SetActive(false);
            else
                _minimapToggle.ApplyState();
        }
    }

    // ---- Follow ----

    void UpdateFollow()
    {
        if (target == null)
        {
            _autoFindTimer -= Time.deltaTime;
            if (_autoFindTimer <= 0f)
            {
                _autoFindTimer = 1f;
                var dc = FindObjectOfType<DroneController>();
                if (dc != null) target = dc.transform;
            }
        }
        if (target == null) return;

        if (_prevTarget != target)
        {
            _prevTarget = target;
            transform.position = target.position + offset;
        }

        if (isFPV)
        {
            transform.position = target.position + Vector3.up * fpvOffsetY;
            transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position,
                target.position + offset, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }

    // ---- Split ----

    void BuildSplitCameras(DroneController[] drones)
    {
        Array.Sort(drones, (a, b) => {
            var na = a.GetComponent<DroneNetworkController>();
            var nb = b.GetComponent<DroneNetworkController>();
            if (na != null && nb != null) return na.spawnIndex.CompareTo(nb.spawnIndex);
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        });

        int n    = drones.Length;
        int cols = n <= 1 ? 1 : n <= 4 ? 2 : n <= 6 ? 3 : 4;
        int rows = Mathf.CeilToInt((float)n / cols);
        float h  = 1f / rows;

        _splitCameras             = new Camera[n];
        _splitTargets             = new Transform[n];
        _splitRigidbodies         = new Rigidbody[n];
        _splitBatteries           = new BatterySystem[n];
        _splitHUDTexts            = new TextMeshProUGUI[n];
        _splitNetworkControllers  = new DroneNetworkController[n];

        for (int i = 0; i < n; i++)
        {
            int rowIdx   = i / cols;
            int unityRow = rows - 1 - rowIdx;

            bool isLastRow = rowIdx == rows - 1;
            int  rowCols   = isLastRow ? (n - rowIdx * cols) : cols;
            int  localCol  = i - rowIdx * cols;
            float w        = 1f / rowCols;

            var go  = new GameObject($"SplitCam_{i}");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(_mainCam);
            cam.depth = i;
            cam.rect  = new Rect(localCol * w, unityRow * h, w, h);

            _splitCameras[i]            = cam;
            _splitTargets[i]            = drones[i].transform;
            _splitRigidbodies[i]        = drones[i].GetComponent<Rigidbody>();
            _splitBatteries[i]          = drones[i].GetComponent<BatterySystem>();
            _splitNetworkControllers[i] = drones[i].GetComponent<DroneNetworkController>();

            float cellPixelWidth = cam.rect.width * Screen.width;
            float fontSize       = Mathf.Clamp(cellPixelWidth * 0.034f, 13f, 27f);
            _splitHUDTexts[i]    = BuildSplitHUD(go, cam, fontSize);
        }
    }

    TextMeshProUGUI BuildSplitHUD(GameObject camGo, Camera cam, float fontSize)
    {
        var canvasGo = new GameObject("SplitHUD");
        canvasGo.transform.SetParent(camGo.transform);

        // CanvasScaler は使わない（ConstantPixelSize: 1unit=1px）
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera   = cam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder  = 10;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 帯パネル（上部固定・高さ40px）
        var stripGo   = new GameObject("Strip");
        var stripRect = stripGo.AddComponent<RectTransform>();
        stripRect.SetParent(canvasGo.transform, false);
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot     = new Vector2(0.5f, 1f);
        stripRect.offsetMin = new Vector2(0f, -40f);
        stripRect.offsetMax = new Vector2(0f, 0f);

        var bg = stripGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        var textGo   = new GameObject("Text");
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.SetParent(stripRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 2f);
        textRect.offsetMax = new Vector2(-10f, -2f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize     = fontSize;
        tmp.alignment    = TextAlignmentOptions.MidlineLeft;
        tmp.color        = Color.white;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return tmp;
    }

    void UpdateSplit()
    {
        var currentDrones = FindAllDrones();
        int currentCount  = currentDrones.Length;

        if (currentCount == 0)
        {
            DestroySplitCameras();
            _mode = CameraMode.Follow;
            _mainCam.enabled = true;
            if (_droneHUD != null) _droneHUD.gameObject.SetActive(true);
            return;
        }

        if (_splitCameras == null || currentCount != _splitCameras.Length)
        {
            DestroySplitCameras();
            BuildSplitCameras(currentDrones);
        }

        if (_splitCameras == null) return;
        for (int i = 0; i < _splitCameras.Length; i++)
        {
            if (_splitCameras[i] == null || _splitTargets[i] == null) continue;
            var t = _splitTargets[i];
            _splitCameras[i].transform.position = t.position + offset;
            _splitCameras[i].transform.LookAt(t.position + Vector3.up * 0.5f);

            if (_splitHUDTexts == null || i >= _splitHUDTexts.Length || _splitHUDTexts[i] == null) continue;

            float alt = t.position.y;
            float spd = _splitRigidbodies[i] != null ? _splitRigidbodies[i].velocity.magnitude : 0f;
            var   bat = _splitBatteries[i];
            string batStr = (bat != null && bat.isEnabled)
                ? $"BAT: {bat.Percentage:F0}%"
                : "BAT: ∞";

            var dnc = _splitNetworkControllers[i];
            int label = dnc != null ? dnc.spawnIndex + 1 : i + 1;
            _splitHUDTexts[i].text = $"#{label}  ALT: {alt:F1} m  SPD: {spd:F1} m/s  {batStr}";
        }
    }

    void DestroySplitCameras()
    {
        if (_splitCameras == null) return;
        foreach (var cam in _splitCameras)
            if (cam != null) Destroy(cam.gameObject);
        _splitCameras            = null;
        _splitTargets            = null;
        _splitRigidbodies        = null;
        _splitBatteries          = null;
        _splitHUDTexts           = null;
        _splitNetworkControllers = null;
    }

    // ---- Overview ----

    void UpdateOverview()
    {
        var drones = FindAllDrones();
        if (drones.Length == 0) return;

        Vector3 center = Vector3.zero;
        foreach (var d in drones) center += d.transform.position;
        center /= drones.Length;

        float maxDist = 0f;
        foreach (var d in drones)
            maxDist = Mathf.Max(maxDist, Vector3.Distance(center, d.transform.position));

        float altitude = center.y + Mathf.Max(maxDist * 2f, 30f);
        transform.position = new Vector3(center.x, altitude, center.z);
        transform.LookAt(center);
    }

    // ---- Helpers ----

    DroneController[] FindAllDrones()
    {
        var drones = FindObjectsOfType<DroneController>();
        Array.Sort(drones, (a, b) => {
            var na = a.GetComponent<DroneNetworkController>();
            var nb = b.GetComponent<DroneNetworkController>();
            if (na != null && nb != null) return na.spawnIndex.CompareTo(nb.spawnIndex);
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        });
        return drones;
    }

    void OnDestroy()
    {
        DestroySplitCameras();
    }
}
