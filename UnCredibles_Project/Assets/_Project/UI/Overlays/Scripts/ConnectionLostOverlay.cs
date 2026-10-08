using TMPro;
using UnCredibles.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnCredibles.UI.Overlays
{
    // Shown when the online connection drops: the game stays frozen behind a message, then
    // everybody goes back to the main menu. Built in code and kept across scenes, so it works
    // in the lobby, a minigame or Results without adding it to every scene.
    public sealed class ConnectionLostOverlay : MonoBehaviour
    {
        private const float ReturnSeconds = 5f;

        private CoreRoot core;
        private GameObject panel;
        private TMP_Text titleText;
        private TMP_Text footerText;
        private float timeLeft;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var root = new GameObject("Connection Lost Overlay");
            DontDestroyOnLoad(root);
            root.AddComponent<ConnectionLostOverlay>();
        }

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (core != null) core.ConnectionLost -= Show;
        }

        private void Update()
        {
            // Core comes in after the first scene; hook to it as soon as it exists.
            if (core != CoreRoot.Instance)
            {
                if (core != null) core.ConnectionLost -= Show;
                core = CoreRoot.Instance;
                if (core != null) core.ConnectionLost += Show;
            }
            if (!showing) return;

            timeLeft -= Time.unscaledDeltaTime;
            footerText.text = $"Volviendo al menu en {Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}...";
            if (timeLeft <= 0f || ContinuePressed()) Close();
        }

        // Only a short message; the technical reason stays out of the screen.
        private void Show(string title, string detail)
        {
            titleText.text = title;
            timeLeft = ReturnSeconds;
            showing = true;
            panel.SetActive(true);
        }

        private void Close()
        {
            showing = false;
            panel.SetActive(false);
            if (core != null && core.IsConnectionLost) core.ReturnToMainMenu();
        }

        private static bool ContinuePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.escapeKey.wasPressedThisFrame)) return true;
            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above every scene UI
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Dark full-screen panel; it also blocks clicks on the frozen scene behind it.
            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            titleText = CreateText("Title", 72f, FontStyles.Bold, new Vector2(0f, 50f));
            footerText = CreateText("Footer", 32f, FontStyles.Normal, new Vector2(0f, -50f));
            footerText.color = new Color(1f, 1f, 1f, 0.7f);
            panel.SetActive(false);
        }

        private TMP_Text CreateText(string objectName, float size, FontStyles style, Vector2 position)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1500f, 120f);
            rect.anchoredPosition = position;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
