using UnityEngine;

public class PropellerEffect : MonoBehaviour
{
    public DroneController drone;
    public float maxRPM = 2400f;
    public bool clockwise = true;

    void Update()
    {
        if (drone == null) return;
        float throttle = drone.Throttle;
        if (throttle <= 0f) return;

        float degreesPerFrame = throttle * maxRPM / 60f * 360f * Time.deltaTime;
        float dir = clockwise ? 1f : -1f;
        transform.Rotate(Vector3.up, degreesPerFrame * dir, Space.World);
    }
}
