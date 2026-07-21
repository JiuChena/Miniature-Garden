using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace CoreFramework
{
    /// <summary>
    /// 音频管理器，统一播放世界音频并复用 AudioSource。
    /// 旧接口继续保留，新系统优先使用带句柄的 Play/Stop。
    /// </summary>
    public class AudioManager
    {
        private static readonly AudioManager instance = new AudioManager();
        public static AudioManager Instance => instance;

        private readonly Queue<AudioSource> idleSources = new Queue<AudioSource>();
        private readonly Dictionary<int, ActiveAudioInfo> activeAudios = new Dictionary<int, ActiveAudioInfo>();
        private readonly List<int> completedAudioHandles = new List<int>();

        private AudioManagerHost host;
        private Transform root;
        private int nextHandle = 1;
        private bool musicEnabled = true;
        private float musicVolume = 0.5f;
        private bool soundEnabled = true;
        private float soundVolume = 0.5f;

        private AudioManager() { }

        /// <summary>
        /// 播放一个世界空间音频，并返回可用于停止播放的句柄。
        /// </summary>
        public int Play(AudioClip clip, AudioType type, Vector3 position, bool loop = false, float volume = 1f, Transform followTarget = null)
        {
            if (clip == null)
                return -1;

            EnsureHost();

            AudioSource source = GetPooledSource();
            source.transform.position = position;
            source.loop = loop;
            ApplyTypeSettings(source, type, Mathf.Clamp01(volume));
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.gameObject.SetActive(true);
            source.Play();

            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(source, loop, followTarget, type, Mathf.Clamp01(volume));
            return handle;
        }

        /// <summary>
        /// 按句柄停止一个正在播放的音频。
        /// </summary>
        public void Stop(int audioHandle)
        {
            if (activeAudios.TryGetValue(audioHandle, out ActiveAudioInfo info))
                ReleaseHandle(audioHandle, info);
        }

        /// <summary>
        /// 停止所有使用指定 AudioClip 的播放实例。
        /// </summary>
        public void Stop(AudioClip clip)
        {
            if (clip == null)
                return;

            completedAudioHandles.Clear();
            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                if (pair.Value.Source != null && pair.Value.Source.clip == clip)
                    completedAudioHandles.Add(pair.Key);
            }

            for (int i = 0; i < completedAudioHandles.Count; i++)
                Stop(completedAudioHandles[i]);
        }

        /// <summary>
        /// 通过 Addressable Key 异步加载 AudioClip 并播放。
        /// </summary>
        public async void SetAudio(string addressableKey, GameObject obj, AudioType type, UnityAction<AudioSource> callback = null,
            bool open3D = false, AudioRolloffMode rolloffMode = AudioRolloffMode.Linear)
        {
            if (string.IsNullOrWhiteSpace(addressableKey))
                return;

            ResourceLease<AudioClip> clipLease = await AddressableManager.Instance.AcquireAssetAsync<AudioClip>(addressableKey);
            if (clipLease == null || clipLease.Asset == null)
            {
                Debug.LogError($"音频资源加载失败：{addressableKey}");
                return;
            }

            SetAudioInternal(clipLease.Asset, clipLease, obj, type, callback, open3D, rolloffMode);
        }

        /// <summary>
        /// 直接使用已加载的 AudioClip 播放音频。
        /// </summary>
        public void SetAudio(AudioClip clip, GameObject obj, AudioType type, UnityAction<AudioSource> callback = null,
            bool open3D = false, AudioRolloffMode rolloffMode = AudioRolloffMode.Linear)
        {
            SetAudioInternal(clip, null, obj, type, callback, open3D, rolloffMode);
        }

        private void SetAudioInternal(AudioClip clip, ResourceLease<AudioClip> clipLease, GameObject obj, AudioType type,
            UnityAction<AudioSource> callback, bool open3D, AudioRolloffMode rolloffMode)
        {
            if (clip == null)
            {
                clipLease?.Dispose();
                return;
            }

            if (obj != null)
            {
                AudioSource audioSource = obj.GetComponent<AudioSource>() ?? obj.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.clip = clip;
                audioSource.spatialBlend = open3D ? 1 : 0;
                audioSource.rolloffMode = rolloffMode;
                audioSource.loop = false;
                ApplyTypeSettings(audioSource, type, 1f);
                AudioDataManager.Instance.AddAudioListener(audioSource, type);
                audioSource.Play();
                callback?.Invoke(audioSource);

                if (clipLease != null)
                {
                    EnsureHost();
                    host.StartTrackedClipRelease(audioSource, clipLease);
                }

                return;
            }

            EnsureHost();
            AudioSource pooledSource = GetPooledSource();
            pooledSource.transform.position = Vector3.zero;
            pooledSource.playOnAwake = false;
            pooledSource.clip = clip;
            pooledSource.spatialBlend = open3D ? 1 : 0;
            pooledSource.rolloffMode = rolloffMode;
            pooledSource.loop = false;
            ApplyTypeSettings(pooledSource, type, 1f);
            pooledSource.gameObject.SetActive(true);
            pooledSource.Play();
            callback?.Invoke(pooledSource);

            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(pooledSource, false, null, type, 1f, clipLease);
        }

        /// <summary>
        /// 停止/暂停/静音/清除指定 AudioSource 的音频。
        /// </summary>
        public void RemoveAudio(AudioSource audioSource, StopAudioMode mode = StopAudioMode.ClipClear,
            UnityAction<AudioSource> callback = null)
        {
            if (audioSource == null) return;

            if (mode == StopAudioMode.ClipPause) audioSource.Pause();
            else if (mode == StopAudioMode.ClipStop) audioSource.Stop();
            else if (mode == StopAudioMode.ClipMute) audioSource.mute = true;
            else if (mode == StopAudioMode.ClipClear)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            callback?.Invoke(audioSource);
        }

        /// <summary>
        /// 移除 AudioSource 组件或销毁其所在 GameObject。
        /// </summary>
        public void RemoveAudio(AudioSource audioSource, RemoveAudioMode mode = RemoveAudioMode.RemoveAudioSource,
            UnityAction callback = null)
        {
            if (audioSource == null) return;

            AudioDataManager.Instance.RemoveAudioListener(audioSource);

            if (mode == RemoveAudioMode.RemoveAudioSource)
                Object.Destroy(audioSource);
            else
                Object.Destroy(audioSource.gameObject);

            callback?.Invoke();
        }

        internal void Tick()
        {
            if (activeAudios.Count == 0)
                return;

            completedAudioHandles.Clear();
            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                AudioSource source = pair.Value.Source;
                if (source == null)
                {
                    completedAudioHandles.Add(pair.Key);
                    continue;
                }

                if (pair.Value.FollowTarget != null)
                    source.transform.position = pair.Value.FollowTarget.position;

                if (!pair.Value.IsLoop && !source.isPlaying)
                    completedAudioHandles.Add(pair.Key);
            }

            for (int i = 0; i < completedAudioHandles.Count; i++)
            {
                int handle = completedAudioHandles[i];
                if (activeAudios.TryGetValue(handle, out ActiveAudioInfo info))
                    ReleaseHandle(handle, info);
            }
        }

        public void ApplyAudioSettings(bool musicEnabledValue, float musicVolumeValue, bool soundEnabledValue, float soundVolumeValue)
        {
            musicEnabled = musicEnabledValue;
            musicVolume = Mathf.Clamp01(musicVolumeValue);
            soundEnabled = soundEnabledValue;
            soundVolume = Mathf.Clamp01(soundVolumeValue);

            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                AudioSource source = pair.Value.Source;
                if (source == null)
                    continue;

                ApplyTypeSettings(source, pair.Value.Type, pair.Value.BaseVolume);
            }
        }

        private void ApplyTypeSettings(AudioSource source, AudioType type, float baseVolume)
        {
            if (source == null)
                return;

            switch (type)
            {
                case AudioType.Music:
                    source.mute = !musicEnabled;
                    source.volume = Mathf.Clamp01(baseVolume) * musicVolume;
                    break;

                case AudioType.Sound:
                default:
                    source.mute = !soundEnabled;
                    source.volume = Mathf.Clamp01(baseVolume) * soundVolume;
                    break;
            }
        }

        private void EnsureHost()
        {
            if (host != null)
                return;

            GameObject hostObject = new GameObject("AudioManager");
            Object.DontDestroyOnLoad(hostObject);
            root = hostObject.transform;
            host = hostObject.AddComponent<AudioManagerHost>();
        }

        private AudioSource GetPooledSource()
        {
            EnsureHost();

            while (idleSources.Count > 0)
            {
                AudioSource cached = idleSources.Dequeue();
                if (cached != null)
                    return cached;
            }

            GameObject sourceObject = new GameObject("PooledAudioSource");
            sourceObject.transform.SetParent(root, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private void ReleaseHandle(int audioHandle, ActiveAudioInfo info)
        {
            activeAudios.Remove(audioHandle);

            AudioSource source = info.Source;
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.mute = false;
            source.transform.SetParent(root, false);
            source.gameObject.SetActive(false);
            idleSources.Enqueue(source);
            info.ClipLease?.Dispose();
        }
    }

}
