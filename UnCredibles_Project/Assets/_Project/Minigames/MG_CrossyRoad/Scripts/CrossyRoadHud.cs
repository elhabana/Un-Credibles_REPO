using TMPro;
using UnityEngine;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.CrossyRoad
{
    // CrossyRoad extras on top of the shared MinigameUI: the final rush banner. The red, beating
    // timer comes from MinigameUI (IsTimerUrgent). Only listens to the controller.
    public sealed class CrossyRoadHud : MonoBehaviour
    {
        [SerializeField] private CrossyRoadController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;

        private float bannerTimer;

        private void Awake()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (controller != null) controller.FinalRushStarted += HandleFinalRush;
        }

        private void OnDisable()
        {
            if (controller != null) controller.FinalRushStarted -= HandleFinalRush;
        }

        private void HandleFinalRush()
        {
            bannerTimer = bannerSeconds;
            if (bannerText == null) return;
            bannerText.text = L10n.Get(L10n.Minigames, "crossy.final_rush");
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
