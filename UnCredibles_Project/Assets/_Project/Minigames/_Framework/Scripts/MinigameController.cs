using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Base for every minigame. Owns the shared flow:
    // Initialize -> Waiting -> Countdown -> Playing -> Finishing -> Results -> Exit.
    // Subclasses only add their specific rules through the protected hooks.
    //
    // Online the host runs this flow and sends it to the clients (see MinigameNetwork). On a client
    // the same controller is a replica (IsReplica): it receives the flow, raises the same events so
    // the UI works unchanged, and the subclass only draws the snapshots it receives.
    public abstract class MinigameController : MonoBehaviour, IMinigame
    {
        private const float SnapshotInterval = 0.05f; // 20 snapshots per second

        [SerializeField] private MinigameData data;
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private RoundTimer roundTimer;

        private readonly List<PlayerSlot> players = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);
        private readonly MemoryStream sendStream = new MemoryStream(1024);
        private BinaryWriter sendWriter;
        private IReadOnlyList<MinigameResult> results = Array.Empty<MinigameResult>();
        private Coroutine countdownRoutine;
        private IMinigameNetwork network;
        private float nextSnapshotTime;
        private int lastCountdown = -1;
        private int lastTimerSeconds = -1;

        public MinigameData Data => data;
        public MinigameState State { get; private set; } = MinigameState.None;
        public bool IsPlaying => State == MinigameState.Playing;
        public bool IsDebugSession { get; private set; }
        public IReadOnlyList<PlayerSlot> Players => players;
        public SpawnManager Spawns => spawnManager;
        public ScoreManager Score => scoreManager;
        public RoundTimer Timer => roundTimer;

        // Online client: draws what the host sends and simulates nothing.
        public bool IsReplica { get; private set; }
        // Online host: simulates and sends HUD, snapshots and events to the clients.
        public bool IsNetworkHost { get; private set; }

        public event Action<MinigameState> StateChanged;
        public event Action<int> CountdownTick; // 3, 2, 1 ... and 0 for START.
        public event Action<IReadOnlyList<MinigameResult>> Finished;

        protected virtual void Start() => MinigameHost.TryHandOff(this);

        public void Initialize(MinigameContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (State != MinigameState.None)
            {
                Debug.LogWarning($"{name} is already initialized.", this);
                return;
            }

            if (context.Data != null) data = context.Data;
            IsDebugSession = context.IsDebug;
            network = context.IsDebug ? null : MinigameNetwork.Current;
            IsReplica = network != null && !network.IsHost;
            IsNetworkHost = network != null && network.IsHost;

            SetState(MinigameState.Initialize);
            players.Clear();
            players.AddRange(context.Players);
            if (scoreManager != null) scoreManager.ResetScores(players);
            ConnectNetwork();
            OnInitialize(context);
            SetState(MinigameState.Waiting);
        }

        public void StartCountdown()
        {
            if (IsReplica || State != MinigameState.Waiting) return;
            SetState(MinigameState.Countdown);
            countdownRoutine = StartCoroutine(CountdownRoutine());
        }

        public void StartGame()
        {
            if (IsReplica || State != MinigameState.Waiting && State != MinigameState.Countdown) return;
            StopCountdown();
            SetState(MinigameState.Playing);
            if (roundTimer != null && data != null && data.Duration > 0f)
            {
                roundTimer.Expired += HandleTimerExpired;
                roundTimer.StartTimer(data.Duration);
            }
            OnGameStarted();
        }

        // Every ending goes through here: lock gameplay, compute, show and report results.
        public void EndGame(MinigameEndReason reason)
        {
            if (IsReplica || State != MinigameState.Playing) return;
            SetState(MinigameState.Finishing);
            StopTimer();
            OnGameplayLocked(reason);
            results = CalculateResults() ?? Array.Empty<MinigameResult>();
            SetState(MinigameState.Results);
            Finished?.Invoke(results);
        }

        public IReadOnlyList<MinigameResult> GetResults() => results;

        public void Exit()
        {
            if (State == MinigameState.Exit) return;
            StopCountdown();
            StopTimer();
            SetState(MinigameState.Exit);
            DisconnectNetwork();
            OnExit();
        }

        protected virtual void OnInitialize(MinigameContext context) { }
        protected abstract void OnGameStarted();
        protected virtual void OnGameplayLocked(MinigameEndReason reason) { }
        protected virtual void OnExit() { }

        // ---------- Online hooks for subclasses ----------

        // Host: write everything that moves. Called ~20 times per second while the game is on screen.
        protected virtual void WriteSnapshot(BinaryWriter writer) { }
        // Client: apply what WriteSnapshot wrote, in the same order.
        protected virtual void ReadSnapshot(BinaryReader reader) { }
        // Client: a one-shot event sent by the host with BeginEvent / SendEvent.
        protected virtual void OnNetworkEvent(byte eventId, BinaryReader reader) { }

        // Host: start an event, write its payload into the returned writer, then call SendEvent().
        protected BinaryWriter BeginEvent(byte eventId)
        {
            ResetWriter();
            sendWriter.Write(eventId);
            return sendWriter;
        }

        protected void SendEvent()
        {
            if (IsNetworkHost) network.SendEvent(sendStream.GetBuffer(), (int)sendStream.Length);
        }

        // Default: rank by ScoreManager. Override for custom scoring.
        protected virtual IReadOnlyList<MinigameResult> CalculateResults()
        {
            if (scoreManager != null) return scoreManager.BuildResults();

            var tied = new MinigameResult[players.Count];
            for (int i = 0; i < players.Count; i++) tied[i] = new MinigameResult(players[i].PlayerId, 0, 1);
            return tied;
        }

        protected virtual void OnDestroy()
        {
            StopTimer();
            DisconnectNetwork();
        }

        // Host: snapshots at a fixed rate while there is something to see.
        protected virtual void LateUpdate()
        {
            if (!IsNetworkHost || State < MinigameState.Waiting || State > MinigameState.Results) return;
            if (Time.unscaledTime < nextSnapshotTime) return;
            nextSnapshotTime = Time.unscaledTime + SnapshotInterval;

            ResetWriter();
            WriteSnapshot(sendWriter);
            if (sendStream.Length > 0) network.SendSnapshot(sendStream.GetBuffer(), (int)sendStream.Length);
        }

        private IEnumerator CountdownRoutine()
        {
            int seconds = data != null ? data.CountdownSeconds : 3;
            var wait = new WaitForSeconds(1f);
            for (int i = seconds; i > 0; i--)
            {
                RaiseCountdown(i);
                yield return wait;
            }
            countdownRoutine = null;
            RaiseCountdown(0);
            StartGame();
        }

        private void RaiseCountdown(int secondsLeft)
        {
            lastCountdown = secondsLeft;
            CountdownTick?.Invoke(secondsLeft);
            SendHud();
        }

        private void HandleTimerExpired() => EndGame(MinigameEndReason.TimerFinished);

        private void StopCountdown()
        {
            if (countdownRoutine == null) return;
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        private void StopTimer()
        {
            if (roundTimer == null) return;
            roundTimer.Expired -= HandleTimerExpired;
            roundTimer.Stop();
        }

        private void SetState(MinigameState next)
        {
            State = next;
            StateChanged?.Invoke(next);
            SendHud();
        }

        // ---------- Network plumbing ----------

        private void ConnectNetwork()
        {
            if (network == null) return;
            sendWriter ??= new BinaryWriter(sendStream);
            if (IsNetworkHost)
            {
                if (roundTimer != null) roundTimer.SecondChanged += HandleTimerSecond;
                if (scoreManager != null) scoreManager.ScoreChanged += HandleScoreChanged;
            }
            else
            {
                network.HudReceived += ApplyHud;
                network.SnapshotReceived += ApplySnapshot;
                network.EventReceived += ApplyEvent;
            }
        }

        private void DisconnectNetwork()
        {
            if (network == null) return;
            if (roundTimer != null) roundTimer.SecondChanged -= HandleTimerSecond;
            if (scoreManager != null) scoreManager.ScoreChanged -= HandleScoreChanged;
            network.HudReceived -= ApplyHud;
            network.SnapshotReceived -= ApplySnapshot;
            network.EventReceived -= ApplyEvent;
            network = null;
            IsNetworkHost = false;
        }

        private void HandleTimerSecond(int seconds)
        {
            lastTimerSeconds = seconds;
            SendHud();
        }

        private void HandleScoreChanged(int playerId, int score) => SendHud();

        private void ResetWriter()
        {
            sendWriter ??= new BinaryWriter(sendStream);
            sendStream.Position = 0;
            sendStream.SetLength(0);
        }

        // HUD layout: state, countdown, timer, scores (playerId, score), results (playerId, score, placement).
        private void SendHud()
        {
            if (!IsNetworkHost || State < MinigameState.Waiting) return;
            ResetWriter();
            sendWriter.Write((byte)State);
            sendWriter.Write((short)lastCountdown);
            sendWriter.Write((short)lastTimerSeconds);

            sendWriter.Write((byte)players.Count);
            foreach (var player in players)
            {
                sendWriter.Write(player.PlayerId);
                sendWriter.Write(scoreManager != null ? scoreManager.GetScore(player.PlayerId) : 0);
            }

            sendWriter.Write((byte)results.Count);
            foreach (var result in results)
            {
                sendWriter.Write(result.PlayerId);
                sendWriter.Write(result.Score);
                sendWriter.Write((byte)result.Placement);
            }
            network.SendHud(sendStream.GetBuffer(), (int)sendStream.Length);
        }

        private void ApplyHud(byte[] buffer, int length)
        {
            using var reader = new BinaryReader(new MemoryStream(buffer, 0, length, false));
            var state = (MinigameState)reader.ReadByte();
            int countdown = reader.ReadInt16();
            int timerSeconds = reader.ReadInt16();

            int scoreCount = reader.ReadByte();
            for (int i = 0; i < scoreCount; i++)
            {
                int playerId = reader.ReadInt32();
                int score = reader.ReadInt32();
                if (scoreManager != null) scoreManager.SetScore(playerId, score);
            }

            int resultCount = reader.ReadByte();
            var received = new MinigameResult[resultCount];
            for (int i = 0; i < resultCount; i++)
                received[i] = new MinigameResult(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte());

            if (countdown != lastCountdown)
            {
                lastCountdown = countdown;
                if (countdown >= 0) CountdownTick?.Invoke(countdown);
            }
            if (timerSeconds != lastTimerSeconds && roundTimer != null)
            {
                lastTimerSeconds = timerSeconds;
                if (timerSeconds >= 0) roundTimer.ShowRemote(timerSeconds);
            }
            if (state == State) return;

            if (state == MinigameState.Results) results = received;
            State = state;
            StateChanged?.Invoke(state);
            if (state == MinigameState.Results) Finished?.Invoke(results);
        }

        private void ApplySnapshot(byte[] buffer, int length)
        {
            if (State < MinigameState.Waiting) return;
            using var reader = new BinaryReader(new MemoryStream(buffer, 0, length, false));
            ReadSnapshot(reader);
        }

        private void ApplyEvent(byte[] buffer, int length)
        {
            using var reader = new BinaryReader(new MemoryStream(buffer, 0, length, false));
            OnNetworkEvent(reader.ReadByte(), reader);
        }
    }
}
