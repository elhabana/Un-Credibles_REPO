using TMPro;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Mow The Lawn extras on top of the shared MinigameUI: a start banner that explains the turbo and
    // the final frenzy banner. The red, beating timer comes from MinigameUI (IsTimerUrgent).
    // Only listens to the controller.
    public sealed class MowHud : MonoBehaviour
    {
        [SerializeField] private MowTheLawnController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;

        private float bannerTimer;

        private void Awake()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.FrenzyStarted += HandleFrenzy;
            controller.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.FrenzyStarted -= HandleFrenzy;
            controller.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(MinigameState state)
        {
            if (state == MinigameState.Playing)
                ShowBanner(L10n.Get(L10n.Minigames, "mow.start"));
        }

        private void HandleFrenzy() =>
            ShowBanner(L10n.Get(L10n.Minigames, "mow.frenzy"));

        private void ShowBanner(string message)
        {
            bannerTimer = bannerSeconds;
            if (bannerText == null) return;
            bannerText.text = message;
            bannerText.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (bannerTimer <= 0f) return;
            bannerTimer -= Time.deltaTime;
            float pop = 1f + Mathf.Max(0f, bannerTimer - bannerSeconds + 0.3f) * 1.5f;
            if (bannerText != null) bannerText.transform.localScale = new Vector3(pop, pop, 1f);
            if (bannerTimer <= 0f && bannerText != null) bannerText.gameObject.SetActive(false);
        }
    }
}
