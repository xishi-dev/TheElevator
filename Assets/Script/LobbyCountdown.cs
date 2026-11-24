
using System;
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class LobbyCountdown : MonoBehaviour
{
    [Header("Countdown")]
    public float countdownTime = 45f;
    public TMP_Text countdownText;

    [Header("Countdown Sound")]
    public int soundStartTime = 10;
    public int urgentStartTime = 5;
    [Range(0f, 1f)]
    public float soundVolume = 0.7f;

    [Header("Countdown Colors")]
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.83f, 0.15f);
    public Color urgentColor = Color.red;

    public event Action OnCountdownFinished;

    private Coroutine timerRoutine;
    private float remainingTime;
    private int lastPlayedSecond = -1;

    private AudioSource countdownAudio;
    private AudioClip tickClip;
    private AudioClip urgentClip;

    public bool IsRunning => timerRoutine != null;
    public float RemainingTime => remainingTime;

    private void Awake()
    {
        remainingTime = Mathf.Max(0f, countdownTime);

        countdownAudio = GetComponent<AudioSource>();

        countdownAudio.playOnAwake = false;
        countdownAudio.loop = false;
        countdownAudio.spatialBlend = 0f;

        tickClip = CreateBeep(850f, 0.08f);
        urgentClip = CreateBeep(1250f, 0.15f);

        UpdateText();
    }

    private AudioClip CreateBeep(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;

            samples[i] = Mathf.Sin(
                2f * Mathf.PI * frequency * t
            ) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create(
            "CountdownBeep",
            sampleCount,
            1,
            sampleRate,
            false
        );

        clip.SetData(samples, 0);
        return clip;
    }

    public void StartCountdown(float seconds = -1f)
    {
        StopCountdown();

        remainingTime = seconds > 0f
            ? seconds
            : Mathf.Max(0f, countdownTime);

        lastPlayedSecond = -1;

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

        if (countdownAudio != null)
            countdownAudio.Stop();
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
            PlayCountdownSound();

            yield return null;
        }

        remainingTime = 0f;
        UpdateText();

        countdownAudio.Stop();

        timerRoutine = null;

        OnCountdownFinished?.Invoke();
    }

    private void PlayCountdownSound()
    {
        int currentSecond = Mathf.CeilToInt(remainingTime);

        if (currentSecond == lastPlayedSecond)
            return;

        lastPlayedSecond = currentSecond;

        if (currentSecond <= 0)
            return;

        if (currentSecond <= urgentStartTime)
        {
            countdownAudio.PlayOneShot(
                urgentClip,
                soundVolume
            );
        }
        else if (currentSecond <= soundStartTime)
        {
            countdownAudio.PlayOneShot(
                tickClip,
                soundVolume
            );
        }
    }

    private void UpdateText()
    {
        if (countdownText == null)
            return;

        int seconds = Mathf.CeilToInt(remainingTime);

        // เปลี่ยนสีตามเวลาที่เหลือ
        if (seconds > 10)
        {
            countdownText.color = normalColor;
        }
        else if (seconds > 5)
        {
            countdownText.color = warningColor;
        }
        else
        {
            countdownText.color = urgentColor;
        }

        // แสดงเฉพาะตัวเลข เพราะ TIME : มี Text แยกอยู่แล้ว
        countdownText.text = seconds.ToString("00");
    }

    private void OnDisable()
    {
        StopCountdown();
    }
}
