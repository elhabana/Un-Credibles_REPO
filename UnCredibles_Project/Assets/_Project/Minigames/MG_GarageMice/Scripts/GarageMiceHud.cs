using System.Collections.Generic;
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

        private GarageMiceController controller;

        public void Setup(GarageMiceController game)
        {
            controller = game;
            center.gameObject.SetActive(false);
            ShowFixedTexts();

            controller.CountdownTick += ShowCountdown;
            controller.Finished += HideForResults;
            L10n.LanguageChanged += ShowFixedTexts;
        }

        private void ShowFixedTexts() => instructions.text = L10n.Get(L10n.Minigames, "mice.controls");

        private void ShowCountdown(int seconds)
        {
            center.text = seconds > 0 ? seconds.ToString() : L10n.Get(L10n.Minigames, "mice.go");
            center.gameObject.SetActive(true);
            if (seconds == 0) Invoke(nameof(HideCenter), 1f);
        }

        // The results are the framework sheet (MinigameUI); only the countdown text has to leave.
        private void HideForResults(IReadOnlyList<MinigameResult> results)
        {
            CancelInvoke(nameof(HideCenter));
            HideCenter();
        }

        private void HideCenter() => center.gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.CountdownTick -= ShowCountdown;
                controller.Finished -= HideForResults;
            }
            L10n.LanguageChanged -= ShowFixedTexts;
        }
    }
}
