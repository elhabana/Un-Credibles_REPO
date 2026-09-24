using UnCredibles.Players.Inputs;

namespace UnCredibles.Players
{
    public sealed class PlayerSlot
    {
        public const int NoPlayer = -1;

        public int SlotIndex { get; }
        public int PlayerId { get; internal set; } = NoPlayer;
        public string PlayerName { get; internal set; } = string.Empty;
        public PlayerType PlayerType { get; internal set; }
        public SlotState State { get; internal set; }
        public IPlayerInput Input { get; internal set; }
        public int CharacterId { get; internal set; }
        public bool IsReady { get; internal set; }
        public bool IsConnected { get; internal set; }
        public bool IsHost { get; internal set; }

        public InputSourceType InputSource => Input?.Source ?? InputSourceType.None;
        public bool IsOccupied => PlayerType != PlayerType.Empty;
        public bool IsAI => PlayerType == PlayerType.AIPlayer;

        internal PlayerSlot(int slotIndex) { SlotIndex = slotIndex; }

        internal void Clear()
        {
            Input?.Dispose();
            PlayerId = NoPlayer;
            PlayerName = string.Empty;
            PlayerType = PlayerType.Empty;
            State = SlotState.Empty;
            Input = null;
            CharacterId = 0;
            IsReady = false;
            IsConnected = false;
            IsHost = false;
        }
    }
}
