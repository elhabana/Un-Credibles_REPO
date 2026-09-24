using System.Collections;
using System.Collections.Generic;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    // Composition root of 01_Core: creates the global systems once and wires them together.
    // It is the only global access point; systems do not reference each other through it.
    public sealed class CoreRoot : MonoBehaviour
    {
        [SerializeField] private SceneFlowManager sceneFlow;
        [SerializeField] private MinigameManager minigames;
        [SerializeField] private InputManager input;
        [SerializeField] private AudioManager audioManager;
        [Tooltip("Match points given for 1st, 2nd, 3rd and 4th place in each minigame.")]
        [SerializeField] private int[] pointsByPlacement = { 4, 3, 2, 1 };
        [Tooltip("Seconds the minigame's own results stay visible before the Results scene.")]
        [SerializeField, Min(0f)] private float minigameResultsSeconds = 3f;

        private readonly List<PlayerSlot> playersBuffer = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);

        public static CoreRoot Instance { get; private set; }

        public GameFlowManager GameFlow { get; private set; }
        public PlayerRegistry Players { get; private set; }
        public MatchManager Match { get; private set; }
        public SettingsManager Settings { get; private set; }
        public SceneFlowManager Scenes => sceneFlow;
        public MinigameManager Minigames => minigames;
        public InputManager Input => input;
        public AudioManager Audio => audioManager;
        public RelayConnection Online { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // NGO persists its own root; never attach it to Core's scene objects.
            Online = new GameObject("Online Connection").AddComponent<RelayConnection>();

            GameFlow = new GameFlowManager();
            Players = new PlayerRegistry();
            Match = new MatchManager(pointsByPlacement);
            Settings = new SettingsManager();

            audioManager.Bind(Settings);
            Settings.Load();
            minigames.Bind(Players, sceneFlow);
            minigames.MinigameStarted += HandleMinigameStarted;
            minigames.MinigameFinished += HandleMinigameFinished;
        }

        private IEnumerator Start()
        {
            if (Instance != this) yield break;

            // Boot flow loads the menu; a scene played directly from the editor is kept as it is.
            bool launchedFromBoot = SceneManager.GetSceneByName(GameScenes.Boot).isLoaded;
            if (!launchedFromBoot && sceneFlow.TryAdoptLoadedContentScene(out var sceneName))
            {
                if (sceneName == GameScenes.MainMenu) GameFlow.ChangeState(GameState.MainMenu);
                yield break;
            }

            yield return sceneFlow.LoadContentRoutine(GameScenes.MainMenu);
            GameFlow.ChangeState(GameState.MainMenu);
        }

        public void OpenPartyLobby(SessionMode mode)
        {
            GameFlow.SetSession(mode);
            sceneFlow.LoadContent(GameScenes.PartyLobby);
        }

        public void ReturnToMainMenu()
        {
            if (GameFlow.Session == SessionMode.Online) Online.Disconnect();
            GameFlow.SetSession(SessionMode.Local);
            minigames.ExitActiveMinigame();
            Match.CancelMatch();
            Players.Clear();
            sceneFlow.LoadContent(GameScenes.MainMenu, () => GameFlow.ChangeState(GameState.MainMenu));
        }

        // Called by the PartyLobby when every player is ready.
        public bool StartMatch(int rounds)
        {
            if (GameFlow.Session == SessionMode.Online) return false;
            Players.GetActivePlayers(playersBuffer);
            Match.StartMatch(playersBuffer, rounds);
            return StartNextRound();
        }

        // Called by the Results screen: next minigame of the running match.
        public bool StartNextRound()
        {
            if (!Match.IsRunning) return false;
            var next = minigames.PickNextMinigame(Match.PlayedMinigames, Players.OccupiedCount);
            if (next == null)
            {
                Debug.LogError("No registered minigame supports this number of players. Check MinigameManager.", this);
                return false;
            }

            GameFlow.ChangeState(GameState.Loading);
            return minigames.LoadMinigame(next);
        }

        // Same players go back to the lobby for another match.
        public void ReturnToLobby()
        {
            minigames.ExitActiveMinigame();
            Match.CancelMatch();
            OpenPartyLobby(GameFlow.Session);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (Online != null) Destroy(Online.gameObject);
            minigames.MinigameStarted -= HandleMinigameStarted;
            minigames.MinigameFinished -= HandleMinigameFinished;
            Players.Clear();
            Instance = null;
        }

        private void HandleMinigameStarted(IMinigame minigame) => GameFlow.ChangeState(GameState.Minigame);

        private void Update()
        {
            if (GameFlow.Session == SessionMode.Online && !Online.IsConnected && !sceneFlow.IsLoading)
                ReturnToMainMenu();
        }

        private void HandleMinigameFinished(MinigameData data, IReadOnlyList<MinigameResult> results)
        {
            Match.RegisterResults(data, results);
            StartCoroutine(ShowResultsRoutine());
        }

        // Leave the minigame's own results on screen for a moment, then show the match standings.
        private IEnumerator ShowResultsRoutine()
        {
            yield return new WaitForSeconds(minigameResultsSeconds);
            minigames.ExitActiveMinigame();
            GameFlow.ChangeState(Match.IsRunning ? GameState.Results : GameState.FinalResults);
            sceneFlow.LoadContent(GameScenes.Results);
        }
    }
}
