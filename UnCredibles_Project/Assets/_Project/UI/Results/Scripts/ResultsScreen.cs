using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnCredibles.Localization;

namespace UnCredibles.UI.Results
{
    // Intermediate rounds use the standings list. The final round uses a character podium.
    public sealed class ResultsScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("Paper standings sheet shown between rounds.")] private ResultsSheet sheet;
        [SerializeField, Tooltip("Hidden with the standings sheet on the final screen.")] private GameObject listBackground;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private GameObject finalButtons;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField, Min(1f)] private float autoContinueSeconds = 6f;
        [SerializeField] private PlayerFigureSet figures;
        [SerializeField] private PodiumPrefabs podiumPrefabs;

        private CoreRoot core;
        private bool isOnlineClient; // shows the host's standings and waits for the host
        private bool isFinal;
        private bool leaving;
        private bool musicStarted;
        private float remaining;
        private int shownSeconds = -1;
        private Camera resultsCamera;
        private ResultsPodium podium;
        private readonly PlayerPortrait[] portraits = new PlayerPortrait[PlayerRegistry.MaxPlayers];

        private void Awake()
        {
            resultsCamera = Camera.main;
            podium = new ResultsPodium(transform, figures, podiumPrefabs, sheet.TitleFont);
        }

        private IEnumerator Start()
        {
            // CoreSceneLoader brings Core in when this scene is played directly.
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            isOnlineClient = core.GameFlow.Session == SessionMode.Online && !core.Online.IsHost;
            L10n.LanguageChanged += HandleLanguageChanged;

            if (isOnlineClient)
            {
                core.Room.StandingsReceived += RenderOnlineStandings;
                RenderOnlineStandings();
                yield break;
            }

            isFinal = !core.Match.IsRunning;
            ShowHeader(core.Match.CurrentRound, core.Match.TotalRounds);
            remaining = autoContinueSeconds;
            RenderStandings();
        }

        private void OnDestroy()
        {
            if (core != null) core.Room.StandingsReceived -= RenderOnlineStandings;
            L10n.LanguageChanged -= HandleLanguageChanged;
            podium?.Clear();
        }

        private void HandleLanguageChanged()
        {
            if (isOnlineClient)
            {
                RenderOnlineStandings();
                return;
            }
            ShowHeader(core.Match.CurrentRound, core.Match.TotalRounds);
            shownSeconds = -1; // Update rewrites the countdown hint in the new language
            RenderStandings();
        }

        private void ShowHeader(int round, int totalRounds)
        {
            titleText.text = isFinal ? L10n.Get(L10n.UI, "results.final_title") : L10n.Get(L10n.UI, "results.title");
            roundText.text = isFinal ? L10n.Get(L10n.UI, "results.total_points") : L10n.Format(L10n.UI, "results.round", round, totalRounds);
            finalButtons.SetActive(isFinal);
            // The pad needs a focused button to navigate the final screen.
            if (isFinal && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject((lobbyButton.gameObject.activeSelf ? lobbyButton : mainMenuButton).gameObject);
            titleText.gameObject.SetActive(isFinal);
            roundText.gameObject.SetActive(isFinal);
            listBackground.SetActive(!isFinal);
            sheet.SetHeader(titleText.text, roundText.text);
            sheet.SetVisible(!isFinal);
            podium.SetVisible(isFinal);
            if (isFinal && resultsCamera != null)
            {
                resultsCamera.clearFlags = CameraClearFlags.SolidColor;
                resultsCamera.backgroundColor = new Color(.035f, .05f, .08f);
            }
        }

        // Online client: the host already decided the standings; it also decides when to continue.
        private void RenderOnlineStandings()
        {
            var standings = core.Room.Standings;
            isFinal = standings.IsFinal;
            lobbyButton.gameObject.SetActive(false); // only the host takes everybody back to the lobby
            ShowHeader(standings.Round, standings.TotalRounds);
            sheet.SetHint(L10n.Get(L10n.UI, "results.waiting_host"));
            var podiumEntries = new List<PodiumEntry>(standings.Rows.Count);
            bool humanWon = false;
            for (int i = 0; i < sheet.RowCount; i++)
            {
                if (i >= standings.Rows.Count)
                {
                    sheet.HideRow(i);
                    continue;
                }
                var row = standings.Rows[i];
                sheet.SetRow(i, row.Placement, row.Name, PlayerSlotIndex(row.PlayerId, i), IsBot(row.PlayerId),
                    row.Gained, row.Total, PortraitFor(PlayerSlotIndex(row.PlayerId, i), IsBot(row.PlayerId)));
                if (isFinal) podiumEntries.Add(new PodiumEntry(row.Placement, row.Name, row.Total,
                    PlayerSlotIndex(row.PlayerId, i), IsBot(row.PlayerId)));
                if (row.Placement == 1) humanWon |= IsHuman(row.PlayerId);
            }
            if (isFinal) podium.Render(podiumEntries);
            StartMusic(humanWon);
        }

        private void OnEnable()
        {
            lobbyButton.onClick.AddListener(GoToLobby);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        private void OnDisable()
        {
            lobbyButton.onClick.RemoveListener(GoToLobby);
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        // Between rounds: continue automatically or as soon as any human presses Jump.
        private void Update()
        {
            if (core == null || isFinal || leaving || isOnlineClient) return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f || AnyHumanPressed(PlayerAction.Jump))
            {
                leaving = true;
                // No next minigame could load: back to the lobby instead of hanging on this screen.
                if (!core.StartNextRound()) core.ReturnToLobby();
                return;
            }

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            sheet.SetHint(L10n.Format(L10n.UI, "results.next_in", seconds));
        }

        private void RenderStandings()
        {
            var standings = core.Match.GetStandings();
            var lastRound = core.Match.LastRoundResults;
            var podiumEntries = new List<PodiumEntry>(standings.Length);
            bool humanWon = false;

            for (int i = 0; i < sheet.RowCount; i++)
            {
                if (i >= standings.Length)
                {
                    sheet.HideRow(i);
                    continue;
                }

                var standing = standings[i];
                int gained = 0;
                foreach (var result in lastRound)
                    if (result.PlayerId == standing.PlayerId) gained = core.Match.PointsFor(result.Placement);

                sheet.SetRow(i, standing.Placement, PlayerName(standing.PlayerId),
                    PlayerSlotIndex(standing.PlayerId, i), IsBot(standing.PlayerId), gained, standing.Score,
                    PortraitFor(PlayerSlotIndex(standing.PlayerId, i), IsBot(standing.PlayerId)));
                if (isFinal) podiumEntries.Add(new PodiumEntry(standing.Placement, PlayerName(standing.PlayerId),
                    standing.Score, PlayerSlotIndex(standing.PlayerId, i), IsBot(standing.PlayerId)));
                if (standing.Placement == 1) humanWon |= IsHuman(standing.PlayerId);
            }
            if (isFinal) podium.Render(podiumEntries);
            StartMusic(humanWon);
        }

        // The player's character for the standings sheet, filmed once per slot. The final podium already
        // shows the figures themselves.
        private Texture PortraitFor(int slotIndex, bool isAI)
        {
            if (isFinal || slotIndex < 0 || slotIndex >= portraits.Length) return null;
            if (portraits[slotIndex] == null)
                portraits[slotIndex] = PlayerPortrait.Create(figures, slotIndex, core.Players.Slots[slotIndex].Loadout,
                    PlayerIdentity.ColorFor(slotIndex, isAI));
            return portraits[slotIndex] != null ? portraits[slotIndex].Texture : null;
        }

        private bool IsHuman(int playerId) => core.Players.TryGetByPlayerId(playerId, out var slot) && !slot.IsAI;
        private bool IsBot(int playerId) => core.Players.TryGetByPlayerId(playerId, out var slot) && slot.IsAI;

        // Once per screen (renders repeat on language changes): the fanfare, or the defeat loop on a
        // final podium that no human won.
        private void StartMusic(bool humanWon)
        {
            if (musicStarted) return;
            musicStarted = true;
            if (isFinal && !humanWon) core.Audio.PlayDefeatMusic();
            else core.Audio.PlayResultsMusic();
        }

        private string PlayerName(int playerId) =>
            core.Players.TryGetByPlayerId(playerId, out var slot) ? slot.PlayerName : L10n.Format(L10n.UI, "results.player_n", playerId + 1);

        private int PlayerSlotIndex(int playerId, int fallbackIndex) =>
            core.Players.TryGetByPlayerId(playerId, out var slot) ? slot.SlotIndex : fallbackIndex;

        private bool AnyHumanPressed(PlayerAction action)
        {
            foreach (var slot in core.Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && slot.Input != null && slot.Input.WasPressed(action)) return true;
            return false;
        }

        private void GoToLobby()
        {
            if (leaving) return;
            leaving = true;
            core.ReturnToLobby();
        }

        private void GoToMainMenu()
        {
            if (leaving) return;
            leaving = true;
            core.ReturnToMainMenu();
        }
    }
}
