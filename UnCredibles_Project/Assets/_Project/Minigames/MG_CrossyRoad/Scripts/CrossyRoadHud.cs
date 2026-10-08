using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // CrossyRoad extras on top of the shared MinigameUI: the final rush banner and a red,
    // pulsing timer while it lasts. Only listens to the controller.
    public sealed class CrossyRoadHud : MonoBehaviour
    {
        [SerializeField] private CrossyRoadController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;
        [SerializeField] private Color rushColor = new Color(1f, 0.3f, 0.2f);

        private float bannerTimer;
        private Color timerColor;

        private void Awake()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
            if (timerText != null) timerColor = timerText.color;
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
            bannerText.text = "¡RECTA FINAL!\n<size=60%>Cada abuela vale x2</size>";
            bannerText.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                float pop = 1f + Mathf.Max(0f, bannerTimer - bannerSeconds + 0.3f) * 1.5f;
                if (bannerText != null) bannerText.transform.localScale = new Vector3(pop, pop, 1f);
                if (bannerTimer <= 0f && bannerText != null) bannerText.gameObject.SetActive(false);
            }

            if (timerText == null || controller == null) return;
            if (controller.IsFinalRush)
            {
                float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.15f;
                timerText.color = rushColor;
                timerText.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            else
            {
                timerText.color = timerColor;
                timerText.transform.localScale = Vector3.one;
            }
        }
    }
}
