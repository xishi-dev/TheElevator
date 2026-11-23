
using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class LobbyCountdown : MonoBehaviour
{
    [Header("Countdown")]
    public float countdownTime = 30f;
    public TMP_Text countdownText;

    public event Action OnCountdownFinished;

    private Coroutine timerRoutine;
    private float remainingTime;

    public bool IsRunning => timerRoutine != null;

    public float RemainingTime => remainingTime;

    private void Awake()
    {
        remainingTime = Mathf.Max(0f, countdownTime);
        UpdateText();
    }

    public void StartCountdown(float seconds = -1f)
    {
        StopCountdown();

        remainingTime = seconds > 0f
            ? seconds
            : Mathf.Max(0f, countdownTime);

        UpdateText();
        timerRoutine = StartCoroutine(CountdownRoutine());
    }

    public void StopCountdown()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }
    }

    private IEnumerator CountdownRoutine()
    {
        while (remainingTime > 0f)
        {
            remainingTime = Mathf.Max(
                0f,
                remainingTime - Time.deltaTime
            );

            UpdateText();
            yield return null;
        }

        remainingTime = 0f;
        UpdateText();

        timerRoutine = null;
        OnCountdownFinished?.Invoke();
    }

    private void UpdateText()
    {
        if (countdownText != null)
        {
            countdownText.text =
                Mathf.CeilToInt(remainingTime).ToString();
        }
    }

    private void OnDisable()
    {
        StopCountdown();
    }
}
