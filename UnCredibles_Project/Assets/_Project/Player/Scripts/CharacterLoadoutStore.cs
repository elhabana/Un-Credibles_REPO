using UnityEngine;

namespace UnCredibles.Players
{
    // The local player's chosen loadout, kept between sessions on this machine.
    public static class CharacterLoadoutStore
    {
        private const string Key = "pj.loadout";

        public static CharacterLoadout Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return default;
            try { return JsonUtility.FromJson<CharacterLoadout>(json); }
            catch (System.ArgumentException) { return default; }
        }

        public static void Save(CharacterLoadout loadout)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(loadout));
            PlayerPrefs.Save();
        }
    }
}
