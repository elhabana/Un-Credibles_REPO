using System.Collections.Generic;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Bot: grabs loose bags nearby, sometimes attacks a rival's tail (with a turbo when lined up)
    // and otherwise drives to the tallest grass it can find. It slows down for sharp turns.
    // It only writes into an AIInput, like a person would.
    public sealed class MowerAIBrain
    {
        private const int GrassSamples = 14;
        private const float EdgeMargin = 1.5f;
        private const float ReachedDistance = 0.8f;

        private readonly MowerPlayer player;
        private readonly AIInput input;
        private readonly MowLawn lawn;
        private readonly MowBags bags;
        private readonly IReadOnlyList<MowerPlayer> mowers;
        private readonly MowTheLawnSettings settings;
        private readonly float aggression;   // personality: how often it goes for other tails
        private readonly float wobble;       // personality: how straight it drives

        private float thinkTimer;
        private Vector3 target;
        private Vector3 grassTarget;
        private bool hasGrassTarget;
        private bool attacking;

        public MowerPlayer Player => player;

        public MowerAIBrain(MowerPlayer player, AIInput input, MowLawn lawn, MowBags bags,
            IReadOnlyList<MowerPlayer> mowers, MowTheLawnSettings settings)
        {
            this.player = player;
            this.input = input;
            this.lawn = lawn;
            this.bags = bags;
            this.mowers = mowers;
            this.settings = settings;
            aggression = Random.Range(0.2f, 0.75f);
            wobble = Random.Range(0f, 0.35f);
            thinkTimer = Random.Range(0f, settings.AIThinkInterval);
            target = player.Position;
        }

        public void Tick(float deltaTime)
        {
            thinkTimer -= deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = settings.AIThinkInterval;
                target = ChooseTarget();
            }
            Steer();
        }

        private Vector3 ChooseTarget()
        {
            var position = player.Position;
            attacking = false;
            if (TryNearestLooseBag(position, out var looseBag)) return looseBag;
            if (Random.value < aggression && TryRivalBag(position, out var rivalBag))
            {
                attacking = true;
                return rivalBag;
            }

            // Keep the current patch of grass until it is cut or reached.
            if (hasGrassTarget && lawn.GetHeight(grassTarget) > 0.8f && Flat(grassTarget - position).magnitude > ReachedDistance)
                return grassTarget;
            grassTarget = FindTallGrass(position);
            hasGrassTarget = true;
            return grassTarget;
        }

        private bool TryNearestLooseBag(Vector3 position, out Vector3 best)
        {
            best = default;
            float bestDistance = settings.AILooseBagRange * settings.AILooseBagRange;
            bool found = false;
            for (int i = 0; i < bags.LooseCount; i++)
            {
                if (!bags.CanPickUp(i)) continue;
                float distance = Flat(bags.GetLoosePosition(i) - position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = bags.GetLoosePosition(i);
                found = true;
            }
            return found;
        }

        // The closest bag of another mower's tail, a bit ahead of where it will be.
        private bool TryRivalBag(Vector3 position, out Vector3 best)
        {
            best = default;
            float bestDistance = settings.AIAttackRange * settings.AIAttackRange;
            bool found = false;
            foreach (var rival in mowers)
            {
                if (rival == player || rival.IsTailProtected) continue;
                var tail = rival.BagPositions;
                for (int i = 0; i < tail.Count; i++)
                {
                    float distance = Flat(tail[i] - position).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = tail[i] + rival.Forward * 0.5f;
                    found = true;
                }
            }
            return found;
        }

        // Best of a few random spots: tall grass, not too far away.
        private Vector3 FindTallGrass(Vector3 position)
        {
            var best = lawn.RandomPoint(EdgeMargin);
            float bestScore = float.MinValue;
            for (int i = 0; i < GrassSamples; i++)
            {
                var candidate = lawn.RandomPoint(EdgeMargin);
                float score = lawn.GetHeight(candidate) * 10f - Flat(candidate - position).magnitude * 0.35f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private void Steer()
        {
            var position = player.Position;
            var direction = Flat(target - position);
            if (direction.sqrMagnitude < 0.01f) direction = player.Forward;
            direction.Normalize();

            // Turn away from the edges before getting stuck on them.
            var center = lawn.Center;
            if (position.x < center.x - lawn.HalfWidth + EdgeMargin) direction.x += 1f;
            if (position.x > center.x + lawn.HalfWidth - EdgeMargin) direction.x -= 1f;
            if (position.z < center.z - lawn.HalfDepth + EdgeMargin) direction.z += 1f;
            if (position.z > center.z + lawn.HalfDepth - EdgeMargin) direction.z -= 1f;

            float noise = (Mathf.PerlinNoise(Time.time * 0.7f, aggression * 50f) - 0.5f) * 2f * wobble;
            direction = Quaternion.Euler(0f, noise * 60f, 0f) * direction;
            // Ease off for sharp turns, turbo into a rival tail once lined up and close.
            float angle = Vector3.Angle(player.Forward, direction);
            float throttle = angle > 100f ? 0.45f : angle > 50f ? 0.75f : 1f;
            var flat = new Vector2(direction.x, direction.z).normalized;
            input.SetMove(flat * throttle);
            if (attacking && player.CanBoost && angle < 20f && Flat(target - position).magnitude < 3.5f)
                input.Press(PlayerAction.Jump);
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
