using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnCredibles.Core
{
    // Creates the IPlayerInput for each source. The PartyLobby will use it to join players;
    // PrestoPad and Network inputs will be added here in their own phases.
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset playerControls;

        public InputActionAsset PlayerControls => playerControls;

        public IPlayerInput CreateKeyboardInput() =>
            Keyboard.current != null ? new KeyboardInput(playerControls, Keyboard.current) : null;

        public IPlayerInput CreateGamepadInput(Gamepad gamepad) =>
            gamepad != null ? new GamepadInput(playerControls, gamepad) : null;

        public AIInput CreateAIInput() => new AIInput();
    }
}
