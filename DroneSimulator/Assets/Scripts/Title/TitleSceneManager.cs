using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    public TMP_InputField portInputField;
    public GameObject     tcpOptionsPanel;

    const string KeyboardScene = "NagoyaCity";
    const string TCPScene      = "NagoyaCityTCP";

    void Start()
    {
        if (portInputField  != null) portInputField.text = "8080";
        if (tcpOptionsPanel != null) tcpOptionsPanel.SetActive(false);
        EnsureGameSettings();
    }

    public void OnKeyboardModeSelected()
    {
        GameSettings.Instance.IsTCPMode = false;
        SceneManager.LoadScene(KeyboardScene);
    }

    public void OnTCPModeSelected()
    {
        if (tcpOptionsPanel != null) tcpOptionsPanel.SetActive(true);
    }

    public void OnStartTCP()
    {
        if (portInputField != null &&
            int.TryParse(portInputField.text, out int p))
            GameSettings.Instance.TCPPort = p;

        GameSettings.Instance.IsTCPMode = true;
        SceneManager.LoadScene(TCPScene);
    }

    static void EnsureGameSettings()
    {
        if (GameSettings.Instance == null)
            new GameObject("GameSettings").AddComponent<GameSettings>();
    }
}
