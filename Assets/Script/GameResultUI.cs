
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
        resultPanel.SetActive(false);
    }

    // เรียกใช้เมื่อจบชั้น 7
    public void ShowResult(int score)
    {
        resultPanel.SetActive(true);
        scoreText.text = "YOUR SCORE: " + score;

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
}