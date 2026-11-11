using UnityEngine;

namespace UnCredibles.Players
{
    // The local player's chosen loadout, kept between sessions on this machine.
    public static class CharacterLoadoutStore
    {
        // v2: saves from before the test parts were deleted are ignored.
        private const string Key = "pj.loadout.v2";

        // Nothing saved yet: `fallback`. A saved empty loadout (all BodyNude) is a real choice and is kept.
        public static CharacterLoadout Load(CharacterLoadout fallback)
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return fallback;
            try { return JsonUtility.FromJson<CharacterLoadout>(json); }
            catch (System.ArgumentException) { return fallback; }
        }

        public static void Save(CharacterLoadout loadout)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(loadout));
            PlayerPrefs.Save();
        }
    }
}
