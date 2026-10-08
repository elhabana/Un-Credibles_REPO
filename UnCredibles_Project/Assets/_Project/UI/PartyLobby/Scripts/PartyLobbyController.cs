using System;
using System.Collections;
using System.Collections.Generic;
using UnCredibles.BatPad;
using UnCredibles.Core;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace UnCredibles.UI.PartyLobby
{
    // Lobby rules shared by Singleplayer and Multiplayer: joining, AI, ready and the start countdown.
    // The authority (offline, or the host online) edits the PlayerRegistry; an online client only
    // shows the host's snapshot and asks to be ready. Views render Slots and forward clicks here.
    public sealed class PartyLobbyController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int roundsPerMatch = 3;
        [SerializeField, Min(0)] private int countdownSeconds = 3;

        // A match needs at least two players in the room; bots count.
        private const int MinPlayers = 2;
        // A phone that drops in the lobby keeps its slot this long (screen lock, app switch, Wi-Fi blip).
        private const float PhoneReconnectSeconds = 10f;

        private readonly Dictionary<InputDevice, int> deviceSlots = new Dictionary<InputDevice, int>();
        private readonly int[] joinFrame = new int[PlayerRegistry.MaxPlayers];
        private readonly Dictionary<BatPadInput, float> phoneTimeouts = new Dictionary<BatPadInput, float>();
        private readonly List<BatPadInput> expiredPhones = new List<BatPadInput>();
        private readonly LobbySlotInfo[] slotInfos = new LobbySlotInfo[PlayerRegistry.MaxPlayers];
        private CoreRoot core;
        private BatPadService batPad;
        private OnlineSession room;
        private IDisposable joinListener;
        private Coroutine countdown;
        private bool starting;

        public PlayerRegistry Players { get; private set; }
        public BatPadService BatPad => batPad;
        public SessionMode Mode { get; private set; }
        public bool InvitesEnabled => false; // Invitations are shared through the room code.
        public bool IsCountingDown => countdown != null;

        // Offline, or the host online: owns the PlayerRegistry and the countdown.
        public bool IsAuthority => Mode == SessionMode.Local || core.Online.IsHost;
        public bool CanEditSlots => Players != null && IsAuthority && !starting;
        public bool IsOnline => Mode == SessionMode.Online;
        public string RoomCode => IsOnline ? core.Online.JoinCode : string.Empty;
        public ulong LocalClientId => IsOnline ? core.Online.LocalClientId : LobbySlotInfo.NoOwner;

        // What the 4 cards show, on every machine.
        public IReadOnlyList<LobbySlotInfo> Slots => IsAuthority ? slotInfos : room.Slots;

        // Phones are local players of this machine: always offline, and only on the host online
        // (remote players cannot scan the host's screen).
        public bool UsesPhones => Mode == SessionMode.Local || core.Online.IsHost;

        // Online, the BatPad room reuses the Relay join code so players only share one code.
        private string PhoneRoomCode => IsOnline ? core.Online.JoinCode : null;

        public event Action Initialized;
        public event Action SlotsChanged;
        public event Action<int> CountdownTick; // 3, 2, 1 ... 0 = START
        public event Action CountdownCancelled;
        public event Action<string> Notice;     // short message for the hint line

        private void OnEnable() => StartCoroutine(Initialize());

        private IEnumerator Initialize()
        {
            // Let views subscribe and discard the button that opened the lobby.
            yield return null;
            // CoreSceneLoader brings Core in when this scene is played directly.
            while (CoreRoot.Instance == null) yield return null;

            core = CoreRoot.Instance;
            batPad = core.BatPad;
            room = core.Room;
            Players = core.Players;
            Mode = core.GameFlow.Session;
            core.GameFlow.ChangeState(GameState.PartyLobby);

            if (IsAuthority)
            {
                if (IsOnline) room.AcceptingPlayers = true;
                RebuildDeviceMap();
                if (IsOnline) JoinOnlineHost();
                Players.SlotChanged += HandleSlotChanged;
                RefreshSlotInfos();
            }
            else
            {
                room.SlotsChanged += HandleRemoteSlots;
                room.CountdownReceived += HandleRemoteCountdown;
                room.RequestSnapshot();
            }
            joinListener = InputSystem.onAnyButtonPress.Call(HandleAnyButton);

            WaitForDisconnectedPhones();
            if (UsesPhones)
            {
                batPad.PhoneDisconnected += HandlePhoneDisconnected;
                batPad.PhoneReconnected += HandlePhoneReconnected;
                batPad.OpenRoom(PhoneRoomCode);
            }

            Initialized?.Invoke();
            EvaluateCountdown();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            countdown = null;
            starting = false;
            joinListener?.Dispose();
            joinListener = null;
            if (Players != null) Players.SlotChanged -= HandleSlotChanged;
            if (room != null)
            {
                room.SlotsChanged -= HandleRemoteSlots;
                room.CountdownReceived -= HandleRemoteCountdown;
            }
            if (batPad != null)
            {
                batPad.PhoneDisconnected -= HandlePhoneDisconnected;
                batPad.PhoneReconnected -= HandlePhoneReconnected;
            }
            Players = null;
            deviceSlots.Clear();
            phoneTimeouts.Clear();
            expiredPhones.Clear();
        }

        // Lobby input of the players of this machine: Jump toggles Ready, Pause un-readies or leaves.
        // Remote players' ready arrives through the online session instead.
        private void Update()
        {
            if (Players == null || starting || !IsAuthority) return;
            RemoveExpiredPhones();
            if (UsesPhones) JoinPressingPhones();
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || slot.Input == null || slot.Input is NetworkInput) continue;
                if (joinFrame[slot.SlotIndex] == Time.frameCount) continue; // the join press is not a ready press

                if (slot.Input.WasPressed(PlayerAction.Jump)) Players.SetReady(slot.SlotIndex, !slot.IsReady);
                else if (slot.Input.WasPressed(PlayerAction.Pause))
                {
                    if (slot.IsReady) Players.SetReady(slot.SlotIndex, false);
                    else RemovePlayer(slot.SlotIndex);
                }
            }
        }

        public bool AddAI(int slotIndex)
        {
            if (!CanEditSlots) return false;
            return Players.TryAddPlayerAt(slotIndex, PlayerType.AIPlayer, core.Input.CreateAIInput(), null, out _);
        }

        public bool RemovePlayer(int slotIndex)
        {
            if (!CanEditSlots) return false;
            // A remote player is kicked; the online session frees the slot when they disconnect.
            if (IsOnline && room.IsRemoteSlot(slotIndex))
            {
                room.Kick(slotIndex, "El Host te ha sacado de la sala.");
                return true;
            }

            var slot = Players.Slots[slotIndex];
            bool wasHost = slot.IsHost;
            if (slot.Input is DeviceInput device) deviceSlots.Remove(device.Device);
            var phone = slot.Input as BatPadInput;
            if (!Players.RemovePlayer(slotIndex)) return false;
            if (phone != null) batPad.AssignSlot(phone, BatPadInput.NoSlot);
            if (wasHost) AssignHost();
            return true;
        }

        public void InviteFriend(int slotIndex)
        {
            // Steam invites arrive in the Steam phase (see architecture guide, step 15).
            Debug.Log($"Invite for slot {slotIndex} is not available yet.", this);
        }

        public void BackToMainMenu()
        {
            if (starting) return;
            StopCountdown(false);
            deviceSlots.Clear();
            core.ReturnToMainMenu();
        }

        private void HandleAnyButton(InputControl control)
        {
            if (!IsAuthority)
            {
                HandleClientButton(control);
                return;
            }
            if (!CanEditSlots || deviceSlots.ContainsKey(control.device) || !IsJoinButton(control)) return;
            if (TryClaimHostSlot(control.device)) return;

            IPlayerInput input = control.device switch
            {
                Keyboard => core.Input.CreateKeyboardInput(),
                Gamepad gamepad => core.Input.CreateGamepadInput(gamepad),
                _ => null,
            };
            if (input == null) return;

            if (!Players.TryAddPlayer(PlayerType.LocalPlayer, input, null, out var slot))
            {
                input.Dispose(); // lobby full
                return;
            }
            deviceSlots[control.device] = slot.SlotIndex;
            joinFrame[slot.SlotIndex] = Time.frameCount;
            if (!HasHost()) Players.SetHost(slot.SlotIndex);
        }

        // Online host: they are in their room from the start, with whatever device they use.
        // The slot listens to every device until the first join press picks the real one.
        private void JoinOnlineHost()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && !(slot.Input is NetworkInput)) return; // back from a match

            if (!Players.TryAddPlayer(PlayerType.LocalPlayer, core.Input.CreateAnyDeviceInput(), null, out var hostSlot)) return;
            Players.SetHost(hostSlot.SlotIndex);
        }

        private bool TryClaimHostSlot(InputDevice device)
        {
            foreach (var slot in Players.Slots)
            {
                if (!(slot.Input is AnyDeviceInput)) continue;
                IPlayerInput input = device switch
                {
                    Keyboard => core.Input.CreateKeyboardInput(),
                    Gamepad gamepad => core.Input.CreateGamepadInput(gamepad),
                    _ => null,
                };
                if (input == null) return false;

                Players.ReplaceInput(slot.SlotIndex, input);
                deviceSlots[device] = slot.SlotIndex;
                joinFrame[slot.SlotIndex] = Time.frameCount;
                Players.SetReady(slot.SlotIndex, !slot.IsReady); // the press also counts as Ready
                return true;
            }
            return false;
        }

        // Online client: its slot exists as soon as it connects; SPACE / A toggles ready, ESC / B cancels.
        private void HandleClientButton(InputControl control)
        {
            if (starting) return;
            if (IsJoinButton(control)) room.SendReady(!IsMyPlayerReady());
            else if (IsCancelButton(control)) room.SendReady(false);
        }

        private bool IsMyPlayerReady()
        {
            foreach (var info in room.Slots)
                if (info.Occupied && info.Owner == LocalClientId) return info.Ready;
            return false;
        }

        // Phones join like gamepads: pressing A on a connected phone takes the first free slot.
        private void JoinPressingPhones()
        {
            foreach (var phone in batPad.Phones)
            {
                if (!phone.WasPressed(PlayerAction.Jump) || FindSlot(phone) != null) continue;
                if (!Players.TryAddPlayer(PlayerType.PrestoPadPlayer, phone, null, out var slot)) return; // lobby full

                joinFrame[slot.SlotIndex] = Time.frameCount;
                batPad.AssignSlot(phone, slot.SlotIndex);
                if (!HasHost()) Players.SetHost(slot.SlotIndex);
            }
        }

        // CoreRoot already marked the slot as Disconnected; the phone gets a few seconds to come back.
        private void HandlePhoneDisconnected(BatPadInput phone)
        {
            var slot = FindSlot(phone);
            if (slot == null) return;
            Players.SetReady(slot.SlotIndex, false); // no countdown without it
            phoneTimeouts[phone] = Time.unscaledTime + PhoneReconnectSeconds;
        }

        private void HandlePhoneReconnected(BatPadInput phone) => phoneTimeouts.Remove(phone);

        // Phones that dropped during the match get the same time to come back once the lobby opens.
        private void WaitForDisconnectedPhones()
        {
            foreach (var slot in Players.Slots)
                if (slot.Input is BatPadInput { IsConnected: false } phone)
                    phoneTimeouts[phone] = Time.unscaledTime + PhoneReconnectSeconds;
        }

        private void RemoveExpiredPhones()
        {
            if (phoneTimeouts.Count == 0) return;
            expiredPhones.Clear();
            foreach (var pair in phoneTimeouts)
                if (Time.unscaledTime >= pair.Value) expiredPhones.Add(pair.Key);

            foreach (var phone in expiredPhones)
            {
                phoneTimeouts.Remove(phone);
                var slot = FindSlot(phone);
                if (slot != null && !phone.IsConnected) RemovePlayer(slot.SlotIndex);
            }
        }

        private PlayerSlot FindSlot(IPlayerInput input)
        {
            foreach (var slot in Players.Slots)
                if (slot.Input == input) return slot;
            return null;
        }

        private static bool IsJoinButton(InputControl control) => control.device switch
        {
            Keyboard => control.name == "space" || control.name == "enter" || control.name == "numpadEnter",
            Gamepad => control.name == "buttonSouth" || control.name == "start",
            _ => false,
        };

        private static bool IsCancelButton(InputControl control) => control.device switch
        {
            Keyboard => control.name == "escape",
            Gamepad => control.name == "buttonEast" || control.name == "select",
            _ => false,
        };

        private void HandleSlotChanged(PlayerSlot slot)
        {
            RefreshSlotInfos();
            EvaluateCountdown();
        }

        private void RefreshSlotInfos()
        {
            for (int i = 0; i < slotInfos.Length; i++)
                slotInfos[i] = LobbySlotInfo.From(Players.Slots[i], IsOnline ? room.OwnerOf(i) : LobbySlotInfo.NoOwner);
            SlotsChanged?.Invoke();
        }

        private void HandleRemoteSlots() => SlotsChanged?.Invoke();

        private void HandleRemoteCountdown(int secondsLeft)
        {
            if (secondsLeft < 0) CountdownCancelled?.Invoke();
            else CountdownTick?.Invoke(secondsLeft);
            starting = secondsLeft == 0;
        }

        private void EvaluateCountdown()
        {
            if (starting || !IsAuthority) return;
            bool everyoneReady = Players.AllPlayersReady && HasHuman();
            bool canStart = everyoneReady && Players.OccupiedCount >= MinPlayers;
            if (everyoneReady && !canStart) Notice?.Invoke("Hacen falta al menos 2 jugadores. Puedes anadir una IA.");
            if (canStart && countdown == null) countdown = StartCoroutine(CountdownRoutine());
            else if (!canStart) StopCountdown(true);
        }

        private IEnumerator CountdownRoutine()
        {
            var wait = new WaitForSeconds(1f);
            for (int i = countdownSeconds; i > 0; i--)
            {
                Tick(i);
                yield return wait;
            }
            countdown = null;
            starting = true;
            if (IsOnline) room.AcceptingPlayers = false; // nobody joins once the match starts
            Tick(0);
            if (core.StartMatch(roundsPerMatch)) yield break;

            // The match could not start: back to the lobby.
            starting = false;
            if (IsOnline) room.AcceptingPlayers = true;
            Tick(-1);
            CountdownCancelled?.Invoke();
            UnreadyHumans();
            Notice?.Invoke("No se pudo empezar la partida.");
        }

        private void Tick(int secondsLeft)
        {
            if (secondsLeft >= 0) CountdownTick?.Invoke(secondsLeft);
            if (IsOnline) room.SendCountdown(secondsLeft);
        }

        private void StopCountdown(bool notify)
        {
            if (countdown == null) return;
            StopCoroutine(countdown);
            countdown = null;
            if (!notify) return;
            CountdownCancelled?.Invoke();
            if (IsOnline) room.SendCountdown(-1);
        }

        private void RebuildDeviceMap()
        {
            deviceSlots.Clear();
            foreach (var slot in Players.Slots)
                if (slot.Input is DeviceInput device) deviceSlots[device.Device] = slot.SlotIndex;
            UnreadyHumans(); // coming back from a match: humans confirm Ready again
            if (!HasHost()) AssignHost();
        }

        private void UnreadyHumans()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI) Players.SetReady(slot.SlotIndex, false);
        }

        // The crown goes to a player of this machine, never to a remote one.
        private void AssignHost()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && !(slot.Input is NetworkInput) && Players.SetHost(slot.SlotIndex)) return;
        }

        private bool HasHost()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsHost) return true;
            return false;
        }

        private bool HasHuman()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI) return true;
            return false;
        }
    }
}
