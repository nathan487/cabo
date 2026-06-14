using UnityEngine;

namespace Cabo.Client.Art
{
    public static class CaboAudio
    {
        static AudioSource _source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _source = null;
        }

        public static void Play(CaboSfx cue, float volume = 1f)
        {
            var clip = CaboArt.GetSfx(cue);
            if (clip == null)
                return;

            EnsureSource();
            _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        static void EnsureSource()
        {
            if (_source != null)
                return;

            var go = new GameObject("CaboAudio");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
        }
    }
}
