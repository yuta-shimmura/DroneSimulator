using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DroneHUD : MonoBehaviour
{
    public Transform drone;
    public TextMeshProUGUI altText;
    public TextMeshProUGUI spdText;
    public TextMeshProUGUI batText;
    public TextMeshProUGUI altHoldText;
    public Button altHoldButton;
    public Image altHoldImage;
    public TextMeshProUGUI droneIndexText;

    Rigidbody       rb;
    BatterySystem   _battery;
    DroneController _controller;
    CameraFollow    _cameraFollow;
    Transform       _currentTarget;
    bool            _isTCP;

    void Start()
    {
        _cameraFollow = FindObjectOfType<CameraFollow>();
        SyncTarget(drone);

        _isTCP = (GameSettings.Instance != null && GameSettings.Instance.IsTCPMode)
                 || SceneManager.GetActiveScene().name.EndsWith("TCP");
        if (_isTCP && altHoldButton != null)
            altHoldButton.gameObject.SetActive(false);
        if (droneIndexText != null)
            droneIndexText.gameObject.SetActive(_isTCP);
    }

    void SyncTarget(Transform t)
    {
        if (t == _currentTarget) return;
        _currentTarget = t;
        rb          = t != null ? t.GetComponent<Rigidbody>()       : null;
        _battery    = t != null ? t.GetComponent<BatterySystem>()   : null;
        _controller = t != null ? t.GetComponent<DroneController>() : null;

        if (altHoldButton != null)
        {
            altHoldButton.onClick.RemoveAllListeners();
            if (_controller != null)
                altHoldButton.onClick.AddListener(_controller.ToggleAltitudeHold);
        }

    }

    void Update()
    {
        var target = (_cameraFollow != null && _cameraFollow.target != null)
            ? _cameraFollow.target : drone;
        SyncTarget(target);

        if (_currentTarget == null) return;

        if (droneIndexText != null && _isTCP)
        {
            var nc = _currentTarget.GetComponent<DroneNetworkController>();
            droneIndexText.text = nc != null ? $"#{nc.spawnIndex + 1}" : "#-";
        }

        float alt = _currentTarget.position.y;
        float spd = rb != null ? rb.velocity.magnitude  : 0f;

        bool holding = _controller != null && _controller.altitudeHoldEnabled;
        altText.text = $"ALT<pos=135>:<pos=170>{alt:F1} m";
        spdText.text = $"SPD<pos=135>:<pos=170>{spd:F1} m/s";
        if (altHoldText != null)
            altHoldText.color = holding ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        if (altHoldImage != null)
            altHoldImage.color = holding ? new Color(0.08f, 0.82f, 0.92f, 0.9f) : new Color(0.2f, 0.2f, 0.2f, 0.9f);

        if (batText != null)
        {
            batText.gameObject.SetActive(true);
            if (_battery != null && _battery.isEnabled)
            {
                float pct = _battery.Percentage;
                batText.text  = $"BAT<pos=135>:<pos=170>{pct:F1} %";
                batText.color = pct > 50f ? Color.white
                              : pct > 20f ? Color.yellow
                              : Color.red;
            }
            else
            {
                batText.text  = "BAT<pos=135>:<pos=170>∞";
                batText.color = Color.white;
            }
        }
    }
}
