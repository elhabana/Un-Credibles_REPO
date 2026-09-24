using System;
using UnityEngine;

namespace UnCredibles.Players.Inputs
{
    // Written by a minigame-specific AI brain, read by gameplay exactly like a device.
    // Presses last one frame, so brains should run before the avatars that read them
    // (e.g. [DefaultExecutionOrder(-50)] on the brain).
    public sealed class AIInput : IPlayerInput
    {
        private static readonly int ActionCount = Enum.GetValues(typeof(PlayerAction)).Length;

        private readonly int[] pressedFrame = new int[ActionCount];
        private readonly bool[] held = new bool[ActionCount];
        private Vector2 move;

        public AIInput()
        {
            for (int i = 0; i < ActionCount; i++) pressedFrame[i] = -1;
        }

        public InputSourceType Source => InputSourceType.AI;
        public bool IsEnabled { get; set; } = true;
        public Vector2 Move => IsEnabled ? move : Vector2.zero;

        public bool WasPressed(PlayerAction action) => IsEnabled && pressedFrame[(int)action] == Time.frameCount;
        public bool IsHeld(PlayerAction action) => IsEnabled && held[(int)action];

        public void SetMove(Vector2 value) => move = Vector2.ClampMagnitude(value, 1f);
        public void Press(PlayerAction action) => pressedFrame[(int)action] = Time.frameCount;
        public void SetHeld(PlayerAction action, bool isHeld) => held[(int)action] = isHeld;

        public void Dispose() { }
    }
}
