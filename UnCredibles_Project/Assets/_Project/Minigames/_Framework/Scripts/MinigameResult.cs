using System;

namespace UnCredibles.Minigames
{
    // Common output of every minigame, whatever its scoring rules are.
    public readonly struct MinigameResult
    {
        public int PlayerId { get; }
        public int Score { get; }
        public int Placement { get; }

        public MinigameResult(int playerId, int score, int placement)
        {
            if (placement < 1) throw new ArgumentOutOfRangeException(nameof(placement));
            PlayerId = playerId;
            Score = score;
            Placement = placement;
        }
    }
}
