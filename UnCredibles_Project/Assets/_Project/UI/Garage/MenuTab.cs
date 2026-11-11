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
    // The look lives in PF_MenuTab; the internal values are the ones it was baked with.
    public sealed class MenuTab : MonoBehaviour
    {
        internal static readonly Color Side = new Color(.541f, .416f, .2f); // darker mustard, the tab's depth
        internal static readonly Vector2 Depth = new Vector2(8f, -8f);
        private const float SlideSeconds = .12f;
        internal const float Reach = 40f;       // how far the tab passes the button's right edge
        internal const float TabBorder = .75f;  // sprite pixels per unit: thicker ink than a paper panel
        internal const float FontSize = 60f;
        internal const float TextInset = 18f;
        private const float TextGrow = .15f;
        private const float Tilt = 2.5f;       // degrees, tip up
        private const float DisabledAlpha = .5f;

        [SerializeField] private Selectable target;
        [SerializeField] private RectTransform tab;
        [SerializeField] private TMP_Text label;
        private float shown;                   // 0 idle .. 1 fully out
        private bool shownDisabled;

        private void Awake() => Pose(0f); // idle, whatever pose the prefab was saved in

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
            float textX = Mathf.Lerp(TextInset, centred, e);
            text.anchoredPosition = new Vector2(textX, textX * Mathf.Tan(Tilt * e * Mathf.Deg2Rad));
        }
    }
}
