using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Decides when and at whom a beach ball is thrown (host only). Balls come more often as the
    // round goes on, and only when the timing is fair: the ball never reaches a player close to
    // the moment the churro passes them, so there is always time to stand up and jump.
    public sealed class ChurroBallDirector
    {
        private const float RetrySeconds = 0.25f;
        private const float HoldMargin = 0.3f;

        private readonly ChurroSettings settings;
        private readonly ChurroSpinner spinner;
        private readonly ChurroBalls balls;
        private readonly IReadOnlyList<ChurroPlayer> players;
        private readonly float hitHalfAngle;
        private readonly List<ChurroPlayer> candidates = new List<ChurroPlayer>();
        private float timer;
        private float spinTime; // seconds the churro has been spinning this round

        public ChurroBallDirector(ChurroSettings settings, ChurroSpinner spinner, ChurroBalls balls,
            IReadOnlyList<ChurroPlayer> players, float hitHalfAngle)
        {
            this.settings = settings;
            this.spinner = spinner;
            this.balls = balls;
            this.players = players;
            this.hitHalfAngle = hitHalfAngle;
        }

        public void ResetForRound()
        {
            timer = settings.BallFirstDelay;
            spinTime = 0f;
        }

        // Called every frame while the churro spins. Returns the player a ball was thrown at this
        // frame (to tell the clients), or null.
        public ChurroPlayer Tick(float deltaTime, ChurroSettings.Round round)
        {
            spinTime += deltaTime;
            if (!round.balls || (timer -= deltaTime) > 0f) return null;
            // Never while the kid is changing direction: the churro timing is not stable then.
            if (spinner.IsChangingDirection)
            {
                timer = RetrySeconds;
                return null;
            }

            candidates.Clear();
            foreach (var player in players)
                if (player.IsIn && !balls.IsTargeted(player)) candidates.Add(player);

            float flight = settings.BallFlightSeconds;
            while (candidates.Count > 0)
            {
                int pick = Random.Range(0, candidates.Count);
                var target = candidates[pick];
                candidates.RemoveAt(pick);
                if (!IsFair(target, flight)) continue;

                balls.Throw(target, flight);
                // The churro keeps its direction until the ball is gone, so the timing holds.
                spinner.HoldDirection(flight + settings.BallSafetyGap + HoldMargin);
                // More and more balls as the round goes on.
                timer = Random.Range(round.ballInterval.x, round.ballInterval.y) * settings.BallIntervalFactor(spinTime);
                return target;
            }
            timer = RetrySeconds; // nobody can get a fair ball right now: try again soon
            return null;
        }

        // Checked with the current speed and with the speed it will have by then (it accelerates).
        private bool IsFair(ChurroPlayer target, float arrival)
        {
            float laterSpeed = Mathf.Min(spinner.Speed + spinner.Acceleration * arrival, spinner.MaxSpeed);
            return IsFairAtSpeed(target, arrival, spinner.Speed) && IsFairAtSpeed(target, arrival, laterSpeed);
        }

        private bool IsFairAtSpeed(ChurroPlayer target, float arrival, float speed)
        {
            float gap = settings.BallSafetyGap;
            speed = Mathf.Max(speed, 1f);
            float passSeconds = hitHalfAngle * 2f / speed;
            for (int arm = 0; arm < spinner.ArmCount; arm++)
            {
                float distance = spinner.DistanceToZone(arm, target.Angle, hitHalfAngle);
                // Previous, next and following passes of this arm over the player.
                for (int lap = -1; lap <= 2; lap++)
                {
                    float start = (distance + lap * 360f) / speed;
                    if (arrival > start - gap && arrival < start + passSeconds + gap) return false;
                }
            }
            return true;
        }
    }
}
