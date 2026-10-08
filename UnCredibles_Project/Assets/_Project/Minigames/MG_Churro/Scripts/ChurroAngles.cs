using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Angle helpers shared by the churro start and the cannonball landing spot.
    // Angles follow Unity's yaw around the pool centre: 0 = +Z, 90 = +X.
    public static class ChurroAngles
    {
        // Random angle whose arms (evenly spaced) stay at least `wanted` degrees from every player.
        // Returns the first random try that is far enough, or the best of all tries.
        public static float RandomClearAngle(IReadOnlyList<ChurroPlayer> players, int armCount, float wanted, int attempts, float fallback)
        {
            float spacing = 360f / Mathf.Max(1, armCount);
            float best = fallback, bestGap = -1f;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float candidate = Random.Range(0f, 360f);
                float gap = float.MaxValue;
                for (int arm = 0; arm < armCount; arm++)
                    foreach (var player in players)
                        gap = Mathf.Min(gap, Mathf.Abs(Mathf.DeltaAngle(candidate + arm * spacing, player.Angle)));
                if (gap >= wanted) return candidate;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }
            }
            return best;
        }

        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
