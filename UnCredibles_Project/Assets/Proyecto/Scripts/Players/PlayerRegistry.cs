using System;
using System.Collections.Generic;

namespace UnCredibles.Players
{
    // Reserved kinds describe slots only; they do not implement input or networking.
    public enum PlayerKind { Empty, LocalPlayer, PrestoPadPlayer, RemotePlayer, AIPlayer }

    public sealed class PlayerSlot
    {
        public int Index { get; }
        public PlayerKind Kind { get; internal set; }
        public bool IsReady { get; internal set; }
        internal PlayerSlot(int index) { Index = index; }
    }

    public sealed class PlayerRegistry
    {
        public const int MaxPlayers = 4;
        private readonly PlayerSlot[] slots = new PlayerSlot[MaxPlayers];
        public IReadOnlyList<PlayerSlot> Slots { get; }
        public event Action Changed;

        public PlayerRegistry()
        {
            for (int i = 0; i < MaxPlayers; i++) slots[i] = new PlayerSlot(i);
            Slots = Array.AsReadOnly(slots);
        }

        public bool TryAdd(PlayerKind kind, out int slotIndex)
        {
            slotIndex = -1;
            if (kind == PlayerKind.Empty || !Enum.IsDefined(typeof(PlayerKind), kind)) return false;
            foreach (var slot in slots)
            {
                if (slot.Kind != PlayerKind.Empty) continue;
                slot.Kind = kind;
                slot.IsReady = kind == PlayerKind.AIPlayer;
                slotIndex = slot.Index;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool Remove(int index)
        {
            if (!IsOccupied(index)) return false;
            slots[index].Kind = PlayerKind.Empty;
            slots[index].IsReady = false;
            Changed?.Invoke();
            return true;
        }

        public bool SetReady(int index, bool ready)
        {
            if (!IsOccupied(index)) return false;
            var slot = slots[index];
            bool value = slot.Kind == PlayerKind.AIPlayer || ready;
            if (slot.IsReady == value) return true;
            slot.IsReady = value;
            Changed?.Invoke();
            return true;
        }

        public bool AllOccupiedSlotsReady
        {
            get
            {
                bool any = false;
                foreach (var slot in slots)
                {
                    if (slot.Kind == PlayerKind.Empty) continue;
                    any = true;
                    if (!slot.IsReady) return false;
                }
                return any;
            }
        }

        private bool IsOccupied(int index) =>
            index >= 0 && index < MaxPlayers && slots[index].Kind != PlayerKind.Empty;
    }
}
