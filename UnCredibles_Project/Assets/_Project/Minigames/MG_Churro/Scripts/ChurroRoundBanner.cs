using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.Churro
{
    // "ROUND 2 / 3" at the start of each round and the survivor at the end. Event driven.
    public sealed class ChurroRoundBanner : MonoBehaviour
    {
        [SerializeField] private ChurroController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField, Min(0f)] private float introVisibleSeconds = 1.2f;

        private GameObject roundPill;
        private float hideTimer;
        private int shownRound;
        private int shownTotal;

        // Subscribed in Awake: the component toggles itself on/off, so OnEnable would subscribe twice.
        private void Awake()
        {
            controller.RoundStarted += HandleRoundStarted;
            controller.RoundEnded += HandleRoundEnded;
            L10n.LanguageChanged += ShowRound;
            bannerText.gameObject.SetActive(false);
            BuildRoundPill();
            enabled = false; // Update only runs while a banner is visible
        }

        private void OnDestroy()
        {
            controller.RoundStarted -= HandleRoundStarted;
            controller.RoundEnded -= HandleRoundEnded;
            L10n.LanguageChanged -= ShowRound;
        }

        // The round counter stays on screen, so it follows a language change; the banners are short-lived.
        private void ShowRound()
        {
            if (shownRound <= 0) return;
            roundText.text = L10n.Format(L10n.Minigames, "churro.round", shownRound, shownTotal);
            roundPill.SetActive(true);
        }

        // The round counter in a paper pill, top centre, where other minigames show the time (Churro has none).
        // The font is the title one, set by MinigameUI on every text of the HUD canvas.
        private void BuildRoundPill()
        {
            var pill = PaperUI.Pill("Round pill", roundText.transform.parent, PaperUI.Paper, 76f);
            PaperUI.Place(pill.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 76f));
            roundText.transform.SetParent(pill.transform, false);
            PaperUI.Stretch(roundText.rectTransform);
            roundText.color = PaperUI.Ink;
            roundText.fontSize = 46f;
            roundText.alignment = TextAlignmentOptions.Center;
            roundText.textWrappingMode = TextWrappingModes.NoWrap;
            roundText.text = string.Empty;
            roundPill = pill.gameObject;
            roundPill.SetActive(false);
        }

        private void HandleRoundStarted(int round, int total)
        {
            shownRound = round;
            shownTotal = total;
            ShowRound();
            // The first round also reminds the two controls.
            if (round == 1) Show(L10n.Get(L10n.Minigames, "churro.round_intro"), introVisibleSeconds + 1f);
            else Show(L10n.Format(L10n.Minigames, "churro.round_n", round), introVisibleSeconds);
        }

        private void HandleRoundEnded(int round, IReadOnlyList<ChurroPlayer> survivors)
        {
            string message = survivors.Count == 1 ? L10n.Format(L10n.Minigames, "churro.survives", survivors[0].Slot.PlayerName) : L10n.Get(L10n.Minigames, "churro.everybody_fell");
            Show(message, 0f);
        }

        private void Show(string message, float seconds)
        {
            bannerText.text = message;
            bannerText.gameObject.SetActive(true);
            hideTimer = seconds;
            enabled = seconds > 0f;
        }

        private void Update()
        {
            if ((hideTimer -= Time.deltaTime) > 0f) return;
            bannerText.gameObject.SetActive(false);
            enabled = false;
        }
    }
}
