using System;
using System.Collections.Generic;
using UnCredibles.Minigames;
using UnCredibles.Players;

namespace UnCredibles.Core
{
    // Whole match: players, points per round, minigames played and final winner.
    // Minigame scores are not comparable, so rounds award points by placement.
    public sealed class MatchManager
    {
        private readonly int[] pointsByPlacement;
        private readonly List<KeyValuePair<int, int>> totals = new List<KeyValuePair<int, int>>(PlayerRegistry.MaxPlayers);
        private readonly List<MinigameData> played = new List<MinigameData>();
        private readonly List<IReadOnlyList<MinigameResult>> roundResults = new List<IReadOnlyList<MinigameResult>>();

        public bool IsRunning { get; private set; }
        public int CurrentRound => played.Count;
        public int TotalRounds { get; private set; }
        public IReadOnlyList<MinigameData> PlayedMinigames => played;
        public IReadOnlyList<IReadOnlyList<MinigameResult>> RoundResults => roundResults;

        public event Action<int> RoundCompleted; // round number (1-based)
        public event Action<IReadOnlyList<MinigameResult>> MatchFinished; // final standings

        public MatchManager(int[] pointsByPlacement)
        {
            this.pointsByPlacement = pointsByPlacement ?? throw new ArgumentNullException(nameof(pointsByPlacement));
        }

        public void StartMatch(IReadOnlyList<PlayerSlot> players, int rounds)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (rounds < 1) throw new ArgumentOutOfRangeException(nameof(rounds));

            totals.Clear();
            played.Clear();
            roundResults.Clear();
            foreach (var player in players) totals.Add(new KeyValuePair<int, int>(player.PlayerId, 0));
            TotalRounds = rounds;
            IsRunning = true;
        }

        public void RegisterResults(MinigameData minigame, IReadOnlyList<MinigameResult> results)
        {
            if (!IsRunning || results == null) return;

            foreach (var result in results)
            {
                int index = IndexOf(result.PlayerId);
                if (index < 0) continue;
                totals[index] = new KeyValuePair<int, int>(result.PlayerId, totals[index].Value + PointsFor(result.Placement));
            }
            played.Add(minigame);
            roundResults.Add(results);
            RoundCompleted?.Invoke(CurrentRound);

            if (CurrentRound >= TotalRounds)
            {
                IsRunning = false;
                MatchFinished?.Invoke(GetStandings());
            }
        }

        public int GetPoints(int playerId)
        {
            int index = IndexOf(playerId);
            return index >= 0 ? totals[index].Value : 0;
        }

        // Standings use MinigameResult: Score = match points, Placement = position (bots included).
        public MinigameResult[] GetStandings() => MinigameRanking.Rank(totals);

        public void CancelMatch() => IsRunning = false;

        public IReadOnlyList<MinigameResult> LastRoundResults =>
            roundResults.Count > 0 ? roundResults[roundResults.Count - 1] : Array.Empty<MinigameResult>();

        public int PointsFor(int placement)
        {
            int index = placement - 1;
            return index >= 0 && index < pointsByPlacement.Length ? pointsByPlacement[index] : 0;
        }

        private int IndexOf(int playerId)
        {
            for (int i = 0; i < totals.Count; i++)
                if (totals[i].Key == playerId) return i;
            return -1;
        }
    }
}
