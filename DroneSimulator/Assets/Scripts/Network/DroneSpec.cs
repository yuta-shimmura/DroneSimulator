using System;

[Serializable]
public class DroneSpec
{
    public string modelId      = "default";
    public string displayName  = "Default Drone";
    public PhysicsSpec        physics        = new PhysicsSpec();
    public BatterySpec        battery        = new BatterySpec();
    public AltitudeHoldSpec   altitudeHold   = new AltitudeHoldSpec();
    public SensorSpec         sensor         = new SensorSpec();
    public CommunicationSpec  communication  = new CommunicationSpec();

    [Serializable]
    public class PhysicsSpec
    {
        public float mass                = 1f;
        public float maxThrust           = 20f;
        public float moveForce           = 15f;
        public float yawTorque           = 5f;
        public float drag                = 0.5f;
        public float angularDrag         = 3f;
        public float tiltAngle           = 20f;
        public float tiltSpeed           = 5f;
        public float maxHorizontalSpeed  = 19f;
        public float maxVerticalSpeed    = 6f;
        public float bounceDamping            = 0.5f;
        public float colliderRadius           = 0.55f;
        public float windResistanceFactor     = 1.0f;
        public float maxOperatingWindSpeedMs  = 15.0f;
    }

    [Serializable]
    public class BatterySpec
    {
        public float capacitySeconds                = 1800f;
        public float idleConsumptionRate            = 0.3f;
        public float fullThrottleConsumptionRate    = 1f;
    }

    [Serializable]
    public class AltitudeHoldSpec
    {
        public bool  defaultEnabled = true;
        public float pidKp          = 2f;
        public float pidKi          = 0.1f;
        public float pidKd          = 0.5f;
    }

    [Serializable]
    public class SensorSpec
    {
        public float detectionRangeM     = 50f;
        public float detectionFOVDeg     = 360f;
        public bool  useLineOfSight      = true;
        public float positionNoiseSigma  = 0.1f;
        public float velocityNoiseSigma  = 0.05f;
        public float altitudeNoiseSigma  = 0.05f;
    }

    [Serializable]
    public class CommunicationSpec
    {
        public int broadcastIntervalMs = 50;
    }
}
