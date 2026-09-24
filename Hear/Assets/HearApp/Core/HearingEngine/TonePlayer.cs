using UnityEngine;

namespace HearApp.Core.HearingEngine
{
    /// <summary>
    /// Synthesizes and plays the pure-tone stimulus. Kept intentionally simple for this
    /// architecture slice: a plain enveloped sine wave, panned hard left/right on an
    /// AudioSource for Headphones mode (a legitimate, standard way to lateralize a mono
    /// clip), or centered for Speaker/Combined mode. Real calibration/level-stepping is out
    /// of scope for this milestone (see docs/13-open-questions.md).
    /// </summary>
    public sealed class TonePlayer : MonoBehaviour
    {
        private AudioSource _audioSource;
        private readonly System.Collections.Generic.Dictionary<int, AudioClip> _toneCache = new();

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // pure 2D stereo pan, not 3D positional audio
        }

        public void PlayTone(float frequencyHz, float durationSeconds, EarChannel channel)
        {
            _audioSource.panStereo = channel switch
            {
                EarChannel.Left => -1f,
                EarChannel.Right => 1f,
                _ => 0f
            };
            _audioSource.PlayOneShot(GetOrCreateTone(frequencyHz, durationSeconds));
        }

        private AudioClip GetOrCreateTone(float frequencyHz, float durationSeconds)
        {
            int key = Mathf.RoundToInt(frequencyHz * 1000f) ^ Mathf.RoundToInt(durationSeconds * 100000f);
            if (_toneCache.TryGetValue(key, out var cached))
                return cached;

            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            var clip = AudioClip.Create($"Tone_{frequencyHz:0}Hz", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            float fadeSamples = sampleRate * 0.01f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, Mathf.Min(i / fadeSamples, (sampleCount - i) / fadeSamples));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * 0.5f * envelope;
            }
            clip.SetData(data, 0);
            _toneCache[key] = clip;
            return clip;
        }
    }
}
