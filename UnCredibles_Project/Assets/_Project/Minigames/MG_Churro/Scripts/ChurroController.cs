using System;
using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Players on floats around a kid spinning a churro. Jump to dodge it and duck under the beach
    // balls thrown at you; if either hits you, you are out
    // for the round. Each round ends when one player is left. Points per round = players that fell
    // before you, so after all rounds whoever lasted longest wins.
    // The only Update of the minigame: spinner, AI and avatars are ticked from here in a fixed order.
    public sealed class ChurroController : MinigameController
    {
        private enum RoundPhase { None, Intro, Spinning, Outro }

        private const byte RoundStartedEvent = 1;
        private const byte RoundEndedEvent = 2;
        private const byte BallThrownEvent = 3;

        [SerializeField] private ChurroSettings settings;
        [SerializeField] private ChurroSpinner spinner;
        [SerializeField] private ChurroBalls balls;
        [SerializeField] private ChurroPlayer playerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<ChurroPlayer> avatars = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroAIBrain> brains = new List<ChurroAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> hitThisFrame = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> ballHits = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> ballCandidates = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private float ballTimer;
        private float spinTime; // seconds the churro has been spinning this round
        private float firstStartAngle = 45f;
        private int firstDirection = 1;
        private RoundPhase phase;
        private int roundIndex;
        private int fallenThisRound;
        private float phaseTimer;
        private float hitHalfAngle;

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
                avatar.Setup(player, settings, spinner.Center, settings.GetPlayerColor(player.SlotIndex));
                avatars.Add(avatar);
                floatDistance = Vector3.Distance(Flat(avatar.transform.position), Flat(spinner.Center));
            }

            // Angular half width of a player seen from the centre.
            hitHalfAngle = floatDistance > 0.01f ? Mathf.Atan2(settings.PlayerRadius, floatDistance) * Mathf.Rad2Deg : 10f;

            balls.Initialize(settings, spinner.Center);
            if (!IsReplica)
            {
                EnsureBrains();
                // Pick the first round start now, so the countdown already shows the churro where it will start.
                firstStartAngle = FairStartAngle(settings.GetRound(0).arms);
                firstDirection = UnityEngine.Random.value < 0.5f ? 1 : -1;
            }
            spinner.ResetForRound(settings, settings.GetRound(0), firstStartAngle, firstDirection);
        }

        protected override void OnGameStarted() => BeginRound(0);

        private void Update()
        {
            float deltaTime = Time.deltaTime;
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
                    spinner.Tick(deltaTime);
                    spinTime += deltaTime;
                    TryThrowBall(deltaTime);
                    foreach (var brain in brains) brain.Tick(); // before avatars read their input
                    TickAvatars(deltaTime, true);
                    ballHits.Clear();
                    balls.Tick(deltaTime, ballHits);
                    CheckHits();
                    break;

                case RoundPhase.Outro:
                    TickAvatars(deltaTime, false);
                    balls.Tick(deltaTime, null);
                    if ((phaseTimer -= deltaTime) <= 0f) NextRoundOrFinish();
                    break;
            }
        }

        private void BeginRound(int index)
        {
            roundIndex = index;
            fallenThisRound = 0;
            foreach (var avatar in avatars) avatar.ResetOnFloat();

            // Random place and direction every round, never right next to a player.
            var round = settings.GetRound(index);
            if (index == 0) spinner.ResetForRound(settings, round, firstStartAngle, firstDirection);
            else spinner.ResetForRound(settings, round, FairStartAngle(round.arms), UnityEngine.Random.value < 0.5f ? 1 : -1);
            balls.Clear();
            ballTimer = settings.BallFirstDelay;
            spinTime = 0f;
            phase = RoundPhase.Intro;
            phaseTimer = settings.RoundIntroSeconds;
            RoundStarted?.Invoke(CurrentRound, TotalRounds);
            var message = BeginEvent(RoundStartedEvent);
            message.Write((byte)CurrentRound);
            message.Write((byte)TotalRounds);
            SendEvent();
        }

        private void TickAvatars(float deltaTime, bool canJump)
        {
            foreach (var avatar in avatars) avatar.Tick(deltaTime, canJump);
        }

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

            int remaining = CountIn();
            int stopAt = avatars.Count > 1 ? 1 : 0;
            if (remaining <= stopAt) EndRound();
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

        // ---------- Beach balls ----------

        // Throws a ball at a random player whenever the timing is fair for them.
        private void TryThrowBall(float deltaTime)
        {
            var round = settings.GetRound(roundIndex);
            if (!round.balls || (ballTimer -= deltaTime) > 0f) return;
            // Never while the kid is changing direction: the churro timing is not stable then.
            if (spinner.IsChangingDirection)
            {
                ballTimer = 0.25f;
                return;
            }

            ballCandidates.Clear();
            foreach (var avatar in avatars)
                if (avatar.IsIn && !balls.IsTargeted(avatar)) ballCandidates.Add(avatar);
            float flight = settings.BallFlightSeconds;
            while (ballCandidates.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, ballCandidates.Count);
                var target = ballCandidates[pick];
                ballCandidates.RemoveAt(pick);
                if (!IsFairBall(target, flight)) continue;

                balls.Throw(target, flight);
                // The churro keeps its direction until the ball is gone, so the timing holds.
                spinner.HoldDirection(flight + settings.BallSafetyGap + 0.3f);
                var message = BeginEvent(BallThrownEvent);
                message.Write((byte)avatars.IndexOf(target));
                message.Write(flight);
                SendEvent();
                // More and more balls as the round goes on.
                ballTimer = UnityEngine.Random.Range(round.ballInterval.x, round.ballInterval.y) * settings.BallIntervalFactor(spinTime);
                return;
            }
            ballTimer = 0.25f; // nobody can get a fair ball right now: try again soon
        }

        // Fair = the ball reaches the player well apart from any moment the churro passes them,
        // so there is always time to stand up and jump (or land and duck). Checked with the
        // current speed and with the speed it will have by then, as it keeps accelerating.
        private bool IsFairBall(ChurroPlayer target, float arrival)
        {
            float gap = settings.BallSafetyGap;
            float laterSpeed = Mathf.Min(spinner.Speed + spinner.Acceleration * arrival, spinner.MaxSpeed);
            return IsFairAtSpeed(target, arrival, gap, spinner.Speed) && IsFairAtSpeed(target, arrival, gap, laterSpeed);
        }

        private bool IsFairAtSpeed(ChurroPlayer target, float arrival, float gap, float speed)
        {
            speed = Mathf.Max(speed, 1f);
            float passSeconds = hitHalfAngle * 2f / speed;
            for (int arm = 0; arm < spinner.ArmCount; arm++)
            {
                float armAngle = spinner.Angle + arm * spinner.ArmSpacing;
                float distance = spinner.Direction > 0
                    ? Mathf.Repeat(target.Angle - hitHalfAngle - armAngle, 360f)
                    : Mathf.Repeat(armAngle - (target.Angle + hitHalfAngle), 360f);
                // Previous, next and following passes of this arm over the player.
                for (int lap = -1; lap <= 2; lap++)
                {
                    float start = (distance + lap * 360f) / speed;
                    if (arrival > start - gap && arrival < start + passSeconds + gap) return false;
                }
            }
            return true;
        }

        // A random angle for the churro that keeps every arm well away from every player.
        private float FairStartAngle(int armCount)
        {
            float spacing = 360f / Mathf.Max(1, armCount);
            float wanted = hitHalfAngle + settings.StartClearance;
            float best = 45f, bestGap = -1f;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float candidate = UnityEngine.Random.Range(0f, 360f);
                float gap = float.MaxValue;
                for (int arm = 0; arm < armCount; arm++)
                    foreach (var avatar in avatars)
                        gap = Mathf.Min(gap, Mathf.Abs(Mathf.DeltaAngle(candidate + arm * spacing, avatar.Angle)));
                if (gap >= wanted) return candidate;
                if (gap > bestGap) { bestGap = gap; best = candidate; }
            }
            return best;
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

        // Spinner (angle + speed for prediction) and every avatar, in the order of Players on all machines.
        protected override void WriteSnapshot(BinaryWriter writer)
        {
            writer.Write(spinner.Angle);
            writer.Write(spinner.AngularVelocity);
            writer.Write((byte)spinner.ArmCount);
            writer.Write((byte)avatars.Count);
            foreach (var avatar in avatars)
            {
                var position = avatar.transform.position;
                var rotation = avatar.transform.rotation;
                writer.Write(avatar.IsIn);
                writer.Write(avatar.gameObject.activeSelf);
                writer.Write(avatar.FeetHeight);
                writer.Write(avatar.IsDucking);
                writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
                writer.Write(rotation.x); writer.Write(rotation.y); writer.Write(rotation.z); writer.Write(rotation.w);
            }
        }

        protected override void ReadSnapshot(BinaryReader reader)
        {
            spinner.ApplyRemote(reader.ReadSingle(), reader.ReadSingle(), reader.ReadByte());
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                bool isIn = reader.ReadBoolean();
                bool visible = reader.ReadBoolean();
                float height = reader.ReadSingle();
                bool ducking = reader.ReadBoolean();
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (i < avatars.Count) avatars[i].ApplyRemote(isIn, visible, height, ducking, position, rotation);
            }
        }

        protected override void OnNetworkEvent(byte eventId, BinaryReader reader)
        {
            if (eventId == RoundStartedEvent)
            {
                roundIndex = reader.ReadByte() - 1;
                RoundStarted?.Invoke(CurrentRound, reader.ReadByte());
            }
            else if (eventId == BallThrownEvent)
            {
                int index = reader.ReadByte();
                float flight = reader.ReadSingle();
                if (index < avatars.Count) balls.Throw(avatars[index], flight);
            }
            else if (eventId == RoundEndedEvent)
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
            }
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
