
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameResultUI : MonoBehaviour
{
    [Header("Result UI")]
    public GameObject resultPanel;
    public TMP_Text scoreText;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    // แสดงผลคะแนนทั้งหมด
    public void ShowResult(int mainScore, int bonusScore, int penaltyScore)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        int totalScore = mainScore + bonusScore - penaltyScore;

        if (scoreText != null)
        {
            scoreText.text =
                "GAME COMPLETE!\n\n" +
                "MAIN SCORE: " + mainScore.ToString("N0") + "\n" +
                "BONUS SCORE: +" + bonusScore.ToString("N0") + "\n" +
                "PENALTY SCORE: -" + penaltyScore.ToString("N0") + "\n\n" +
                "TOTAL SCORE: " + totalScore.ToString("N0");
        }

        Time.timeScale = 0f;
    }

    // เล่นใหม่
    public void PlayAgain()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // กลับหน้าเมนู
    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}