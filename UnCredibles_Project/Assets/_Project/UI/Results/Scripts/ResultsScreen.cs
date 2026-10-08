using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // Intermediate rounds use the standings list. The final round uses a character podium.
    public sealed class ResultsScreen : MonoBehaviour
    {
        [SerializeField] private ResultRowView[] rows = new ResultRowView[PlayerRegistry.MaxPlayers];
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private GameObject finalButtons;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField, Min(1f)] private float autoContinueSeconds = 6f;

        private CoreRoot core;
        private bool isOnlineClient; // shows the host's standings and waits for the host
        private bool isFinal;
        private bool leaving;
        private float remaining;
        private int shownSeconds = -1;
        private GameObject listBackground;
        private GameObject listHeader;
        private GameObject listRows;
        private Camera resultsCamera;
        private ResultsPodium podium;

        private void Awake()
        {
            var canvas = titleText.GetComponentInParent<Canvas>();
            listBackground = canvas.transform.Find("Background").gameObject;
            listHeader = canvas.transform.Find("Header").gameObject;
            listRows = rows[0].transform.parent.gameObject;
            resultsCamera = Camera.main;
            podium = new ResultsPodium(transform);
        }

        private IEnumerator Start()
        {
            // CoreSceneLoader brings Core in when this scene is played directly.
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            isOnlineClient = core.GameFlow.Session == SessionMode.Online && !core.Online.IsHost;

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
            podium?.Clear();
        }

        private void ShowHeader(int round, int totalRounds)
        {
            titleText.text = isFinal ? "PODIO FINAL" : "RESULTS";
            roundText.text = isFinal ? "PUNTOS TOTALES" : $"Round {round} / {totalRounds}";
            finalButtons.SetActive(isFinal);
            hintText.gameObject.SetActive(!isFinal);
            listBackground.SetActive(!isFinal);
            listHeader.SetActive(!isFinal);
            listRows.SetActive(!isFinal);
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
            ShowHeader(standings.Round, standings.TotalRounds);
            lobbyButton.gameObject.SetActive(false); // only the host takes everybody back to the lobby
            hintText.text = "Waiting for the host...   SPACE / A to continue";
            var podiumEntries = new List<PodiumEntry>(standings.Rows.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= standings.Rows.Count)
                {
                    rows[i].Hide();
                    continue;
                }
                var row = standings.Rows[i];
                rows[i].Render(row.Placement, row.Name, row.Gained, row.Total, isFinal && row.Placement == 1);
                if (isFinal) podiumEntries.Add(new PodiumEntry(row.Placement, row.Name, row.Total,
                    PlayerColor(row.PlayerId, i)));
            }
            if (isFinal) podium.Render(podiumEntries);
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
                core.StartNextRound();
                return;
            }

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            hintText.text = $"Next minigame in {seconds}...   SPACE / A to continue";
        }

        private void RenderStandings()
        {
            var standings = core.Match.GetStandings();
            var lastRound = core.Match.LastRoundResults;
            var podiumEntries = new List<PodiumEntry>(standings.Length);

            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= standings.Length)
                {
                    rows[i].Hide();
                    continue;
                }

                var standing = standings[i];
                int gained = 0;
                foreach (var result in lastRound)
                    if (result.PlayerId == standing.PlayerId) gained = core.Match.PointsFor(result.Placement);

                rows[i].Render(standing.Placement, PlayerName(standing.PlayerId), gained, standing.Score,
                    isFinal && standing.Placement == 1);
                if (isFinal) podiumEntries.Add(new PodiumEntry(standing.Placement, PlayerName(standing.PlayerId),
                    standing.Score, PlayerColor(standing.PlayerId, i)));
            }
            if (isFinal) podium.Render(podiumEntries);
        }

        private string PlayerName(int playerId) =>
            core.Players.TryGetByPlayerId(playerId, out var slot) ? slot.PlayerName : $"Player {playerId + 1}";

        private Color PlayerColor(int playerId, int fallbackIndex) =>
            core.Players.TryGetByPlayerId(playerId, out var slot)
                ? PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI)
                : PlayerIdentity.ColorFor(fallbackIndex);

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
