using System.Collections;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // Shown after every minigame with the match totals. After the last round it becomes
    // the final results: the leader is highlighted and players choose lobby or main menu.
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
        private bool isFinal;
        private bool leaving;
        private float remaining;
        private int shownSeconds = -1;

        private IEnumerator Start()
        {
            // CoreSceneLoader brings Core in when this scene is played directly.
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;

            isFinal = !core.Match.IsRunning;
            titleText.text = isFinal ? "FINAL RESULTS" : "RESULTS";
            roundText.text = $"Round {core.Match.CurrentRound} / {core.Match.TotalRounds}";
            finalButtons.SetActive(isFinal);
            hintText.gameObject.SetActive(!isFinal);
            remaining = autoContinueSeconds;
            RenderStandings();
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
            if (core == null || isFinal || leaving) return;

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
            }
        }

        private string PlayerName(int playerId) =>
            core.Players.TryGetByPlayerId(playerId, out var slot) ? slot.PlayerName : $"Player {playerId + 1}";

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
