using UnityEngine;

[RequireComponent(typeof(DroneController))]
public class DroneNetworkController : MonoBehaviour
{
    public string    droneId;
    public DroneSpec spec;
    public int       spawnIndex;

    DroneController    _dc;
    BatterySystem      _bat;
    AutopilotController _ap;

    float _pitch, _roll, _yaw, _vertical;
    bool  _takeoffPending, _landPending;

    void Start()
    {
        _dc  = GetComponent<DroneController>();
        _bat = GetComponent<BatterySystem>();
        _ap  = GetComponent<AutopilotController>();

        _dc.EnableExternalInput(true);

        if (spec != null)
        {
            _dc.ApplySpec(spec);
            _bat?.ApplySpec(spec.battery, spec.physics);
        }
    }

    public void ApplyCommand(float pitch, float roll, float yaw, float vertical,
                             bool takeoff, bool land)
    {
        if (_ap == null) _ap = GetComponent<AutopilotController>();
        if (_ap != null && _ap.IsActive) return;

        _pitch    = pitch;
        _roll     = roll;
        _yaw      = yaw;
        _vertical = vertical;

        if (takeoff) _takeoffPending = true;
        if (land)    _landPending    = true;
    }

    void Update()
    {
        if (_ap == null) _ap = GetComponent<AutopilotController>();
        if (_ap != null && _ap.IsActive) return;

        _dc.SetExternalInput(_pitch, _roll, _yaw, _vertical);

        if (_takeoffPending) { _dc.RequestTakeoff(); _takeoffPending = false; }
        if (_landPending)    { _dc.RequestLand();    _landPending    = false; }
    }
}
