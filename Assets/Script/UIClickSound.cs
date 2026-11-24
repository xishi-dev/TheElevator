
using UnityEngine;

public class UIClickSound : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource sfxSource;
    public AudioClip clickClip;

    [Range(0f, 1f)]
    public float volume = 1f;

    private void Awake()
    {
        
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        
        if (clickClip != null)
        {
            clickClip.LoadAudioData();
        }
    }

    public void PlayClickSound()
    {
        if (sfxSource == null)
        {
            Debug.LogWarning("ไม่ได้ใส่ AudioSource ใน UIClickSound");
            return;
        }

        if (clickClip == null)
        {
            Debug.LogWarning("ไม่ได้ใส่ Click Clip ใน UIClickSound");
            return;
        }

       
        sfxSource.PlayOneShot(clickClip, volume);
    }
}
