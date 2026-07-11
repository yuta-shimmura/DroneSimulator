using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HelpUI : MonoBehaviour
{
    public GameObject hintLabel;
    public GameObject helpPanel;
    public Button hintButton;
    public GameObject overlay;

    const string KEYBOARD_HELP =
        "<b><color=#E6E64A>[ Controls ]</color></b>\n<size=7> </size>\n" +
        "Space<pos=80>: Take off / Land\n" +
        "W / S<pos=80>: Forward / Backward\n" +
        "A / D<pos=80>: Left / Right\n" +
        "↑ / ↓<pos=80>: Ascend / Descend\n" +
        "Q / E<pos=80>: Rotate Left / Right\n<size=7> </size>\n" +
        "F<pos=80>: Toggle Camera\n" +
        "M<pos=80>: Toggle Minimap\n<size=7> </size>\n" +
        "P<pos=80>: Toggle Weather Panel\n" +
        "B<pos=80>: Toggle Battery\n" +
        "H<pos=80>: Altitude Hold\n" +
        "L<pos=80>: Toggle Flight Log\n<size=7> </size>\n" +
        "C<pos=80>: Toggle Controls\n" +
        "Esc<pos=80>: Return to Title";

    const string TCP_HELP =
        "<b><color=#E6E64A>[ Controls ]</color></b>\n<size=7> </size>\n" +
        "Tab<pos=80>: Switch Drone\n" +
        "V<pos=80>: Follow / Split / Overview\n<size=7> </size>\n" +
        "F<pos=80>: Toggle Camera (FPV)\n" +
        "M<pos=80>: Toggle Minimap\n<size=7> </size>\n" +
        "P<pos=80>: Toggle Weather Panel\n" +
        "B<pos=80>: Toggle Battery\n" +
        "L<pos=80>: Toggle Flight Log\n<size=7> </size>\n" +
        "C<pos=80>: Toggle Controls\n" +
        "Esc<pos=80>: Return to Title";

    public Button titleReturnButton;
    CameraFollow    _cameraFollow;
    TitleReturnUI   _titleReturnUI;

    bool IsSplitMode()
    {
        if (_cameraFollow == null) _cameraFollow = FindObjectOfType<CameraFollow>();
        return _cameraFollow != null && _cameraFollow.Mode == CameraFollow.CameraMode.Split;
    }

    void Start()
    {
        _titleReturnUI = GetComponent<TitleReturnUI>() ?? FindObjectOfType<TitleReturnUI>();
        if (titleReturnButton != null && _titleReturnUI != null)
            titleReturnButton.onClick.AddListener(_titleReturnUI.ShowDialog);
        if (hintButton != null) hintButton.onClick.AddListener(ToggleHelp);
        if (overlay != null) overlay.GetComponent<Button>()?.onClick.AddListener(CloseHelp);
        hintLabel.SetActive(true);
        helpPanel.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        ApplyHelpText();
    }

    void ApplyHelpText()
    {
        var tmp = helpPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) return;

        bool isTCP = (GameSettings.Instance != null && GameSettings.Instance.IsTCPMode)
                     || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.EndsWith("TCP");
        string text = isTCP ? TCP_HELP : KEYBOARD_HELP;
        tmp.text = text;

        var panelRect = helpPanel.GetComponent<RectTransform>();
        if (panelRect != null)
            panelRect.sizeDelta = new Vector2(isTCP ? 260f : 252f, CalcPanelHeight(text));
    }

    static float CalcPanelHeight(string text)
    {
        int totalLines = CountOccurrences(text, "\n") + 1;
        int spacers    = CountOccurrences(text, "<size=7>");
        return 24f + (totalLines - spacers) * 15f + spacers * 8f;
    }

    static int CountOccurrences(string text, string sub)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(sub, i)) >= 0) { count++; i += sub.Length; }
        return count;
    }

    void Update()
    {
        if (TitleReturnUI.IsOpen) return;
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            ToggleHelp();
    }

    public void ToggleHelp()
    {
        bool show = !helpPanel.activeSelf;
        helpPanel.SetActive(show);
        hintLabel.SetActive(!show && !IsSplitMode());
        if (overlay != null) overlay.SetActive(show);
        PauseAllDrones(show);
    }

    public void ForceClose()
    {
        helpPanel.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        PauseAllDrones(false);
    }

    void CloseHelp()
    {
        ForceClose();
        hintLabel.SetActive(!IsSplitMode());
    }

    static void PauseAllDrones(bool pause)
    {
        foreach (var d in DroneController.AllDrones)
            d.PausePhysics(pause);
    }
}
