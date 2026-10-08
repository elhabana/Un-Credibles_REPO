using System;
using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Players on floats around a kid spinning a churro. Jump to dodge it and duck under the beach
    // balls thrown at you; if either hits you, you are out for the round. Each round ends when one
    // player is left. Points per round = players that fell before you, so after all rounds whoever
    // lasted longest wins. From round 2 a kid cannonballs into the pool and splashes the screen.
    // The only Update of the minigame: spinner, hazards, AI and avatars are ticked from here in a
    // fixed order. Who gets a ball and when the kid jumps is decided by the two directors.
    public sealed class ChurroController : MinigameController
    {
        private enum RoundPhase { None, Intro, Spinning, Outro }

        private const byte RoundStartedEvent = 1;
        private const byte RoundEndedEvent = 2;
        private const byte BallThrownEvent = 3;
        private const byte CannonballEvent = 4;
        private const int StartAngleAttempts = 32;
        private const float DefaultStartAngle = 45f;

        [SerializeField] private ChurroSettings settings;
        [SerializeField] private ChurroSpinner spinner;
        [SerializeField] private ChurroBalls balls;
        [SerializeField] private ChurroCannonball cannonball;
        [SerializeField] private ChurroPlayer playerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<ChurroPlayer> avatars = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroAIBrain> brains = new List<ChurroAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> hitThisFrame = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> ballHits = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private ChurroBallDirector ballDirector;
        private ChurroCannonDirector cannonDirector;
        private RoundPhase phase;
        private int roundIndex;
        private int fallenThisRound;
        private float phaseTimer;
        private float hitHalfAngle;
        private float firstStartAngle = DefaultStartAngle;
        private int firstDirection = 1;

        public int CurrentRound => roundIndex + 1;
        public int TotalRounds => settings.RoundCount;

        public event Action<int, int> RoundStarted;                 // round, total
        public event Action<int, IReadOnlyList<ChurroPlayer>> RoundEnded; // round, survivors

        protected override void OnInitialize(MinigameContext context)
        {
            float floatDistance = 0f;
            foreach (var player in Players)
            {
                var avatar = Spawns.Spawn(playerPrefab, player, playersParent);
                avatar.Setup(player, settings, spinner.Center);
                avatars.Add(avatar);
                floatDistance = Vector3.Distance(ChurroAngles.Flat(avatar.transform.position), ChurroAngles.Flat(spinner.Center));
            }

            // Angular half width of a player seen from the centre.
            hitHalfAngle = floatDistance > 0.01f ? Mathf.Atan2(settings.PlayerRadius, floatDistance) * Mathf.Rad2Deg : 10f;

            balls.Initialize(settings, spinner.Center);
            cannonball.Initialize(settings);
            if (!IsReplica)
            {
                ballDirector = new ChurroBallDirector(settings, spinner, balls, avatars, hitHalfAngle);
                cannonDirector = new ChurroCannonDirector(settings, cannonball, avatars, spinner.Center);
                EnsureBrains();
                // Pick the first round start now, so the countdown already shows the churro where it will start.
                firstStartAngle = FairStartAngle(settings.GetRound(0).arms);
                firstDirection = RandomDirection();
            }
            spinner.ResetForRound(settings, settings.GetRound(0), firstStartAngle, firstDirection);
        }

        protected override void OnGameStarted() => BeginRound(0);

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            cannonball.Tick(deltaTime); // only visual: runs the same on host and clients
            if (IsReplica)
            {
                // Online client: only smooth what the host sends (spinner and avatars).
                spinner.TickRemote(deltaTime);
                balls.Tick(deltaTime, null);
                foreach (var avatar in avatars) avatar.TickRemote(deltaTime);
                return;
            }
            if (!IsPlaying) return;
            EnsureBrains(); // an online player who left is now a bot

            switch (phase)
            {
                case RoundPhase.Intro:
                    TickAvatars(deltaTime, false);
                    balls.Tick(deltaTime, null);
                    if ((phaseTimer -= deltaTime) <= 0f) phase = RoundPhase.Spinning;
                    break;

                case RoundPhase.Spinning:
                    TickSpinning(deltaTime);
                    break;

                case RoundPhase.Outro:
                    TickAvatars(deltaTime, false);
                    balls.Tick(deltaTime, null);
                    if ((phaseTimer -= deltaTime) <= 0f) NextRoundOrFinish();
                    break;
            }
        }

        private void TickSpinning(float deltaTime)
        {
            var round = settings.GetRound(roundIndex);
            spinner.Tick(deltaTime);

            var ballTarget = ballDirector.Tick(deltaTime, round);
            if (ballTarget != null) SendBallThrown(ballTarget, settings.BallFlightSeconds);
            if (cannonDirector.Tick(deltaTime, round, out var start, out var landing)) SendCannonball(start, landing);

            foreach (var brain in brains) brain.Tick(); // before avatars read their input
            TickAvatars(deltaTime, true);
            ballHits.Clear();
            balls.Tick(deltaTime, ballHits);
            CheckHits();
        }

        // ---------- Rounds ----------

        private void BeginRound(int index)
        {
            roundIndex = index;
            fallenThisRound = 0;
            foreach (var avatar in avatars) avatar.ResetOnFloat();

            // Random place and direction every round, never right next to a player.
            var round = settings.GetRound(index);
            if (index == 0) spinner.ResetForRound(settings, round, firstStartAngle, firstDirection);
            else spinner.ResetForRound(settings, round, FairStartAngle(round.arms), RandomDirection());
            balls.Clear();
            ballDirector.ResetForRound();
            cannonDirector.ResetForRound();
            phase = RoundPhase.Intro;
            phaseTimer = settings.RoundIntroSeconds;

            RoundStarted?.Invoke(CurrentRound, TotalRounds);
            var message = BeginEvent(RoundStartedEvent);
            message.Write((byte)CurrentRound);
            message.Write((byte)TotalRounds);
            SendEvent();
        }

        private void EndRound()
        {
            var survivors = new List<ChurroPlayer>(1);
            foreach (var avatar in avatars)
            {
                if (!avatar.IsIn) continue;
                Score.AddScore(avatar.Slot.PlayerId, fallenThisRound);
                survivors.Add(avatar);
            }

            phase = RoundPhase.Outro;
            phaseTimer = settings.RoundOutroSeconds;

            RoundEnded?.Invoke(CurrentRound, survivors);
            var message = BeginEvent(RoundEndedEvent);
            message.Write((byte)CurrentRound);
            message.Write((byte)survivors.Count);
            foreach (var survivor in survivors) message.Write((byte)avatars.IndexOf(survivor));
            SendEvent();
        }

        private void NextRoundOrFinish()
        {
            if (roundIndex + 1 < settings.RoundCount) BeginRound(roundIndex + 1);
            else
            {
                phase = RoundPhase.None;
                EndGame(MinigameEndReason.ObjectiveCompleted);
            }
        }

        // A random angle for the churro that keeps every arm well away from every player.
        private float FairStartAngle(int armCount) =>
            ChurroAngles.RandomClearAngle(avatars, armCount, hitHalfAngle + settings.StartClearance, StartAngleAttempts, DefaultStartAngle);

        private static int RandomDirection() => UnityEngine.Random.value < 0.5f ? 1 : -1;

        // ---------- Avatars and hits ----------

        private void TickAvatars(float deltaTime, bool canJump)
        {
            foreach (var avatar in avatars) avatar.Tick(deltaTime, canJump);
        }

        // Churro (feet too low while an arm sweeps over) and beach balls (standing when one arrives).
        private void CheckHits()
        {
            hitThisFrame.Clear();
            foreach (var avatar in avatars)
                if (avatar.IsIn && avatar.FeetHeight < settings.ClearHeight && spinner.SweptThrough(avatar.Angle, hitHalfAngle))
                    hitThisFrame.Add(avatar);
            foreach (var avatar in ballHits)
                if (avatar.IsIn && !hitThisFrame.Contains(avatar)) hitThisFrame.Add(avatar);

            // Players hit in the same frame share the same result.
            foreach (var avatar in hitThisFrame)
            {
                Score.AddScore(avatar.Slot.PlayerId, fallenThisRound);
                avatar.KnockOut(avatar.transform.position - spinner.Center);
            }
            fallenThisRound += hitThisFrame.Count;

            int stopAt = avatars.Count > 1 ? 1 : 0;
            if (CountIn() <= stopAt) EndRound();
        }

        private int CountIn()
        {
            int count = 0;
            foreach (var avatar in avatars)
                if (avatar.IsIn) count++;
            return count;
        }

        // Every bot gets a brain, including an online player replaced by AI mid-match.
        private void EnsureBrains()
        {
            foreach (var avatar in avatars)
            {
                if (!(avatar.Slot.Input is AIInput aiInput) || HasBrain(avatar)) continue;
                brains.Add(new ChurroAIBrain(avatar, aiInput, spinner, balls, settings, hitHalfAngle));
            }
        }

        private bool HasBrain(ChurroPlayer avatar)
        {
            foreach (var brain in brains)
                if (brain.Player == avatar) return true;
            return false;
        }

        // ---------- Online ----------

        // Spinner and every avatar, in the order of Players on all machines.
        protected override void WriteSnapshot(BinaryWriter writer)
        {
            spinner.WriteState(writer);
            writer.Write((byte)avatars.Count);
            foreach (var avatar in avatars) avatar.WriteState(writer);
        }

        protected override void ReadSnapshot(BinaryReader reader)
        {
            spinner.ReadState(reader);
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                if (i < avatars.Count) avatars[i].ReadState(reader);
                else reader.ReadBytes(ChurroPlayer.StateSize);
            }
        }

        private void SendBallThrown(ChurroPlayer target, float flight)
        {
            var message = BeginEvent(BallThrownEvent);
            message.Write((byte)avatars.IndexOf(target));
            message.Write(flight);
            SendEvent();
        }

        private void SendCannonball(Vector3 start, Vector3 landing)
        {
            var message = BeginEvent(CannonballEvent);
            WriteVector(message, start);
            WriteVector(message, landing);
            SendEvent();
        }

        protected override void OnNetworkEvent(byte eventId, BinaryReader reader)
        {
            switch (eventId)
            {
                case RoundStartedEvent:
                    roundIndex = reader.ReadByte() - 1;
                    RoundStarted?.Invoke(CurrentRound, reader.ReadByte());
                    break;

                case BallThrownEvent:
                {
                    int index = reader.ReadByte();
                    float flight = reader.ReadSingle();
                    if (index < avatars.Count) balls.Throw(avatars[index], flight);
                    break;
                }

                case CannonballEvent:
                    cannonball.Play(ReadVector(reader), ReadVector(reader));
                    break;

                case RoundEndedEvent:
                {
                    int round = reader.ReadByte();
                    int count = reader.ReadByte();
                    var survivors = new List<ChurroPlayer>(count);
                    for (int i = 0; i < count; i++)
                    {
                        int index = reader.ReadByte();
                        if (index < avatars.Count) survivors.Add(avatars[index]);
                    }
                    RoundEnded?.Invoke(round, survivors);
                    break;
                }
            }
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static Vector3 ReadVector(BinaryReader reader) =>
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
