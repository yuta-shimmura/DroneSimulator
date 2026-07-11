using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    [Header("Physics")]
    public float maxThrust = 20f;
    public float moveForce = 15f;
    public float yawTorque = 5f;
    public float tiltAngle = 20f;
    public float tiltSpeed = 5f;
    public float verticalSpeed = 5f;

    [Header("Landing")]
    public float landingDescentSpeed = 3f;
    public float landingStopVelocity = 0.5f;

    [Header("Collision")]
    public float bounceDamping = 0.5f;

    [Header("Altitude Hold PID")]
    public bool  altitudeHoldEnabled = true;
    public float pidKp = 2.0f;
    public float pidKi = 0.1f;
    public float pidKd = 0.5f;

    [Header("Propellers")]
    public Transform propFL;
    public Transform propFR;
    public Transform propBL;
    public Transform propBR;

    // 外部から読める状態
    public bool IsAirborne    => isAirborne;
    public bool IsLanding     => isLanding;
    public bool IsBouncing    => _isBouncing;
    public bool IsInCollision => _isInCollision;

    Rigidbody      rb;
    WeatherLogger  _weatherLogger;
    BatterySystem  _batterySystem;

    [SerializeField] bool  isAirborne;
    [SerializeField] bool  isLanding;
    bool  isAutoAscending;
    float targetAscendY;
    float airborneTime;
    float pitchInput;
    float rollInput;
    float yawInput;
    float verticalInput;
    int   _uiPauseCount;
    Vector3 _lastVelocity;
    bool  _isBouncing;
    float _bounceTimer;
    const float BounceDuration = 0.15f;
    bool  _isInCollision;
    bool  _landingRequested;
    float _targetAltitude;
    float _pidIntegral;
    float _pidPrevError;

    // 外部入力モード（TCP）
    bool  _useExternalInput;
    float _extPitch, _extRoll, _extYaw, _extVertical;
    bool  _extTakeoffPending, _extLandPending;

    // 速度上限（spec から設定）
    float _maxHorizontalSpeed     = float.MaxValue;
    float _maxVerticalSpeed       = float.MaxValue;
    float _windResistanceFactor   = 1.0f;
    float _maxOperatingWindSpeed  = 15.0f;

    const float DownwashRadiusXZ    = 5f;
    const float DownwashMaxDepth = 8f;

    static readonly System.Collections.Generic.List<DroneController> _allDrones
        = new System.Collections.Generic.List<DroneController>();

    // ------------------------------------------------------------------
    public float Throttle
    {
        get
        {
            if (!isAirborne || rb.isKinematic) return 0f;
            if (isLanding) return 0.3f;
            float horizontal = new Vector2(pitchInput, rollInput).magnitude;
            return Mathf.Clamp01(0.5f + Mathf.Abs(verticalInput) * 0.3f + horizontal * 0.2f);
        }
    }

    // ------------------------------------------------------------------
    // 外部入力 API（DroneNetworkController から呼ばれる）
    public void EnableExternalInput(bool enable) => _useExternalInput = enable;

    public void SetExternalInput(float pitch, float roll, float yaw, float vertical)
    {
        _extPitch    = pitch;
        _extRoll     = roll;
        _extYaw      = yaw;
        _extVertical = vertical;
    }

    public void RequestTakeoff() => _extTakeoffPending = true;
    public void RequestLand()    => _extLandPending    = true;

    // ------------------------------------------------------------------
    // 機体スペック適用（DroneNetworkController から呼ばれる）
    public void ApplySpec(DroneSpec spec)
    {
        var p = spec.physics;
        maxThrust          = p.maxThrust;
        moveForce          = p.moveForce;
        yawTorque          = p.yawTorque;
        tiltAngle          = p.tiltAngle;
        tiltSpeed          = p.tiltSpeed;
        verticalSpeed      = p.maxVerticalSpeed;
        bounceDamping      = p.bounceDamping;
        _maxHorizontalSpeed    = p.maxHorizontalSpeed;
        _maxVerticalSpeed      = p.maxVerticalSpeed;
        _windResistanceFactor  = p.windResistanceFactor;
        _maxOperatingWindSpeed = p.maxOperatingWindSpeedMs;

        rb.mass        = p.mass;
        rb.drag        = p.drag;
        rb.angularDrag = p.angularDrag;

        var sc = GetComponent<SphereCollider>();
        if (sc != null) sc.radius = p.colliderRadius;

        var ah = spec.altitudeHold;
        altitudeHoldEnabled = ah.defaultEnabled;
        pidKp = ah.pidKp;
        pidKi = ah.pidKi;
        pidKd = ah.pidKd;

        Debug.Log($"[DroneController] Spec applied: {spec.modelId}.");
    }

    // ------------------------------------------------------------------
    // エディタでコンポーネントを追加したとき SphereCollider を事前設定する
    void Reset()
    {
        var sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.center = new Vector3(0f, 0.05f, 0f);
        sc.radius = 0.55f;
    }

    void Awake()
    {
        _allDrones.Add(this);
        rb = GetComponent<Rigidbody>();
        rb.isKinematic            = false;
        rb.drag                   = 0.5f;
        rb.angularDrag            = 3f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.useGravity             = false;
        rb.constraints            = RigidbodyConstraints.FreezeAll;

        var sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.center = new Vector3(0f, 0.05f, 0f);
        sc.radius = 0.55f;
    }

    void OnDestroy()
    {
        _allDrones.Remove(this);
    }

    void Start()
    {
        _weatherLogger      = FindObjectOfType<WeatherLogger>();
        _batterySystem      = GetComponent<BatterySystem>();
        altitudeHoldEnabled = false;
    }

    // ------------------------------------------------------------------
    void Update()
    {
        pitchInput = rollInput = yawInput = verticalInput = 0f;

        if (rb.isKinematic || _uiPauseCount > 0) return;

        if (_extTakeoffPending) { _extTakeoffPending = false; HandleTakeoff(); }
        if (_extLandPending)    { _extLandPending    = false; HandleLand();    }

        if (_useExternalInput)
        {
            pitchInput    = _extPitch;
            rollInput     = _extRoll;
            yawInput      = _extYaw;
            verticalInput = _extVertical;
        }
        else
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.isPressed)         pitchInput    += 1f;
            if (keyboard.sKey.isPressed)         pitchInput    -= 1f;
            if (keyboard.aKey.isPressed)         rollInput     -= 1f;
            if (keyboard.dKey.isPressed)         rollInput     += 1f;
            if (keyboard.qKey.isPressed)         yawInput      -= 1f;
            if (keyboard.eKey.isPressed)         yawInput      += 1f;
            if (keyboard.upArrowKey.isPressed)   verticalInput += 1f;
            if (keyboard.downArrowKey.isPressed) verticalInput -= 1f;

            if (keyboard.hKey.wasPressedThisFrame) ToggleAltitudeHold();

            if (keyboard.spaceKey.wasPressedThisFrame) HandleSpaceKey();
        }
    }

    void HandleSpaceKey()
    {
        if (!isAirborne)          HandleTakeoff();
        else if (isLanding)       isLanding = false;
        else if (!isAutoAscending) HandleLand();
    }

    void HandleTakeoff()
    {
        if (isAirborne) return;
        isAirborne          = true;
        isLanding           = false;
        isAutoAscending     = true;
        altitudeHoldEnabled = true;
        targetAscendY       = transform.position.y + 10f;
        _targetAltitude     = targetAscendY;
        _pidIntegral        = 0f;
        _pidPrevError       = 0f;
        airborneTime        = 0f;
        rb.useGravity       = true;
        rb.constraints      = RigidbodyConstraints.None;
    }

    void HandleLand()
    {
        if (!isAirborne) return;
        isAutoAscending   = false;
        isLanding         = true;
        _landingRequested = true;
        pitchInput = rollInput = 0f;
    }

    // ------------------------------------------------------------------
    void FixedUpdate()
    {
        if (!isAirborne) return;

        _lastVelocity = rb.velocity;
        airborneTime += Time.fixedDeltaTime;

        if (_isBouncing)
        {
            _bounceTimer -= Time.fixedDeltaTime;
            if (_bounceTimer <= 0f)
            {
                _isBouncing = false;
                if (_landingRequested) isLanding = true;
            }
            return;
        }

        bool batteryDead = _batterySystem != null && _batterySystem.IsDepleted;

        // 自動上昇
        if (isAutoAscending)
        {
            float hoverF = batteryDead ? 0f : rb.mass * -Physics.gravity.y;
            if (transform.position.y < targetAscendY)
                rb.AddForce(Vector3.up * (hoverF + verticalSpeed * 2f));
            else
                isAutoAscending = false;
            return;
        }

        if (isLanding)
        {
            float landHover = batteryDead ? 0f : rb.mass * -Physics.gravity.y * 0.6f;
            rb.AddForce(Vector3.up * landHover);
            var flat = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(-flat * 5f);
            Quaternion level = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, level, 5f * Time.fixedDeltaTime));
            // 着地降下中にオートパイロットのヨートルク蓄積を解消（クルクル防止）
            rb.angularVelocity *= 0.85f;
            return;
        }

        // 通常飛行
        float hoverForce = batteryDead ? 0f : rb.mass * -Physics.gravity.y;
        rb.AddForce(Vector3.up * (hoverForce + verticalInput * verticalSpeed));

        // 高度維持PID
        if (altitudeHoldEnabled && !batteryDead)
        {
            if (Mathf.Abs(verticalInput) > 0.01f)
                _targetAltitude = transform.position.y;

            float err    = _targetAltitude - transform.position.y;
            _pidIntegral = Mathf.Clamp(_pidIntegral + err * Time.fixedDeltaTime, -5f, 5f);
            float deriv  = (err - _pidPrevError) / Time.fixedDeltaTime;
            _pidPrevError = err;
            rb.AddForce(Vector3.up * (pidKp * err + pidKi * _pidIntegral + pidKd * deriv),
                        ForceMode.Acceleration);
        }

        Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 right   = new Vector3(transform.right.x,   0f, transform.right.z).normalized;
        rb.AddForce(forward * pitchInput * moveForce);
        rb.AddForce(right   * rollInput  * moveForce);
        rb.AddTorque(Vector3.up * yawInput * yawTorque);

        Quaternion targetTilt = Quaternion.Euler(
            -pitchInput * tiltAngle,
             transform.eulerAngles.y,
            -rollInput  * tiltAngle);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetTilt, tiltSpeed * Time.fixedDeltaTime));

        // 天候力の自己適用（windResistanceFactor でスケール、ForceMode.Force で質量考慮）
        if (WeatherSystem.Instance != null)
        {
            Vector3 weatherForce = WeatherSystem.Instance.GetForceAt(transform.position, Time.time);
            rb.AddForce(weatherForce * _windResistanceFactor, ForceMode.Force);
        }

        // 他機のダウンウォッシュを受ける
        foreach (var other in _allDrones)
        {
            if (other == this) continue;
            Vector3 dw = other.GetDownwashForceAt(transform.position);
            if (dw != Vector3.zero)
                rb.AddForce(dw * _windResistanceFactor, ForceMode.Force);
        }

        // 速度上限クランプ
        ClampVelocity();
    }

    // 現在の風速スカラーを取得（AutopilotController が速度絞りに使用）
    public float GetCurrentWindSpeed()
    {
        if (WeatherSystem.Instance == null) return 0f;
        return WeatherSystem.Instance.GetForceAt(transform.position, Time.time).magnitude;
    }

    public float MaxOperatingWindSpeed => _maxOperatingWindSpeed;
    public Rigidbody Rb => rb;
    public static System.Collections.Generic.IReadOnlyList<DroneController> AllDrones => _allDrones;

    // pos に対してこの機体が発生させるダウンウォッシュ力を返す
    public Vector3 GetDownwashForceAt(Vector3 pos)
    {
        if (!isAirborne || isLanding || rb.isKinematic) return Vector3.zero;

        float dXZ = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(pos.x, pos.z));
        float dY = transform.position.y - pos.y; // 正 = pos が発生源より下

        if (dXZ >= DownwashRadiusXZ || dY <= 0f || dY >= DownwashMaxDepth)
            return Vector3.zero;

        float strength = rb.mass * 9.81f * 0.15f
            * Throttle
            * (1f - dXZ / DownwashRadiusXZ)
            * (1f - dY / DownwashMaxDepth);

        return Vector3.down * strength;
    }

    void ClampVelocity()
    {
        if (rb.isKinematic) return;
        Vector3 v = rb.velocity;
        var hVel = new Vector3(v.x, 0f, v.z);
        if (_maxHorizontalSpeed < float.MaxValue && hVel.magnitude > _maxHorizontalSpeed)
        {
            hVel = hVel.normalized * _maxHorizontalSpeed;
            v    = new Vector3(hVel.x, v.y, hVel.z);
        }
        if (_maxVerticalSpeed < float.MaxValue)
            v.y = Mathf.Clamp(v.y, -_maxVerticalSpeed, _maxVerticalSpeed);
        rb.velocity = v;
    }

    // ------------------------------------------------------------------
    void OnCollisionEnter(Collision col)
    {
        if (!isAirborne || airborneTime < 1f) return;
        _isInCollision = true;

        foreach (var contact in col.contacts)
        {
            if (contact.normal.y > 0.7f && rb.velocity.magnitude < landingStopVelocity + 2f)
            { Land(); return; }
        }
        // 着地降下中はバウンスしない（浮き上がり再着地を防止）
        if (isLanding) return;
        ApplyBounce(col);
    }

    void OnCollisionStay(Collision col)
    {
        if (!isAirborne || airborneTime < 1f) return;
        _isInCollision = true;

        foreach (var contact in col.contacts)
        {
            if (contact.normal.y > 0.7f && rb.velocity.magnitude < landingStopVelocity)
            { Land(); return; }
        }
    }

    void OnCollisionExit(Collision col)
    {
        _isInCollision = false;
    }

    void ApplyBounce(Collision col)
    {
        if (col.contacts.Length == 0) return;

        Vector3 normal     = col.contacts[0].normal;
        float   impactSpeed = _lastVelocity.magnitude;

        isLanding       = false;
        isAutoAscending = false;
        rb.velocity         = Vector3.Reflect(_lastVelocity, normal) * bounceDamping;
        rb.angularVelocity  = Vector3.zero;
        _isBouncing  = true;
        _bounceTimer = BounceDuration;

        _weatherLogger?.LogCollision(transform.position, normal, impactSpeed, gameObject.name);
        Debug.Log($"[DroneController] Collision: normal={normal:F2}, impact={impactSpeed:F2} m/s");
    }

    // ------------------------------------------------------------------
    public void PausePhysics(bool pause)
    {
        int prev = _uiPauseCount;
        _uiPauseCount = Mathf.Max(0, _uiPauseCount + (pause ? 1 : -1));
        if (prev == 0 && _uiPauseCount > 0)
        {
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
        }
        else if (prev > 0 && _uiPauseCount == 0)
        {
            rb.isKinematic = false;
        }
    }

    public void ToggleAltitudeHold()
    {
        if (!isAirborne) return;
        altitudeHoldEnabled = !altitudeHoldEnabled;
        if (altitudeHoldEnabled)
        {
            _targetAltitude = transform.position.y;
            _pidIntegral    = 0f;
            _pidPrevError   = 0f;
        }
        Debug.Log($"[DroneController] Altitude hold {(altitudeHoldEnabled ? "ON" : "OFF")}.");
    }

    void Land()
    {
        _landingRequested   = false;
        _uiPauseCount       = 0;
        altitudeHoldEnabled = false;
        rb.isKinematic      = false;
        isAirborne          = false;
        isLanding           = false;
        _isBouncing         = false;
        _isInCollision      = false;
        rb.useGravity       = false;
        rb.constraints      = RigidbodyConstraints.FreezeAll;
        rb.velocity         = Vector3.zero;
        rb.angularVelocity  = Vector3.zero;
    }
}
