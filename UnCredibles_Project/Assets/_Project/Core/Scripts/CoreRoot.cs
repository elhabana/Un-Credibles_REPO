using System.Collections;
using System.Collections.Generic;
using UnCredibles.BatPad;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnCredibles.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnCredibles.Localization;

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
        // Per slot: the bot that plays for a player whose controller dropped, whose controller it was,
        // what kind of player they were and when the bot takes over (0 = not pending).
        private readonly AIInput[] standIns = new AIInput[PlayerRegistry.MaxPlayers];
        private readonly IPlayerInput[] standInOwners = new IPlayerInput[PlayerRegistry.MaxPlayers];
        private readonly PlayerType[] standInTypes = new PlayerType[PlayerRegistry.MaxPlayers];
        private readonly float[] standInAt = new float[PlayerRegistry.MaxPlayers];
        private const float AwayBotDelay = 10f;
        private readonly List<MinigameData> votedMinigames = new List<MinigameData>(3);
        private readonly List<MinigameData> voteCandidates = new List<MinigameData>();
        private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> voteCursor = new Dictionary<int, int>();
        private readonly Dictionary<int, float> nextVoteMove = new Dictionary<int, float>();
        private float voteDeadline;
        private bool voteFinalizing;
        private int voteGeneration;
        private Coroutine resultsRoutine;
        private int nextVoteBallotId;
        private int voteBallotId;
        private const float VoteSeconds = 12f;
        // The vote screen has three stops and the online vote message carries at most three choices.
        public const int VotedRounds = 3;
        public IReadOnlyList<MinigameData> VoteCandidates => voteCandidates;
        public IReadOnlyList<MinigameData> VotedMinigames => votedMinigames;
        public IReadOnlyDictionary<int, int> Votes => votes;
        public IReadOnlyDictionary<int, int> VoteCursor => voteCursor;
        public float VoteSecondsLeft => Mathf.Max(0f, voteDeadline - Time.unscaledTime);
        public bool CanChangeVote => GameFlow.State == GameState.Voting && !voteFinalizing && VoteSecondsLeft > 0f;
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
            gameObject.AddComponent<DisplayModeToggle>();
            // NGO persists its own root; never attach it to Core's scene objects.
            Online = new GameObject("Online Connection").AddComponent<RelayConnection>();

            GameFlow = new GameFlowManager();
            PlayerRegistry.DefaultNameProvider = (index, isAI) =>
                isAI ? "BOT" : L10n.Format(L10n.UI, "results.player_n", index + 1);
            PlayerIdentity.LabelProvider = index => L10n.Format(L10n.UI, "player.tag", index + 1);
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
            Room.VoteRequested += HandleVoteRequested;
            minigames.MinigameStarted += HandleMinigameStarted;
            minigames.MinigameFinished += HandleMinigameFinished;
            batPad.PhoneDisconnected += HandlePhoneDisconnected;
            batPad.PhoneReconnected += HandlePhoneReconnected;
            InputSystem.onDeviceChange += HandleDeviceChange;
            GameFlow.StateChanged += UpdatePhoneHint;
            L10n.LanguageChanged += ResendPhoneHint;
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
            StopResultsRoutine();
            minigames.ExitActiveMinigame();
            Match.CancelMatch();
            ClearVote();
            Players.Clear();
            System.Array.Clear(standInOwners, 0, standInOwners.Length);
            System.Array.Clear(standInAt, 0, standInAt.Length);
            batPad.SendByeToAll(); // phones show "thanks for playing"
            batPad.ClearSlots(); // phones stay connected but have to join again from the lobby
            sceneFlow.LoadContent(GameScenes.MainMenu, () => GameFlow.ChangeState(GameState.MainMenu));
        }

        // Called by the PartyLobby when every player is ready.
        public bool StartMatch(int rounds)
        {
            Players.GetActivePlayers(playersBuffer);
            ClearVote();
            BuildVoteCandidates();
            if (rounds != 1 && rounds != VotedRounds)
                Debug.LogWarning($"Only {VotedRounds}-round matches are voted; {rounds} rounds get random minigames.", this);
            if (voteCandidates.Count < (rounds == VotedRounds ? VotedRounds : 1))
            {
                Debug.LogError("Not enough registered minigames support this player count.", this);
                return false;
            }
            Match.StartMatch(playersBuffer, rounds);
            // A controller lost during the lobby's last seconds gets the same 10 s before its bot.
            foreach (var slot in Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && !slot.IsConnected) ScheduleStandIn(slot);
            // Direct one-round starts remain useful for editor smoke tests.
            if (rounds != VotedRounds) return StartNextRound();
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
            StopResultsRoutine();
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
            Room.VoteRequested -= HandleVoteRequested;
            Room?.Dispose();
            onlineInput?.Dispose();
            if (Online != null) Destroy(Online.gameObject);
            minigames.MinigameStarted -= HandleMinigameStarted;
            minigames.MinigameFinished -= HandleMinigameFinished;
            batPad.PhoneDisconnected -= HandlePhoneDisconnected;
            batPad.PhoneReconnected -= HandlePhoneReconnected;
            InputSystem.onDeviceChange -= HandleDeviceChange;
            GameFlow.StateChanged -= UpdatePhoneHint;
            L10n.LanguageChanged -= ResendPhoneHint;
            Players.Clear();
            Instance = null;
        }

        private bool InLobby => GameFlow.State == GameState.MainMenu || GameFlow.State == GameState.PartyLobby;

        // A player whose controller drops (a phone, or a gamepad unplugged or out of battery) keeps the slot as
        // Disconnected. In the lobby, the lobby frees it if it takes too long. During a match, if they are still
        // away after AwayBotDelay, a bot plays for them (same PlayerId and score) until the controller comes back.
        private void MarkAway(PlayerSlot slot)
        {
            Players.SetConnected(slot.SlotIndex, false);
            if (!InLobby) ScheduleStandIn(slot);
        }

        private void ScheduleStandIn(PlayerSlot slot)
        {
            standInOwners[slot.SlotIndex] = slot.Input;
            standInTypes[slot.SlotIndex] = slot.PlayerType;
            standInAt[slot.SlotIndex] = Time.unscaledTime + AwayBotDelay;
        }

        private void ReplaceLatePlayers()
        {
            for (int i = 0; i < standInAt.Length; i++)
            {
                if (standInAt[i] <= 0f || (!InLobby && Time.unscaledTime < standInAt[i])) continue;
                bool late = !InLobby; // back in the lobby, the lobby's own timeout decides
                standInAt[i] = 0f;
                var slot = Players.Slots[i];
                if (!late || slot.IsConnected || slot.Input == null || slot.Input != standInOwners[i]) continue;
                // One stand-in per slot, reused: minigame AI brains stay bound to the AIInput they were given.
                standIns[i] ??= input.CreateAIInput();
                Players.ReplaceWithAI(i, standIns[i]);
            }
        }

        private bool BotStandsInFor(int index, System.Func<IPlayerInput, bool> isOwner) =>
            standIns[index] != null && Players.Slots[index].Input == standIns[index] &&
            standInOwners[index] != null && isOwner(standInOwners[index]);

        // The controller is back: before the bot came in it just goes on; after, it takes the slot back from the bot.
        private void MarkBack(int index, IPlayerInput controller)
        {
            standInAt[index] = 0f;
            if (Players.Slots[index].Input == standIns[index] && standIns[index] != null)
            {
                standInOwners[index] = null;
                Players.RestoreHuman(index, standInTypes[index], controller);
            }
            else Players.SetConnected(index, true);
        }

        private void HandlePhoneDisconnected(BatPadInput phone)
        {
            if (TryGetSlot(phone, out var slot)) MarkAway(slot);
        }

        private void HandlePhoneReconnected(BatPadInput phone)
        {
            if (TryGetSlot(phone, out var slot))
            {
                MarkBack(slot.SlotIndex, phone);
                return;
            }
            int index = phone.SlotIndex;
            if (index >= 0 && index < PlayerRegistry.MaxPlayers && BotStandsInFor(index, owner => owner == phone))
                MarkBack(index, phone);
            else if (index != BatPadInput.NoSlot) batPad.AssignSlot(phone, BatPadInput.NoSlot); // its slot is gone
        }

        // Gamepads (and keyboards) that are unplugged or lose their wireless link, and come back.
        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool lost = change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed;
            bool back = change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added;
            if (!lost && !back) return;
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++)
            {
                var slot = Players.Slots[i];
                bool ownDevice = slot.Input is DeviceInput own && own.Device == device;
                if (lost && ownDevice && slot.IsConnected) MarkAway(slot);
                else if (back && ownDevice && !slot.IsConnected) MarkBack(i, slot.Input);
                else if (back && BotStandsInFor(i, owner => owner is DeviceInput gone && gone.Device == device))
                {
                    // The old input was disposed when the bot came in: the device gets a fresh one.
                    var fresh = device switch
                    {
                        Gamepad gamepad => input.CreateGamepadInput(gamepad),
                        Keyboard => input.CreateKeyboardInput(),
                        _ => null,
                    };
                    if (fresh != null) MarkBack(i, fresh);
                }
            }
        }

        private bool TryGetSlot(IPlayerInput controller, out PlayerSlot found)
        {
            foreach (var slot in Players.Slots)
            {
                if (slot.Input != controller) continue;
                found = slot;
                return true;
            }
            found = null;
            return false;
        }

        private void HandleMinigameStarted(IMinigame minigame)
        {
            minigame.StateChanged += HandleMinigameStateChanged;
            GameFlow.ChangeState(GameState.Minigame);
        }

        // The rules card ends when the countdown starts: phones go from "Ready" to the minigame controls.
        private void HandleMinigameStateChanged(MinigameState state)
        {
            if (state == MinigameState.Countdown) ResendPhoneHint();
        }

        // What BatPad phones show for each moment of the game: instruction, button names, exit button.
        private void UpdatePhoneHint(GameState previous, GameState current)
        {
            var hint = new BatPadService.PhoneHint();
            switch (current)
            {
                case GameState.MainMenu:
                case GameState.PartyLobby:
                    hint.Text = L10n.Get(L10n.UI, "phone.lobby");
                    hint.A = L10n.Get(L10n.UI, "phone.a_ready");
                    hint.B = L10n.Get(L10n.UI, "phone.b_unready");
                    hint.ShowExit = true;
                    hint.Pulse = true;
                    break;
                case GameState.Voting:
                    hint.Text = L10n.Get(L10n.UI, "phone.vote");
                    hint.A = L10n.Get(L10n.UI, "phone.a_vote");
                    break;
                case GameState.Loading:
                    hint.Text = L10n.Get(L10n.UI, "phone.loading");
                    break;
                case GameState.Minigame:
                    hint = PhoneMinigameHint();
                    break;
                case GameState.Results:
                    hint.Text = L10n.Get(L10n.UI, "phone.results");
                    hint.A = L10n.Get(L10n.UI, "phone.a_continue");
                    hint.Pulse = true;
                    break;
                case GameState.FinalResults:
                    hint.Text = L10n.Get(L10n.UI, "phone.final");
                    break;
                default:
                    return;
            }
            batPad.SetHint(hint);
            if (current == GameState.FinalResults && previous != current) SendPhoneResults();
        }

        // Same moment, new language: the phones get the instruction again (not the result screen).
        private void ResendPhoneHint() => UpdatePhoneHint(GameFlow.State, GameFlow.State);

        // Every phone in a slot learns who won the match and where its own player finished.
        private void SendPhoneResults()
        {
            var standings = Match.GetStandings();
            if (standings.Length == 0) return;
            var winners = new List<string>();
            foreach (var standing in standings)
                if (standing.Placement == 1) winners.Add(PlayerNameOf(standing.PlayerId));
            string winner = string.Join(" / ", winners);

            foreach (var phone in batPad.Phones)
            {
                if (phone.SlotIndex == BatPadInput.NoSlot) continue;
                var slot = Players.Slots[phone.SlotIndex];
                foreach (var standing in standings)
                    if (standing.PlayerId == slot.PlayerId)
                        batPad.SendResult(phone, winner, standing.Placement, standing.Placement == 1);
            }
        }

        private string PlayerNameOf(int playerId) =>
            Players.TryGetByPlayerId(playerId, out var slot) ? slot.PlayerName : L10n.Format(L10n.UI, "results.player_n", playerId + 1);

        // Each minigame has its own lines in the Minigames table: "phone.<id>" for the instruction and
        // "phone.<id>.a" / ".b" for the buttons (missing = that button does nothing there).
        private BatPadService.PhoneHint PhoneMinigameHint()
        {
            if (minigames.ActiveMinigame?.State == MinigameState.Waiting) // rules card on screen
                return new BatPadService.PhoneHint
                {
                    Text = L10n.Get(L10n.UI, "phone.rules"),
                    A = L10n.Get(L10n.UI, "phone.a_ready"),
                    Pulse = true,
                };
            var data = minigames.ActiveData;
            var hint = new BatPadService.PhoneHint { Text = L10n.Get(L10n.UI, "phone.minigame") };
            if (data == null) return hint;
            string key = "phone." + data.Id;
            hint.Text = Optional(L10n.Minigames, key) ?? hint.Text;
            hint.A = Optional(L10n.Minigames, key + ".a");
            hint.B = Optional(L10n.Minigames, key + ".b");
            return hint;
        }

        private static string Optional(string table, string key)
        {
            string text = L10n.Get(table, key);
            return text == key ? null : text;
        }

        private void Update()
        {
            ReplaceLatePlayers();
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
            ConnectionLost.Invoke(wasOnlineHost ? L10n.Get(L10n.UI, "overlay.connection_lost") : L10n.Get(L10n.UI, "overlay.host_left"), Online.Status);
        }

        private void HandleMinigameFinished(MinigameData data, IReadOnlyList<MinigameResult> results)
        {
            if (IsOnlineClient) return; // the host decides what comes next
            Match.RegisterResults(data, results);
            StopResultsRoutine();
            resultsRoutine = StartCoroutine(ShowResultsRoutine());
        }

        // Leaving the match during the pause must not load the Results scene afterwards.
        private void StopResultsRoutine()
        {
            if (resultsRoutine != null) StopCoroutine(resultsRoutine);
            resultsRoutine = null;
        }

        // Leave the minigame's own results on screen for a moment, then show the match standings.
        private IEnumerator ShowResultsRoutine()
        {
            yield return new WaitForSeconds(minigameResultsSeconds);
            resultsRoutine = null;
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

        private void ClearVote()
        {
            voteGeneration++;
            voteBallotId = 0;
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
            voteBallotId = ++nextVoteBallotId;
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
            if (Time.unscaledTime >= voteDeadline || EveryoneVoted()) { FinishVote(); return; }
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected || slot.Input == null) continue;
                if (IsOnlineHost && Room.IsRemoteSlot(slot.SlotIndex)) continue;
                StepVoteCursor(slot.PlayerId, slot.Input);
            }
        }

        // Left/right moves the player's cursor (with repeat delay); Jump votes for it.
        private void StepVoteCursor(int playerId, IPlayerInput controller)
        {
            int cursor = voteCursor.TryGetValue(playerId, out int selected) ? selected : FirstAvailableVote();
            float axis = controller.Move.x;
            if (Mathf.Abs(axis) > .55f && (!nextVoteMove.TryGetValue(playerId, out float repeat) || Time.unscaledTime >= repeat))
            {
                cursor = NextAvailableVote(cursor, axis > 0 ? 1 : -1);
                voteCursor[playerId] = cursor;
                nextVoteMove[playerId] = Time.unscaledTime + .25f;
                VoteChanged?.Invoke();
            }
            if (controller.WasPressed(PlayerAction.Jump)) CastVote(playerId, cursor);
        }

        // Host and clients must build the same list in the same order: votes travel as indices into it.
        private void BuildVoteCandidates()
        {
            foreach (var data in minigames.Minigames)
                if (data != null && data.SupportsPlayerCount(Players.OccupiedCount) && !voteCandidates.Contains(data))
                    voteCandidates.Add(data);
        }

        // The ballot closes as soon as every connected human has voted; bots never vote.
        private bool EveryoneVoted()
        {
            int voters = 0;
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected) continue;
                if (!votes.ContainsKey(slot.PlayerId)) return false;
                voters++;
            }
            return voters > 0;
        }

        public void CastVote(int playerId, int candidateIndex)
        {
            if (!CanChangeVote ||
                candidateIndex < 0 || candidateIndex >= voteCandidates.Count || votedMinigames.Contains(voteCandidates[candidateIndex])) return;
            if (!Players.TryGetByPlayerId(playerId, out var slot) || slot.IsAI || !slot.IsConnected) return;
            if (IsOnlineClient)
            {
                if (Room.Slots[slot.SlotIndex].Owner != Online.LocalClientId) return;
                voteCursor[playerId] = candidateIndex;
                Room.SendVote(voteBallotId, candidateIndex);
                VoteChanged?.Invoke();
                return;
            }
            voteCursor[playerId] = candidateIndex;
            if (votes.TryGetValue(playerId, out int current) && current == candidateIndex)
            {
                VoteChanged?.Invoke();
                return;
            }
            votes[playerId] = candidateIndex;
            if (IsOnlineHost) SendVoteProgress(VoteSecondsLeft);
            VoteChanged?.Invoke();
        }

        private void HandleVoteRequested(int ballotId, int playerId, int candidateIndex)
        {
            if (IsOnlineHost && ballotId == voteBallotId) CastVote(playerId, candidateIndex);
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
            if (!IsOnlineClient || (GameFlow.State != GameState.PartyLobby && GameFlow.State != GameState.Voting)) return;
            bool newBallot = voteBallotId != Room.VoteBallotId;
            if (newBallot && selectedCount == 0) voteCandidates.Clear();
            if (voteCandidates.Count == 0) BuildVoteCandidates();
            voteBallotId = Room.VoteBallotId;
            voteFinalizing = !Room.VoteIsOpen;
            voteDeadline = Time.unscaledTime + seconds;
            // The host sends chosen indices in the same registered candidate order.
            votedMinigames.Clear();
            votes.Clear();
            if (newBallot)
            {
                voteCursor.Clear();
                nextVoteMove.Clear();
            }
            foreach (int index in Room.VoteChoices)
                if (index >= 0 && index < voteCandidates.Count) votedMinigames.Add(voteCandidates[index]);
            foreach (var vote in Room.ConfirmedVotes) votes[vote.Key] = vote.Value;
            GameFlow.ChangeState(GameState.Voting);
            VoteChanged?.Invoke();
        }

        private void UpdateClientVoteCursor()
        {
            onlineInput ??= input.CreateAnyDeviceInput();
            if (onlineInput == null || voteCandidates.Count == 0 || !CanChangeVote) return;
            foreach (var slot in Players.Slots)
            {
                if (!slot.IsOccupied || Room.Slots[slot.SlotIndex].Owner != Online.LocalClientId) continue;
                StepVoteCursor(slot.PlayerId, onlineInput);
                break;
            }
        }

        private void SendVoteProgress(float seconds)
        {
            var indices = new List<int>(votedMinigames.Count);
            foreach (var data in votedMinigames) indices.Add(voteCandidates.IndexOf(data));
            Room.SendVoteProgress(voteBallotId, !voteFinalizing, indices, votes, seconds);
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
                    Name = Players.TryGetByPlayerId(standing.PlayerId, out var slot) ? slot.PlayerName : L10n.Format(L10n.UI, "results.player_n", standing.PlayerId + 1),
                    Placement = standing.Placement,
                    Gained = gained,
                    Total = standing.Score,
                });
            }
            return standings;
        }
    }
}
