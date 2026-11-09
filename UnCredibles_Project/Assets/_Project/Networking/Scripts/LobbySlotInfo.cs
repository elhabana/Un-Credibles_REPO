using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using Unity.Netcode;

namespace UnCredibles.Networking
{
    // What a lobby card shows about one slot. The host builds it from its PlayerRegistry
    // and sends it to the clients, so every machine draws the same lobby.
    public struct LobbySlotInfo
    {
        public const ulong NoOwner = ulong.MaxValue;

        public bool Occupied;
        public int PlayerId;
        public string Name;
        public PlayerType Type;
        public SlotState State;
        public InputSourceType Source;
        public bool Ready;
        public bool Host;
        public ulong Owner; // connection that controls the slot (host = 0), NoOwner when empty or offline
        public CharacterLoadout Loadout;
        public BotDifficulty BotLevel;

        public bool IsAI => Type == PlayerType.AIPlayer;

        public static LobbySlotInfo From(PlayerSlot slot, ulong owner) => new LobbySlotInfo
        {
            Occupied = slot.IsOccupied,
            PlayerId = slot.PlayerId,
            Name = slot.PlayerName ?? string.Empty,
            Type = slot.PlayerType,
            State = slot.State,
            Source = slot.InputSource,
            Ready = slot.IsReady,
            Host = slot.IsHost,
            Owner = slot.IsOccupied ? owner : NoOwner,
            Loadout = slot.Loadout,
            BotLevel = slot.Input is AIInput bot ? bot.Difficulty : BotDifficulty.Hard,
        };

        internal void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(Occupied);
            writer.WriteValueSafe(PlayerId);
            writer.WriteValueSafe(Name ?? string.Empty);
            writer.WriteValueSafe((byte)Type);
            writer.WriteValueSafe((byte)State);
            writer.WriteValueSafe((byte)Source);
            writer.WriteValueSafe(Ready);
            writer.WriteValueSafe(Host);
            writer.WriteValueSafe(Owner);
            writer.WriteValueSafe(Loadout.Helmet ?? string.Empty);
            writer.WriteValueSafe(Loadout.Outfit ?? string.Empty);
            writer.WriteValueSafe(Loadout.Boots ?? string.Empty);
            writer.WriteValueSafe((byte)BotLevel);
        }

        internal static LobbySlotInfo Read(FastBufferReader reader)
        {
            var info = new LobbySlotInfo();
            reader.ReadValueSafe(out info.Occupied);
            reader.ReadValueSafe(out info.PlayerId);
            reader.ReadValueSafe(out info.Name);
            reader.ReadValueSafe(out byte type);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte source);
            reader.ReadValueSafe(out info.Ready);
            reader.ReadValueSafe(out info.Host);
            reader.ReadValueSafe(out info.Owner);
            reader.ReadValueSafe(out info.Loadout.Helmet);
            reader.ReadValueSafe(out info.Loadout.Outfit);
            reader.ReadValueSafe(out info.Loadout.Boots);
            reader.ReadValueSafe(out byte botLevel);
            info.BotLevel = (BotDifficulty)botLevel;
            info.Type = (PlayerType)type;
            info.State = (SlotState)state;
            info.Source = (InputSourceType)source;
            return info;
        }
    }
}
