using System;
using System.Collections;
using System.Collections.Generic;
using UnCredibles.Core;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace UnCredibles.UI.PartyLobby
{
    // Lobby rules shared by Singleplayer and Multiplayer: joining, AI, ready and the start countdown.
    // Views only render the PlayerRegistry and forward clicks here.
    public sealed class PartyLobbyController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int roundsPerMatch = 3;
        [SerializeField, Min(0)] private int countdownSeconds = 3;
        [SerializeField, Range(1, PlayerRegistry.MaxPlayers)] private int minPlayers = 1;

        private readonly Dictionary<InputDevice, int> deviceSlots = new Dictionary<InputDevice, int>();
        private readonly int[] joinFrame = new int[PlayerRegistry.MaxPlayers];
        private CoreRoot core;
        private IDisposable joinListener;
        private Coroutine countdown;
        private bool starting;

        public PlayerRegistry Players { get; private set; }
        public SessionMode Mode { get; private set; }
        public bool InvitesEnabled => Mode == SessionMode.Online;
        public bool IsCountingDown => countdown != null;

        public event Action Initialized;
        public event Action<int> CountdownTick; // 3, 2, 1 ... 0 = START
        public event Action CountdownCancelled;

        private IEnumerator Start()
        {
            // CoreSceneLoader brings Core in when this scene is played directly.
            while (CoreRoot.Instance == null) yield return null;

            core = CoreRoot.Instance;
            Players = core.Players;
            Mode = core.GameFlow.Session;
            core.GameFlow.ChangeState(GameState.PartyLobby);

            RebuildDeviceMap();
            Players.SlotChanged += HandleSlotChanged;
            joinListener = InputSystem.onAnyButtonPress.Call(HandleAnyButton);
            Initialized?.Invoke();
            EvaluateCountdown();
        }

        private void OnDestroy()
        {
            joinListener?.Dispose();
            if (Players != null) Players.SlotChanged -= HandleSlotChanged;
        }

        // Lobby input of players already joined: Jump toggles Ready, Pause un-readies or leaves.
        private void Update()
        {
            if (Players == null || starting) return;
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || slot.Input == null) continue;
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
            if (!CanEdit()) return false;
            return Players.TryAddPlayerAt(slotIndex, PlayerType.AIPlayer, core.Input.CreateAIInput(), null, out _);
        }

        public bool RemovePlayer(int slotIndex)
        {
            if (!CanEdit()) return false;
            var slot = Players.Slots[slotIndex];
            bool wasHost = slot.IsHost;
            if (slot.Input is DeviceInput device) deviceSlots.Remove(device.Device);
            if (!Players.RemovePlayer(slotIndex)) return false;
            if (wasHost) AssignHost();
            return true;
        }

        public void InviteFriend(int slotIndex)
        {
            // Steam invites arrive in the Multiplayer phases (see architecture guide, steps 13-15).
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
            if (!CanEdit() || deviceSlots.ContainsKey(control.device) || !IsJoinButton(control)) return;

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

        private static bool IsJoinButton(InputControl control) => control.device switch
        {
            Keyboard => control.name == "space" || control.name == "enter" || control.name == "numpadEnter",
            Gamepad => control.name == "buttonSouth" || control.name == "start",
            _ => false,
        };

        private void HandleSlotChanged(PlayerSlot slot) => EvaluateCountdown();

        private void EvaluateCountdown()
        {
            if (starting) return;
            bool canStart = Players.OccupiedCount >= minPlayers && Players.AllPlayersReady && HasHuman();
            if (canStart && countdown == null) countdown = StartCoroutine(CountdownRoutine());
            else if (!canStart) StopCountdown(true);
        }

        private IEnumerator CountdownRoutine()
        {
            var wait = new WaitForSeconds(1f);
            for (int i = countdownSeconds; i > 0; i--)
            {
                CountdownTick?.Invoke(i);
                yield return wait;
            }
            countdown = null;
            starting = true;
            CountdownTick?.Invoke(0);
            if (!core.StartMatch(roundsPerMatch)) starting = false;
        }

        private void StopCountdown(bool notify)
        {
            if (countdown == null) return;
            StopCoroutine(countdown);
            countdown = null;
            if (notify) CountdownCancelled?.Invoke();
        }

        private void RebuildDeviceMap()
        {
            deviceSlots.Clear();
            foreach (var slot in Players.Slots)
            {
                if (slot.Input is DeviceInput device) deviceSlots[device.Device] = slot.SlotIndex;
                // Coming back from a match: humans confirm Ready again.
                if (slot.IsOccupied && !slot.IsAI) Players.SetReady(slot.SlotIndex, false);
            }
            if (!HasHost()) AssignHost();
        }

        private void AssignHost()
        {
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && Players.SetHost(slot.SlotIndex)) return;
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

        private bool CanEdit() => Players != null && !starting;
    }
}
