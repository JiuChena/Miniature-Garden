using System;
using MessagePack;

/// <summary>
/// 全局音频设置数据。
/// </summary>
[Serializable]
[MessagePackObject]
public class AudioData
{
    [Key(0)]
    public bool musicEnabled = true;

    [Key(1)]
    public float musicVolume = 0.5f;

    [Key(2)]
    public bool soundEnabled = true;

    [Key(3)]
    public float soundVolume = 0.5f;
}
