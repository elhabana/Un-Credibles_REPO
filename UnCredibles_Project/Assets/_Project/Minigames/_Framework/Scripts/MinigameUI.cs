using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnCredibles.Localization;
using UnCredibles.Players;

namespace UnCredibles.Minigames
{
    // Generic HUD: countdown, timer, player badges and results. Every scene text is optional.
    // The results are a paper sheet built over the HUD when the minigame ends: a strip per player, best first.
    // The paper HUD (PF_MinigameHud: the turquoise timer pill, plus a PF_PlayerBadge per player in the
    // corners) is the same in every minigame. It only listens to the controller's events, so it works
    // unchanged on online replicas.
    // Subclass it when a minigame needs its own UI on top of this.
    public class MinigameUI : MonoBehaviour
    {
        private const int UrgentSeconds = 10;
        private const float VerdictDelay = .8f;
        // Results sheet, in the HUD's 1920x1080 reference.
        private const float SheetWidth = 860f;
        private const float SheetRowHeight = 92f;
        private const float SheetRowStep = 104f;
        private const float SheetHeader = 136f;
        private const float RowDelay = .12f; // stagger between rows
        private const float PopSeconds = .3f;
        private static readonly Color LeaderPaper = new Color(1f, .96f, .82f);
        private static readonly Color[] MedalColors =
        {
            new Color(1f, .78f, .2f), new Color(.78f, .8f, .84f), new Color(.85f, .55f, .3f),
        };
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1f);
        private static readonly Vector2 Left = new Vector2(0f, .5f);
        private static readonly Vector2 Right = new Vector2(1f, .5f);

        [SerializeField] private MinigameController controller;
        [SerializeField, Tooltip("Title font of the art bible (Bangers): countdown and banners of the scene.")]
        private TMP_FontAsset titleFont;
        [SerializeField, Tooltip("Paper HUD canvas (PF_MinigameHud instance): the badges go here.")]
        private RectTransform hud;
        [SerializeField] private Image timerPill;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField, Tooltip("One per player, in the corners of the paper HUD.")]
        private PlayerBadge badgePrefab;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField, Tooltip("Old plain timer: hidden, the paper HUD shows the time.")]
        private TMP_Text timerText;
        [SerializeField, Tooltip("Old plain score list: hidden, the paper HUD shows the badges.")]
        private TMP_Text scoreText;
        [SerializeField, Tooltip("Old plain results: hidden, the paper sheet shows them.")]
        private GameObject resultsPanel;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField, Min(0f)] private float startLabelSeconds = 0.75f;

        private readonly List<PlayerBadge> badges = new List<PlayerBadge>(PlayerRegistry.MaxPlayers);
        private readonly int[] shownScores = new int[PlayerRegistry.MaxPlayers]; // same order as badges
        private WaitForSeconds hideStartLabel;
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
            // Hidden until the first time arrives (minigames without a time limit never show it).
            timerPill.gameObject.SetActive(false);
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
            StartCoroutine(PopRows(BuildResultsSheet(results)));
        }

        // Taped paper sheet in the middle of the HUD: the title and one strip per player with the place on its
        // medal, the player's disc (the badge's portrait when there is one), the name and the score.
        private RectTransform[] BuildResultsSheet(IReadOnlyList<MinigameResult> results)
        {
            var sheet = PaperUI.Panel("Results sheet", hud, PaperUI.Paper, -1f, true).rectTransform;
            Center(sheet, Middle, new Vector2(0f, -100f),
                new Vector2(SheetWidth, SheetHeader + results.Count * SheetRowStep + 20f));
            var title = PaperUI.Text("Title", sheet, titleFont, 76f, PaperUI.Ink, TextAlignmentOptions.Center);
            title.text = L10n.Get(L10n.Minigames, "common.results");
            Center(title.rectTransform, Top, new Vector2(0f, -70f), new Vector2(SheetWidth - 80f, 90f));

            var rows = new RectTransform[results.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                var result = results[i];
                PlayerSlot player = null;
                foreach (var candidate in controller.Players)
                    if (candidate.PlayerId == result.PlayerId) player = candidate;
                PlayerBadge badge = null;
                foreach (var candidate in badges)
                    if (candidate.PlayerId == result.PlayerId) badge = candidate;
                int slotIndex = player != null ? player.SlotIndex : i;
                bool isAI = player != null && player.IsAI;
                var color = PlayerIdentity.ColorFor(slotIndex, isAI);

                var row = PaperUI.Panel("Row " + (i + 1), sheet,
                    result.Placement == 1 ? LeaderPaper : Color.Lerp(Color.white, color, .1f), i % 2 == 0 ? -.6f : .6f);
                Center(row.rectTransform, Top, new Vector2(0f, -SheetHeader - i * SheetRowStep - SheetRowHeight * .5f),
                    new Vector2(SheetWidth - 70f, SheetRowHeight));
                var content = row.transform;

                if (result.Placement <= MedalColors.Length)
                    Center(PaperUI.Disc("Medal", content, MedalColors[result.Placement - 1]).rectTransform, Left,
                        new Vector2(58f, 0f), new Vector2(78f, 78f));
                var place = PaperUI.Text("Place", content, titleFont, 50f, PaperUI.Ink, TextAlignmentOptions.Center);
                place.SetText("{0}", result.Placement);
                Center(place.rectTransform, Left, new Vector2(58f, 0f), new Vector2(78f, 78f));

                var disc = PaperUI.Disc("Disc", content, color);
                Center(disc.rectTransform, Left, new Vector2(150f, 0f), new Vector2(78f, 78f));
                if (badge != null && badge.Portrait != null) PlayerBadge.ShowPortrait(disc, badge.Portrait);
                else
                {
                    var tag = PaperUI.Text("Tag", disc.transform, titleFont, 34f, PaperUI.Ink, TextAlignmentOptions.Center);
                    tag.text = PlayerIdentity.LabelFor(slotIndex, isAI);
                    PaperUI.Stretch(tag.rectTransform);
                }

                // Typed player names can hold any character: the default font, not the title one.
                var playerName = PaperUI.Text("Name", content, null, 40f, PaperUI.Ink, TextAlignmentOptions.Left);
                playerName.text = PlayerName(result.PlayerId);
                playerName.overflowMode = TextOverflowModes.Ellipsis;
                PaperUI.Place(playerName.rectTransform, Left, new Vector2(206f, 0f), new Vector2(380f, 70f));

                var score = PaperUI.Text("Score", content, titleFont, 66f, PaperUI.Ink, TextAlignmentOptions.Right);
                score.SetText("{0}", result.Score);
                PaperUI.Place(score.rectTransform, Right, new Vector2(-30f, 0f), new Vector2(170f, 80f));

                row.rectTransform.localScale = Vector3.zero; // until its turn to pop in
                rows[i] = row.rectTransform;
            }
            return rows;
        }

        // Each row pops in a little after the one above: from nothing, past full size, and back (ease-out back).
        private static IEnumerator PopRows(RectTransform[] rows)
        {
            float time = 0f;
            float total = (rows.Length - 1) * RowDelay + PopSeconds;
            while (time < total)
            {
                yield return null;
                time += Time.unscaledDeltaTime;
                for (int i = 0; i < rows.Length; i++)
                {
                    float t = Mathf.Clamp01((time - i * RowDelay) / PopSeconds);
                    float k = t - 1f;
                    float scale = t <= 0f ? 0f : 1f + 2.70158f * k * k * k + 1.70158f * k * k;
                    rows[i].localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        // Like PaperUI.Place but turning around its centre (tilt and pop).
        private static void Center(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            PaperUI.Place(rect, anchor, position, size);
            rect.pivot = Middle;
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

        private void BuildBadges()
        {
            if (badges.Count > 0 || controller == null) return;
            var icon = controller.Data != null ? controller.Data.ScoreIcon : null;
            foreach (var player in controller.Players)
            {
                controller.GetHudPlacement(player.SlotIndex, out var corner, out var margin);
                bool left = corner.x < .5f;
                // Tilt away from the edge; tape on the top-left and bottom-right badges only.
                var badge = Instantiate(badgePrefab, hud);
                badge.Setup(player, corner, margin, icon, left ? 1.5f : -1.5f, left == (corner.y > .5f));
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
