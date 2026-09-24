using System;
using System.Collections.Generic;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Core
{
    // Loads any registered minigame and drives it through IMinigame, without knowing its rules.
    public sealed class MinigameManager : MonoBehaviour
    {
        [Tooltip("Registered minigames. Adding a new one only requires its MinigameData here.")]
        [SerializeField] private MinigameData[] minigames = Array.Empty<MinigameData>();

        private readonly List<PlayerSlot> participants = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);
        private PlayerRegistry players;
        private SceneFlowManager sceneFlow;

        public IReadOnlyList<MinigameData> Minigames => minigames;
        public MinigameData ActiveData { get; private set; }
        public IMinigame ActiveMinigame { get; private set; }

        public event Action<IMinigame> MinigameStarted;
        public event Action<MinigameData, IReadOnlyList<MinigameResult>> MinigameFinished;

        public void Bind(PlayerRegistry registry, SceneFlowManager scenes)
        {
            players = registry;
            sceneFlow = scenes;
            if (isActiveAndEnabled) MinigameHost.Register(HandleMinigameLoaded);
        }

        // Only host minigames once Core has bound us; an unbound manager must not steal them.
        private void OnEnable()
        {
            if (players != null) MinigameHost.Register(HandleMinigameLoaded);
        }

        private void OnDisable()
        {
            MinigameHost.Unregister(HandleMinigameLoaded);
            Detach();
        }

        public bool LoadMinigame(MinigameData data)
        {
            if (data == null || players == null || sceneFlow == null || sceneFlow.IsLoading) return false;
            if (ActiveMinigame != null)
            {
                Debug.LogWarning("Exit the active minigame before loading another one.", this);
                return false;
            }
            if (!data.SupportsPlayerCount(players.OccupiedCount))
            {
                Debug.LogWarning($"{data.DisplayName} needs {data.MinPlayers}-{data.MaxPlayers} players.", this);
                return false;
            }

            ActiveData = data;
            sceneFlow.LoadContent(data.SceneName);
            return true;
        }

        // Prefers minigames not played yet in this match that support the current player count.
        public MinigameData PickNextMinigame(IReadOnlyList<MinigameData> alreadyPlayed, int playerCount)
        {
            var candidates = new List<MinigameData>(minigames.Length);
            foreach (var data in minigames)
                if (data != null && data.SupportsPlayerCount(playerCount) && !Contains(alreadyPlayed, data))
                    candidates.Add(data);

            if (candidates.Count == 0)
                foreach (var data in minigames)
                    if (data != null && data.SupportsPlayerCount(playerCount))
                        candidates.Add(data);

            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        public void ExitActiveMinigame()
        {
            ActiveMinigame?.Exit();
            Detach();
            ActiveData = null;
        }

        // Called by the minigame scene itself once it is loaded (see MinigameHost).
        private void HandleMinigameLoaded(IMinigame minigame)
        {
            if (ActiveMinigame != null)
            {
                Debug.LogWarning("A minigame is already running; ignoring the new one.", this);
                return;
            }

            ActiveMinigame = minigame;
            if (ActiveData == null) ActiveData = minigame.Data;
            minigame.Finished += HandleFinished;
            players.GetActivePlayers(participants);
            minigame.Initialize(new MinigameContext(ActiveData, participants, false));
            MinigameStarted?.Invoke(minigame);
            minigame.StartCountdown();
        }

        private void HandleFinished(IReadOnlyList<MinigameResult> results) =>
            MinigameFinished?.Invoke(ActiveData, results);

        private static bool Contains(IReadOnlyList<MinigameData> list, MinigameData data)
        {
            if (list == null) return false;
            foreach (var item in list)
                if (item == data) return true;
            return false;
        }

        private void Detach()
        {
            if (ActiveMinigame == null) return;
            ActiveMinigame.Finished -= HandleFinished;
            ActiveMinigame = null;
        }
    }
}
