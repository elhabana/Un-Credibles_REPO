using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Pick up a grandma in the first lane, cross 5 roads and drop her in the last lane for points.
    // Hit by a car: the grandma goes back home and the player respawns in the spawn lane.
    // The only Update of the minigame: traffic, AI and avatars are ticked from here in a fixed order.
    public sealed class CrossyRoadController : MinigameController
    {
        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private CrossyRoadTraffic traffic;
        [SerializeField] private CrossyRoadGrandmas grandmas;
        [SerializeField] private CrossyRoadPlayer playerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<CrossyRoadPlayer> avatars = new List<CrossyRoadPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<CrossyRoadAIBrain> brains = new List<CrossyRoadAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly float[] respawnTimers = new float[PlayerRegistry.MaxPlayers];
        private System.Func<Vector2Int, CrossyRoadPlayer, bool> isMoveBlocked;

        private CrossyRoadSettings Settings => board.Settings;

        protected override void OnInitialize(MinigameContext context)
        {
            isMoveBlocked = IsMoveBlocked;
            board.Build();
            traffic.Initialize();
            grandmas.Initialize();

            foreach (var player in Players)
            {
                var avatar = Spawns.Spawn(playerPrefab, player, playersParent);
                var preferred = board.WorldToCell(Spawns.GetSpawnPoint(player.SlotIndex).position);
                var spawnCell = FindFreeSpawnCell(preferred.x);
                avatar.Setup(player, board, spawnCell, Settings.GetPlayerColor(player.SlotIndex), Settings.GrandmaColor);
                avatar.CellReached += HandleCellReached;
                avatars.Add(avatar);

                if (player.Input is AIInput aiInput)
                    brains.Add(new CrossyRoadAIBrain(avatar, aiInput, board, traffic, grandmas));
            }
        }

        protected override void OnGameStarted() { }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var avatar in avatars)
                if (avatar != null) avatar.CellReached -= HandleCellReached;
        }

        private void Update()
        {
            // Cars already drive during the countdown so the scene feels alive.
            if (State == MinigameState.Waiting || State == MinigameState.Countdown || IsPlaying)
                traffic.Tick(Time.deltaTime);
            if (!IsPlaying) return;

            float deltaTime = Time.deltaTime;
            grandmas.Tick(deltaTime);
            foreach (var brain in brains) brain.Tick(deltaTime); // before avatars read their input

            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (!avatar.IsAlive)
                {
                    TickRespawn(i, deltaTime);
                    continue;
                }

                avatar.Tick(deltaTime, isMoveBlocked);
                if (IsHitByCar(avatar)) KillPlayer(i);
            }
        }

        private void HandleCellReached(CrossyRoadPlayer avatar)
        {
            if (!IsPlaying) return;
            int lane = avatar.Cell.y;

            if (lane == CrossyRoadBoard.PickupLane && !avatar.IsCarrying && grandmas.TryTake(avatar.Cell.x))
            {
                avatar.SetCarrying(avatar.Cell.x);
                return;
            }

            if (lane == board.GoalLane && avatar.IsCarrying)
            {
                grandmas.Return(avatar.CarriedGrandmaColumn);
                avatar.SetCarrying(-1);
                Score.AddScore(avatar.Slot.PlayerId, Settings.PointsPerDelivery);
                if (Settings.ReturnToSpawnAfterDelivery)
                    avatar.Respawn(FindFreeSpawnCell(avatar.SpawnColumn, avatar), 0f);
            }
        }

        private bool IsHitByCar(CrossyRoadPlayer avatar)
        {
            if (avatar.IsInvulnerable) return false;
            var position = avatar.transform.position;
            int road = board.RoadIndex(board.WorldToLane(position.z));
            return road >= 0 && traffic.IsHit(road, position.x, Settings.PlayerHalfWidth);
        }

        private void KillPlayer(int index)
        {
            var avatar = avatars[index];
            if (avatar.IsCarrying)
            {
                grandmas.Return(avatar.CarriedGrandmaColumn);
                avatar.SetCarrying(-1);
            }
            avatar.Kill();
            respawnTimers[index] = Settings.RespawnDelay;
        }

        private void TickRespawn(int index, float deltaTime)
        {
            respawnTimers[index] -= deltaTime;
            if (respawnTimers[index] > 0f) return;

            var avatar = avatars[index];
            var cell = FindFreeSpawnCell(avatar.SpawnColumn, avatar);
            if (IsCellBlocked(cell, avatar)) return; // spawn lane full, try again next frame
            avatar.Respawn(cell, Settings.InvulnerableTime);
        }

        // Closest free cell of the spawn lane to the preferred column.
        private Vector2Int FindFreeSpawnCell(int preferredColumn, CrossyRoadPlayer self = null)
        {
            for (int offset = 0; offset < board.Columns; offset++)
            {
                var left = new Vector2Int(preferredColumn - offset, CrossyRoadBoard.SpawnLane);
                if (board.IsInside(left) && !IsCellBlocked(left, self)) return left;
                var right = new Vector2Int(preferredColumn + offset, CrossyRoadBoard.SpawnLane);
                if (board.IsInside(right) && !IsCellBlocked(right, self)) return right;
            }
            return new Vector2Int(Mathf.Clamp(preferredColumn, 0, board.Columns - 1), CrossyRoadBoard.SpawnLane);
        }

        // Players cannot hop into a cell taken by another player or by a car.
        private bool IsMoveBlocked(Vector2Int cell, CrossyRoadPlayer self) =>
            IsCellBlocked(cell, self) || IsCarInCell(cell);

        private bool IsCarInCell(Vector2Int cell)
        {
            int road = board.RoadIndex(cell.y);
            return road >= 0 && traffic.IsHit(road, board.ColumnToX(cell.x), Settings.PlayerHalfWidth);
        }

        private bool IsCellBlocked(Vector2Int cell, CrossyRoadPlayer self)
        {
            foreach (var other in avatars)
                if (other != self && other.IsAlive && other.Cell == cell) return true;
            return false;
        }
    }
}
