using System;
using System.Collections.Generic;
using UnCredibles.Players;

namespace UnCredibles.Minigames
{
    // Everything a minigame receives from outside: its data and the players taking part.
    public sealed class MinigameContext
    {
        public MinigameData Data { get; }
        public IReadOnlyList<PlayerSlot> Players { get; }
        public bool IsDebug { get; }

        public MinigameContext(MinigameData data, IEnumerable<PlayerSlot> players, bool isDebug)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            Data = data;
            Players = Array.AsReadOnly(new List<PlayerSlot>(players).ToArray());
            IsDebug = isDebug;
        }
    }
}
