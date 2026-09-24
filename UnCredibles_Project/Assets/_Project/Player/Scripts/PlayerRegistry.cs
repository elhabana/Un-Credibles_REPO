using System;
using System.Collections.Generic;
using UnCredibles.Players.Inputs;

namespace UnCredibles.Players
{
    // Single source of truth for who is playing. The registry owns the inputs it is given
    // and disposes them when a slot is cleared or replaced.
    public sealed class PlayerRegistry
    {
        public const int MaxPlayers = 4;

        private readonly PlayerSlot[] slots = new PlayerSlot[MaxPlayers];
        private int nextPlayerId;

        public IReadOnlyList<PlayerSlot> Slots { get; }
        public event Action<PlayerSlot> SlotChanged;

        public PlayerRegistry()
        {
            for (int i = 0; i < MaxPlayers; i++) slots[i] = new PlayerSlot(i);
            Slots = Array.AsReadOnly(slots);
        }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                foreach (var slot in slots)
                    if (slot.IsOccupied) count++;
                return count;
            }
        }

        public bool AllPlayersReady
        {
            get
            {
                bool any = false;
                foreach (var slot in slots)
                {
                    if (!slot.IsOccupied) continue;
                    any = true;
                    if (!slot.IsReady) return false;
                }
                return any;
            }
        }

        public bool TryAddPlayer(PlayerType type, IPlayerInput input, string playerName, out PlayerSlot slot)
        {
            foreach (var candidate in slots)
                if (candidate.State == SlotState.Empty)
                    return TryAddPlayerAt(candidate.SlotIndex, type, input, playerName, out slot);
            slot = null;
            return false;
        }

        public bool TryAddPlayerAt(int index, PlayerType type, IPlayerInput input, string playerName, out PlayerSlot slot)
        {
            slot = null;
            if (!IsValidIndex(index) || type == PlayerType.Empty || input == null) return false;
            var target = slots[index];
            // Inviting/Connecting slots are reserved for the player being invited.
            if (target.IsOccupied) return false;

            bool isAI = type == PlayerType.AIPlayer;
            target.PlayerId = nextPlayerId++;
            target.PlayerType = type;
            target.State = isAI ? SlotState.AI : SlotState.Occupied;
            target.Input = input;
            target.PlayerName = string.IsNullOrWhiteSpace(playerName) ? DefaultName(index, isAI) : playerName;
            target.IsReady = isAI;
            target.IsConnected = true;
            slot = target;
            SlotChanged?.Invoke(target);
            return true;
        }

        public bool RemovePlayer(int index)
        {
            if (!IsValidIndex(index) || slots[index].State == SlotState.Empty) return false;
            slots[index].Clear();
            SlotChanged?.Invoke(slots[index]);
            return true;
        }

        // Online flow: OnlinePlayer -> Disconnected -> AIPlayer, keeping the same PlayerId and score.
        public bool ReplaceWithAI(int index, IPlayerInput aiInput)
        {
            if (!IsOccupied(index) || aiInput == null || slots[index].IsAI) return false;
            var slot = slots[index];
            slot.Input?.Dispose();
            slot.Input = aiInput;
            slot.PlayerType = PlayerType.AIPlayer;
            slot.State = SlotState.AI;
            slot.IsReady = true;
            slot.IsHost = false;
            SlotChanged?.Invoke(slot);
            return true;
        }

        public bool SetSlotState(int index, SlotState state)
        {
            // Only empty slots can move between Empty/Inviting/Connecting; the rest is driven by joins.
            if (!IsValidIndex(index) || slots[index].IsOccupied) return false;
            if (state != SlotState.Empty && state != SlotState.Inviting && state != SlotState.Connecting) return false;
            if (slots[index].State == state) return true;
            slots[index].State = state;
            SlotChanged?.Invoke(slots[index]);
            return true;
        }

        public bool SetReady(int index, bool ready)
        {
            if (!IsOccupied(index)) return false;
            var slot = slots[index];
            bool value = slot.IsAI || ready;
            if (slot.IsReady == value) return true;
            slot.IsReady = value;
            SlotChanged?.Invoke(slot);
            return true;
        }

        public bool SetConnected(int index, bool connected)
        {
            if (!IsOccupied(index)) return false;
            var slot = slots[index];
            if (slot.IsConnected == connected) return true;
            slot.IsConnected = connected;
            if (!connected) slot.State = SlotState.Disconnected;
            SlotChanged?.Invoke(slot);
            return true;
        }

        public bool SetHost(int index)
        {
            if (!IsOccupied(index) || slots[index].IsAI) return false;
            foreach (var slot in slots)
            {
                bool isHost = slot.SlotIndex == index;
                if (slot.IsHost == isHost) continue;
                slot.IsHost = isHost;
                SlotChanged?.Invoke(slot);
            }
            return true;
        }

        public bool SetCharacter(int index, int characterId)
        {
            if (!IsOccupied(index)) return false;
            if (slots[index].CharacterId == characterId) return true;
            slots[index].CharacterId = characterId;
            SlotChanged?.Invoke(slots[index]);
            return true;
        }

        public bool TryGetByPlayerId(int playerId, out PlayerSlot slot)
        {
            foreach (var candidate in slots)
            {
                if (!candidate.IsOccupied || candidate.PlayerId != playerId) continue;
                slot = candidate;
                return true;
            }
            slot = null;
            return false;
        }

        // Fills the buffer instead of allocating so callers can reuse lists.
        public int GetActivePlayers(List<PlayerSlot> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            foreach (var slot in slots)
                if (slot.IsOccupied) buffer.Add(slot);
            return buffer.Count;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxPlayers; i++) RemovePlayer(i);
        }

        private static string DefaultName(int index, bool isAI) => isAI ? $"Bot {index + 1}" : $"Player {index + 1}";
        private static bool IsValidIndex(int index) => index >= 0 && index < MaxPlayers;
        private bool IsOccupied(int index) => IsValidIndex(index) && slots[index].IsOccupied;
    }
}
