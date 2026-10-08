using System;
using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using Unity.Collections;
using Unity.Netcode;

namespace UnCredibles.Networking
{
    // Keeps the players of an online room in sync. The host is the authority: its PlayerRegistry
    // is the truth, each remote connection owns one OnlinePlayer slot, and every change is sent
    // to the clients as a snapshot of the 4 slots. Clients only ask (ready, snapshot).
    public sealed class OnlineSession : IDisposable
    {
        private const string SnapshotMessage = "uc.lobby.snapshot";
        private const string CountdownMessage = "uc.lobby.countdown";
        private const string ReadyMessage = "uc.lobby.ready";
        private const string HelloMessage = "uc.lobby.hello";

        private readonly RelayConnection connection;
        private readonly PlayerRegistry players;
        private readonly Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
        private readonly LobbySlotInfo[] slots = new LobbySlotInfo[PlayerRegistry.MaxPlayers];
        private readonly List<ulong> remoteClients = new List<ulong>(PlayerRegistry.MaxPlayers);
        private CustomMessagingManager messaging;

        public OnlineSession(RelayConnection connection, PlayerRegistry players)
        {
            this.connection = connection;
            this.players = players;
            connection.RejectJoin = RejectReason;
            connection.SessionStarted += HandleSessionStarted;
            connection.SessionStopped += HandleSessionStopped;
            connection.RemoteClientConnected += HandleRemoteConnected;
            connection.RemoteClientDisconnected += HandleRemoteDisconnected;
            players.SlotChanged += HandleSlotChanged;
            ResetSlots();
        }

        // Closed once the match starts: nobody new can join and leavers become AI.
        public bool AcceptingPlayers { get; set; } = true;
        public bool IsActive => messaging != null;
        public bool IsHost => connection.IsHost;
        public ulong LocalClientId => connection.LocalClientId;

        // Client: last snapshot received from the host. Host: use the PlayerRegistry directly.
        public IReadOnlyList<LobbySlotInfo> Slots => slots;

        public event Action SlotsChanged;           // client
        public event Action<int> CountdownReceived; // client: seconds left, 0 = start, -1 = cancelled

        // Connection that controls a slot: the remote client, or the host for its own players.
        public ulong OwnerOf(int slotIndex)
        {
            foreach (var pair in clientSlots)
                if (pair.Value == slotIndex) return pair.Key;
            return players.Slots[slotIndex].IsOccupied ? NetworkManager.ServerClientId : LobbySlotInfo.NoOwner;
        }

        public bool IsRemoteSlot(int slotIndex) => clientSlots.ContainsValue(slotIndex);

        // Host: removes a remote player; their slot is freed when the disconnection arrives.
        public void Kick(int slotIndex, string reason)
        {
            foreach (var pair in clientSlots)
                if (pair.Value == slotIndex) { connection.Kick(pair.Key, reason); return; }
        }

        public void SendCountdown(int secondsLeft)
        {
            if (!IsActive || !IsHost) return;
            CollectRemoteClients();
            if (remoteClients.Count == 0) return;
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(secondsLeft);
            messaging.SendNamedMessage(CountdownMessage, remoteClients, writer);
        }

        public void SendReady(bool ready)
        {
            if (!IsActive || IsHost) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(ready);
            messaging.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer);
        }

        // Client: a freshly loaded lobby asks for the current state (earlier snapshots may have
        // arrived while the menu was still on screen).
        public void RequestSnapshot()
        {
            if (!IsActive || IsHost) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe((byte)0);
            messaging.SendNamedMessage(HelloMessage, NetworkManager.ServerClientId, writer);
        }

        public void Dispose()
        {
            connection.RejectJoin = null;
            connection.SessionStarted -= HandleSessionStarted;
            connection.SessionStopped -= HandleSessionStopped;
            connection.RemoteClientConnected -= HandleRemoteConnected;
            connection.RemoteClientDisconnected -= HandleRemoteDisconnected;
            players.SlotChanged -= HandleSlotChanged;
            Unregister();
        }

        private string RejectReason()
        {
            if (!AcceptingPlayers) return "La partida ya ha empezado.";
            return players.OccupiedCount >= PlayerRegistry.MaxPlayers ? "Sala llena." : null;
        }

        private void HandleSessionStarted()
        {
            clientSlots.Clear();
            AcceptingPlayers = true;
            messaging = connection.Manager.CustomMessagingManager;
            if (connection.Manager.IsServer)
            {
                messaging.RegisterNamedMessageHandler(ReadyMessage, HandleReadyMessage);
                messaging.RegisterNamedMessageHandler(HelloMessage, HandleHelloMessage);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshotMessage);
                messaging.RegisterNamedMessageHandler(CountdownMessage, HandleCountdownMessage);
            }
        }

        private void HandleSessionStopped()
        {
            Unregister();
            clientSlots.Clear();
            AcceptingPlayers = true;
            ResetSlots();
            SlotsChanged?.Invoke();
        }

        private void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(ReadyMessage);
            messaging.UnregisterNamedMessageHandler(HelloMessage);
            messaging.UnregisterNamedMessageHandler(SnapshotMessage);
            messaging.UnregisterNamedMessageHandler(CountdownMessage);
            messaging = null;
        }

        // ---------- Host ----------

        private void HandleRemoteConnected(ulong clientId)
        {
            if (!AcceptingPlayers ||
                !players.TryAddPlayer(PlayerType.OnlinePlayer, new NetworkInput(clientId), null, out var slot))
            {
                connection.Kick(clientId, AcceptingPlayers ? "Sala llena." : "La partida ya ha empezado.");
                return;
            }
            // Map first: the snapshot sent by TryAddPlayer's event already ran, so send again with the owner.
            clientSlots[clientId] = slot.SlotIndex;
            SendSnapshot();
        }

        // In the lobby the place is freed; during a match the bot takes over (score kept).
        private void HandleRemoteDisconnected(ulong clientId)
        {
            if (!clientSlots.TryGetValue(clientId, out int slotIndex)) return;
            clientSlots.Remove(clientId);
            if (AcceptingPlayers) players.RemovePlayer(slotIndex);
            else players.ReplaceWithAI(slotIndex, new AIInput());
        }

        private void HandleSlotChanged(PlayerSlot slot)
        {
            if (IsActive && IsHost) SendSnapshot();
        }

        private void HandleReadyMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool ready);
            if (clientSlots.TryGetValue(sender, out int slotIndex)) players.SetReady(slotIndex, ready);
        }

        private void HandleHelloMessage(ulong sender, FastBufferReader reader) => SendSnapshot();

        private void SendSnapshot()
        {
            CollectRemoteClients();
            if (remoteClients.Count == 0) return;
            using var writer = new FastBufferWriter(256, Allocator.Temp, 4096);
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++)
                LobbySlotInfo.From(players.Slots[i], OwnerOf(i)).Write(writer);
            messaging.SendNamedMessage(SnapshotMessage, remoteClients, writer);
        }

        private void CollectRemoteClients()
        {
            remoteClients.Clear();
            foreach (ulong id in connection.Connections)
                if (id != connection.LocalClientId) remoteClients.Add(id);
        }

        // ---------- Client ----------

        private void HandleSnapshotMessage(ulong sender, FastBufferReader reader)
        {
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++) slots[i] = LobbySlotInfo.Read(reader);
            SlotsChanged?.Invoke();
        }

        private void HandleCountdownMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int secondsLeft);
            CountdownReceived?.Invoke(secondsLeft);
        }

        private void ResetSlots()
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LobbySlotInfo { Name = string.Empty, Owner = LobbySlotInfo.NoOwner };
        }
    }
}
