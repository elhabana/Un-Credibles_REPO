using System;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Networking
{
    // Input of a player on another machine, as the host sees it. Gameplay reads it like a gamepad;
    // the online session fills it with what the client sends.
    public sealed class NetworkInput : IPlayerInput
    {
        private static readonly int ActionCount = Enum.GetValues(typeof(PlayerAction)).Length;

        private readonly int[] pressedFrame = new int[ActionCount];
        private readonly bool[] held = new bool[ActionCount];
        private Vector2 move;

        public NetworkInput(ulong clientId)
        {
            ClientId = clientId;
            for (int i = 0; i < ActionCount; i++) pressedFrame[i] = -1;
        }

        public ulong ClientId { get; }
        public InputSourceType Source => InputSourceType.Network;
        public bool IsEnabled { get; set; } = true;
        public Vector2 Move => IsEnabled ? move : Vector2.zero;

        public bool WasPressed(PlayerAction action) => IsEnabled && pressedFrame[(int)action] == Time.frameCount;
        public bool IsHeld(PlayerAction action) => IsEnabled && held[(int)action];

        // pressedMask / heldMask: one bit per PlayerAction.
        public void Apply(Vector2 stick, int pressedMask, int heldMask)
        {
            move = Vector2.ClampMagnitude(stick, 1f);
            for (int i = 0; i < ActionCount; i++)
            {
                if ((pressedMask & (1 << i)) != 0) pressedFrame[i] = Time.frameCount;
                held[i] = (heldMask & (1 << i)) != 0;
            }
        }

        public void Dispose() { }
    }
}
