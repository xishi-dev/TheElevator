using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuSettings : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Audio UI")]
    [SerializeField] private Slider masterVolumeSlider;

    private const string VolumePrefKey = "MasterVolume";

    private void Start()
    {
        // ??????????????????????????? (??????????? = 1 ???? 100%)
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        AudioListener.volume = savedVolume;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = savedVolume;
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        // ???????????? Settings ???????????
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    // ?????????? Play
    public void PlayGame(string gameSceneName)
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // ?????????? Settings
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    // ?????????? Back / Close ?????? Settings
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    // ?????????? Exit
    public void ExitGame()
    {
        Application.Quit();
        Debug.Log("Exit Game");
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumePrefKey, value);
        PlayerPrefs.Save();
    }
}