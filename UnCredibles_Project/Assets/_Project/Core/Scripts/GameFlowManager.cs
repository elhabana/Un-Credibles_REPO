using System;

namespace UnCredibles.Core
{
    public enum GameState { Boot, MainMenu, PartyLobby, Loading, Minigame, Results, FinalResults }

    // Singleplayer/local and Multiplayer/online share the same flow; only the available features change.
    public enum SessionMode { Local, Online }

    // Tracks the overall game state only; no minigame logic lives here.
    public sealed class GameFlowManager
    {
        public GameState State { get; private set; } = GameState.Boot;
        public SessionMode Session { get; private set; } = SessionMode.Local;

        public event Action<GameState, GameState> StateChanged; // previous, current

        public void SetSession(SessionMode mode) => Session = mode;

        public bool ChangeState(GameState next)
        {
            if (State == next) return false;
            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
            return true;
        }
    }
}
