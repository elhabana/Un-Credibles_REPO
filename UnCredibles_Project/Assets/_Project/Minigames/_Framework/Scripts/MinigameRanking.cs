using System.Collections.Generic;

namespace UnCredibles.Minigames
{
    public static class MinigameRanking
    {
        // Highest score first. Ties share a placement (1, 1, 3, 4).
        public static MinigameResult[] Rank(IReadOnlyList<KeyValuePair<int, int>> scoresByPlayerId)
        {
            var ordered = new List<KeyValuePair<int, int>>(scoresByPlayerId);
            // Stable sort keeps slot order for ties so results are deterministic.
            for (int i = 1; i < ordered.Count; i++)
            {
                var current = ordered[i];
                int j = i - 1;
                while (j >= 0 && ordered[j].Value < current.Value)
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }
                ordered[j + 1] = current;
            }

            var results = new MinigameResult[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                int placement = i > 0 && ordered[i].Value == ordered[i - 1].Value ? results[i - 1].Placement : i + 1;
                results[i] = new MinigameResult(ordered[i].Key, ordered[i].Value, placement);
            }
            return results;
        }
    }
}
