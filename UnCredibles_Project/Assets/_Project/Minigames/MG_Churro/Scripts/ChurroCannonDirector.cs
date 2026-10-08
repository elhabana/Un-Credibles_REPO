using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Decides when the cannonball kid jumps in and where he lands (host only): on the water a bit
    // inside the ring of floats, as far as possible from every player.
    public sealed class ChurroCannonDirector
    {
        private const int Attempts = 16;

        private readonly ChurroSettings settings;
        private readonly ChurroCannonball cannonball;
        private readonly IReadOnlyList<ChurroPlayer> players;
        private readonly Vector3 center;
        private float timer;

        public ChurroCannonDirector(ChurroSettings settings, ChurroCannonball cannonball,
            IReadOnlyList<ChurroPlayer> players, Vector3 poolCenter)
        {
            this.settings = settings;
            this.cannonball = cannonball;
            this.players = players;
            center = ChurroAngles.Flat(poolCenter);
        }

        public void ResetForRound() => timer = settings.CannonFirstDelay;

        // Called every frame while the churro spins. True when a jump started this frame, with its
        // start and landing points (to tell the clients).
        public bool Tick(float deltaTime, ChurroSettings.Round round, out Vector3 start, out Vector3 landing)
        {
            start = landing = default;
            if (!round.cannonballs || cannonball.IsBusy || (timer -= deltaTime) > 0f) return false;
            timer = Random.Range(round.cannonInterval.x, round.cannonInterval.y);

            float angle = ChurroAngles.RandomClearAngle(players, 1, float.MaxValue, Attempts, 0f);
            var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            landing = center + direction * settings.CannonLandRadius;
            start = center + direction * settings.CannonStartRadius + Vector3.up;
            cannonball.Play(start, landing);
            return true;
        }
    }
}
