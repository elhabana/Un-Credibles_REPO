using System;
using System.Collections.Generic;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnCredibles.Networking
{
    // Keeps an online room in sync. The host is the authority: its PlayerRegistry is the truth,
    // each remote connection owns one OnlinePlayer slot, and the host decides which scene everybody
    // is in. Clients mirror the host's players, send their controller and draw what the host sends.
    //
    // Messages (all through NGO named messages, no NetworkObjects):
    //   lobby    host -> clients: snapshot of the 4 slots, countdown   | clients -> host: ready, hello
    //   flow     host -> clients: scene to load, standings             | clients -> host: scene loaded
    //   input    clients -> host: stick + buttons of the client's player
    //   minigame host -> clients: HUD, snapshots and events (see IMinigameNetwork)
    public sealed class OnlineSession : IMinigameNetwork, IDisposable
    {
        private const string SnapshotMessage = "uc.lobby.snapshot";
        private const string CountdownMessage = "uc.lobby.countdown";
        private const string ReadyMessage = "uc.lobby.ready";
        private const string HelloMessage = "uc.lobby.hello";
        private const string SceneMessage = "uc.flow.scene";
        private const string LoadedMessage = "uc.flow.loaded";
        private const string StandingsMessage = "uc.flow.standings";
        private const string VoteMessage = "uc.flow.vote";
        private const string InputMessage = "uc.input";
        private const string HudMessage = "uc.mg.hud";
        private const string MinigameSnapshotMessage = "uc.mg.snapshot";
        private const string EventMessage = "uc.mg.event";

        // Bigger snapshots cannot travel unreliable in one packet.
        private const int UnreliableLimit = 1000;

        private static readonly string[] HostMessages = { ReadyMessage, HelloMessage, LoadedMessage, InputMessage };
        private static readonly string[] ClientMessages =
            { SnapshotMessage, CountdownMessage, SceneMessage, StandingsMessage, VoteMessage, HudMessage, MinigameSnapshotMessage, EventMessage };

        private readonly RelayConnection connection;
        private readonly PlayerRegistry players;
        private readonly Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, string> clientScenes = new Dictionary<ulong, string>();
        private readonly LobbySlotInfo[] slots = new LobbySlotInfo[PlayerRegistry.MaxPlayers];
        private readonly List<ulong> remoteClients = new List<ulong>(PlayerRegistry.MaxPlayers);
        private CustomMessagingManager messaging;
        private byte[] receiveBuffer = new byte[2048];

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

        // Client: last lobby snapshot received from the host. Host: use the PlayerRegistry directly.
        public IReadOnlyList<LobbySlotInfo> Slots => slots;
        // Client: last standings received for the Results screen.
        public OnlineStandings Standings { get; } = new OnlineStandings();
        public List<int> VoteChoices { get; } = new List<int>(3);

        public event Action SlotsChanged;                   // client
        public event Action<int> CountdownReceived;         // client: seconds left, 0 = start, -1 = cancelled
        public event Action<string, byte> SceneRequested;   // client: scene to load + game state
        public event Action StandingsReceived;              // client
        public event Action<int, float> VoteProgressReceived; // client
        public event Action<byte[], int> HudReceived;       // client (IMinigameNetwork)
        public event Action<byte[], int> SnapshotReceived;  // client (IMinigameNetwork)
        public event Action<byte[], int> EventReceived;     // client (IMinigameNetwork)

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

        // ---------- Lobby ----------

        public void SendCountdown(int secondsLeft)
        {
            if (!IsActive || !IsHost || !CollectRemoteClients()) return;
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

        // ---------- Match flow ----------

        // Host: every client loads the same scene (minigame, results, lobby).
        public void SendScene(string sceneName, byte gameState)
        {
            if (!IsActive || !IsHost) return;
            foreach (ulong id in clientSlots.Keys) clientScenes[id] = null;
            if (!CollectRemoteClients()) return;
            using var writer = new FastBufferWriter(128, Allocator.Temp, 1024);
            writer.WriteValueSafe(sceneName);
            writer.WriteValueSafe(gameState);
            messaging.SendNamedMessage(SceneMessage, remoteClients, writer, NetworkDelivery.ReliableSequenced);
        }

        // Client: the requested scene is loaded and ready.
        public void SendLoaded(string sceneName)
        {
            if (!IsActive || IsHost) return;
            using var writer = new FastBufferWriter(128, Allocator.Temp, 1024);
            writer.WriteValueSafe(sceneName);
            messaging.SendNamedMessage(LoadedMessage, NetworkManager.ServerClientId, writer);
        }

        // Host: have all the remote players loaded this scene? (players that left do not count)
        public bool AllClientsIn(string sceneName)
        {
            foreach (ulong id in clientSlots.Keys)
                if (!clientScenes.TryGetValue(id, out var scene) || scene != sceneName) return false;
            return true;
        }

        public void SendStandings(OnlineStandings standings)
        {
            if (!IsActive || !IsHost || !CollectRemoteClients()) return;
            using var writer = new FastBufferWriter(256, Allocator.Temp, 4096);
            standings.Write(writer);
            messaging.SendNamedMessage(StandingsMessage, remoteClients, writer, NetworkDelivery.ReliableSequenced);
        }

        public void SendVoteProgress(IReadOnlyList<int> choices, float seconds)
        {
            if (!IsActive || !IsHost || !CollectRemoteClients()) return;
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(choices.Count);
            writer.WriteValueSafe(seconds);
            foreach (int index in choices) writer.WriteValueSafe(index);
            messaging.SendNamedMessage(VoteMessage, remoteClients, writer, NetworkDelivery.ReliableSequenced);
        }

        // Client: the controller of this machine's player, every frame while playing.
        public void SendInput(Vector2 move, int pressedMask, int heldMask)
        {
            if (!IsActive || IsHost) return;
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(move.x);
            writer.WriteValueSafe(move.y);
            writer.WriteValueSafe((byte)pressedMask);
            writer.WriteValueSafe((byte)heldMask);
            // A press must never be lost; plain movement can.
            var delivery = pressedMask != 0 ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced;
            messaging.SendNamedMessage(InputMessage, NetworkManager.ServerClientId, writer, delivery);
        }

        // ---------- IMinigameNetwork (host side) ----------

        public void SendHud(byte[] data, int length) => SendBytes(HudMessage, data, length, NetworkDelivery.ReliableFragmentedSequenced);
        public void SendEvent(byte[] data, int length) => SendBytes(EventMessage, data, length, NetworkDelivery.ReliableFragmentedSequenced);
        public void SendSnapshot(byte[] data, int length) => SendBytes(MinigameSnapshotMessage, data, length,
            length > UnreliableLimit ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);

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
            clientScenes.Clear();
            AcceptingPlayers = true;
            messaging = connection.Manager.CustomMessagingManager;
            if (connection.Manager.IsServer)
            {
                messaging.RegisterNamedMessageHandler(ReadyMessage, HandleReadyMessage);
                messaging.RegisterNamedMessageHandler(HelloMessage, HandleHelloMessage);
                messaging.RegisterNamedMessageHandler(LoadedMessage, HandleLoadedMessage);
                messaging.RegisterNamedMessageHandler(InputMessage, HandleInputMessage);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshotMessage);
                messaging.RegisterNamedMessageHandler(CountdownMessage, HandleCountdownMessage);
                messaging.RegisterNamedMessageHandler(SceneMessage, HandleSceneMessage);
                messaging.RegisterNamedMessageHandler(StandingsMessage, HandleStandingsMessage);
                messaging.RegisterNamedMessageHandler(VoteMessage, HandleVoteMessage);
                messaging.RegisterNamedMessageHandler(HudMessage, HandleHudMessage);
                messaging.RegisterNamedMessageHandler(MinigameSnapshotMessage, HandleMinigameSnapshotMessage);
                messaging.RegisterNamedMessageHandler(EventMessage, HandleEventMessage);
            }
            MinigameNetwork.Current = this;
        }

        private void HandleSessionStopped()
        {
            Unregister();
            clientSlots.Clear();
            clientScenes.Clear();
            AcceptingPlayers = true;
            ResetSlots();
            SlotsChanged?.Invoke();
        }

        private void Unregister()
        {
            if (MinigameNetwork.Current == this) MinigameNetwork.Current = null;
            if (messaging == null) return;
            foreach (var message in HostMessages) messaging.UnregisterNamedMessageHandler(message);
            foreach (var message in ClientMessages) messaging.UnregisterNamedMessageHandler(message);
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
            SendLobbySnapshot();
        }

        // In the lobby the place is freed; during a match the bot takes over (score kept).
        private void HandleRemoteDisconnected(ulong clientId)
        {
            clientScenes.Remove(clientId);
            if (!clientSlots.TryGetValue(clientId, out int slotIndex)) return;
            clientSlots.Remove(clientId);
            if (AcceptingPlayers) players.RemovePlayer(slotIndex);
            else players.ReplaceWithAI(slotIndex, new AIInput());
        }

        private void HandleSlotChanged(PlayerSlot slot)
        {
            if (IsActive && IsHost) SendLobbySnapshot();
        }

        private void HandleReadyMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool ready);
            if (clientSlots.TryGetValue(sender, out int slotIndex)) players.SetReady(slotIndex, ready);
        }

        private void HandleHelloMessage(ulong sender, FastBufferReader reader) => SendLobbySnapshot();

        private void HandleLoadedMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string sceneName);
            clientScenes[sender] = sceneName;
        }

        private void HandleInputMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out byte pressed);
            reader.ReadValueSafe(out byte held);
            if (clientSlots.TryGetValue(sender, out int slotIndex) && players.Slots[slotIndex].Input is NetworkInput input)
                input.Apply(new Vector2(x, y), pressed, held);
        }

        private void SendLobbySnapshot()
        {
            if (!CollectRemoteClients()) return;
            using var writer = new FastBufferWriter(256, Allocator.Temp, 4096);
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++)
                LobbySlotInfo.From(players.Slots[i], OwnerOf(i)).Write(writer);
            messaging.SendNamedMessage(SnapshotMessage, remoteClients, writer);
        }

        private void SendBytes(string message, byte[] data, int length, NetworkDelivery delivery)
        {
            if (!IsActive || !IsHost || !CollectRemoteClients()) return;
            using var writer = new FastBufferWriter(length + 8, Allocator.Temp);
            writer.WriteValueSafe(length);
            writer.WriteBytesSafe(data, length);
            messaging.SendNamedMessage(message, remoteClients, writer, delivery);
        }

        private bool CollectRemoteClients()
        {
            remoteClients.Clear();
            foreach (ulong id in connection.Connections)
                if (id != connection.LocalClientId) remoteClients.Add(id);
            return remoteClients.Count > 0;
        }

        // ---------- Client ----------

        // The lobby cards, and a mirror of the host's players so local code (names, ids) matches.
        private void HandleSnapshotMessage(ulong sender, FastBufferReader reader)
        {
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++)
            {
                var info = LobbySlotInfo.Read(reader);
                slots[i] = info;
                ulong owner = info.Owner;
                players.MirrorSlot(i, info.PlayerId, info.Occupied ? info.Type : PlayerType.Empty, info.State,
                    info.Name, info.Ready, info.Host, () => new NetworkInput(owner));
            }
            SlotsChanged?.Invoke();
        }

        private void HandleCountdownMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int secondsLeft);
            CountdownReceived?.Invoke(secondsLeft);
        }

        private void HandleSceneMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string sceneName);
            reader.ReadValueSafe(out byte gameState);
            SceneRequested?.Invoke(sceneName, gameState);
        }

        private void HandleStandingsMessage(ulong sender, FastBufferReader reader)
        {
            Standings.Read(reader);
            StandingsReceived?.Invoke();
        }

        private void HandleVoteMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            reader.ReadValueSafe(out float seconds);
            VoteChoices.Clear();
            for (int i = 0; i < count && i < 3; i++)
            {
                reader.ReadValueSafe(out int index);
                VoteChoices.Add(index);
            }
            VoteProgressReceived?.Invoke(count, seconds);
        }

        private void HandleHudMessage(ulong sender, FastBufferReader reader) => RaiseBytes(reader, HudReceived);
        private void HandleMinigameSnapshotMessage(ulong sender, FastBufferReader reader) => RaiseBytes(reader, SnapshotReceived);
        private void HandleEventMessage(ulong sender, FastBufferReader reader) => RaiseBytes(reader, EventReceived);

        private void RaiseBytes(FastBufferReader reader, Action<byte[], int> handler)
        {
            reader.ReadValueSafe(out int length);
            if (receiveBuffer.Length < length) receiveBuffer = new byte[Mathf.NextPowerOfTwo(length)];
            reader.ReadBytesSafe(ref receiveBuffer, length);
            handler?.Invoke(receiveBuffer, length);
        }

        private void ResetSlots()
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LobbySlotInfo { Name = string.Empty, Owner = LobbySlotInfo.NoOwner };
        }
    }
}
