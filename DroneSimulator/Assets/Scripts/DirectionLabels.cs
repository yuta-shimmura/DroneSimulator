using UnityEngine;

public class DirectionLabels : MonoBehaviour
{
    public Transform drone;
    public RectTransform labelsContainer;

    CameraFollow _cameraFollow;

    void LateUpdate()
    {
        if (drone == null)
        {
            if (_cameraFollow == null) _cameraFollow = FindObjectOfType<CameraFollow>();
            if (_cameraFollow != null) drone = _cameraFollow.target;
        }
        if (drone == null || labelsContainer == null) return;
        float angle = -drone.eulerAngles.y;
        // コンテナを回転させてラベルを正しい方角位置へ移動
        labelsContainer.localEulerAngles = new Vector3(0f, 0f, angle);
        // 各ラベルを逆回転して文字が常にまっすぐ読めるようにする
        foreach (RectTransform child in labelsContainer)
            child.localEulerAngles = new Vector3(0f, 0f, -angle);
    }
}
