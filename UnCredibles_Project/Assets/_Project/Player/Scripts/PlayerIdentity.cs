using System;
using UnityEngine;

namespace UnCredibles.Players
{
    // Slot identity is already shared by the lobby and network snapshots.
    public static class PlayerIdentity
    {
        // Bots keep their slot color, so a human replaced by a bot keeps it too.
        public static Color ColorFor(int slotIndex, bool isAI = false)
        {
            switch (slotIndex)
            {
                case 0: return new Color(.9f, .12f, .12f);
                case 1: return new Color(.12f, .35f, 1f);
                case 2: return new Color(.15f, .8f, .25f);
                case 3: return new Color(1f, .85f, .1f);
                default: return Color.white;
            }
        }

        // Short tag ("P1" / "J1"). Core plugs in the translated one so this assembly stays free of
        // the localization package; the English tag is only the fallback.
        public static Func<int, string> LabelProvider;

        public static string LabelFor(int slotIndex, bool isAI = false) =>
            isAI ? "BOT" : LabelProvider?.Invoke(slotIndex) ?? $"P{slotIndex + 1}";
    }
}
