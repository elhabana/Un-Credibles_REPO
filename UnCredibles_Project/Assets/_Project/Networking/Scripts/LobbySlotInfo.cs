using UnCredibles.Players;
using Unity.Netcode;

namespace UnCredibles.Networking
{
    // What a lobby card shows about one slot. The host builds it from its PlayerRegistry
    // and sends it to the clients, so every machine draws the same lobby.
    public struct LobbySlotInfo
    {
        public const ulong NoOwner = ulong.MaxValue;

        public bool Occupied;
        public string Name;
        public PlayerType Type;
        public SlotState State;
        public InputSourceType Source;
        public bool Ready;
        public bool Host;
        public ulong Owner; // connection that controls the slot (host = 0), NoOwner when empty or offline

        public bool IsAI => Type == PlayerType.AIPlayer;

        public static LobbySlotInfo From(PlayerSlot slot, ulong owner) => new LobbySlotInfo
        {
            Occupied = slot.IsOccupied,
            Name = slot.PlayerName ?? string.Empty,
            Type = slot.PlayerType,
            State = slot.State,
            Source = slot.InputSource,
            Ready = slot.IsReady,
            Host = slot.IsHost,
            Owner = slot.IsOccupied ? owner : NoOwner,
        };

        internal void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(Occupied);
            writer.WriteValueSafe(Name ?? string.Empty);
            writer.WriteValueSafe((byte)Type);
            writer.WriteValueSafe((byte)State);
            writer.WriteValueSafe((byte)Source);
            writer.WriteValueSafe(Ready);
            writer.WriteValueSafe(Host);
            writer.WriteValueSafe(Owner);
        }

        internal static LobbySlotInfo Read(FastBufferReader reader)
        {
            var info = new LobbySlotInfo();
            reader.ReadValueSafe(out info.Occupied);
            reader.ReadValueSafe(out info.Name);
            reader.ReadValueSafe(out byte type);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte source);
            reader.ReadValueSafe(out info.Ready);
            reader.ReadValueSafe(out info.Host);
            reader.ReadValueSafe(out info.Owner);
            info.Type = (PlayerType)type;
            info.State = (SlotState)state;
            info.Source = (InputSourceType)source;
            return info;
        }
    }
}
