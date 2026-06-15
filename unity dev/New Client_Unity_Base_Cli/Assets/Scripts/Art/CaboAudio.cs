using System.Collections;
using UnityEngine;

namespace Cabo.Client.Art
{
    public static class CaboAudio
    {
        const float FadeDuration = 0.5f;

        static GameObject _hostObject;
        static CaboAudioHost _host;
        static AudioSource _sfxSource;
        static AudioSource _bgmSource;
        static float _bgmVolume = 0.42f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _hostObject = null;
            _host = null;
            _sfxSource = null;
            _bgmSource = null;
            _bgmVolume = 0.42f;
        }

        public static void Play(CaboSfx cue, float volume = 1f)
        {
            var clip = CaboArt.GetSfx(cue);
            if (clip == null)
                return;

            EnsureSources();
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public static void PlayBGM()
        {
            var clip = CaboArt.BGM;
            if (clip == null)
                return;

            EnsureSources();
            _host.PlayBGM(_bgmSource, clip, _bgmVolume, FadeDuration);
        }

        public static void StopBGM()
        {
            if (_bgmSource == null || _host == null)
                return;

            _host.StopBGM(_bgmSource, FadeDuration);
        }

        public static void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            if (_host != null && _bgmSource != null)
                _host.SetBGMVolume(_bgmSource, _bgmVolume);
        }

        static void EnsureSources()
        {
            if (_sfxSource != null && _bgmSource != null && _host != null)
                return;

            _hostObject = new GameObject("CaboAudio");
            Object.DontDestroyOnLoad(_hostObject);
            _host = _hostObject.AddComponent<CaboAudioHost>();
            _sfxSource = CreateSource(_hostObject, false);
            _bgmSource = CreateSource(_hostObject, true);
        }

        static AudioSource CreateSource(GameObject host, bool loop)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

    }

    sealed class CaboAudioHost : MonoBehaviour
    {
        Coroutine _bgmRoutine;
        float _playVolume = 0.42f;

        public void PlayBGM(AudioSource source, AudioClip clip, float targetVolume, float fadeDuration)
        {
            _playVolume = Mathf.Clamp01(targetVolume);
            Restart(PlayRoutine(source, clip, fadeDuration));
        }

        public void SetBGMVolume(AudioSource source, float volume)
        {
            _playVolume = Mathf.Clamp01(volume);
            if (source != null && source.isPlaying)
                source.volume = _playVolume;
        }

        public void StopBGM(AudioSource source, float fadeDuration)
        {
            Restart(StopRoutine(source, fadeDuration));
        }

        void Restart(IEnumerator routine)
        {
            if (_bgmRoutine != null)
                StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(routine);
        }

        IEnumerator PlayRoutine(AudioSource source, AudioClip clip, float fadeDuration)
        {
            if (source.isPlaying && source.clip != clip)
            {
                yield return Fade(source, 0f, fadeDuration);
                source.Stop();
            }

            if (!source.isPlaying || source.clip != clip)
            {
                source.clip = clip;
                source.loop = true;
                source.volume = 0f;
                source.Play();
            }

            yield return FadeToPlayVolume(source, fadeDuration);
            _bgmRoutine = null;
        }

        IEnumerator StopRoutine(AudioSource source, float fadeDuration)
        {
            if (source.isPlaying)
                yield return Fade(source, 0f, fadeDuration);
            source.Stop();
            source.clip = null;
            _bgmRoutine = null;
        }

        static IEnumerator Fade(AudioSource source, float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float progress = (Time.realtimeSinceStartup - startedAt) / Mathf.Max(0.01f, duration);
                source.volume = Mathf.Lerp(startVolume, targetVolume, progress);
                yield return null;
            }
            source.volume = targetVolume;
        }

        IEnumerator FadeToPlayVolume(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float progress = (Time.realtimeSinceStartup - startedAt) / Mathf.Max(0.01f, duration);
                source.volume = Mathf.Lerp(startVolume, _playVolume, progress);
                yield return null;
            }
            source.volume = _playVolume;
        }
    }
}
