using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace UnCredibles.Networking
{
    // Connection lifetime only; menu, lobby and test scene supply their own UI.
    public sealed class RelayConnection : MonoBehaviour
    {
        private NetworkManager manager;
        private UnityTransport transport;
        private bool busy;
        private bool destroyed;
        private string status = "Crea una partida o introduce un codigo para unirte.";
        private string joinCode = "";

        private bool connecting;
        private bool closing;
        private float connectionDeadline;
        private const int MaxConnections = 4;

        public bool CanConnect => !busy && !connecting && !closing && manager != null &&
            !manager.IsListening && !manager.ShutdownInProgress;

        private void Awake()
        {
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
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = manager.ConnectedClientsIds.Count < MaxConnections;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "Sala llena (4 conexiones).";
        }

        private void Update()
        {
            if (closing && !manager.IsListening && !manager.ShutdownInProgress) closing = false;
            if (connecting && Time.realtimeSinceStartup >= connectionDeadline)
                StopConnection("No se recibio respuesta del Host. Puedes volver a intentarlo.");
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
                        return "Codigo incorrecto o partida cerrada. Comprueba el codigo con el Host.";
                    case RelayExceptionReason.InvalidArgument:
                    case RelayExceptionReason.InvalidRequest:
                        return "Solicitud no valida. Revisa el codigo e intentalo de nuevo.";
                    case RelayExceptionReason.Forbidden:
                    case RelayExceptionReason.Conflict:
                        return "Relay no permite entrar: la sala puede estar llena o el acceso no esta permitido.";
                    case RelayExceptionReason.RateLimited:
                        return "Demasiados intentos. Espera un momento y vuelve a intentarlo.";
                    case RelayExceptionReason.NetworkError:
                    case RelayExceptionReason.ServiceUnavailable:
                        return "Relay no esta disponible. Comprueba internet y vuelve a intentarlo.";
                }
            }
            return "No se pudo completar la conexion: " + exception.Message;
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
            status = "Conectando con Unity Services...";
            try
            {
                await InitializeServices();
                if (destroyed) return;

                status = "Creando la conexion Relay...";
                // Three remote connections plus the host: four players in total.
                var allocation = await RelayService.Instance.CreateAllocationAsync(3);
                if (destroyed) return;
                var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (destroyed) return;
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                if (!manager.StartHost()) throw new InvalidOperationException("No se pudo iniciar el Host.");
                joinCode = code;
                status = "Host iniciado. Codigo generado.";
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
            status = "Buscando la partida...";
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
                status = "Conectando con el Host...";
                if (!manager.StartClient()) throw new InvalidOperationException("No se pudo iniciar el cliente.");
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
                status = $"Host activo. Conexiones: {manager.ConnectedClientsIds.Count} (incluido Host).";
            else if (clientId == manager.LocalClientId)
            {
                connecting = false;
                status = "Conectado al Host.";
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (closing || destroyed) return;
            if (manager.IsHost)
                status = "Un cliente se ha desconectado.";
            else if (clientId == manager.LocalClientId)
            {
                string reason = manager.DisconnectReason;
                StopConnection(string.IsNullOrWhiteSpace(reason)
                    ? "Se perdio la conexion o el Host cerro la partida. Puedes volver a unirte."
                    : "Conexion cerrada: " + reason);
            }
        }

        private void HandleTransportFailure()
        {
            if (!closing) StopConnection("La conexion Relay ha fallado. Puedes volver a intentarlo.");
        }

        private void HandleServerStopped(bool wasHost)
        {
            joinCode = "";
            if (!closing) status = "Host detenido.";
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
                StopConnection("Desconectado. Puedes crear o unirte a otra partida.");
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (manager == null) return;
            manager.OnTransportFailure -= HandleTransportFailure;
            manager.OnServerStopped -= HandleServerStopped;
            manager.OnClientConnectedCallback -= HandleClientConnected;
            manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            manager.ConnectionApprovalCallback = null;
            if (manager.IsListening) manager.Shutdown();
        }
    }
}

