using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MinimapToggle : MonoBehaviour
{
    public GameObject minimapCanvas;
    public Camera minimapCamera;

    readonly float[] sizes = { 100f, 150f, 200f };
    const float borderPixels = 1f;
    const float baseOrthoSize = 150f; // 100pxサイズ時の基準値
    int state = 0;

    void Start()
    {
        ApplyState();
    }

    void Update()
    {
        if (minimapCanvas == null) return;
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            state = (state + 1) % 4;
            ApplyState();
        }
    }

    public void ApplyState()
    {
        if (minimapCanvas == null) return;

        if (state == 3)
        {
            minimapCanvas.SetActive(false);
            return;
        }

        minimapCanvas.SetActive(true);

        var panel = minimapCanvas.transform.Find("MinimapPanel");
        if (panel == null) return;

        float size = sizes[state];

        // パネルサイズ変更
        var rect = panel.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(size, size);

        // カメラの表示範囲をパネルサイズに比例して拡大（ズームではなく範囲拡張）
        if (minimapCamera != null)
            minimapCamera.orthographicSize = baseOrthoSize * (size / 100f);

        // ボーダー幅を1px固定でUV変換
        var mapView = panel.Find("MinimapView");
        if (mapView == null) return;
        var rawImage = mapView.GetComponent<RawImage>();
        if (rawImage == null || rawImage.material == null) return;
        rawImage.material.SetFloat("_BorderWidth", borderPixels / size);
    }
}
