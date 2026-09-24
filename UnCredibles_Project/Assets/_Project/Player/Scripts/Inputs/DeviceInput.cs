using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace UnCredibles.Players.Inputs
{
    // Each device player gets its own copy of the actions, limited to its device and control scheme.
    public abstract class DeviceInput : IPlayerInput
    {
        public const string PlayerMap = "Player";
        public const string MoveAction = "Move";

        private static readonly PlayerAction[] Actions = (PlayerAction[])Enum.GetValues(typeof(PlayerAction));

        private readonly InputActionAsset actions;
        private readonly InputActionMap map;
        private readonly InputAction move;
        private readonly InputAction[] buttons = new InputAction[Actions.Length];

        public abstract InputSourceType Source { get; }
        public InputDevice Device { get; }

        protected DeviceInput(InputActionAsset template, string controlScheme, InputDevice device)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            Device = device ?? throw new ArgumentNullException(nameof(device));

            actions = Object.Instantiate(template);
            actions.devices = new[] { device };
            actions.bindingMask = InputBinding.MaskByGroup(controlScheme);
            map = actions.FindActionMap(PlayerMap, true);
            move = map.FindAction(MoveAction, true);
            foreach (var action in Actions)
                buttons[(int)action] = map.FindAction(action.ToString(), true);
            map.Enable();
        }

        public bool IsEnabled
        {
            get => map.enabled;
            set
            {
                if (value) map.Enable();
                else map.Disable();
            }
        }

        public Vector2 Move => map.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
        public bool WasPressed(PlayerAction action) => buttons[(int)action].WasPressedThisFrame();
        public bool IsHeld(PlayerAction action) => buttons[(int)action].IsPressed();

        public void Dispose()
        {
            if (actions == null) return;
            map.Disable();
            if (Application.isPlaying) Object.Destroy(actions);
            else Object.DestroyImmediate(actions);
        }
    }

    public sealed class KeyboardInput : DeviceInput
    {
        public const string Scheme = "Keyboard";
        public override InputSourceType Source => InputSourceType.Keyboard;
        public KeyboardInput(InputActionAsset template, Keyboard keyboard) : base(template, Scheme, keyboard) { }
    }

    public sealed class GamepadInput : DeviceInput
    {
        public const string Scheme = "Gamepad";
        public override InputSourceType Source => InputSourceType.Gamepad;
        public GamepadInput(InputActionAsset template, Gamepad gamepad) : base(template, Scheme, gamepad) { }
    }
}
