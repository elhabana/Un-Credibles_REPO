using System;
using System.Collections;
using System.Collections.Generic;
using UnCredibles.BatPad;
using UnCredibles.Core;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnCredibles.Localization;

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
        // A player whose phone or gamepad drops in the lobby keeps the slot this long (screen lock, app switch,
        // Wi-Fi blip, controller out of battery).
        private const float AwaySeconds = 10f;

        private readonly Dictionary<InputDevice, int> deviceSlots = new Dictionary<InputDevice, int>();
        private readonly int[] joinFrame = new int[PlayerRegistry.MaxPlayers];
        private readonly Dictionary<IPlayerInput, float> awayTimeouts = new Dictionary<IPlayerInput, float>();
        private readonly List<IPlayerInput> expired = new List<IPlayerInput>();
        private readonly LobbySlotInfo[] slotInfos = new LobbySlotInfo[PlayerRegistry.MaxPlayers];
        private CoreRoot core;
        private BatPadService batPad;
        private OnlineSession room;
        private IDisposable joinListener;
        private Coroutine countdown;
        private bool starting;

        // Customisation: row picked on each card (helmet / outfit / boots), last stick of each
        // local player (to act once per push) and whether the slot already got its first outfit.
        private const float StickPush = .6f;
        private readonly int[] partRow = new int[PlayerRegistry.MaxPlayers];
        private readonly Vector2[] lastMove = new Vector2[PlayerRegistry.MaxPlayers];
        private readonly bool[] dressed = new bool[PlayerRegistry.MaxPlayers];
        private bool sentSavedLoadout;

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

        // Wearable parts the lobby lets the players pick (given by the garage figures).
        public CharacterPartCatalog PartCatalog { get; set; }
        public event Action CustomizationChanged; // the highlighted row of a card moved

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

            WaitForAwayPlayers();
            if (UsesPhones) batPad.OpenRoom(PhoneRoomCode);

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
            Players = null;
            deviceSlots.Clear();
            awayTimeouts.Clear();
            expired.Clear();
        }

        // Lobby input of the players of this machine: Jump toggles Ready, Pause un-readies or leaves,
        // up/down picks helmet, outfit or boots and left/right changes it.
        // Remote players' ready and outfit arrive through the online session instead.
        private void Update()
        {
            if (Players == null || starting) return;
            if (!IsAuthority)
            {
                SendSavedLoadoutOnce();
                return;
            }
            DressNewPlayers();
            RemoveExpiredAway();
            if (UsesPhones) JoinPressingPhones();
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || slot.Input == null || slot.Input is NetworkInput) continue;
                if (joinFrame[slot.SlotIndex] == Time.frameCount) continue; // the join press is not a ready press

                ReadCustomizeStick(slot.SlotIndex, slot.Input.Move);
                if (slot.Input.WasPressed(PlayerAction.Jump)) Players.SetReady(slot.SlotIndex, !slot.IsReady);
                else if (slot.Input.WasPressed(PlayerAction.Crouch)) Players.SetReady(slot.SlotIndex, false);
                else if (slot.Input.WasPressed(PlayerAction.Pause))
                {
                    // The phone shows Pause as an exit button: it leaves at once.
                    // Keyboard and gamepad keep the two steps (un-ready first, then leave).
                    if (slot.IsReady && !(slot.Input is BatPadInput)) Players.SetReady(slot.SlotIndex, false);
                    else
                    {
                        if (slot.Input is BatPadInput leaving) batPad.SendBye(leaving); // "thanks for playing" on the phone
                        RemovePlayer(slot.SlotIndex);
                    }
                }
            }
        }

        // ---------- Customisation (helmet, outfit, boots) ----------

        public int SelectedPartRow(int slotIndex) => partRow[slotIndex];

        // Who may change a card: the authority for the players of its own machine and the bots,
        // an online client only for its own player.
        public bool CanCustomize(int slotIndex)
        {
            if (Players == null || starting || PartCatalog == null) return false;
            if (!IsAuthority)
                return room.Slots[slotIndex].Occupied && room.Slots[slotIndex].Owner == LocalClientId;
            var slot = Players.Slots[slotIndex];
            return slot.IsOccupied && !(slot.Input is NetworkInput);
        }

        public void StepPart(int slotIndex, CharacterPartSlot partSlot, int step)
        {
            if (!CanCustomize(slotIndex)) return;
            partRow[slotIndex] = (int)partSlot;
            var current = Slots[slotIndex].Loadout;
            var next = current.With(partSlot, PartCatalog.Step(partSlot, current.Get(partSlot), step));
            CustomizationChanged?.Invoke();
            if (IsAuthority)
            {
                Players.SetLoadout(slotIndex, next);
                if (Players.Slots[slotIndex].IsHost) CharacterLoadoutStore.Save(next); // this machine's own player
            }
            else
            {
                room.SendLoadout(next);
                CharacterLoadoutStore.Save(next);
            }
        }

        public void MovePartRow(int slotIndex, int delta)
        {
            if (!CanCustomize(slotIndex)) return;
            int rows = Enum.GetValues(typeof(CharacterPartSlot)).Length;
            partRow[slotIndex] = ((partRow[slotIndex] + delta) % rows + rows) % rows;
            CustomizationChanged?.Invoke();
        }

        // Once per push of the stick / arrow keys (holding does not repeat).
        private void ReadCustomizeStick(int slotIndex, Vector2 move)
        {
            var last = lastMove[slotIndex];
            lastMove[slotIndex] = move;
            if (move.y > StickPush && last.y <= StickPush) MovePartRow(slotIndex, -1);
            else if (move.y < -StickPush && last.y >= -StickPush) MovePartRow(slotIndex, 1);
            else if (move.x > StickPush && last.x <= StickPush) StepPart(slotIndex, (CharacterPartSlot)partRow[slotIndex], 1);
            else if (move.x < -StickPush && last.x >= -StickPush) StepPart(slotIndex, (CharacterPartSlot)partRow[slotIndex], -1);
        }

        // New bots get a random outfit; this machine's own player wears the one it saved.
        private void DressNewPlayers()
        {
            if (PartCatalog == null) return;
            foreach (var slot in Players.Slots)
            {
                int index = slot.SlotIndex;
                if (!slot.IsOccupied)
                {
                    dressed[index] = false;
                    partRow[index] = 0;
                    continue;
                }
                if (dressed[index] || !slot.Loadout.Equals(default(CharacterLoadout)))
                {
                    dressed[index] = true;
                    continue;
                }
                dressed[index] = true;
                if (slot.IsAI) Players.SetLoadout(index, PartCatalog.RandomLoadout());
                else if (slot.IsHost) Players.SetLoadout(index, CharacterLoadoutStore.Load());
            }
        }

        // Online client: as soon as its player is in the room, it wears the outfit saved here.
        private void SendSavedLoadoutOnce()
        {
            if (sentSavedLoadout) return;
            int mine = MySlotIndex();
            if (mine < 0) return;
            sentSavedLoadout = true;
            var saved = CharacterLoadoutStore.Load();
            if (!saved.Equals(default(CharacterLoadout)) && room.Slots[mine].Loadout.Equals(default(CharacterLoadout)))
                room.SendLoadout(saved);
        }

        private int MySlotIndex()
        {
            var slots = room.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Occupied && slots[i].Owner == LocalClientId) return i;
            return -1;
        }

        // Arrow keys / WASD / d-pad of an online client move its own outfit.
        private static bool TryDirection(InputControl control, out int dx, out int dy)
        {
            dx = dy = 0;
            string name = control.name;
            if (control.device is Keyboard)
            {
                if (name == "leftArrow" || name == "a") dx = -1;
                else if (name == "rightArrow" || name == "d") dx = 1;
                else if (name == "upArrow" || name == "w") dy = 1;
                else if (name == "downArrow" || name == "s") dy = -1;
            }
            else if (control.device is Gamepad && control.parent != null && control.parent.name == "dpad")
            {
                if (name == "left") dx = -1;
                else if (name == "right") dx = 1;
                else if (name == "up") dy = 1;
                else if (name == "down") dy = -1;
            }
            return dx != 0 || dy != 0;
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
                room.Kick(slotIndex, L10n.Token(L10n.Online, "reason.kicked"));
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
            if (TryDirection(control, out int dx, out int dy))
            {
                int mine = MySlotIndex();
                if (mine < 0) return;
                if (dy != 0) MovePartRow(mine, -dy);
                else StepPart(mine, (CharacterPartSlot)partRow[mine], dx);
                return;
            }
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

        // CoreRoot marks a slot Disconnected when its phone or gamepad drops; the player gets a few seconds
        // to come back. Called for every slot change, so it also sees them come back.
        private void TrackAway(PlayerSlot slot)
        {
            if (slot.Input == null) return;
            if (slot.IsConnected || !slot.IsOccupied || slot.IsAI)
            {
                awayTimeouts.Remove(slot.Input);
                return;
            }
            if (awayTimeouts.ContainsKey(slot.Input)) return;
            awayTimeouts[slot.Input] = Time.unscaledTime + AwaySeconds;
            Players.SetReady(slot.SlotIndex, false); // no countdown without them
        }

        // Players who dropped during the match get the same time to come back once the lobby opens.
        private void WaitForAwayPlayers()
        {
            if (!IsAuthority) return; // an online client only mirrors the host's slots
            foreach (var slot in Players.Slots) TrackAway(slot);
        }

        private void RemoveExpiredAway()
        {
            if (awayTimeouts.Count == 0) return;
            expired.Clear();
            foreach (var pair in awayTimeouts)
                if (Time.unscaledTime >= pair.Value) expired.Add(pair.Key);

            foreach (var controller in expired)
            {
                awayTimeouts.Remove(controller);
                var slot = FindSlot(controller);
                if (slot != null && !slot.IsConnected) RemovePlayer(slot.SlotIndex);
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
            TrackAway(slot);
            // A gamepad that took its slot back from a bot comes with a fresh input: map the device again.
            if (slot.Input is DeviceInput { Device: not null } device) deviceSlots[device.Device] = slot.SlotIndex;
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
            if (everyoneReady && !canStart) Notice?.Invoke(L10n.Get(L10n.UI, "lobby.need_two"));
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
            yield return new WaitForSeconds(GaragePlayers.DepartureSeconds + .4f);
            if (core.StartMatch(roundsPerMatch)) yield break;

            // The match could not start: back to the lobby.
            starting = false;
            if (IsOnline) room.AcceptingPlayers = true;
            Tick(-1);
            CountdownCancelled?.Invoke();
            UnreadyHumans();
            Notice?.Invoke(L10n.Get(L10n.UI, "lobby.start_failed"));
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
                if (slot.Input is DeviceInput { Device: not null } device) deviceSlots[device.Device] = slot.SlotIndex;
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
