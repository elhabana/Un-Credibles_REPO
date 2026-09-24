using System;
using UnCredibles.Players;

namespace UnCredibles.Minigames
{
    public readonly struct MinigameResult
    {
        public int PlayerSlot { get; }
        public int Score { get; }

        public MinigameResult(int playerSlot, int score)
        {
            if (playerSlot < 0 || playerSlot >= PlayerRegistry.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));

            PlayerSlot = playerSlot;
            Score = score;
        }
    }
}
