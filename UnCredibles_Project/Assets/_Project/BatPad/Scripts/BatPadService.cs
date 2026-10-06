using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.BatPad
{
    // Lives in 01_Core: keeps the BatPad room and its phones alive across every scene.
    // Socket awaits resume on Unity's main thread; messages are queued and applied in Update, which runs
    // before gameplay so a phone press is visible to WasPressed during the same frame.
    [DefaultExecutionOrder(-100)]
    public sealed class BatPadService : MonoBehaviour
    {
        [Tooltip("Public address of the BatPad Worker. The controller web is served from the same address.")]
        [SerializeField] private string serverUrl = "https://batpad.batpad-studio.workers.dev";

        private readonly Queue<string> incoming = new Queue<string>();
        private readonly Queue<string> outgoing = new Queue<string>();
        private const float RetrySeconds = 3f;

        private readonly List<BatPadInput> phones = new List<BatPadInput>();
        // Phones that dropped: if they come back with the same ClientId they get the same BatPadInput,
        // so their slot, character and score stay theirs.
        private readonly List<BatPadInput> departed = new List<BatPadInput>();
        private ClientWebSocket socket;
        private bool sending;
        private bool wantsRoom;
        private string roomRequest; // code asked for the current room; null lets the server pick one
        private bool reopenAfterClose;
        // Code and key of the last room, so a dropped connection gets the same room (and phones) back.
        private string resumeCode;
        private string resumeKey;
        private int retriesWithoutRoom;

        // Both null until the server has opened the room.
        public string RoomCode { get; private set; }
        public string ControllerUrl { get; private set; }
        public IReadOnlyList<BatPadInput> Phones => phones;

        public event Action RoomChanged;
        public event Action<BatPadInput> PhoneConnected;
        public event Action<BatPadInput> PhoneDisconnected;
        public event Action<BatPadInput> PhoneReconnected;

        [Serializable]
        private sealed class Message
        {
            public string t;
            public string room;
            public string key;
            public int player;
            public string b;
            public bool down;
            public int x;
            public int y;
            public string id;
        }

        // Safe to call every time the lobby opens: keeps the open room unless a different code is asked for.
        // Null lets the server pick a random code; online the lobby passes the Relay join code.
        // Once asked for, the room is kept alive (reconnecting if needed) until the game closes.
        public void OpenRoom(string roomCode = null)
        {
            wantsRoom = true;
            if (roomCode == roomRequest)
            {
                if (socket == null) Connect(); // a retry may already be scheduled; connecting now is fine
                return;
            }

            // A different code is a different room: the old phones scan the new QR.
            roomRequest = roomCode;
            ForgetRoom();
            if (socket == null) Connect();
            else
            {
                reopenAfterClose = true;
                CloseRoom();
            }
        }

        // Tells the phone which lobby slot it plays in (NoSlot = not joined).
        public void AssignSlot(BatPadInput phone, int slotIndex)
        {
            phone.SlotIndex = slotIndex;
            if (!phone.IsConnected) return;
            Send($"{{\"t\":\"send\",\"player\":{phone.PhoneId},\"msg\":{{\"t\":\"slot\",\"slot\":{slotIndex}}}}}");
        }

        public void ClearSlots()
        {
            foreach (var phone in phones) AssignSlot(phone, BatPadInput.NoSlot);
        }

        private async void Connect()
        {
            CancelInvoke(nameof(Retry));
            var ws = new ClientWebSocket();
            socket = ws;
            string baseUrl = serverUrl.TrimEnd('/');
            string code = resumeCode ?? roomRequest;
            string query = code == null ? string.Empty : "?room=" + Uri.EscapeDataString(code);
            if (code != null && resumeKey != null) query += "&key=" + Uri.EscapeDataString(resumeKey);
            var hostUri = new Uri(baseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/host" + query);

            try
            {
                await ws.ConnectAsync(hostUri, CancellationToken.None);
                await ReceiveLoop(ws);
            }
            catch (Exception e)
            {
                if (this != null) Debug.LogWarning($"[BatPad] Connection lost: {e.Message}", this);
            }

            ws.Dispose();
            if (this != null && socket == ws) HandleRoomClosed();
        }

        private async Task ReceiveLoop(ClientWebSocket ws)
        {
            var buffer = new byte[1024];
            var text = new StringBuilder();

            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) return;

                text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                incoming.Enqueue(text.ToString());
                text.Clear();
            }
        }

        private void Update()
        {
            while (incoming.Count > 0) HandleMessage(incoming.Dequeue());
        }

        private void HandleMessage(string json)
        {
            var msg = JsonUtility.FromJson<Message>(json);
            switch (msg.t)
            {
                case "room":
                    RoomCode = resumeCode = msg.room;
                    resumeKey = msg.key;
                    retriesWithoutRoom = 0;
                    ControllerUrl = $"{serverUrl.TrimEnd('/')}/?room={msg.room}&key={msg.key}";
                    GUIUtility.systemCopyBuffer = ControllerUrl;
                    Debug.Log($"[BatPad] Room {msg.room} ready. Controller URL (copied to clipboard): {ControllerUrl}", this);
                    RoomChanged?.Invoke();
                    break;

                case "join":
                    HandleJoin(msg.player, msg.id);
                    break;

                case "leave":
                    if (TryGetPhone(msg.player, out var gone)) Disconnect(gone);
                    break;

                case "btn":
                    if (TryGetPhone(msg.player, out var pressed) && TryMapButton(msg.b, out var action))
                        pressed.SetButton(action, msg.down);
                    break;

                case "stick":
                    if (TryGetPhone(msg.player, out var moved)) moved.SetMove(new Vector2(msg.x, msg.y) / 100f);
                    break;
            }
        }

        private void HandleJoin(int phoneId, string clientId)
        {
            // Same phone on a fresh connection (the server already dropped the old one).
            var phone = FindByClientId(phones, clientId);
            if (phone != null)
            {
                phone.PhoneId = phoneId;
                ResendSlot(phone);
                return;
            }

            phone = FindByClientId(departed, clientId);
            if (phone != null)
            {
                departed.Remove(phone);
                phone.PhoneId = phoneId;
                phone.IsConnected = true;
                phones.Add(phone);
                ResendSlot(phone);
                PhoneReconnected?.Invoke(phone);
                return;
            }

            phone = new BatPadInput(phoneId, clientId);
            phones.Add(phone);
            PhoneConnected?.Invoke(phone);
        }

        private static BatPadInput FindByClientId(List<BatPadInput> list, string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return null;
            foreach (var phone in list)
                if (phone.ClientId == clientId) return phone;
            return null;
        }

        private void ResendSlot(BatPadInput phone)
        {
            if (phone.SlotIndex != BatPadInput.NoSlot) AssignSlot(phone, phone.SlotIndex);
        }

        private static bool TryMapButton(string button, out PlayerAction action)
        {
            switch (button)
            {
                case "A": action = PlayerAction.Jump; return true;
                case "B": action = PlayerAction.Interact; return true;
                case "START": action = PlayerAction.Pause; return true;
                default: action = default; return false;
            }
        }

        private bool TryGetPhone(int phoneId, out BatPadInput phone)
        {
            foreach (var candidate in phones)
            {
                if (candidate.PhoneId != phoneId) continue;
                phone = candidate;
                return true;
            }
            phone = null;
            return false;
        }

        private void Disconnect(BatPadInput phone)
        {
            phones.Remove(phone);
            if (phone.ClientId.Length > 0) departed.Add(phone);
            phone.IsConnected = false;
            phone.ReleaseAll(); // no stuck buttons when a phone drops mid-press
            PhoneDisconnected?.Invoke(phone);
        }

        // The connection to the server ended. On a drop the server keeps the room and its phones for a while:
        // reconnecting with the same code and key gets them back (they arrive again as "join").
        private void HandleRoomClosed()
        {
            Debug.Log("[BatPad] Disconnected from the room", this);
            socket = null;
            RoomCode = null;
            ControllerUrl = null;
            incoming.Clear();
            outgoing.Clear();
            for (int i = phones.Count - 1; i >= 0; i--) Disconnect(phones[i]);

            if (reopenAfterClose)
            {
                reopenAfterClose = false;
                Connect();
            }
            else if (wantsRoom)
            {
                Invoke(nameof(Retry), RetrySeconds);
            }
            RoomChanged?.Invoke();
        }

        private void Retry()
        {
            if (socket != null) return;
            // The server no longer had our room (it expired or the code was taken): start a new one.
            if (resumeCode != null && retriesWithoutRoom >= 2) ForgetRoom();
            retriesWithoutRoom++;
            Connect();
        }

        // Next connection opens a brand new room; phones of the old one cannot come back.
        private void ForgetRoom()
        {
            resumeCode = null;
            resumeKey = null;
            retriesWithoutRoom = 0;
            departed.Clear();
        }

        private void CloseRoom()
        {
            // A clean close makes the server close the room and disconnect the phones.
            if (socket.State == WebSocketState.Open)
                _ = socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Host left", CancellationToken.None);
            else
                socket.Abort();
        }

        private void Send(string json)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            outgoing.Enqueue(json);
            if (!sending) PumpOutgoing(socket);
        }

        // ClientWebSocket allows a single pending send, so messages go out one after another.
        private async void PumpOutgoing(ClientWebSocket ws)
        {
            sending = true;
            try
            {
                while (outgoing.Count > 0 && ws.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(outgoing.Dequeue());
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (this != null) Debug.LogWarning($"[BatPad] Send failed: {e.Message}", this);
            }
            finally
            {
                sending = false;
            }
        }

        private void OnDestroy()
        {
            wantsRoom = false;
            CancelInvoke();
            if (socket != null) CloseRoom(); // clean close: the server closes the room right away
        }
    }
}
