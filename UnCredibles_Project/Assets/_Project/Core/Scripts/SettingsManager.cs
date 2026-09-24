using System;
using UnityEngine;

namespace UnCredibles.Core
{
    public sealed class SettingsManager
    {
        private const string MasterKey = "settings.volume.master";
        private const string MusicKey = "settings.volume.music";
        private const string SfxKey = "settings.volume.sfx";

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        public event Action Changed;

        public void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
            Changed?.Invoke();
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.Save();
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            Changed?.Invoke();
        }
    }
}
