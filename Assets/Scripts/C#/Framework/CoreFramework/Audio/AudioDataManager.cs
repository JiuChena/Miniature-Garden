using System;
using System.Collections.Generic;
using CoreFramework;
using UnityEngine;

/// <summary>
/// 全局音频设置管理器。负责本地持久化音乐/音效开关与音量，并向运行时音频系统广播变更。
/// </summary>
public class AudioDataManager
{
    private const string SaveFolder = "PlayerData/Setting/";
    private const string SaveFileName = "GlobalAudio";

    private static AudioDataManager instance;
    public static AudioDataManager Instance => instance ??= new AudioDataManager();

    private readonly Dictionary<AudioSource, CoreFramework.AudioType> listeners =
        new Dictionary<AudioSource, CoreFramework.AudioType>();
    private readonly List<AudioSource> invalidListeners = new List<AudioSource>(4);
    private AudioData data;

    private AudioDataManager()
    {
        LoadData();
        ApplyRuntimeSettings();
    }

    public AudioData Data
    {
        get
        {
            data ??= new AudioData();
            return data;
        }
    }

    public event Action<AudioData> SettingsChanged;

    public void AddAudioListener(AudioSource source, CoreFramework.AudioType type)
    {
        if (source == null)
            return;

        listeners[source] = type;
        ApplySoundSettingsToSource(source, type);
    }

    public void RemoveAudioListener(AudioSource source)
    {
        if (source == null)
            return;

        listeners.Remove(source);
    }

    public void PushData(AudioData nextData)
    {
        if (nextData == null)
            return;

        Data.musicEnabled = nextData.musicEnabled;
        Data.musicVolume = Mathf.Clamp01(nextData.musicVolume);
        Data.soundEnabled = nextData.soundEnabled;
        Data.soundVolume = Mathf.Clamp01(nextData.soundVolume);
        ApplyRuntimeSettings();
        SaveData();
    }

    public void SetMusicEnabled(bool enabled)
    {
        if (Data.musicEnabled == enabled)
            return;

        Data.musicEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    public void SetMusicVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.musicVolume, clampedVolume))
            return;

        Data.musicVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    public void SetSoundEnabled(bool enabled)
    {
        if (Data.soundEnabled == enabled)
            return;

        Data.soundEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    public void SetSoundVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.soundVolume, clampedVolume))
            return;

        Data.soundVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    public void SaveData()
    {
        BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, Data);
    }

    public void LoadData()
    {
        data = BinaryDataManager.Instance.Load<AudioData>(SaveFolder, SaveFileName) ?? new AudioData();
    }

    private void ApplyRuntimeSettings()
    {
        AudioManager.Instance.ApplyAudioSettings(
            Data.musicEnabled,
            Data.musicVolume,
            Data.soundEnabled,
            Data.soundVolume);
        RefreshListeners();
        SettingsChanged?.Invoke(Data);
    }

    private void RefreshListeners()
    {
        invalidListeners.Clear();
        foreach (KeyValuePair<AudioSource, CoreFramework.AudioType> pair in listeners)
        {
            AudioSource source = pair.Key;
            if (source == null)
            {
                invalidListeners.Add(source);
                continue;
            }

            ApplySoundSettingsToSource(source, pair.Value);
        }

        for (int i = 0; i < invalidListeners.Count; i++)
            listeners.Remove(invalidListeners[i]);
    }

    private void ApplySoundSettingsToSource(AudioSource source, CoreFramework.AudioType type)
    {
        if (source == null)
            return;

        switch (type)
        {
            case CoreFramework.AudioType.Music:
                source.mute = !Data.musicEnabled;
                source.volume = Data.musicVolume;
                break;

            case CoreFramework.AudioType.Sound:
            default:
                source.mute = !Data.soundEnabled;
                source.volume = Data.soundVolume;
                break;
        }
    }
}
