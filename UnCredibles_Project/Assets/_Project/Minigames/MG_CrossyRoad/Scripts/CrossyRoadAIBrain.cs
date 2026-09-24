using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Simple bot: fetch the nearest grandma, then cross when the next lane is clear.
    // It only writes into an AIInput, exactly like a human pressing keys.
    public sealed class CrossyRoadAIBrain
    {
        private const float MinReaction = 0.08f;
        private const float MaxReaction = 0.3f;
        private const float SafetyMargin = 0.25f;

        private readonly CrossyRoadPlayer player;
        private readonly AIInput input;
        private readonly CrossyRoadBoard board;
        private readonly CrossyRoadTraffic traffic;
        private readonly CrossyRoadGrandmas grandmas;
        private float thinkTimer;

        public CrossyRoadAIBrain(CrossyRoadPlayer player, AIInput input, CrossyRoadBoard board,
            CrossyRoadTraffic traffic, CrossyRoadGrandmas grandmas)
        {
            this.player = player;
            this.input = input;
            this.board = board;
            this.traffic = traffic;
            this.grandmas = grandmas;
        }

        public void Tick(float deltaTime)
        {
            input.SetMove(Vector2.zero);
            if (!player.IsAlive || player.IsHopping) return;

            thinkTimer -= deltaTime;
            if (thinkTimer > 0f) return;
            thinkTimer = Random.Range(MinReaction, MaxReaction);

            var direction = ChooseDirection();
            input.SetMove(new Vector2(direction.x, direction.y));
        }

        private Vector2Int ChooseDirection()
        {
            var cell = player.Cell;

            // Standing on a road with a car coming: escape to whichever neighbour lane is safe.
            if (!IsSafe(cell))
            {
                if (IsSafe(cell + Vector2Int.up)) return Vector2Int.up;
                if (IsSafe(cell + Vector2Int.down)) return Vector2Int.down;
            }

            if (player.IsCarrying) return Step(Vector2Int.up);

            if (cell.y > CrossyRoadBoard.PickupLane)
            {
                // Line up with a free grandma while crossing back, then go down.
                int target = grandmas.NearestAvailable(cell.x);
                if (cell.y == CrossyRoadBoard.SpawnLane && target >= 0 && target != cell.x)
                    return new Vector2Int(target > cell.x ? 1 : -1, 0);
                return Step(Vector2Int.down);
            }

            int column = grandmas.NearestAvailable(cell.x);
            return column < 0 || column == cell.x ? Vector2Int.zero : new Vector2Int(column > cell.x ? 1 : -1, 0);
        }

        private Vector2Int Step(Vector2Int direction) => IsSafe(player.Cell + direction) ? direction : Vector2Int.zero;

        private bool IsSafe(Vector2Int cell)
        {
            if (!board.IsInside(cell)) return false;
            int road = board.RoadIndex(cell.y);
            if (road < 0) return true;
            float lookAhead = board.Settings.HopDuration + SafetyMargin + MaxReaction;
            return !traffic.IsDangerous(road, board.ColumnToX(cell.x), board.Settings.PlayerHalfWidth, lookAhead);
        }
    }
}
