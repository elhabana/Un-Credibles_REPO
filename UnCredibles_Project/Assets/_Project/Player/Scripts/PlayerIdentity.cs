using UnityEngine;

namespace UnCredibles.Players
{
    // Slot identity is already shared by the lobby and network snapshots.
    public static class PlayerIdentity
    {
        public static Color ColorFor(int slotIndex, bool isAI = false)
        {
            if (isAI) return new Color(.5f, .5f, .5f);
            switch (slotIndex)
            {
                case 0: return new Color(.9f, .12f, .12f);
                case 1: return new Color(.12f, .35f, 1f);
                case 2: return new Color(.15f, .8f, .25f);
                case 3: return new Color(1f, .85f, .1f);
                default: return Color.white;
            }
        }

        public static string LabelFor(int slotIndex, bool isAI = false) => isAI ? "IA" : $"P{slotIndex + 1}";
    }
}
