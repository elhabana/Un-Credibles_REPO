using System;
using System.Collections;
using System.Collections.Generic;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Base for every minigame. Owns the shared flow:
    // Initialize -> Waiting -> Countdown -> Playing -> Finishing -> Results -> Exit.
    // Subclasses only add their specific rules through the protected hooks.
    public abstract class MinigameController : MonoBehaviour, IMinigame
    {
        [SerializeField] private MinigameData data;
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private RoundTimer roundTimer;

        private readonly List<PlayerSlot> players = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);
        private IReadOnlyList<MinigameResult> results = Array.Empty<MinigameResult>();
        private Coroutine countdownRoutine;

        public MinigameData Data => data;
        public MinigameState State { get; private set; } = MinigameState.None;
        public bool IsPlaying => State == MinigameState.Playing;
        public bool IsDebugSession { get; private set; }
        public IReadOnlyList<PlayerSlot> Players => players;
        public SpawnManager Spawns => spawnManager;
        public ScoreManager Score => scoreManager;
        public RoundTimer Timer => roundTimer;

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
            SetState(MinigameState.Initialize);
            players.Clear();
            players.AddRange(context.Players);
            if (scoreManager != null) scoreManager.ResetScores(players);
            OnInitialize(context);
            SetState(MinigameState.Waiting);
        }

        public void StartCountdown()
        {
            if (State != MinigameState.Waiting) return;
            SetState(MinigameState.Countdown);
            countdownRoutine = StartCoroutine(CountdownRoutine());
        }

        public void StartGame()
        {
            if (State != MinigameState.Waiting && State != MinigameState.Countdown) return;
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
            if (State != MinigameState.Playing) return;
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
            OnExit();
        }

        protected virtual void OnInitialize(MinigameContext context) { }
        protected abstract void OnGameStarted();
        protected virtual void OnGameplayLocked(MinigameEndReason reason) { }
        protected virtual void OnExit() { }

        // Default: rank by ScoreManager. Override for custom scoring.
        protected virtual IReadOnlyList<MinigameResult> CalculateResults()
        {
            if (scoreManager != null) return scoreManager.BuildResults();

            var tied = new MinigameResult[players.Count];
            for (int i = 0; i < players.Count; i++) tied[i] = new MinigameResult(players[i].PlayerId, 0, 1);
            return tied;
        }

        protected virtual void OnDestroy() => StopTimer();

        private IEnumerator CountdownRoutine()
        {
            int seconds = data != null ? data.CountdownSeconds : 3;
            var wait = new WaitForSeconds(1f);
            for (int i = seconds; i > 0; i--)
            {
                CountdownTick?.Invoke(i);
                yield return wait;
            }
            countdownRoutine = null;
            CountdownTick?.Invoke(0);
            StartGame();
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
        }
    }
}
