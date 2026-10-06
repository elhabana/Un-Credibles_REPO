using System;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.BatPad
{
    // One phone connected to the BatPad room. BatPadService writes it, gameplay reads it like any device.
    // The phone outlives its lobby slot: Dispose only releases the buttons, the connection belongs to BatPadService.
    public sealed class BatPadInput : IPlayerInput
    {
        public const int NoSlot = -1;

        private static readonly int ActionCount = Enum.GetValues(typeof(PlayerAction)).Length;

        private readonly int[] pressedFrame = new int[ActionCount];
        private readonly bool[] held = new bool[ActionCount];
        private Vector2 move;

        internal BatPadInput(int phoneId)
        {
            PhoneId = phoneId;
            for (int i = 0; i < ActionCount; i++) pressedFrame[i] = -1;
        }

        // Number the server gave this phone inside the room (not the lobby slot).
        public int PhoneId { get; }
        public int SlotIndex { get; internal set; } = NoSlot;
        public bool IsConnected { get; internal set; } = true;

        public InputSourceType Source => InputSourceType.PrestoPad;
        public bool IsEnabled { get; set; } = true;
        public Vector2 Move => IsEnabled ? move : Vector2.zero;

        public bool WasPressed(PlayerAction action) => IsEnabled && pressedFrame[(int)action] == Time.frameCount;
        public bool IsHeld(PlayerAction action) => IsEnabled && held[(int)action];

        internal void SetButton(PlayerAction action, bool down)
        {
            int index = (int)action;
            if (down && !held[index]) pressedFrame[index] = Time.frameCount;
            held[index] = down;
        }

        internal void SetMove(Vector2 value) => move = Vector2.ClampMagnitude(value, 1f);

        internal void ReleaseAll()
        {
            Array.Clear(held, 0, held.Length);
            move = Vector2.zero;
        }

        public void Dispose() => ReleaseAll();
    }
}
