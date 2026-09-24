using System;
using System.Collections.Generic;

namespace UnCredibles.Minigames
{
    public enum MinigameState { None, Initialize, Waiting, Countdown, Playing, Finishing, Results, Exit }

    public enum MinigameEndReason { TimerFinished, ObjectiveCompleted, AllPlayersFinished, CustomCondition }

    // Contract that lets MinigameManager drive any minigame without knowing its rules.
    public interface IMinigame
    {
        MinigameData Data { get; }
        MinigameState State { get; }
        event Action<MinigameState> StateChanged;
        event Action<IReadOnlyList<MinigameResult>> Finished;

        void Initialize(MinigameContext context);
        void StartCountdown();
        void StartGame();
        void EndGame(MinigameEndReason reason);
        IReadOnlyList<MinigameResult> GetResults();
        void Exit();
    }
}
