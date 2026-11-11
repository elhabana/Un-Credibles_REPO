using TMPro;
using UnCredibles.Minigames;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Home menu option over the comic panel: ink text only while idle. Selected (pad, keys or mouse hover,
    // which SelectionHighlight turns into a selection) a cream tab with a darker side slides out past the
    // panel's diagonal edge, tilted a little, and the text grows on it. Disabled, the text fades.
    // The button itself only keeps an invisible hit area: the intro slide moves it, this moves its children.
    public sealed class MenuTab : MonoBehaviour
    {
        private static readonly Color Side = new Color(.541f, .416f, .2f); // darker mustard, the tab's depth
        private static readonly Vector2 Depth = new Vector2(8f, -8f);
        private const float SlideSeconds = .12f;
        private const float Reach = 40f;       // how far the tab passes the button's right edge
        private const float TabBorder = .75f;  // sprite pixels per unit: thicker ink than a paper panel
        private const float FontSize = 60f;
        private const float TextInset = 18f;
        private const float TextGrow = .15f;
        private const float Tilt = 2.5f;       // degrees, tip up
        private const float DisabledAlpha = .5f;

        [SerializeField] private Selectable target;
        [SerializeField] private RectTransform tab;
        [SerializeField] private TMP_Text label;
        private float shown;                   // 0 idle .. 1 fully out
        private bool shownDisabled;

        public static void Apply(Selectable button, TMP_FontAsset font)
        {
            button.transition = Selectable.Transition.None; // the tab replaces the pink tint
            if (button.GetComponent<SelectionHighlight.KeepSize>() == null)
                button.gameObject.AddComponent<SelectionHighlight.KeepSize>(); // nor does it grow
            if (button.targetGraphic != null) button.targetGraphic.color = Color.clear;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            SignButton.Text(label, font, PaperUI.Ink, false);
            label.enableAutoSizing = false;
            label.fontSize = FontSize;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var text = label.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.pivot = new Vector2(0f, .5f);
            text.offsetMin = new Vector2(TextInset, 0f);
            text.offsetMax = Vector2.zero;

            var face = PaperUI.Panel("Tab", button.transform, PaperUI.Paper);
            face.pixelsPerUnitMultiplier = TabBorder;
            var depth = face.GetComponent<Shadow>();
            depth.effectColor = Side;
            depth.effectDistance = Depth;
            var rect = face.rectTransform;
            rect.SetAsFirstSibling(); // under the text
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(Reach, 0f);

            var look = button.GetComponent<MenuTab>();
            if (look == null) look = button.gameObject.AddComponent<MenuTab>();
            look.target = button;
            look.tab = rect;
            look.label = label;
            look.shown = 0f;
            look.shownDisabled = false;
            look.Pose(0f);
        }

        private void Update()
        {
            if (target == null) return;
            bool disabled = !target.IsInteractable();
            if (disabled != shownDisabled)
            {
                shownDisabled = disabled;
                var ink = PaperUI.Ink;
                if (disabled) ink.a = DisabledAlpha;
                label.color = ink;
            }
            var events = EventSystem.current;
            float goal = !disabled && events != null && events.currentSelectedGameObject == gameObject ? 1f : 0f;
            if (shown == goal) return;
            shown = Mathf.MoveTowards(shown, goal, Time.unscaledDeltaTime / SlideSeconds);
            Pose(shown);
        }

        private void Pose(float t)
        {
            float e = 1f - (1f - t) * (1f - t); // ease out
            var turn = Quaternion.Euler(0f, 0f, Tilt * e);
            tab.localScale = new Vector3(e, 1f, 1f); // grows out from the left: at 0 it has no area
            tab.localRotation = turn;
            var text = label.rectTransform;
            text.localRotation = turn;
            float grow = 1f + TextGrow * e;
            text.localScale = new Vector3(grow, grow, 1f);
            // Idle: left-aligned on the panel. Selected: centred on the tab (the text grows from its left pivot).
            float tabWidth = ((RectTransform)transform).rect.width + Reach;
            float centred = Mathf.Max(TextInset, (tabWidth - label.preferredWidth * grow) * .5f);
            text.anchoredPosition = new Vector2(Mathf.Lerp(TextInset, centred, e), 0f);
        }
    }
}
