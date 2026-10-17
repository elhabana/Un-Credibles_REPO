using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Networking
{
    // Connection lifetime only; menu, lobby and test scene supply their own UI.
    public sealed class RelayConnection : MonoBehaviour
    {
        private NetworkManager manager;
        private UnityTransport transport;
        private bool busy;
        private bool destroyed;
        private string status = string.Empty;
        private string joinCode = "";

        private bool connecting;
        private bool closing;
        private float connectionDeadline;
        private const int MaxConnections = 4;

        public bool CanConnect => !busy && !connecting && !closing && manager != null &&
            !manager.IsListening && !manager.ShutdownInProgress;

        private void Awake()
        {
            status = L10n.Get(L10n.Online, "status.idle");
            transport = gameObject.AddComponent<UnityTransport>();
            manager = gameObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = ApproveConnection;
            manager.OnTransportFailure += HandleTransportFailure;
            manager.OnServerStopped += HandleServerStopped;
            manager.OnClientConnectedCallback += HandleClientConnected;
            manager.OnClientDisconnectCallback += HandleClientDisconnected;
            manager.OnClientStarted += HandleSessionStarted;
            manager.OnClientStopped += HandleSessionStopped;
        }

        // Game rules (lobby full, match already running) decide on top of the connection limit.
        // Returns null to accept, or the reason shown to the rejected player.
        public Func<string> RejectJoin { get; set; }

        public NetworkManager Manager => manager;

        // Raised on host and clients when this machine's session starts or ends.
        public event Action SessionStarted;
        public event Action SessionStopped;
        // Host only: another machine joined or left.
        public event Action<ulong> RemoteClientConnected;
        public event Action<ulong> RemoteClientDisconnected;

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string reason = null;
            if (request.ClientNetworkId != NetworkManager.ServerClientId)
                reason = manager.ConnectedClientsIds.Count >= MaxConnections ? L10n.Token(L10n.Online, "reason.room_full_4") : RejectJoin?.Invoke();
            response.Approved = reason == null;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason ?? "";
        }

        // Host only: removes a player from the room, showing them the reason.
        public void Kick(ulong clientId, string reason)
        {
            if (manager.IsServer && clientId != manager.LocalClientId) manager.DisconnectClient(clientId, reason);
        }

        private void HandleSessionStarted() => SessionStarted?.Invoke();
        private void HandleSessionStopped(bool wasHost) => SessionStopped?.Invoke();

        private void Update()
        {
            if (closing && !manager.IsListening && !manager.ShutdownInProgress) closing = false;
            if (connecting && Time.realtimeSinceStartup >= connectionDeadline)
                StopConnection(L10n.Get(L10n.Online, "error.no_response"));
        }

        private void StopConnection(string message)
        {
            closing = true;
            connecting = false;
            joinCode = "";
            status = message;
            manager.Shutdown();
        }

        private static string ExplainFailure(Exception exception)
        {
            if (exception is RelayServiceException relay)
            {
                switch (relay.Reason)
                {
                    case RelayExceptionReason.JoinCodeNotFound:
                    case RelayExceptionReason.AllocationNotFound:
                    case RelayExceptionReason.EntityNotFound:
                        return L10n.Get(L10n.Online, "error.wrong_code");
                    case RelayExceptionReason.InvalidArgument:
                    case RelayExceptionReason.InvalidRequest:
                        return L10n.Get(L10n.Online, "error.invalid_request");
                    case RelayExceptionReason.Forbidden:
                    case RelayExceptionReason.Conflict:
                        return L10n.Get(L10n.Online, "error.forbidden");
                    case RelayExceptionReason.RateLimited:
                        return L10n.Get(L10n.Online, "error.rate_limited");
                    case RelayExceptionReason.NetworkError:
                    case RelayExceptionReason.ServiceUnavailable:
                        return L10n.Get(L10n.Online, "error.unavailable");
                }
            }
            return L10n.Format(L10n.Online, "error.generic", exception.Message);
        }

        private async Task InitializeServices()
        {
            // Separate anonymous identities for an Editor host and a build on the same PC.
            var options = new InitializationOptions();
            options.SetProfile(Application.isEditor ? "relay-editor" : "relay-build");
            await UnityServices.InitializeAsync(options);
            if (destroyed) return;
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async void CreateHost()
        {
            if (!CanConnect) return;
            busy = true;
            joinCode = "";
            status = L10n.Get(L10n.Online, "status.connecting_services");
            try
            {
                await InitializeServices();
                if (destroyed) return;

                status = L10n.Get(L10n.Online, "status.creating_relay");
                // Three remote connections plus the host: four players in total.
                var allocation = await RelayService.Instance.CreateAllocationAsync(3);
                if (destroyed) return;
                var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (destroyed) return;
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                if (!manager.StartHost()) throw new InvalidOperationException(L10n.Get(L10n.Online, "error.start_host"));
                joinCode = code;
                status = L10n.Get(L10n.Online, "status.host_started");
            }
            catch (Exception exception)
            {
                if (destroyed) return;
                StopConnection(ExplainFailure(exception));
                Debug.LogWarning(status, this);
            }
            finally { busy = false; }
        }

        public async void JoinHost(string codeToJoin)
        {
            if (!CanConnect) return;
            var code = codeToJoin.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return;
            busy = true;
            joinCode = "";
            status = L10n.Get(L10n.Online, "status.searching");
            try
            {
                await InitializeServices();
                if (destroyed) return;
                var allocation = await RelayService.Instance.JoinAllocationAsync(code);
                if (destroyed) return;
                joinCode = code;
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                connecting = true;
                connectionDeadline = Time.realtimeSinceStartup + 20f;
                status = L10n.Get(L10n.Online, "status.connecting_host");
                if (!manager.StartClient()) throw new InvalidOperationException(L10n.Get(L10n.Online, "error.start_client"));
            }
            catch (Exception exception)
            {
                if (destroyed) return;
                StopConnection(ExplainFailure(exception));
                Debug.LogWarning(status, this);
            }
            finally { busy = false; }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (manager.IsHost)
            {
                status = L10n.Format(L10n.Online, "status.host_active", manager.ConnectedClientsIds.Count);
                if (clientId != manager.LocalClientId) RemoteClientConnected?.Invoke(clientId);
            }
            else if (clientId == manager.LocalClientId)
            {
                connecting = false;
                status = L10n.Get(L10n.Online, "status.connected");
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (closing || destroyed) return;
            if (manager.IsHost)
            {
                status = L10n.Get(L10n.Online, "status.client_left");
                if (clientId != manager.LocalClientId) RemoteClientDisconnected?.Invoke(clientId);
            }
            else if (clientId == manager.LocalClientId)
            {
                string reason = manager.DisconnectReason;
                StopConnection(string.IsNullOrWhiteSpace(reason)
                    ? L10n.Get(L10n.Online, "error.lost")
                    : L10n.Format(L10n.Online, "error.closed_reason", L10n.Resolve(reason)));
            }
        }

        private void HandleTransportFailure()
        {
            if (!closing) StopConnection(L10n.Get(L10n.Online, "error.transport"));
        }

        private void HandleServerStopped(bool wasHost)
        {
            joinCode = "";
            if (!closing) status = L10n.Get(L10n.Online, "status.host_stopped");
        }

        public string Status => status;
        public string JoinCode => joinCode;
        public bool IsConnected => manager != null && manager.IsConnectedClient && !closing;
        public bool IsHost => manager != null && manager.IsHost;
        public ulong LocalClientId => manager.LocalClientId;
        public System.Collections.Generic.IReadOnlyList<ulong> Connections => manager.ConnectedClientsIds;
        public void Disconnect()
        {
            // Keep a useful failure reason when Core returns a disconnected client to the menu.
            if (manager.IsListening || connecting || busy)
                StopConnection(L10n.Get(L10n.Online, "status.disconnected"));
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (manager == null) return;
            manager.OnTransportFailure -= HandleTransportFailure;
            manager.OnServerStopped -= HandleServerStopped;
            manager.OnClientConnectedCallback -= HandleClientConnected;
            manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            manager.OnClientStarted -= HandleSessionStarted;
            manager.OnClientStopped -= HandleSessionStopped;
            manager.ConnectionApprovalCallback = null;
            if (manager.IsListening) manager.Shutdown();
        }
    }
}

