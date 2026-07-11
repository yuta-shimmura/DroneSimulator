using UnityEngine;
using UnityEngine.UI;

public class MinimapMarker : MonoBehaviour
{
    public Camera minimapCamera;
    public Transform drone;
    public RawImage markerImage;

    CameraFollow _cameraFollow;

    void LateUpdate()
    {
        if (drone == null)
        {
            if (_cameraFollow == null) _cameraFollow = FindObjectOfType<CameraFollow>();
            if (_cameraFollow != null) drone = _cameraFollow.target;
        }
        if (minimapCamera == null || drone == null || markerImage == null) return;

        Vector3 viewPos = minimapCamera.WorldToViewportPoint(drone.position);
        var rt = markerImage.transform.parent.GetComponent<RectTransform>();
        if (rt == null) return;

        float w = rt.rect.width;
        float h = rt.rect.height;
        var markerRect = markerImage.GetComponent<RectTransform>();
        markerRect.anchoredPosition = new Vector2(
            (viewPos.x - 0.5f) * w,
            (viewPos.y - 0.5f) * h
        );

        // ヘッディングアップのため常に上向き固定
        markerImage.transform.localEulerAngles = Vector3.zero;
    }
}
