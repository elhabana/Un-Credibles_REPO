using System.Collections.Generic;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Bot: mows the tallest grass it finds, grabs loose bags nearby, goes home to unload once its
    // tail is long enough (or time is running out), sometimes cuts a rival's tail and rams rivals
    // carrying bags with the turbo. It slows down for sharp turns.
    // It only writes into an AIInput, like a person would.
    public sealed class MowerAIBrain
    {
        private const int GrassSamples = 14;
        private const float EdgeMargin = 1.5f;
        private const float ReachedDistance = 0.8f;
        private const float RamRange = 3.2f;

        private enum Goal { Grass, LooseBag, RivalTail, Ram, Unload }

        private readonly MowerPlayer player;
        private readonly AIInput input;
        private readonly MowLawn lawn;
        private readonly MowBags bags;
        private readonly IReadOnlyList<MowerPlayer> mowers;
        private readonly MowBin bin;
        private readonly MowTheLawnController game;
        private readonly MowTheLawnSettings settings;
        private readonly float aggression;   // personality: how often it goes for other mowers
        private readonly int greed;          // personality: bags carried before going home
        private readonly float wobble;       // personality: how straight it drives

        private float thinkTimer;
        private Vector3 target;
        private Vector3 grassTarget;
        private bool hasGrassTarget;
        private Goal goal;
        private MowerPlayer ramTarget;

        public MowerPlayer Player => player;

        public MowerAIBrain(MowerPlayer player, AIInput input, MowLawn lawn, MowBags bags,
            IReadOnlyList<MowerPlayer> mowers, MowBin bin, MowTheLawnController game, MowTheLawnSettings settings)
        {
            this.player = player;
            this.input = input;
            this.lawn = lawn;
            this.bags = bags;
            this.mowers = mowers;
            this.bin = bin;
            this.game = game;
            this.settings = settings;
            aggression = Random.Range(0.2f, 0.7f);
            greed = Random.Range(3, 7);
            wobble = Random.Range(0f, 0.3f);
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
            if (goal == Goal.Ram && ramTarget != null) target = ramTarget.Position; // track it every frame
            Steer();
        }

        private Vector3 ChooseTarget()
        {
            var position = player.Position;
            ramTarget = null;

            if (ShouldUnload())
            {
                goal = Goal.Unload;
                return bin.Position;
            }
            goal = Goal.LooseBag;
            if (TryNearestLooseBag(position, out var looseBag)) return looseBag;

            if (Random.value < aggression)
            {
                if (TryRamTarget(position, out ramTarget))
                {
                    goal = Goal.Ram;
                    return ramTarget.Position;
                }
                if (TryRivalBag(position, out var rivalBag))
                {
                    goal = Goal.RivalTail;
                    return rivalBag;
                }
            }

            // Keep the current patch of grass until it is cut or reached.
            goal = Goal.Grass;
            if (hasGrassTarget && lawn.GetHeight(grassTarget) > 0.8f && Flat(grassTarget - position).magnitude > ReachedDistance)
                return grassTarget;
            grassTarget = FindTallGrass(position);
            hasGrassTarget = true;
            return grassTarget;
        }

        // Home with a long enough tail, or with anything at all when the game is about to end.
        private bool ShouldUnload()
        {
            if (player.BagCount == 0) return false;
            if (player.BagCount >= greed) return true;
            float timeToBin = Flat(bin.Position - player.Position).magnitude / Mathf.Max(1f, settings.Speed * 0.7f);
            return game.TimeLeft < timeToBin + 4f;
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

        // A nearby rival worth ramming: it carries bags and can be hit right now.
        private bool TryRamTarget(Vector3 position, out MowerPlayer best)
        {
            best = null;
            if (!player.CanBoost) return false;
            float bestDistance = RamRange * 2f;
            foreach (var rival in mowers)
            {
                if (rival == player || rival.BagCount == 0 || !rival.CanBeRammed) continue;
                float distance = Flat(rival.Position - position).magnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = rival;
            }
            return best != null;
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
            var toTarget = Flat(target - position);
            float distance = toTarget.magnitude;
            var direction = distance > 0.1f ? toTarget / distance : player.Forward;

            // At home: wait next to the bin while the bags fly in.
            if (goal == Goal.Unload && distance < settings.DeliverRadius - 0.3f)
            {
                input.SetMove(Vector2.zero);
                return;
            }

            // Turn away from the edges before getting stuck on them (not when going home).
            if (goal != Goal.Unload)
            {
                var center = lawn.Center;
                if (position.x < center.x - lawn.HalfWidth + EdgeMargin) direction.x += 1f;
                if (position.x > center.x + lawn.HalfWidth - EdgeMargin) direction.x -= 1f;
                if (position.z < center.z - lawn.HalfDepth + EdgeMargin) direction.z += 1f;
                if (position.z > center.z + lawn.HalfDepth - EdgeMargin) direction.z -= 1f;
                direction.Normalize();
            }

            float noise = (Mathf.PerlinNoise(Time.time * 0.7f, aggression * 50f) - 0.5f) * 2f * wobble;
            direction = Quaternion.Euler(0f, noise * 60f, 0f) * direction;

            // Ease off for sharp turns; turbo into a rival tail or a ram once lined up and close.
            float angle = Vector3.Angle(player.Forward, direction);
            float throttle = angle > 100f ? 0.45f : angle > 50f ? 0.75f : 1f;
            input.SetMove(new Vector2(direction.x, direction.z).normalized * throttle);

            bool attacking = goal == Goal.RivalTail || goal == Goal.Ram;
            if (attacking && player.CanBoost && angle < 20f && distance < RamRange)
                input.Press(PlayerAction.Jump);
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
