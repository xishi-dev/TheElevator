using System.Collections.Generic;
using UnityEngine;

public enum ShiftCue { Grab, Release, Land, Bump, Board, Unload, DoorOpen, DoorClose, Arrive, Travel, Tick, Win, Lose, Confirm }

public sealed class ShiftAudio : MonoBehaviour
{
    private const string Preference = "ElevatorShiftMute";
    private const string MusicPreference = "ElevatorShiftMusic";
    private const string EffectsPreference = "ElevatorShiftEffects";
    private static ShiftAudio instance;
    private AudioSource music, effects;
    private readonly Dictionary<ShiftCue, AudioClip> clips = new Dictionary<ShiftCue, AudioClip>();
    private float lastBump = -10f, lastLand = -10f;
    public static bool Muted => PlayerPrefs.GetInt(Preference, 0) != 0;
    public static float MusicLevel => PlayerPrefs.GetFloat(MusicPreference, .44f);
    public static float EffectsLevel => PlayerPrefs.GetFloat(EffectsPreference, .74f);

    public static void SetMusicLevel(float volume) => SetLevel(MusicPreference, volume);
    public static void SetEffectsLevel(float volume) => SetLevel(EffectsPreference, volume);

    private static void SetLevel(string preference, float volume)
    {
        Ready();
        PlayerPrefs.SetFloat(preference, Mathf.Clamp01(volume));
        instance.ApplyVolumes();
    }

    public static void Ready()
    {
        if (instance != null) return;
        var host = new GameObject("Elevator shift audio");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ShiftAudio>();
        instance.music = host.AddComponent<AudioSource>();
        instance.music.playOnAwake = false;
        instance.music.loop = true;
        instance.music.clip = Resources.Load<AudioClip>("ShiftAudio/Music");
        instance.effects = host.AddComponent<AudioSource>();
        instance.effects.playOnAwake = false;
        instance.ApplyVolumes();
        if (instance.music.clip != null) instance.music.Play();
    }

    public static void ToggleMute()
    {
        Ready();
        PlayerPrefs.SetInt(Preference, Muted ? 0 : 1);
        PlayerPrefs.Save();
        instance.ApplyVolumes();
        Play(ShiftCue.Confirm);
    }

    private void ApplyVolumes()
    {
        music.mute = effects.mute = Muted;
        music.volume = MusicLevel;
        effects.volume = EffectsLevel;
    }

    public static void Play(ShiftCue cue)
    {
        if (instance == null || Muted) return;
        if (cue == ShiftCue.Bump)
        {
            if (Time.unscaledTime - instance.lastBump < .25f) return;
            instance.lastBump = Time.unscaledTime;
        }
        if (cue == ShiftCue.Land)
        {
            if (Time.unscaledTime - instance.lastLand < .12f) return;
            instance.lastLand = Time.unscaledTime;
        }
        if (!instance.clips.TryGetValue(cue, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("ShiftAudio/" + cue);
            instance.clips[cue] = clip;
        }
        if (clip != null) instance.effects.PlayOneShot(clip);
    }
}
