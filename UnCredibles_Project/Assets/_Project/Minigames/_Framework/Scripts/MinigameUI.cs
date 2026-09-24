using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Generic HUD: countdown, timer and results. Every text is optional.
    // Subclass it when a minigame needs its own UI on top of this.
    public class MinigameUI : MonoBehaviour
    {
        [SerializeField] private MinigameController controller;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField, Min(0f)] private float startLabelSeconds = 0.75f;

        private readonly StringBuilder builder = new StringBuilder(128);
        private WaitForSeconds hideStartLabel;

        protected MinigameController Controller => controller;

        protected virtual void Awake()
        {
            hideStartLabel = new WaitForSeconds(startLabelSeconds);
            SetActive(countdownText, false);
            if (resultsPanel != null) resultsPanel.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            if (controller == null) return;
            controller.CountdownTick += ShowCountdown;
            controller.Finished += ShowResults;
            if (controller.Timer != null) controller.Timer.SecondChanged += ShowTime;
            if (controller.Score != null) controller.Score.ScoreChanged += ShowScores;
        }

        protected virtual void OnDisable()
        {
            if (controller == null) return;
            controller.CountdownTick -= ShowCountdown;
            controller.Finished -= ShowResults;
            if (controller.Timer != null) controller.Timer.SecondChanged -= ShowTime;
            if (controller.Score != null) controller.Score.ScoreChanged -= ShowScores;
        }

        // Rebuilt only when a score changes, not every frame.
        protected virtual void ShowScores(int changedPlayerId, int newScore)
        {
            if (scoreText == null) return;
            builder.Clear();
            foreach (var player in controller.Players)
                builder.Append(player.PlayerName).Append("  ").Append(controller.Score.GetScore(player.PlayerId)).Append("     ");
            scoreText.text = builder.ToString();
        }

        protected virtual void ShowCountdown(int secondsLeft)
        {
            if (countdownText == null) return;
            countdownText.text = secondsLeft > 0 ? secondsLeft.ToString() : "START!";
            SetActive(countdownText, true);
            if (secondsLeft == 0) StartCoroutine(HideCountdown());
        }

        protected virtual void ShowTime(int seconds)
        {
            if (timerText != null) timerText.SetText("{0}", seconds);
        }

        protected virtual void ShowResults(IReadOnlyList<MinigameResult> results)
        {
            builder.Clear();
            foreach (var result in results)
                builder.Append(result.Placement).Append(". ").Append(PlayerName(result.PlayerId))
                       .Append("  ").Append(result.Score).Append('\n');

            if (resultsText == null)
            {
                Debug.Log($"Results:\n{builder}", this);
                return;
            }
            resultsText.text = builder.ToString();
            if (resultsPanel != null) resultsPanel.SetActive(true);
        }

        protected string PlayerName(int playerId)
        {
            foreach (var player in controller.Players)
                if (player.PlayerId == playerId) return player.PlayerName;
            return $"#{playerId}";
        }

        private IEnumerator HideCountdown()
        {
            yield return hideStartLabel;
            SetActive(countdownText, false);
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }
    }
}
