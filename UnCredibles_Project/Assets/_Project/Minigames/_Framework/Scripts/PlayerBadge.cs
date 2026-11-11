using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames
{
    // One player's corner badge: paper frame, a disc in the slot colour with the tag ("J1" / "BOT")
    // or a portrait, the score and, if the minigame has one, its points icon. A score change pops a
    // +N / -N next to it. Only shows values; MinigameUI feeds it from ScoreManager (also on replicas).
    public sealed class PlayerBadge : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(300f, 110f);
        private const float DiscSize = 88f;
        private const float IconSize = 46f;
        private const float PopupSeconds = 1f;
        private const float PopupRise = 40f;

        private PlayerSlot slot;
        private TMP_Text tagText;
        private Image portrait;
        private TMP_Text score;
        private RectTransform popup;
        private CanvasGroup popupGroup;
        private TMP_Text popupText;
        private Vector2 popupStart;
        private float popupDirection;
        private float popupTime = -1f;
        private int shownScore;
        private int popupDelta;
        private bool wasAI;

        public int PlayerId => slot.PlayerId;

        // corner: (0,1) top-left, (1,1) top-right, (0,0) bottom-left, (1,0) bottom-right.
        public static PlayerBadge Create(Transform parent, PlayerSlot player, Vector2 corner, Vector2 margin,
            TMP_FontAsset font, Sprite icon, float tilt, bool tape)
        {
            var frame = PaperUI.Panel("Badge " + PlayerIdentity.LabelFor(player.SlotIndex), parent, PaperUI.Paper, tilt, tape);
            PaperUI.Place(frame.rectTransform, corner, Vector2.zero, Size);

            var badge = frame.gameObject.AddComponent<PlayerBadge>();
            badge.slot = player;
            badge.SetMargin(margin);
            badge.Build(frame.transform, font, icon, corner.x > .5f, corner.y > .5f);
            return badge;
        }

        public int SlotIndex => slot.SlotIndex;

        // Distance from its corner, in reference pixels. The corner it was built for stays.
        public void SetMargin(Vector2 margin)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchorMin.x > .5f ? -margin.x : margin.x,
                rect.anchorMin.y > .5f ? -margin.y : margin.y);
        }

        // Drawn portraits arrive later; until then the disc shows the tag.
        public void SetPortrait(Sprite sprite)
        {
            portrait.sprite = sprite;
            portrait.gameObject.SetActive(sprite != null);
            tagText.gameObject.SetActive(sprite == null);
        }

        // animate = false for the starting value, which is not a gain.
        public void SetScore(int value, bool animate = true)
        {
            if (value == shownScore) return;
            int delta = value - shownScore;
            shownScore = value;
            score.SetText("{0}", value);
            if (!animate) return;

            // Quick gains add up in the popup that is still on screen.
            popupDelta = popupTime >= 0f ? popupDelta + delta : delta;
            if (popupDelta > 0) popupText.SetText("+{0}", popupDelta);
            else popupText.SetText("{0}", popupDelta);
            popupText.color = popupDelta >= 0 ? PaperUI.Ink : PaperUI.Alarm;
            popupTime = 0f;
            popup.gameObject.SetActive(true);
        }

        private void Build(Transform frame, TMP_FontAsset font, Sprite icon, bool right, bool top)
        {
            // Disc on the screen-edge side, score towards the middle.
            float side = right ? -1f : 1f;
            var edge = new Vector2(right ? 1f : 0f, .5f);
            var disc = PaperUI.Disc("Disc", frame, PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI));
            PaperUI.Place(disc.rectTransform, edge, new Vector2(side * 14f, 0f), new Vector2(DiscSize, DiscSize));

            portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            portrait.transform.SetParent(disc.transform, false);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            PaperUI.Stretch(portrait.rectTransform, 6f);

            tagText = PaperUI.Text("Tag", disc.transform, font, 40f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(tagText.rectTransform);

            // Content area: the rest of the badge, inset from the frame.
            float start = 14f + DiscSize + 8f;
            float width = PlayerBadge.Size.x - start - 14f;
            if (icon != null)
            {
                var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(frame, false);
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                PaperUI.Place(image.rectTransform, edge, new Vector2(side * start, 0f), new Vector2(IconSize, IconSize));
                start += IconSize + 6f;
                width -= IconSize + 6f;
            }
            score = PaperUI.Text("Score", frame, font, 72f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Place(score.rectTransform, edge, new Vector2(side * start, 0f), new Vector2(width, PlayerBadge.Size.y));
            score.SetText("{0}", 0);

            // Popup: towards the middle of the screen, over the inner edge of the badge.
            var bubble = PaperUI.Pill("Popup", frame, PaperUI.Paper, 60f);
            popup = bubble.rectTransform;
            var inner = new Vector2(.5f, top ? 0f : 1f);
            popupStart = new Vector2(right ? -40f : 40f, 0f);
            popupDirection = top ? -1f : 1f;
            PaperUI.Place(popup, inner, popupStart, new Vector2(120f, 60f));
            popup.pivot = new Vector2(.5f, .5f);
            popupGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            popupText = PaperUI.Text("Amount", popup, font, 44f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(popupText.rectTransform);
            popup.gameObject.SetActive(false);

            SetPortrait(null);
            RefreshTag();
            L10n.LanguageChanged += RefreshTag; // "P1" / "J1"
        }

        private void RefreshTag()
        {
            wasAI = slot.IsAI;
            tagText.text = PlayerIdentity.LabelFor(slot.SlotIndex, slot.IsAI);
        }

        private void Update()
        {
            // Online a disconnected human becomes a bot without a new badge.
            if (wasAI != slot.IsAI) RefreshTag();
            if (popupTime < 0f) return;

            popupTime += Time.deltaTime;
            float t = popupTime / PopupSeconds;
            if (t >= 1f)
            {
                popupTime = -1f;
                popup.gameObject.SetActive(false);
                return;
            }
            // Pop past full size, settle, then drift away from the badge while fading.
            float scale = popupTime < .12f ? Mathf.Lerp(.3f, 1.25f, popupTime / .12f)
                : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((popupTime - .12f) / .1f));
            popup.localScale = new Vector3(scale, scale, 1f);
            popup.anchoredPosition = popupStart + new Vector2(0f, popupDirection * PopupRise * t);
            popupGroup.alpha = 1f - Mathf.Clamp01((t - .6f) / .4f);
        }

        private void OnDestroy() => L10n.LanguageChanged -= RefreshTag;
    }
}
