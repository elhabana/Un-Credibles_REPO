using System;
using UnityEngine;

namespace UnCredibles.Players
{
    // Slot identity is already shared by the lobby and network snapshots.
    public static class PlayerIdentity
    {
        // Bots keep their slot color, so a human replaced by a bot keeps it too.
        // Art bible hero colours; the M_*_Player_<colour> materials use the same values.
        public static Color ColorFor(int slotIndex, bool isAI = false)
        {
            switch (slotIndex)
            {
                case 0: return new Color(.118f, .608f, .843f); // #1E9BD7 hero blue
                case 1: return new Color(.902f, .231f, .18f);  // #E63B2E hero red
                case 2: return new Color(.365f, .71f, .184f);  // #5DB52F hero green
                case 3: return new Color(.961f, .761f, .106f); // #F5C21B hero yellow
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
