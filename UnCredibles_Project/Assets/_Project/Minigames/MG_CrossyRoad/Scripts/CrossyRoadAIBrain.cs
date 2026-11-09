using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Bot that plans a little ahead: every decision it scores 8 directions (plus standing still)
    // by how much they bring it closer to its goal, discarding the ones a car will cross soon.
    // That lets it walk diagonally, slip sideways into gaps and only wait when nothing is safe.
    // It only writes into an AIInput, like a human pressing keys.
    public sealed class CrossyRoadAIBrain
    {
        private const float StepSeconds = 0.15f;
        private const float OilPenalty = 0.6f;

        private static readonly Vector2[] Directions =
        {
            new Vector2(0f, 1f), new Vector2(0.7071f, 0.7071f), new Vector2(1f, 0f), new Vector2(0.7071f, -0.7071f),
            new Vector2(0f, -1f), new Vector2(-0.7071f, -0.7071f), new Vector2(-1f, 0f), new Vector2(-0.7071f, 0.7071f),
        };

        private readonly CrossyRoadPlayer player;
        private readonly AIInput input;
        private readonly CrossyRoadBoard board;
        private readonly CrossyRoadTraffic traffic;
        private readonly CrossyRoadGrandmas grandmas;
        private readonly CrossyRoadOil oil;
        private readonly IReadOnlyList<CrossyRoadPlayer> rivals;

        // Personality, rolled once per bot so they do not all move the same way.
        private readonly float reactionMin;
        private readonly float reactionMax;
        private readonly float safetyMargin;   // extra distance kept from cars
        private readonly float lateralBias;    // tendency to drift sideways while crossing
        private readonly float shoveChance;    // per decision, when a rival carrying a grandma is within reach
        private readonly int predictionSteps;  // how far ahead it looks for cars
        private readonly float speedScale;     // of the full stick

        private float thinkTimer;
        private Vector2 decision;
        private int targetGrandma = -1;
        private float goalX;
        private bool wasCarrying;

        public CrossyRoadPlayer Player => player;

        public CrossyRoadAIBrain(CrossyRoadPlayer player, AIInput input, CrossyRoadBoard board,
            CrossyRoadTraffic traffic, CrossyRoadGrandmas grandmas, CrossyRoadOil oil, IReadOnlyList<CrossyRoadPlayer> rivals)
        {
            this.rivals = rivals;
            shoveChance = Random.Range(0.15f, 0.4f);
            this.player = player;
            this.input = input;
            this.board = board;
            this.traffic = traffic;
            this.grandmas = grandmas;
            this.oil = oil;

            // Hard is the original bot. Lower levels react later, see cars later, keep less distance and walk slower.
            var level = input.Difficulty;
            float slowness = level switch { BotDifficulty.Easy => 2.5f, BotDifficulty.Medium => 1.6f, _ => 1f };
            float caution = level switch { BotDifficulty.Easy => 0f, BotDifficulty.Medium => 0.5f, _ => 1f };
            predictionSteps = level switch { BotDifficulty.Easy => 1, BotDifficulty.Medium => 2, _ => 3 };
            speedScale = level switch { BotDifficulty.Easy => 0.75f, BotDifficulty.Medium => 0.9f, _ => 1f };
            shoveChance *= caution;

            reactionMin = Random.Range(0.06f, 0.12f) * slowness;
            reactionMax = reactionMin + Random.Range(0.08f, 0.18f) * slowness;
            safetyMargin = Random.Range(0.05f, 0.3f) * caution;
            lateralBias = Random.Range(-0.35f, 0.35f);
        }

        public void Tick(float deltaTime)
        {
            if (!player.IsAlive)
            {
                decision = Vector2.zero;
                input.SetMove(decision);
                return;
            }

            // Re-think a few times per second, like a person reacting.
            thinkTimer -= deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = Random.Range(reactionMin, reactionMax);
                decision = Decide();
                TryShove();
            }
            input.SetMove(decision * speedScale);
        }

        // Opportunistic only: never goes out of its way, but sometimes shoves a rival carrying
        // a grandma that happens to be within reach.
        private void TryShove()
        {
            if (!player.CanShove) return;
            float range = board.Settings.ShoveRange;
            foreach (var rival in rivals)
            {
                if (rival == player || !rival.IsAlive || rival.IsInvulnerable || !rival.IsCarrying) continue;
                if ((rival.Position - player.Position).sqrMagnitude > range * range) continue;
                if (Random.value < shoveChance) input.Press(PlayerAction.Jump);
                return;
            }
        }

        private Vector2 Decide()
        {
            var position = player.Position;
            var target = CurrentTarget(position);
            var toTarget = new Vector2(target.x - position.x, target.z - position.z);
            if (toTarget.sqrMagnitude < 0.01f) return Vector2.zero;
            var goalDirection = toTarget.normalized;

            var best = Vector2.zero;
            float bestScore = IsSafe(position, Vector2.zero) ? 0f : float.MinValue;
            foreach (var direction in Directions)
            {
                if (!IsSafe(position, direction)) continue;

                float score = Vector2.Dot(direction, goalDirection);
                score += direction.x * lateralBias * 0.3f;          // personality
                if (direction == decision) score += 0.15f;          // keep course, avoid jitter
                if (CrossesOil(position, direction)) score -= OilPenalty; // avoid slicks when there is another way
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }

            // Nothing safe at all (a car is about to hit us): run away along Z, whichever side is free.
            if (bestScore == float.MinValue)
                best = IsSafe(position, Vector2.up) ? Vector2.up : Vector2.down;
            return best;
        }

        private Vector3 CurrentTarget(Vector3 position)
        {
            if (player.IsCarrying)
            {
                // Pick a random drop point once per trip so bots cross diagonally, not in a straight line.
                if (!wasCarrying) goalX = Random.Range(-board.HalfWidth + 1f, board.HalfWidth - 1f);
                wasCarrying = true;
                return new Vector3(board.transform.position.x + goalX, position.y, board.LaneToZ(board.GoalLane));
            }

            wasCarrying = false;
            if (!grandmas.IsAvailable(targetGrandma)) targetGrandma = grandmas.NearestAvailable(position);
            return targetGrandma >= 0
                ? grandmas.GetPosition(targetGrandma)
                : new Vector3(position.x, position.y, board.LaneToZ(CrossyRoadBoard.SpawnLane));
        }

        private bool CrossesOil(Vector3 position, Vector2 direction)
        {
            var step = new Vector3(direction.x, 0f, direction.y) * (player.CurrentSpeed * speedScale * StepSeconds);
            for (int i = 1; i <= predictionSteps; i++)
                if (oil.IsOnOil(position + step * i)) return true;
            return false;
        }

        // Walk that way for the next half second: is any car going to be there?
        private bool IsSafe(Vector3 position, Vector2 direction)
        {
            float radius = board.Settings.PlayerRadius + safetyMargin;
            float speed = player.CurrentSpeed * speedScale;
            var step = new Vector3(direction.x, 0f, direction.y) * speed;
            for (int i = 0; i <= predictionSteps; i++)
            {
                float time = i * StepSeconds;
                var future = board.ClampInside(position + step * time, board.Settings.PlayerRadius);
                if (traffic.OverlapsAt(future, radius, time)) return false;
            }
            return true;
        }
    }
}
