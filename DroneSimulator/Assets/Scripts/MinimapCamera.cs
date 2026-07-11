using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    public Transform target;
    public float height = 200f;

    CameraFollow _cameraFollow;

    void LateUpdate()
    {
        if (target == null)
        {
            if (_cameraFollow == null) _cameraFollow = FindObjectOfType<CameraFollow>();
            if (_cameraFollow != null) target = _cameraFollow.target;
        }
        if (target == null) return;
        transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z);
        transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
    }
}
