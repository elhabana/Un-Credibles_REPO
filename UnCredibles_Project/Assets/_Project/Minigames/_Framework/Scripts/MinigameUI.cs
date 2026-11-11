using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnCredibles.Localization;
using UnCredibles.Players;

namespace UnCredibles.Minigames
{
    // Generic HUD: countdown, timer, player badges and results. Every scene text is optional.
    // The paper HUD (a badge per player in the corners and the turquoise timer pill) is built in code
    // with PaperUI, the same in every minigame. It only listens to the controller's events, so it works
    // unchanged on online replicas.
    // Subclass it when a minigame needs its own UI on top of this.
    public class MinigameUI : MonoBehaviour
    {
        private const int UrgentSeconds = 10;
        private const float PillHeight = 84f;
        private const float VerdictDelay = .8f;

        [SerializeField] private MinigameController controller;
        [SerializeField, Tooltip("Title font of the art bible (Bangers): badges, timer, countdown and banners.")]
        private TMP_FontAsset titleFont;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField, Tooltip("Old plain timer: hidden, the paper HUD shows the time.")]
        private TMP_Text timerText;
        [SerializeField, Tooltip("Old plain score list: hidden, the paper HUD shows the badges.")]
        private TMP_Text scoreText;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField, Min(0f)] private float startLabelSeconds = 0.75f;

        private readonly StringBuilder builder = new StringBuilder(128);
        private readonly List<PlayerBadge> badges = new List<PlayerBadge>(PlayerRegistry.MaxPlayers);
        private readonly int[] shownScores = new int[PlayerRegistry.MaxPlayers]; // same order as badges
        private WaitForSeconds hideStartLabel;
        private Canvas hud;
        private Image timerPill;
        private TMP_Text timerLabel;
        private int shownSeconds = int.MaxValue;
        private bool urgentShown;
        private int placedWidth;
        private int placedHeight;

        protected MinigameController Controller => controller;
        public TMP_FontAsset TitleFont => titleFont;
        public IReadOnlyList<PlayerBadge> Badges => badges;

        protected virtual void Awake()
        {
            hideStartLabel = new WaitForSeconds(startLabelSeconds);
            SetActive(countdownText, false);
            SetActive(timerText, false);
            SetActive(scoreText, false);
            if (resultsPanel != null) resultsPanel.SetActive(false);
            StyleSceneTexts();
            BuildHud();
        }

        protected virtual void OnEnable()
        {
            if (controller == null) return;
            controller.CountdownTick += ShowCountdown;
            controller.Finished += ShowResults;
            controller.StateChanged += HandleStateChanged;
            if (controller.Timer != null) controller.Timer.SecondChanged += ShowTime;
            if (controller.Score != null) controller.Score.ScoreChanged += ShowScores;
            if (controller.State >= MinigameState.Waiting) BuildBadges();
        }

        protected virtual void OnDisable()
        {
            if (controller == null) return;
            controller.CountdownTick -= ShowCountdown;
            controller.Finished -= ShowResults;
            controller.StateChanged -= HandleStateChanged;
            if (controller.Timer != null) controller.Timer.SecondChanged -= ShowTime;
            if (controller.Score != null) controller.Score.ScoreChanged -= ShowScores;
        }

        // The pill turns red and beats in the last seconds, or while the minigame says time is urgent.
        protected virtual void Update()
        {
            if (controller == null) return;
            if (badges.Count > 0 && (Screen.width != placedWidth || Screen.height != placedHeight)) PlaceBadges();
            if (!timerPill.gameObject.activeSelf) return;
            bool urgent = controller.IsPlaying && (controller.IsTimerUrgent || shownSeconds <= UrgentSeconds);
            if (urgent != urgentShown)
            {
                urgentShown = urgent;
                timerPill.color = urgent ? PaperUI.Alarm : PaperUI.TimerTeal;
                timerPill.rectTransform.localScale = Vector3.one;
            }
            if (!urgent) return;
            float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * .12f;
            timerPill.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // Updates only the badge whose score changed, not every frame. Points won during play sound.
        protected virtual void ShowScores(int changedPlayerId, int newScore)
        {
            for (int i = 0; i < badges.Count; i++)
            {
                if (badges[i].PlayerId != changedPlayerId) continue;
                if (newScore > shownScores[i] && controller.IsPlaying) GameAudio.Play(GameSfx.Score);
                shownScores[i] = newScore;
                badges[i].SetScore(newScore);
            }
        }

        protected virtual void ShowCountdown(int secondsLeft)
        {
            GameAudio.Play(secondsLeft > 0 ? GameSfx.CountdownBeep : GameSfx.CountdownGo);
            if (countdownText == null) return;
            countdownText.text = secondsLeft > 0 ? secondsLeft.ToString() : L10n.Get(L10n.Minigames, "common.start");
            SetActive(countdownText, true);
            if (secondsLeft == 0) StartCoroutine(HideCountdown());
        }

        protected virtual void ShowTime(int seconds)
        {
            if (controller.IsPlaying && seconds > 0 && seconds <= UrgentSeconds && seconds < shownSeconds)
                GameAudio.Play(GameSfx.TimerTick);
            shownSeconds = seconds;
            timerLabel.SetText("{0}", seconds);
            // A late tick (online replicas) must not bring the pill back over the results.
            timerPill.gameObject.SetActive(controller.State < MinigameState.Results);
        }

        protected virtual void ShowResults(IReadOnlyList<MinigameResult> results)
        {
            GameAudio.Play(GameSfx.RoundEnd);
            StartCoroutine(PlayVerdict(HumanWon(results)));
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

        private void HandleStateChanged(MinigameState state)
        {
            // Players are known from Waiting on, on the host and on online clients alike.
            if (state == MinigameState.Waiting) BuildBadges();
            else if (state == MinigameState.Results) timerPill.gameObject.SetActive(false);
        }

        // Timer pill, top centre. Hidden until the first time arrives (minigames without a time limit never show it).
        private void BuildHud()
        {
            hud = PaperUI.Canvas("Paper HUD", transform, 10); // under the rules card (50)
            timerPill = PaperUI.Pill("Timer", hud.transform, PaperUI.TimerTeal, PillHeight);
            PaperUI.Place(timerPill.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(180f, PillHeight));
            timerPill.rectTransform.pivot = new Vector2(.5f, .5f); // beats around its centre
            timerPill.rectTransform.anchoredPosition = new Vector2(0f, -24f - PillHeight * .5f);
            timerLabel = PaperUI.Text("Seconds", timerPill.transform, titleFont, 62f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(timerLabel.rectTransform);
            timerPill.gameObject.SetActive(false);
        }

        private void BuildBadges()
        {
            if (badges.Count > 0 || controller == null) return;
            var icon = controller.Data != null ? controller.Data.ScoreIcon : null;
            foreach (var player in controller.Players)
            {
                controller.GetHudPlacement(player.SlotIndex, out var corner, out var margin);
                bool left = corner.x < .5f;
                // Tilt away from the edge; tape on the top-left and bottom-right badges only.
                var badge = PlayerBadge.Create(hud.transform, player, corner, margin, titleFont, icon,
                    left ? 1.5f : -1.5f, left == (corner.y > .5f));
                shownScores[badges.Count] = controller.Score != null ? controller.Score.GetScore(player.PlayerId) : 0;
                badge.SetScore(shownScores[badges.Count], false);
                badges.Add(badge);
            }
            placedWidth = Screen.width;
            placedHeight = Screen.height;
        }

        // The screen shape changed: placements that depend on the camera (see GetHudPlacement) move.
        private void PlaceBadges()
        {
            placedWidth = Screen.width;
            placedHeight = Screen.height;
            foreach (var badge in badges)
            {
                controller.GetHudPlacement(badge.SlotIndex, out _, out var margin);
                badge.SetMargin(margin);
            }
        }

        // The scene's own texts (countdown, banners, round) take the title font; the results get their look later.
        private void StyleSceneTexts()
        {
            if (titleFont == null || countdownText == null) return;
            var canvas = countdownText.GetComponentInParent<Canvas>(true);
            if (canvas == null) return;
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                if (text != resultsText) text.font = titleFont;
        }

        // Win or lose after the end whistle, not on top of it.
        private IEnumerator PlayVerdict(bool humanWon)
        {
            yield return new WaitForSeconds(VerdictDelay);
            GameAudio.Play(humanWon ? GameSfx.Win : GameSfx.Lose);
        }

        private bool HumanWon(IReadOnlyList<MinigameResult> results)
        {
            foreach (var result in results)
            {
                if (result.Placement != 1) continue;
                foreach (var player in controller.Players)
                    if (player.PlayerId == result.PlayerId && !player.IsAI) return true;
            }
            return false;
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
