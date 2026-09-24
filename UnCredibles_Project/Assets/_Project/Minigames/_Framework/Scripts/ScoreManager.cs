using System;
using System.Collections.Generic;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    public sealed class ScoreManager : MonoBehaviour
    {
        // Kept as a list to preserve slot order; at most 4 entries so lookups stay cheap.
        private readonly List<KeyValuePair<int, int>> scores = new List<KeyValuePair<int, int>>(PlayerRegistry.MaxPlayers);

        public event Action<int, int> ScoreChanged; // playerId, newScore

        public void ResetScores(IReadOnlyList<PlayerSlot> players)
        {
            scores.Clear();
            foreach (var player in players)
            {
                scores.Add(new KeyValuePair<int, int>(player.PlayerId, 0));
                ScoreChanged?.Invoke(player.PlayerId, 0);
            }
        }

        public int GetScore(int playerId)
        {
            int index = IndexOf(playerId);
            return index >= 0 ? scores[index].Value : 0;
        }

        public void AddScore(int playerId, int amount) => SetScore(playerId, GetScore(playerId) + amount);

        public void SetScore(int playerId, int score)
        {
            int index = IndexOf(playerId);
            if (index < 0)
            {
                Debug.LogWarning($"Player {playerId} is not part of this minigame.", this);
                return;
            }
            if (scores[index].Value == score) return;
            scores[index] = new KeyValuePair<int, int>(playerId, score);
            ScoreChanged?.Invoke(playerId, score);
        }

        public MinigameResult[] BuildResults() => MinigameRanking.Rank(scores);

        private int IndexOf(int playerId)
        {
            for (int i = 0; i < scores.Count; i++)
                if (scores[i].Key == playerId) return i;
            return -1;
        }
    }
}
