using System;

namespace UnCredibles.Core
{
    public enum GameState { Booting, MainMenu }

    public sealed class GameFlowManager
    {
        public GameState State { get; private set; } = GameState.Booting;
        public event Action<GameState> StateChanged;

        internal void EnterMainMenu()
        {
            if (State == GameState.MainMenu) return;
            State = GameState.MainMenu;
            StateChanged?.Invoke(State);
        }
    }
}
