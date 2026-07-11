using UnityEngine;

public class RainFollowDrone : MonoBehaviour
{
    public Transform target;
    [Tooltip("Height offset above drone")]
    public float heightOffset = 15f;

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = new Vector3(target.position.x, target.position.y + heightOffset, target.position.z);
    }
}
