using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames
{
    // Card shown before every minigame: name, rules, controls and who already pressed Ready.
    // The countdown waits until every connected human is ready (see MinigameController.AwaitingReady).
    // Built in code, so every minigame scene gets it without setup. Texts come from the Minigames
    // table: "name.<id>", "rules.<id>" and "phone.<id>" (controls); the MinigameData fields are the fallback.
    public sealed class MinigameRulesCard : MonoBehaviour
    {
        private static readonly Color Backdrop = new Color(.03f, .05f, .08f, .92f);
        private static readonly Color Panel = new Color(.09f, .15f, .20f, 1f);
        private static readonly Color Muted = new Color(.58f, .67f, .73f);
        private static readonly Color ReadyColor = new Color(.45f, .95f, .69f);

        private MinigameController controller;
        private GameObject root;
        private Image thumbnail;
        private TMP_Text title;
        private TMP_Text rules;
        private TMP_Text controls;
        private TMP_Text footer;
        private readonly TMP_Text[] chips = new TMP_Text[PlayerRegistry.MaxPlayers];

        public void Setup(MinigameController game)
        {
            controller = game;
            root = new GameObject("Rules card", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the minigame HUD
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var backdrop = Box("Backdrop", root.transform, Backdrop);
            Stretch(backdrop.rectTransform);
            var panel = Box("Panel", root.transform, Panel);
            Place(panel.rectTransform, 0f, 0f, 1500f, 820f);

            title = Text("Title", panel.transform, 64f, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, 0f, 330f, 1400f, 90f);

            thumbnail = Box("Thumbnail", panel.transform, Color.white);
            thumbnail.preserveAspect = true;
            Place(thumbnail.rectTransform, -480f, 60f, 400f, 340f);

            rules = Text("Rules", panel.transform, 36f, TextAlignmentOptions.TopLeft, Color.white);
            Place(rules.rectTransform, 210f, 60f, 960f, 380f);

            controls = Text("Controls", panel.transform, 30f, TextAlignmentOptions.Center, new Color(1f, .79f, .35f));
            Place(controls.rectTransform, 0f, -180f, 1400f, 60f);

            for (int i = 0; i < chips.Length; i++)
            {
                chips[i] = Text("Player " + (i + 1), panel.transform, 30f, TextAlignmentOptions.Center, Color.white);
                Place(chips[i].rectTransform, (i - 1.5f) * 350f, -280f, 330f, 90f);
            }

            footer = Text("Footer", panel.transform, 28f, TextAlignmentOptions.Center, Muted);
            Place(footer.rectTransform, 0f, -365f, 1400f, 50f);

            controller.ReadyChanged += Refresh;
            controller.StateChanged += HandleStateChanged;
            L10n.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            L10n.LanguageChanged -= Refresh;
            if (controller == null) return;
            controller.ReadyChanged -= Refresh;
            controller.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(MinigameState state) => Refresh();

        private void Refresh()
        {
            // Waiting ends with the countdown, on the host and on online clients alike.
            bool show = controller.State == MinigameState.Waiting;
            root.SetActive(show);
            if (!show) return;

            var data = controller.Data;
            string id = data != null ? data.Id : string.Empty;
            title.text = Localized("name." + id, data != null ? data.DisplayName : string.Empty);
            rules.text = Localized("rules." + id, data != null ? data.Rules : string.Empty);
            controls.text = Localized("phone." + id, string.Empty);
            footer.text = L10n.Get(L10n.UI, "lobby.press_ready");

            bool hasThumbnail = data != null && data.Thumbnail != null;
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail) thumbnail.sprite = data.Thumbnail;
            Place(rules.rectTransform, hasThumbnail ? 210f : 0f, 60f, hasThumbnail ? 960f : 1360f, 380f);

            var players = controller.Players;
            for (int i = 0; i < chips.Length; i++)
            {
                bool used = i < players.Count;
                chips[i].gameObject.SetActive(used);
                if (!used) continue;
                var player = players[i];
                bool ready = controller.IsReady(i);
                string status = ready ? L10n.Get(L10n.UI, "lobby.ready")
                    : player.IsConnected ? L10n.Get(L10n.UI, "rules.waiting") : "—";
                chips[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerIdentity.ColorFor(player.SlotIndex, player.IsAI))}>" +
                                $"{player.PlayerName}</color>\n{status}";
                chips[i].color = ready ? ReadyColor : Muted;
            }
        }

        // Missing translation (Get returns the key): use the fallback, if any.
        private static string Localized(string key, string fallback)
        {
            string text = L10n.Get(L10n.Minigames, key);
            return text == key ? fallback ?? string.Empty : text;
        }

        private static Image Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
