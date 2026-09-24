using UnityEngine;

namespace UnCredibles.Core
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private SettingsManager settings;

        public void Bind(SettingsManager settingsManager)
        {
            if (settings != null) settings.Changed -= ApplyVolumes;
            settings = settingsManager;
            settings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        // Bind may run before this Awake (both live on the Core object), so sources are created on demand.
        private void Awake() => EnsureSources();

        private void EnsureSources()
        {
            if (musicSource == null) musicSource = CreateSource("Music", true);
            if (sfxSource == null) sfxSource = CreateSource("Sfx", false);
        }

        private void OnDestroy()
        {
            if (settings != null) settings.Changed -= ApplyVolumes;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic() => musicSource.Stop();

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null) sfxSource.PlayOneShot(clip, volumeScale);
        }

        private void ApplyVolumes()
        {
            EnsureSources();
            AudioListener.volume = settings.MasterVolume;
            musicSource.volume = settings.MusicVolume;
            sfxSource.volume = settings.SfxVolume;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }
    }
}
