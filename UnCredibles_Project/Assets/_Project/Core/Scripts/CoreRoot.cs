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
        private readonly List<MinigameData> votedMinigames = new List<MinigameData>(3);
        private readonly List<MinigameData> voteCandidates = new List<MinigameData>();
        private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> voteCursor = new Dictionary<int, int>();
        private readonly Dictionary<int, float> nextVoteMove = new Dictionary<int, float>();
        private float voteDeadline;
        private bool voteFinalizing;
        private int voteGeneration;
        private const float VoteSeconds = 12f;
        public IReadOnlyList<MinigameData> VoteCandidates => voteCandidates;
        public IReadOnlyList<MinigameData> VotedMinigames => votedMinigames;
        public IReadOnlyDictionary<int, int> Votes => votes;
        public IReadOnlyDictionary<int, int> VoteCursor => voteCursor;
        public float VoteSecondsLeft => Mathf.Max(0f, voteDeadline - Time.unscaledTime);
        public event System.Action VoteChanged;

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
            Room.VoteProgressReceived += HandleVoteProgress;
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
            sceneFlow.LoadContent(GameScenes.PartyLobby, () => GameFlow.ChangeState(GameState.PartyLobby));
        }

        public void ReturnToMainMenu()
        {
            // Close the lobby before clearing players or disconnecting its room.
            GameFlow.ChangeState(GameState.MainMenu);
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
            ClearVote();
            Players.Clear();
            batPad.ClearSlots(); // phones stay connected but have to join again from the lobby
            sceneFlow.LoadContent(GameScenes.MainMenu, () => GameFlow.ChangeState(GameState.MainMenu));
        }

        // Called by the PartyLobby when every player is ready.
        public bool StartMatch(int rounds)
        {
            Players.GetActivePlayers(playersBuffer);
            ClearVote();
            foreach (var data in minigames.Minigames)
                if (data != null && data.SupportsPlayerCount(Players.OccupiedCount) && !voteCandidates.Contains(data))
                    voteCandidates.Add(data);
            if (voteCandidates.Count < (rounds == 3 ? 3 : 1))
            {
                Debug.LogError("Not enough registered minigames support this player count.", this);
                return false;
            }
            Match.StartMatch(playersBuffer, rounds);
            // Direct one-round starts remain useful for editor smoke tests.
            if (rounds != 3) return StartNextRound();
            GameFlow.ChangeState(GameState.Voting);
            BeginVote();
            return true;
        }

        // Called by the Results screen: next minigame of the running match.
        public bool StartNextRound()
        {
            if (!Match.IsRunning) return false;
            var next = Match.CurrentRound < votedMinigames.Count
                ? votedMinigames[Match.CurrentRound]
                : minigames.PickNextMinigame(Match.PlayedMinigames, Players.OccupiedCount);
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
            ClearVote();
            OpenPartyLobby(GameFlow.Session);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Room.SceneRequested -= HandleSceneRequested;
            Room.VoteProgressReceived -= HandleVoteProgress;
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
            if (GameFlow.Session != SessionMode.Online)
            {
                if (GameFlow.State == GameState.Voting) UpdateVote();
                return;
            }
            if (Online.IsConnected) wasOnlineHost = Online.IsHost;
            else
            {
                if (!sceneFlow.IsLoading) LoseConnection();
                return;
            }
            if (IsOnlineClient)
            {
                LoadPendingScene();
                if (GameFlow.State == GameState.Voting) UpdateClientVoteCursor();
                SendOnlineInput();
            }
            else if (GameFlow.State == GameState.Voting) UpdateVote();
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
            if (GameFlow.State != GameState.Minigame && GameFlow.State != GameState.Results && GameFlow.State != GameState.Voting) return;
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

        private void ClearVote()
        {
            voteGeneration++;
            voteCandidates.Clear();
            votedMinigames.Clear();
            votes.Clear();
            voteCursor.Clear();
            nextVoteMove.Clear();
            voteFinalizing = false;
            VoteChanged?.Invoke();
        }

        private void BeginVote()
        {
            votes.Clear();
            voteCursor.Clear();
            nextVoteMove.Clear();
            voteDeadline = Time.unscaledTime + VoteSeconds;
            if (IsOnlineHost) SendVoteProgress(VoteSeconds);
            VoteChanged?.Invoke();
        }

        private void UpdateVote()
        {
            if (voteFinalizing) return;
            int required = 0;
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected || slot.Input == null) continue;
                required++;
                if (votes.ContainsKey(slot.PlayerId)) continue;
                int cursor = voteCursor.TryGetValue(slot.PlayerId, out int selected) ? selected : FirstAvailableVote();
                float axis = slot.Input.Move.x;
                if (Mathf.Abs(axis) > .55f && (!nextVoteMove.TryGetValue(slot.PlayerId, out float repeat) || Time.unscaledTime >= repeat))
                {
                    cursor = NextAvailableVote(cursor, axis > 0 ? 1 : -1);
                    voteCursor[slot.PlayerId] = cursor;
                    nextVoteMove[slot.PlayerId] = Time.unscaledTime + .25f;
                    VoteChanged?.Invoke();
                }
                if (slot.Input.WasPressed(PlayerAction.Jump)) CastVote(slot.PlayerId, cursor);
            }
            if ((required > 0 && votes.Count >= required) || Time.unscaledTime >= voteDeadline) FinishVote();
        }

        public void CastVote(int playerId, int candidateIndex)
        {
            if (GameFlow.State != GameState.Voting || IsOnlineClient || votes.ContainsKey(playerId) ||
                candidateIndex < 0 || candidateIndex >= voteCandidates.Count || votedMinigames.Contains(voteCandidates[candidateIndex])) return;
            if (!Players.TryGetByPlayerId(playerId, out var slot) || slot.IsAI || !slot.IsConnected) return;
            votes[playerId] = candidateIndex;
            VoteChanged?.Invoke();
        }

        private int FirstAvailableVote() => NextAvailableVote(voteCandidates.Count - 1, 1);

        private int NextAvailableVote(int current, int direction)
        {
            for (int step = 1; step <= voteCandidates.Count; step++)
            {
                int index = (current + direction * step + voteCandidates.Count * 2) % voteCandidates.Count;
                if (!votedMinigames.Contains(voteCandidates[index])) return index;
            }
            return 0;
        }

        private void FinishVote()
        {
            int best = -1;
            int bestCount = -1;
            int tied = 0;
            for (int i = 0; i < voteCandidates.Count; i++)
            {
                if (votedMinigames.Contains(voteCandidates[i])) continue;
                int count = 0;
                foreach (var choice in votes.Values) if (choice == i) count++;
                if (count > bestCount) { best = i; bestCount = count; tied = 1; }
                else if (count == bestCount && UnityEngine.Random.Range(0, ++tied) == 0) best = i;
            }
            if (best < 0) { StartNextRound(); return; }
            votedMinigames.Add(voteCandidates[best]);
            if (votedMinigames.Count >= Match.TotalRounds)
            {
                voteFinalizing = true;
                voteDeadline = Time.unscaledTime + 2f;
                if (IsOnlineHost) SendVoteProgress(2f);
                VoteChanged?.Invoke();
                StartCoroutine(LaunchVotedMatch(voteGeneration));
            }
            else BeginVote();
        }

        private IEnumerator LaunchVotedMatch(int generation)
        {
            yield return new WaitForSecondsRealtime(2f);
            if (generation == voteGeneration && GameFlow.State == GameState.Voting && Match.IsRunning)
                StartNextRound();
        }

        private void HandleVoteProgress(int selectedCount, float seconds)
        {
            if (!IsOnlineClient) return;
            if (selectedCount == 0) voteCandidates.Clear();
            if (voteCandidates.Count == 0)
                foreach (var data in minigames.Minigames)
                    if (data != null && data.SupportsPlayerCount(Players.OccupiedCount) && !voteCandidates.Contains(data))
                        voteCandidates.Add(data);
            GameFlow.ChangeState(GameState.Voting);
            voteDeadline = Time.unscaledTime + seconds;
            // The host sends chosen indices in the same registered candidate order.
            votedMinigames.Clear();
            votes.Clear();
            voteCursor.Clear();
            foreach (int index in Room.VoteChoices)
                if (index >= 0 && index < voteCandidates.Count) votedMinigames.Add(voteCandidates[index]);
            VoteChanged?.Invoke();
        }

        private void UpdateClientVoteCursor()
        {
            onlineInput ??= input.CreateAnyDeviceInput();
            if (onlineInput == null || voteCandidates.Count == 0) return;
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || Room.Slots[slot.SlotIndex].Owner != Online.LocalClientId) continue;
                int cursor = voteCursor.TryGetValue(slot.PlayerId, out int selected) ? selected : FirstAvailableVote();
                float axis = onlineInput.Move.x;
                if (Mathf.Abs(axis) > .55f && (!nextVoteMove.TryGetValue(slot.PlayerId, out float repeat) || Time.unscaledTime >= repeat))
                {
                    voteCursor[slot.PlayerId] = NextAvailableVote(cursor, axis > 0 ? 1 : -1);
                    nextVoteMove[slot.PlayerId] = Time.unscaledTime + .25f;
                    VoteChanged?.Invoke();
                }
                if (onlineInput.WasPressed(PlayerAction.Jump))
                {
                    votes[slot.PlayerId] = voteCursor.TryGetValue(slot.PlayerId, out selected) ? selected : cursor;
                    VoteChanged?.Invoke();
                }
                break;
            }
        }

        private void SendVoteProgress(float seconds)
        {
            var indices = new List<int>(votedMinigames.Count);
            foreach (var data in votedMinigames) indices.Add(voteCandidates.IndexOf(data));
            Room.SendVoteProgress(indices, seconds);
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
                    PlayerId = standing.PlayerId,
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
