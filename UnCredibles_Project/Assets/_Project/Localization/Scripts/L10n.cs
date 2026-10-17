using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace UnCredibles.Localization
{
    // Text for the player in the selected language. Code-built UI asks for its strings here
    // instead of hardcoding them; scene texts use LocalizeStringEvent instead.
    // A missing table or key returns the key itself, so a gap shows up on screen without breaking anything.
    public static class L10n
    {
        public const string UI = "UI";
        public const string Minigames = "Minigames";
        public const string Online = "Online";

        private const char TokenPrefix = '@';

        // Raised when the selected language changes. UI that writes its text from code listens and
        // redraws, since only scene texts with LocalizeStringEvent update on their own.
        public static event Action LanguageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LanguageChanged = null; // survives Play without domain reload

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ListenForLanguage()
        {
            LocalizationSettings.SelectedLocaleChanged -= RaiseLanguageChanged;
            LocalizationSettings.SelectedLocaleChanged += RaiseLanguageChanged;
        }

        private static void RaiseLanguageChanged(UnityEngine.Localization.Locale _) => LanguageChanged?.Invoke();

        public static string Get(string table, string key)
        {
            try
            {
                var entry = LocalizationSettings.StringDatabase.GetTableEntry(table, key).Entry;
                return entry != null && !string.IsNullOrEmpty(entry.Value) ? entry.Value : key;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[L10n] {table}/{key}: {exception.Message}");
                return key;
            }
        }

        // Entries use {0}, {1}... placeholders.
        public static string Format(string table, string key, params object[] args)
        {
            string text = Get(table, key);
            try { return string.Format(text, args); }
            catch (FormatException) { return text; }
        }

        // Text that travels to another machine (e.g. a kick reason) goes as a token, so every
        // machine shows it in its own language. Resolve turns a token back into text.
        public static string Token(string table, string key) => TokenPrefix + table + "/" + key;

        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != TokenPrefix) return text;
            int slash = text.IndexOf('/');
            return slash > 1 ? Get(text.Substring(1, slash - 1), text.Substring(slash + 1)) : text;
        }
    }
}
