
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyCountdown : MonoBehaviour
{
    [Header("Countdown")]
    public float countdownTime = 10f;
    public TMP_Text countdownText;

    [Header("Next Scene")]
    public string nextSceneName = "Floor01";

    private void Start()
    {
        StartCoroutine(CountdownRoutine());
    }

    private System.Collections.IEnumerator CountdownRoutine()
    {
        while (countdownTime > 0)
        {
            if (countdownText != null)
                countdownText.text = Mathf.Ceil(countdownTime).ToString();

            yield return new WaitForSeconds(1f);
            countdownTime--;
        }

        if (countdownText != null)
            countdownText.text = "0";

        SceneManager.LoadScene(nextSceneName);
    }
}
