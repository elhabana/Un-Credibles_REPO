using TMPro;
using UnCredibles.Core;
using UnCredibles.Localization;
using UnCredibles.Minigames;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Overlays
{
    // Pause during a minigame: any local player presses Pause (Esc / Start) to freeze the game.
    // Pause again resumes, Crouch (Ctrl / B) goes back to the main menu. Offline only: online the host's
    // simulation drives everybody, so one machine cannot stop the others.
    // Built in code and kept across scenes, like ConnectionLostOverlay.
    public sealed class PauseOverlay : MonoBehaviour
    {
        private CoreRoot core;
        private GameObject panel;
        private TMP_Text titleText;
        private TMP_Text hintText;
        private bool paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var root = new GameObject("Pause Overlay");
            DontDestroyOnLoad(root);
            root.AddComponent<PauseOverlay>();
        }

        private void Awake() => Build();

        private void OnEnable() => L10n.LanguageChanged += ShowTexts;
        private void OnDisable() => L10n.LanguageChanged -= ShowTexts;

        private void Update()
        {
            core = CoreRoot.Instance;
            if (core == null) return;

            if (!paused)
            {
                if (CanPause() && AnyLocalPressed(PlayerAction.Pause)) Pause();
                return;
            }
            if (!CanPause()) Resume(); // the minigame ended or the connection dropped behind the menu
            else if (AnyLocalPressed(PlayerAction.Crouch))
            {
                Resume();
                core.ReturnToMainMenu();
            }
            // Not Jump: in most minigames that press would also act in the game the moment it resumes.
            else if (AnyLocalPressed(PlayerAction.Pause)) Resume();
        }

        private bool CanPause()
        {
            if (core.GameFlow.Session != SessionMode.Local || core.GameFlow.State != GameState.Minigame ||
                core.IsConnectionLost) return false;
            var state = core.Minigames.ActiveMinigame?.State;
            return state == MinigameState.Countdown || state == MinigameState.Playing;
        }

        private bool AnyLocalPressed(PlayerAction action)
        {
            foreach (var slot in core.Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && slot.Input != null && slot.Input.WasPressed(action)) return true;
            return false;
        }

        private void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ShowTexts();
            panel.SetActive(true);
        }

        private void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            panel.SetActive(false);
        }

        private void ShowTexts()
        {
            titleText.text = L10n.Get(L10n.UI, "pause.title");
            hintText.text = L10n.Get(L10n.UI, "pause.hint");
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // above the minigame, below the connection-lost overlay
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            titleText = CreateText("Title", 96f, FontStyles.Bold, new Vector2(0f, 60f));
            hintText = CreateText("Hint", 34f, FontStyles.Normal, new Vector2(0f, -60f));
            hintText.color = new Color(1f, 1f, 1f, 0.8f);
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
            rect.sizeDelta = new Vector2(1500f, 140f);
            rect.anchoredPosition = position;
            return text;
        }
    }
}
