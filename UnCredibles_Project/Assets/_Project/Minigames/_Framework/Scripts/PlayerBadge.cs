using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames
{
    // One player's corner badge (PF_PlayerBadge): paper frame, a disc in the slot colour with the player's
    // 3D character (or the tag, "J1" / "BOT", without figures), the score and, if the minigame has one, its
    // points icon. A score change
    // pops a +N / -N next to it. Only shows values; MinigameUI feeds it from ScoreManager (also on replicas).
    // The prefab is laid out for the top-left corner; Setup mirrors it for the other corners.
    public sealed class PlayerBadge : MonoBehaviour
    {
        private const float PopupSeconds = 1f;
        private const float PopupRise = 40f;
        private const float PortraitInset = 4f; // the disc's ink border stays around the picture

        [SerializeField] private GameObject tape;
        [SerializeField] private Image disc;
        [SerializeField] private Image portrait;
        [SerializeField, Tooltip("Lobby figures: the slot's one, dressed the same, is filmed into the disc.")]
        private PlayerFigureSet figures;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text score;
        [SerializeField] private RectTransform popup;
        [SerializeField] private CanvasGroup popupGroup;
        [SerializeField] private TMP_Text popupText;

        private PlayerSlot slot;
        private Vector2 popupStart;
        private float popupDirection;
        private float popupTime = -1f;
        private int shownScore;
        private int popupDelta;
        private bool wasAI;
        private PlayerPortrait portrait3D;

        // Live view of the player's character, and the figure it films (for expressions and reactions).
        // Null when there are no figures.
        public Texture Portrait => portrait3D != null ? portrait3D.Texture : null;
        public GameObject Figure => portrait3D != null ? portrait3D.Figure : null;
        public int PlayerId => slot.PlayerId;
        public int SlotIndex => slot.SlotIndex;

        // corner: (0,1) top-left, (1,1) top-right, (0,0) bottom-left, (1,0) bottom-right. Call once.
        public void Setup(PlayerSlot player, Vector2 corner, Vector2 margin, Sprite scoreIcon, float tilt, bool hasTape)
        {
            slot = player;
            name = "Badge " + PlayerIdentity.LabelFor(player.SlotIndex);
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.localEulerAngles = new Vector3(0f, 0f, tilt);
            tape.SetActive(hasTape);
            SetMargin(margin);
            disc.color = PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI);

            icon.sprite = scoreIcon;
            icon.gameObject.SetActive(scoreIcon != null);
            // Without an icon the score also takes its place.
            var scoreRect = score.rectTransform;
            if (scoreIcon == null) scoreRect.offsetMin = new Vector2(icon.rectTransform.offsetMin.x, scoreRect.offsetMin.y);

            // Disc on the screen-edge side, score towards the middle; the popup towards the middle of the screen.
            bool right = corner.x > .5f;
            bool top = corner.y > .5f;
            if (right)
            {
                Mirror(disc.rectTransform, true, false);
                Mirror(icon.rectTransform, true, false);
                Mirror(scoreRect, true, false);
            }
            Mirror(popup, right, !top);
            popupStart = popup.anchoredPosition;
            popupDirection = top ? -1f : 1f;

            SetPortrait(null);
            RefreshTag();
            BuildPortrait();
            L10n.LanguageChanged += RefreshTag; // "P1" / "J1"
        }

        // Distance from its corner, in reference pixels. The corner it was set up for stays.
        public void SetMargin(Vector2 margin)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchorMin.x > .5f ? -margin.x : margin.x,
                rect.anchorMin.y > .5f ? -margin.y : margin.y);
        }

        private void BuildPortrait()
        {
            portrait3D = PlayerPortrait.Create(figures, slot.SlotIndex, slot.Loadout, disc.color);
            if (portrait3D == null) return;
            ShowPortrait(disc, portrait3D.Texture);
            tagText.gameObject.SetActive(false);
        }

        // Fills a disc with a portrait, cut round just inside its ink border. The cut is a child of its own:
        // masking with the disc itself would let its hard shadow show through beside the picture.
        public static RawImage ShowPortrait(Image disc, Texture picture)
        {
            var cut = new GameObject("Portrait 3D", typeof(RectTransform)).AddComponent<Image>();
            cut.transform.SetParent(disc.transform, false);
            PaperUI.Stretch(cut.rectTransform, PortraitInset);
            cut.sprite = disc.sprite;
            cut.raycastTarget = false;
            cut.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(cut.transform, false);
            PaperUI.Stretch(image.rectTransform);
            image.texture = picture;
            image.raycastTarget = false;
            return image;
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

        // Reflects a rect inside its parent: anchors, pivot and edges swap sides.
        private static void Mirror(RectTransform rect, bool x, bool y)
        {
            Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot;
            Vector2 offsetMin = rect.offsetMin, offsetMax = rect.offsetMax;
            if (x)
            {
                (anchorMin.x, anchorMax.x) = (1f - anchorMax.x, 1f - anchorMin.x);
                (offsetMin.x, offsetMax.x) = (-offsetMax.x, -offsetMin.x);
                pivot.x = 1f - pivot.x;
            }
            if (y)
            {
                (anchorMin.y, anchorMax.y) = (1f - anchorMax.y, 1f - anchorMin.y);
                (offsetMin.y, offsetMax.y) = (-offsetMax.y, -offsetMin.y);
                pivot.y = 1f - pivot.y;
            }
            rect.pivot = pivot;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void OnDestroy()
        {
            L10n.LanguageChanged -= RefreshTag;
            if (portrait3D != null) Destroy(portrait3D.gameObject);
        }
    }
}
