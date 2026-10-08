using System.Collections.Generic;
using System.IO;
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
        private const byte GrandmaLaunchedEvent = 1;

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private CrossyRoadTraffic traffic;
        [SerializeField] private CrossyRoadGrandmas grandmas;
        [SerializeField] private CrossyRoadOil oil;
        [SerializeField] private CrossyRoadPlayer playerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<CrossyRoadPlayer> avatars = new List<CrossyRoadPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<CrossyRoadAIBrain> brains = new List<CrossyRoadAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly float[] respawnTimers = new float[PlayerRegistry.MaxPlayers];
        private System.Func<Vector3, CrossyRoadPlayer, bool> isMoveBlocked;

        private CrossyRoadSettings Settings => board.Settings;
        private float Radius => Settings.PlayerRadius;

        protected override void OnInitialize(MinigameContext context)
        {
            isMoveBlocked = IsMoveBlocked;
            board.Build();
            traffic.Initialize(!IsReplica); // an online client gets its cars from the host
            grandmas.Initialize();
            oil.Initialize();

            foreach (var player in Players)
            {
                var avatar = Spawns.Spawn(playerPrefab, player, playersParent);
                var spawnPoint = Spawns.GetSpawnPoint(player.SlotIndex).position;
                var preferred = new Vector3(spawnPoint.x, board.transform.position.y, board.LaneToZ(CrossyRoadBoard.SpawnLane));
                avatar.Setup(player, board, FindFreeSpawn(preferred, null), Settings.GrandmaColor);
                avatars.Add(avatar);
            }

            if (!IsReplica) EnsureBrains();
        }

        protected override void OnGameStarted() { }

        private void Update()
        {
            if (IsReplica)
            {
                // Online client: everything comes from the host, we only keep it moving smoothly.
                float remoteDelta = Time.deltaTime;
                traffic.TickRemote(remoteDelta);
                grandmas.Tick(remoteDelta); // only flying grandmas move here
                oil.TickRemote(remoteDelta);
                foreach (var avatar in avatars) avatar.TickRemote(remoteDelta);
                return;
            }

            // Cars already drive during the countdown so the scene feels alive.
            if (State == MinigameState.Waiting || State == MinigameState.Countdown || IsPlaying)
                traffic.Tick(Time.deltaTime);
            if (!IsPlaying) return;
            EnsureBrains(); // an online player who left is now a bot

            float deltaTime = Time.deltaTime;
            grandmas.Tick(deltaTime);
            oil.Tick(deltaTime);
            foreach (var brain in brains) brain.Tick(deltaTime); // before avatars read their input

            for (int i = 0; i < avatars.Count; i++)
            {
                if (!avatars[i].IsAlive)
                {
                    avatars[i].TickDead(deltaTime);
                    TickRespawn(i, deltaTime);
                    continue;
                }
                avatars[i].Tick(deltaTime, isMoveBlocked, oil.IsOnOil(avatars[i].Position));
            }

            SeparatePlayers();

            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (!avatar.IsAlive) continue;
                // Players cannot walk into cars, so any overlap now means a car ran into them.
                if (!avatar.IsInvulnerable && traffic.Overlaps(avatar.Position, Radius)) KillPlayer(i);
                else HandlePickupAndDelivery(avatar);
            }
        }

        private void HandlePickupAndDelivery(CrossyRoadPlayer avatar)
        {
            if (!avatar.IsCarrying)
            {
                int grandma = grandmas.FindAvailableNear(avatar.Position, Settings.PickupRadius);
                if (grandma >= 0 && grandmas.TryTake(grandma)) avatar.SetCarrying(grandma);
                return;
            }

            if (avatar.Lane < board.GoalLane) return;
            grandmas.Return(avatar.CarriedGrandma);
            avatar.SetCarrying(-1);
            Score.AddScore(avatar.Slot.PlayerId, Settings.PointsPerDelivery);
            if (Settings.ReturnToSpawnAfterDelivery) avatar.Respawn(FindFreeSpawn(avatar.SpawnPosition, avatar), 0f);
        }

        // Soft push so avatars never stand inside each other.
        private void SeparatePlayers()
        {
            float minDistance = Radius * 2f;
            for (int a = 0; a < avatars.Count; a++)
            {
                if (!avatars[a].IsAlive) continue;
                for (int b = a + 1; b < avatars.Count; b++)
                {
                    if (!avatars[b].IsAlive) continue;
                    var offset = avatars[b].Position - avatars[a].Position;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance >= minDistance) continue;

                    var push = (distance > 0.001f ? offset / distance : Vector3.right) * ((minDistance - distance) * 0.5f);
                    TryPush(avatars[a], -push);
                    TryPush(avatars[b], push);
                }
            }
        }

        // Never push a player into a car.
        private void TryPush(CrossyRoadPlayer avatar, Vector3 offset)
        {
            var target = board.ClampInside(avatar.Position + offset, Radius);
            if (!traffic.Overlaps(target, Radius)) avatar.MoveTo(target);
        }

        private void KillPlayer(int index)
        {
            var avatar = avatars[index];
            if (avatar.IsCarrying)
            {
                // She flies off with a lot of force, then goes back to her spot as usual.
                grandmas.Launch(avatar.CarryPosition);
                var message = BeginEvent(GrandmaLaunchedEvent);
                var from = avatar.CarryPosition;
                message.Write(from.x); message.Write(from.y); message.Write(from.z);
                SendEvent();
                grandmas.Return(avatar.CarriedGrandma);
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
            var position = FindFreeSpawn(avatar.SpawnPosition, avatar);
            if (IsOccupied(position, avatar)) return; // spawn lane full, try again next frame
            avatar.Respawn(position, Settings.InvulnerableTime);
        }

        // Closest free spot of the spawn lane to the preferred position.
        private Vector3 FindFreeSpawn(Vector3 preferred, CrossyRoadPlayer self)
        {
            float step = Radius * 2.5f;
            int attempts = Mathf.CeilToInt(Settings.BoardWidth / step);
            for (int i = 0; i <= attempts; i++)
            {
                for (int side = 1; side >= -1; side -= 2)
                {
                    var candidate = board.ClampInside(preferred + Vector3.right * (side * i * step), Radius);
                    if (!IsOccupied(candidate, self)) return candidate;
                    if (i == 0) break;
                }
            }
            return board.ClampInside(preferred, Radius);
        }

        // Every bot gets a brain, including an online player replaced by AI mid-match.
        private void EnsureBrains()
        {
            foreach (var avatar in avatars)
            {
                if (!(avatar.Slot.Input is AIInput aiInput) || HasBrain(avatar)) continue;
                brains.Add(new CrossyRoadAIBrain(avatar, aiInput, board, traffic, grandmas, oil));
            }
        }

        private bool HasBrain(CrossyRoadPlayer avatar)
        {
            foreach (var brain in brains)
                if (brain.Player == avatar) return true;
            return false;
        }

        // ---------- Online ----------

        // Cars, grandmas, oil and every avatar, in the order of Players on all machines.
        protected override void WriteSnapshot(BinaryWriter writer)
        {
            traffic.WriteState(writer);
            writer.Write(grandmas.AvailableMask);
            oil.WriteState(writer);
            writer.Write((byte)avatars.Count);
            foreach (var avatar in avatars) avatar.WriteState(writer);
        }

        protected override void ReadSnapshot(BinaryReader reader)
        {
            traffic.ReadState(reader);
            grandmas.ApplyAvailableMask(reader.ReadInt32());
            oil.ReadState(reader);
            int count = reader.ReadByte();
            // Avatars always come in the same order and count on host and clients.
            for (int i = 0; i < count && i < avatars.Count; i++) avatars[i].ReadState(reader);
        }

        protected override void OnNetworkEvent(byte eventId, BinaryReader reader)
        {
            if (eventId == GrandmaLaunchedEvent)
                grandmas.Launch(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        private bool IsMoveBlocked(Vector3 position, CrossyRoadPlayer self) => traffic.Overlaps(position, Radius);

        private bool IsOccupied(Vector3 position, CrossyRoadPlayer self)
        {
            float minDistance = Radius * 2f;
            foreach (var other in avatars)
            {
                if (other == self || !other.IsAlive) continue;
                var offset = other.Position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude < minDistance * minDistance) return true;
            }
            return false;
        }
    }
}
