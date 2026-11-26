using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnCredibles.Localization;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Mow The Lawn extras on top of the shared MinigameUI: a start banner that explains the turbo,
    // the final frenzy banner and, on each player's badge, a grass meter for the bag being filled
    // (flashes when the bag is full). The red, beating timer comes from MinigameUI (IsTimerUrgent).
    // Only listens to the controller.
    public sealed class MowHud : MonoBehaviour
    {
        [SerializeField] private MowTheLawnController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;

        private const float MeterHeight = 22f;
        private const float MeterGap = 8f;
        private const float MeterInset = 4f;
        private const float FlashSeconds = .35f;
        private static readonly Color GrassColor = new Color(.42f, .78f, .22f);

        private sealed class Meter
        {
            public int PlayerId;
            public RectTransform Root;
            public Image Fill;
            public float Last;
            public float Flash;
        }

        private readonly List<Meter> meters = new List<Meter>();
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
            if (state != MinigameState.Playing) return;
            ShowBanner(L10n.Get(L10n.Minigames, "mow.start"));
            BuildMeters();
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

        // A paper pill under the score of every badge (above it for the bottom corners), tilted with it.
        private void BuildMeters()
        {
            if (meters.Count > 0) return;
            var ui = FindFirstObjectByType<MinigameUI>();
            if (ui == null) return;
            foreach (var badge in ui.Badges)
            {
                bool top = ((RectTransform)badge.transform).anchorMin.y > .5f;
                var back = PaperUI.Pill("GrassMeter", badge.transform, PaperUI.Paper, MeterHeight);
                var root = back.rectTransform;
                root.anchorMin = new Vector2(0f, top ? 0f : 1f);
                root.anchorMax = new Vector2(1f, top ? 0f : 1f);
                root.pivot = new Vector2(.5f, top ? 1f : 0f);
                root.anchoredPosition = new Vector2(0f, top ? -MeterGap : MeterGap);
                root.sizeDelta = new Vector2(-24f, MeterHeight);

                var fill = PaperUI.Pill("Grass", root, GrassColor, MeterHeight - MeterInset * 2f);
                Destroy(fill.GetComponent<Shadow>()); // only the frame casts the paper shadow
                meters.Add(new Meter { PlayerId = badge.PlayerId, Root = root, Fill = fill });
            }
        }

        private void UpdateMeters()
        {
            foreach (var meter in meters)
            {
                float fill = controller.BagFill(meter.PlayerId);
                if (fill < meter.Last - .5f) meter.Flash = FlashSeconds; // a bag just filled up
                meter.Last = fill;
                if (meter.Flash > 0f) meter.Flash -= Time.deltaTime;
                bool flashing = meter.Flash > 0f;

                var rect = meter.Fill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(flashing ? 1f : fill, 1f);
                rect.offsetMin = new Vector2(MeterInset, MeterInset);
                rect.offsetMax = new Vector2(-MeterInset, -MeterInset);
                // Below the pill's own height its round caps would squash: wait until there is room.
                meter.Fill.enabled = rect.rect.width >= rect.rect.height;
                meter.Fill.color = flashing ? Color.white : GrassColor;
                float pop = 1f + Mathf.Max(0f, meter.Flash / FlashSeconds) * .15f;
                meter.Root.localScale = new Vector3(pop, pop, 1f);
            }
        }

        private void Update()
        {
            if (controller != null) UpdateMeters();
            if (bannerTimer <= 0f) return;
            bannerTimer -= Time.deltaTime;
            float pop = 1f + Mathf.Max(0f, bannerTimer - bannerSeconds + 0.3f) * 1.5f;
            if (bannerText != null) bannerText.transform.localScale = new Vector3(pop, pop, 1f);
            if (bannerTimer <= 0f && bannerText != null) bannerText.gameObject.SetActive(false);
        }
    }
}
