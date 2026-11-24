
using UnityEngine;

public class CountdownSound : MonoBehaviour
{
    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip tickSound;
    public AudioClip warningSound;

    [Header("Settings")]
    public int warningStartTime = 10;
    public int urgentStartTime = 5;

    private int lastSecond = -1;

    public void UpdateCountdownSound(float timeRemaining)
    {
        int currentSecond = Mathf.CeilToInt(timeRemaining);

        // ป้องกันเสียงเล่นซ้ำในเฟรมเดียวกัน
        if (currentSecond == lastSecond)
            return;

        lastSecond = currentSecond;

        if (timeRemaining <= 0)
            return;

        // ช่วง 5 วินาทีสุดท้าย
        if (currentSecond <= urgentStartTime)
        {
            if (warningSound != null)
                audioSource.PlayOneShot(warningSound);
        }
        // ช่วง 10 วินาทีสุดท้าย
        else if (currentSecond <= warningStartTime)
        {
            if (tickSound != null)
                audioSource.PlayOneShot(tickSound);
        }
    }

    public void ResetSound()
    {
        lastSecond = -1;
    }
}
