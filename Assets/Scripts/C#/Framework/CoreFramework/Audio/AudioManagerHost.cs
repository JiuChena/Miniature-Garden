using UnityEngine;
using System.Collections;

namespace CoreFramework
{
    public class AudioManagerHost : MonoBehaviour
    {
        public void StartTrackedClipRelease(AudioSource source, ResourceLease<AudioClip> clipLease)
        {
            if (source == null || clipLease == null)
            {
                clipLease?.Dispose();
                return;
            }

            StartCoroutine(ReleaseWhenPlaybackFinished(source, clipLease));
        }

        private void Update()
        {
            AudioManager.Instance.Tick();
        }

        private static IEnumerator ReleaseWhenPlaybackFinished(AudioSource source, ResourceLease<AudioClip> clipLease)
        {
            while (source != null && source.gameObject != null && source.isActiveAndEnabled && source.isPlaying)
                yield return null;

            clipLease.Dispose();
        }
    }
}
