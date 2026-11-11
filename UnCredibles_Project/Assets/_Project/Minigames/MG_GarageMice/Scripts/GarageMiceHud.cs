using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.GarageMice
{
    // Garage mice extras on top of the shared MinigameUI (badges and timer): the controls in a paper
    // pill at the bottom and the countdown / results in the middle. Lives in PF_GarageMiceHud.
    public sealed class GarageMiceHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text center;
        [SerializeField] private TMP_Text instructions;

        private readonly StringBuilder builder = new StringBuilder(128);
        private GarageMiceController controller;

        public void Setup(GarageMiceController game)
        {
            controller = game;
            center.gameObject.SetActive(false);
            ShowFixedTexts();

            controller.CountdownTick += ShowCountdown;
            controller.Finished += ShowResults;
            L10n.LanguageChanged += ShowFixedTexts;
        }

        private void ShowFixedTexts() => instructions.text = L10n.Get(L10n.Minigames, "mice.controls");

        private void ShowCountdown(int seconds)
        {
            center.text = seconds > 0 ? seconds.ToString() : L10n.Get(L10n.Minigames, "mice.go");
            center.gameObject.SetActive(true);
            if (seconds == 0) Invoke(nameof(HideCenter), 1f);
        }

        private void ShowResults(IReadOnlyList<MinigameResult> results)
        {
            CancelInvoke(nameof(HideCenter));
            builder.Clear().AppendLine(L10n.Get(L10n.Minigames, "mice.results"));
            foreach (var result in results)
            {
                string name = L10n.Format(L10n.Minigames, "mice.player_n", result.PlayerId + 1);
                foreach (var player in controller.Players)
                    if (player.PlayerId == result.PlayerId) { name = player.PlayerName; break; }
                builder.Append(result.Placement).Append(". ").Append(name).Append("  ")
                    .AppendLine(L10n.Format(L10n.Minigames, "mice.count", result.Score));
            }
            center.text = builder.ToString();
            center.fontSize = 46f;
            center.gameObject.SetActive(true);
        }

        private void HideCenter() => center.gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.CountdownTick -= ShowCountdown;
                controller.Finished -= ShowResults;
            }
            L10n.LanguageChanged -= ShowFixedTexts;
        }
    }
}
