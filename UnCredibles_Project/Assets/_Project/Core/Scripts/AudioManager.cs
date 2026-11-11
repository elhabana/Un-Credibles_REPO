using System;
using UnCredibles.Minigames;
using UnityEngine;

namespace UnCredibles.Core
{
    public sealed class AudioManager : MonoBehaviour
    {
        // Each half of a track change (out, then in): about 0.8 s in total.
        private const float FadeSeconds = .4f;

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music")]
        [Tooltip("Menu, lobby and voting: one track that keeps playing from one to the next.")]
        [SerializeField] private AudioClip menuMusic;
        [Tooltip("Fanfare, played once: round results and a final podium won by a human.")]
        [SerializeField] private AudioClip resultsMusic;
        [Tooltip("Loop for the final podium when no human wins.")]
        [SerializeField] private AudioClip defeatMusic;

        [Header("Sound effects")]
        [SerializeField] private AudioClip uiMove;
        [SerializeField] private AudioClip uiConfirm;
        [SerializeField] private AudioClip uiBack;
        [SerializeField] private AudioClip uiError;
        [SerializeField] private AudioClip uiReady;
        [SerializeField] private AudioClip countdownBeep;
        [SerializeField] private AudioClip countdownGo;
        [SerializeField] private AudioClip timerTick;
        [SerializeField] private AudioClip roundEnd;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip lose;
        [SerializeField] private AudioClip score;
        [SerializeField] private AudioClip deliver;
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip stun;
        [SerializeField] private AudioClip hitSoft;
        [SerializeField] private AudioClip hitRival;
        [SerializeField] private AudioClip runOver;
        [SerializeField] private AudioClip failWah;

        private SettingsManager settings;
        // Frame (+1, so the zeroed array means "never") each effect last played: one per effect per frame.
        private readonly int[] playedFrame = new int[Enum.GetValues(typeof(GameSfx)).Length];
        private AudioClip wantedMusic;
        private bool wantedLoops;
        private bool restartMusic;
        private float musicFade;

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

        // A looping track. The same track already playing goes on without restarting.
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || (clip == wantedMusic && wantedLoops)) return;
            wantedMusic = clip;
            wantedLoops = true;
        }

        // Played once from the start (even if it just played), then silence.
        public void PlayMusicOnce(AudioClip clip)
        {
            if (clip == null) return;
            wantedMusic = clip;
            wantedLoops = false;
            restartMusic = true;
        }

        public void StopMusic()
        {
            wantedMusic = null;
            restartMusic = false;
        }

        public void PlayMenuMusic() => PlayMusic(menuMusic);
        public void PlayResultsMusic() => PlayMusicOnce(resultsMusic);
        public void PlayDefeatMusic() => PlayMusic(defeatMusic);

        // Fades the current track out before switching, and the new loop in.
        private void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            if (musicSource.clip != wantedMusic || restartMusic)
            {
                musicFade = musicSource.isPlaying ? musicFade - step : 0f;
                if (musicFade <= 0f) SwitchMusic();
            }
            else musicFade = Mathf.Min(1f, musicFade + step);
            musicSource.volume = MusicVolume * musicFade;
        }

        private void SwitchMusic()
        {
            restartMusic = false;
            musicSource.clip = wantedMusic;
            musicSource.loop = wantedLoops;
            // A one-shot fanfare starts at full volume so its attack is not lost.
            musicFade = wantedLoops ? 0f : 1f;
            if (wantedMusic != null) musicSource.Play();
            else musicSource.Stop();
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null) sfxSource.PlayOneShot(clip, volumeScale);
        }

        // One shared source (PlayOneShot overlaps): no AudioSource is created per sound.
        public void PlaySfx(GameSfx sfx)
        {
            int index = (int)sfx;
            int frame = Time.frameCount + 1;
            if (playedFrame[index] == frame) return;
            playedFrame[index] = frame;
            PlaySfx(ClipFor(sfx));
        }

        private AudioClip ClipFor(GameSfx sfx) => sfx switch
        {
            GameSfx.UiMove => uiMove,
            GameSfx.UiConfirm => uiConfirm,
            GameSfx.UiBack => uiBack,
            GameSfx.UiError => uiError,
            GameSfx.UiReady => uiReady,
            GameSfx.CountdownBeep => countdownBeep,
            GameSfx.CountdownGo => countdownGo,
            GameSfx.TimerTick => timerTick,
            GameSfx.RoundEnd => roundEnd,
            GameSfx.Win => win,
            GameSfx.Lose => lose,
            GameSfx.Score => score,
            GameSfx.Deliver => deliver,
            GameSfx.Jump => jump,
            GameSfx.Stun => stun,
            GameSfx.HitSoft => hitSoft,
            GameSfx.HitRival => hitRival,
            GameSfx.RunOver => runOver,
            GameSfx.FailWah => failWah,
            _ => null,
        };

        private float MusicVolume => settings != null ? settings.MusicVolume : 1f;

        private void ApplyVolumes()
        {
            EnsureSources();
            AudioListener.volume = settings.MasterVolume;
            musicSource.volume = settings.MusicVolume * musicFade;
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
