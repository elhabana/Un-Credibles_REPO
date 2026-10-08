using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Mow The Lawn extras on top of the shared MinigameUI: a start banner that explains the turbo,
    // the final frenzy banner and a red, pulsing timer while it lasts. Only listens to the controller.
    public sealed class MowHud : MonoBehaviour
    {
        [SerializeField] private MowTheLawnController controller;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;
        [SerializeField] private Color frenzyColor = new Color(1f, 0.3f, 0.2f);

        private float bannerTimer;
        private Color timerColor;

        private void Awake()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
            if (timerText != null) timerColor = timerText.color;
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
                ShowBanner("¡A CORTAR!\n<size=50%>ESPACIO / A: TURBO  ·  choca con turbo para quitar bolsas\nDescarga las bolsas en tu contenedor</size>");
        }

        private void HandleFrenzy() =>
            ShowBanner("¡FRENESÍ FINAL!\n<size=60%>El césped vuelve a crecer · bolsas x2</size>");

        private void ShowBanner(string message)
        {
            bannerTimer = bannerSeconds;
            if (bannerText == null) return;
            bannerText.text = message;
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
            if (controller.IsFrenzy)
            {
                float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.15f;
                timerText.color = frenzyColor;
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
