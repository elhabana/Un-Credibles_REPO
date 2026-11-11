using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.GarageMice
{
    // Garage mice extras on top of the shared MinigameUI (badges and timer): the controls in a paper
    // pill at the bottom and the countdown / results in the middle. Built in code, so the scene can be
    // played alone.
    public sealed class GarageMiceHud : MonoBehaviour
    {
        private readonly StringBuilder builder = new StringBuilder(128);
        private GarageMiceController controller;
        private GameObject root;
        private TMP_Text center;
        private TMP_Text instructions;

        public void Setup(GarageMiceController game)
        {
            controller = game;
            root = PaperUI.Canvas("Garage mice HUD", null, 0).gameObject;
            var ui = game.GetComponent<MinigameUI>();
            var titleFont = ui != null ? ui.TitleFont : null;

            var controls = PaperUI.Pill("Controls", root.transform, PaperUI.Paper, 56f);
            PaperUI.Place(controls.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 26f), new Vector2(760f, 56f));
            instructions = PaperUI.Text("Instructions", controls.transform, titleFont, 24f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(instructions.rectTransform);
            center = PaperUI.Text("Countdown and results", root.transform, titleFont, 70f, Color.white, TextAlignmentOptions.Center);
            center.textWrappingMode = TextWrappingModes.Normal;
            PaperUI.Place(center.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900f, 430f));
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
            if (root != null) Destroy(root);
        }
    }
}
