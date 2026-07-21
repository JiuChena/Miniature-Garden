using UnityEngine;

namespace CoreFramework
{
    internal sealed class ActiveAudioInfo
    {
        public AudioSource Source { get; }
        public bool IsLoop { get; }
        public Transform FollowTarget { get; }
        public AudioType Type { get; }
        public float BaseVolume { get; }
        public ResourceLease<AudioClip> ClipLease { get; }

        public ActiveAudioInfo(AudioSource source, bool isLoop, Transform followTarget, AudioType type, float baseVolume,
            ResourceLease<AudioClip> clipLease = null)
        {
            Source = source;
            IsLoop = isLoop;
            FollowTarget = followTarget;
            Type = type;
            BaseVolume = Mathf.Clamp01(baseVolume);
            ClipLease = clipLease;
        }
    }
}
