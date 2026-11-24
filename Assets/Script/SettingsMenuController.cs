
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    public GameObject settingsPanel;
    public Slider volumeSlider;
    public TMP_Text volumeText;

    private float currentVolume = 1f;

    void Start()
    {
        currentVolume = PlayerPrefs.GetFloat("GameVolume", 1f);

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.wholeNumbers = false;

        volumeSlider.SetValueWithoutNotify(currentVolume);

        SetVolume(currentVolume);

        settingsPanel.SetActive(false);

        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void SetVolume(float volume)
    {
        currentVolume = Mathf.Clamp01(volume);

        AudioListener.volume = currentVolume;

        if (volumeText != null)
        {
            volumeText.text =
                "Volume: " +
                Mathf.RoundToInt(currentVolume * 100) + "%";
        }

        PlayerPrefs.SetFloat("GameVolume", currentVolume);
        PlayerPrefs.Save();
    }
}
