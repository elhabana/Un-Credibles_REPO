using System.Collections;
using System.Collections.Generic;
using UnCredibles.BatPad;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
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
        [SerializeField] private BatPadService batPad;
        [Tooltip("Match points given for 1st, 2nd, 3rd and 4th place in each minigame.")]
        [SerializeField] private int[] pointsByPlacement = { 4, 3, 2, 1 };
        [Tooltip("Seconds the minigame's own results stay visible before the Results scene.")]
        [SerializeField, Min(0f)] private float minigameResultsSeconds = 3f;

        private readonly List<PlayerSlot> playersBuffer = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);
        private static readonly PlayerAction[] Actions = (PlayerAction[])System.Enum.GetValues(typeof(PlayerAction));
        private const float InputSendInterval = 1f / 30f;
        private string pendingScene;   // online client: scene the host asked for, loaded when possible
        private IPlayerInput onlineInput; // online client: the controller sent to the host
        private float nextInputSend;
        private bool wasOnlineHost;

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
        public OnlineSession Room { get; private set; }
        public BatPadService BatPad => batPad;

        // Online: the connection dropped. The game is frozen until the overlay sends us to the menu.
        public bool IsConnectionLost { get; private set; }
        public event System.Action<string, string> ConnectionLost; // title, detail

        private bool IsOnlineHost => GameFlow.Session == SessionMode.Online && Online.IsHost;
        private bool IsOnlineClient => GameFlow.Session == SessionMode.Online && !Online.IsHost;

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
            Room = new OnlineSession(Online, Players);

            audioManager.Bind(Settings);
            Settings.Load();
            minigames.Bind(Players, sceneFlow);
            minigames.ClientsReady = Room.AllClientsIn;
            Room.SceneRequested += HandleSceneRequested;
            minigames.MinigameStarted += HandleMinigameStarted;
            minigames.MinigameFinished += HandleMinigameFinished;
            batPad.PhoneDisconnected += HandlePhoneDisconnected;
            batPad.PhoneReconnected += HandlePhoneReconnected;
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
            IsConnectionLost = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (GameFlow.Session == SessionMode.Online) Online.Disconnect();
            pendingScene = null;
            onlineInput?.Dispose();
            onlineInput = null;
            GameFlow.SetSession(SessionMode.Local);
            minigames.ExitActiveMinigame();
            Match.CancelMatch();
            Players.Clear();
            batPad.ClearSlots(); // phones stay connected but have to join again from the lobby
            sceneFlow.LoadContent(GameScenes.MainMenu, () => GameFlow.ChangeState(GameState.MainMenu));
        }

        // Called by the PartyLobby when every player is ready.
        public bool StartMatch(int rounds)
        {
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
            if (IsOnlineHost) Room.SendScene(next.SceneName, (byte)GameState.Minigame);
            return minigames.LoadMinigame(next);
        }

        // Same players go back to the lobby for another match.
        public void ReturnToLobby()
        {
            if (IsOnlineHost) Room.SendScene(GameScenes.PartyLobby, (byte)GameState.PartyLobby);
            minigames.ExitActiveMinigame();
            Match.CancelMatch();
            OpenPartyLobby(GameFlow.Session);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Room.SceneRequested -= HandleSceneRequested;
            Room?.Dispose();
            onlineInput?.Dispose();
            if (Online != null) Destroy(Online.gameObject);
            minigames.MinigameStarted -= HandleMinigameStarted;
            minigames.MinigameFinished -= HandleMinigameFinished;
            batPad.PhoneDisconnected -= HandlePhoneDisconnected;
            batPad.PhoneReconnected -= HandlePhoneReconnected;
            Players.Clear();
            Instance = null;
        }

        // A phone that drops keeps its slot (and score) as Disconnected until it comes back;
        // the lobby frees the slot if it takes too long.
        private void HandlePhoneDisconnected(BatPadInput phone) => SetPhoneConnected(phone, false);
        private void HandlePhoneReconnected(BatPadInput phone) => SetPhoneConnected(phone, true);

        private void SetPhoneConnected(BatPadInput phone, bool connected)
        {
            foreach (var slot in Players.Slots)
                if (slot.Input == phone) Players.SetConnected(slot.SlotIndex, connected);
        }

        private void HandleMinigameStarted(IMinigame minigame) => GameFlow.ChangeState(GameState.Minigame);

        private void Update()
        {
            if (GameFlow.Session != SessionMode.Online) return;
            if (Online.IsConnected) wasOnlineHost = Online.IsHost;
            else
            {
                if (!sceneFlow.IsLoading) LoseConnection();
                return;
            }
            if (IsOnlineClient)
            {
                LoadPendingScene();
                SendOnlineInput();
            }
        }

        // Freeze everything and let the overlay explain what happened before going back to the menu.
        private void LoseConnection()
        {
            if (IsConnectionLost) return;
            if (ConnectionLost == null)
            {
                ReturnToMainMenu();
                return;
            }
            IsConnectionLost = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ConnectionLost.Invoke(wasOnlineHost ? "Conexion perdida" : "El host se ha desconectado", Online.Status);
        }

        private void HandleMinigameFinished(MinigameData data, IReadOnlyList<MinigameResult> results)
        {
            if (IsOnlineClient) return; // the host decides what comes next
            Match.RegisterResults(data, results);
            StartCoroutine(ShowResultsRoutine());
        }

        // Leave the minigame's own results on screen for a moment, then show the match standings.
        private IEnumerator ShowResultsRoutine()
        {
            yield return new WaitForSeconds(minigameResultsSeconds);
            minigames.ExitActiveMinigame();
            GameFlow.ChangeState(Match.IsRunning ? GameState.Results : GameState.FinalResults);
            if (IsOnlineHost)
            {
                Room.SendStandings(BuildStandings());
                Room.SendScene(GameScenes.Results, (byte)GameFlow.State);
            }
            sceneFlow.LoadContent(GameScenes.Results);
        }

        // ---------- Online client: the host decides which scene everybody is in ----------

        private void HandleSceneRequested(string sceneName, byte gameState)
        {
            if (!IsOnlineClient) return;
            minigames.ExitActiveMinigame();
            GameFlow.ChangeState((GameState)gameState);
            pendingScene = sceneName;
            LoadPendingScene();
        }

        private void LoadPendingScene()
        {
            if (pendingScene == null || sceneFlow.IsLoading) return;
            string sceneName = pendingScene;
            pendingScene = null;
            sceneFlow.LoadContent(sceneName, () => Room.SendLoaded(sceneName));
        }

        // The client's player is simulated by the host: send it what this machine's controller does.
        // Presses go out at once (reliable); the stick at most 30 times per second.
        private void SendOnlineInput()
        {
            if (GameFlow.State != GameState.Minigame && GameFlow.State != GameState.Results) return;
            onlineInput ??= input.CreateAnyDeviceInput();

            int pressed = 0, held = 0;
            foreach (var action in Actions)
            {
                if (onlineInput.WasPressed(action)) pressed |= 1 << (int)action;
                if (onlineInput.IsHeld(action)) held |= 1 << (int)action;
            }
            if (pressed == 0 && Time.unscaledTime < nextInputSend) return;
            nextInputSend = Time.unscaledTime + InputSendInterval;
            Room.SendInput(onlineInput.Move, pressed, held);
        }

        // ---------- Online host: what the clients' Results screen shows ----------

        private OnlineStandings BuildStandings()
        {
            var standings = new OnlineStandings
            {
                Round = Match.CurrentRound,
                TotalRounds = Match.TotalRounds,
                IsFinal = !Match.IsRunning,
            };
            var lastRound = Match.LastRoundResults;
            foreach (var standing in Match.GetStandings())
            {
                int gained = 0;
                foreach (var result in lastRound)
                    if (result.PlayerId == standing.PlayerId) gained = Match.PointsFor(result.Placement);
                standings.Rows.Add(new OnlineStandings.Row
                {
                    Name = Players.TryGetByPlayerId(standing.PlayerId, out var slot) ? slot.PlayerName : $"Player {standing.PlayerId + 1}",
                    Placement = standing.Placement,
                    Gained = gained,
                    Total = standing.Score,
                });
            }
            return standings;
        }
    }
}
