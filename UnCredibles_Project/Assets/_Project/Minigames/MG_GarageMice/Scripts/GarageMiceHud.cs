using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames.GarageMice
{
    // A small screen-space HUD built with the prototype arena, so the scene can be played alone.
    public sealed class GarageMiceHud : MonoBehaviour
    {
        private readonly StringBuilder builder = new StringBuilder(128);
        private GarageMiceController controller;
        private GameObject root;
        private TMP_Text timer;
        private TMP_Text scores;
        private TMP_Text center;

        public void Setup(GarageMiceController game)
        {
            controller = game;
            var canvasObject = new GameObject("Garage mice HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root = canvasObject;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Text("Title", "RATONES EN EL GARAJE", new Vector2(0f, -40f), new Vector2(780f, 80f),
                48f, TextAlignmentOptions.Center, new Vector2(.5f, 1f));
            timer = Text("Timer", "", new Vector2(-60f, -42f), new Vector2(280f, 70f),
                40f, TextAlignmentOptions.Right, Vector2.one);
            scores = Text("Scores", "", new Vector2(40f, -45f), new Vector2(430f, 220f),
                28f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
            Text("Instructions", "MUEVE: STICK / WASD    ESCOBAZO: A / ESPACIO", new Vector2(0f, 32f),
                new Vector2(1100f, 55f), 25f, TextAlignmentOptions.Center, new Vector2(.5f, 0f));
            center = Text("Countdown and results", "", Vector2.zero, new Vector2(900f, 430f),
                70f, TextAlignmentOptions.Center, new Vector2(.5f, .5f));
            center.gameObject.SetActive(false);

            controller.CountdownTick += ShowCountdown;
            controller.Timer.SecondChanged += ShowTime;
            controller.Score.ScoreChanged += ShowScores;
            controller.Finished += ShowResults;
            ShowScores(0, 0);
        }

        private TMP_Text Text(string name, string value, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void ShowTime(int seconds) => timer.text = $"{seconds}s";

        private void ShowScores(int playerId, int score)
        {
            builder.Clear();
            foreach (var player in controller.Players)
                builder.Append(player.PlayerName).Append(": ")
                    .Append(controller.Score.GetScore(player.PlayerId)).Append('\n');
            scores.text = builder.ToString();
        }

        private void ShowCountdown(int seconds)
        {
            center.text = seconds > 0 ? seconds.ToString() : "¡A BARRER!";
            center.gameObject.SetActive(true);
            if (seconds == 0) Invoke(nameof(HideCenter), 1f);
        }

        private void ShowResults(IReadOnlyList<MinigameResult> results)
        {
            CancelInvoke(nameof(HideCenter));
            builder.Clear().AppendLine("RESULTADOS");
            foreach (var result in results)
            {
                string name = $"Jugador {result.PlayerId + 1}";
                foreach (var player in controller.Players)
                    if (player.PlayerId == result.PlayerId) { name = player.PlayerName; break; }
                builder.Append(result.Placement).Append(". ").Append(name).Append("  ")
                    .Append(result.Score).AppendLine(" ratones");
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
                controller.Timer.SecondChanged -= ShowTime;
                controller.Score.ScoreChanged -= ShowScores;
                controller.Finished -= ShowResults;
            }
            if (root != null) Destroy(root);
        }
    }
}
