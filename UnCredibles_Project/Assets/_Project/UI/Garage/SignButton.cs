using TMPro;
using UnCredibles.Minigames;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Art bible look of a garage menu button: a paper-kit pill saved in the scene (PF_SignPill and the
    // restyled panel and lobby buttons; the home options use MenuTab). Selected (pad, keys or mouse hover,
    // which SelectionHighlight turns into a selection) it straightens and its text turns TV mustard;
    // SelectionHighlight still grows it and plays the sound. Disabled, it fades.
    public sealed class SignButton : MonoBehaviour
    {
        public static readonly Color Mustard = new Color(.851f, .698f, .416f);    // #D9B26A
        public static readonly Color Brick = new Color(.698f, .294f, .235f);      // #B24B3C
        public static readonly Color SofaBlue = new Color(.302f, .49f, .722f);    // #4D7DB8
        public static readonly Color SoftInk = new Color(.114f, .114f, .106f, .6f);
        private const float TurnSpeed = 14f;  // same feel as SelectionHighlight's grow
        private const float DisabledAlpha = .5f;

        [SerializeField] private Selectable target;
        [SerializeField] private Graphic face;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color fill = Color.white;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private float tilt;
        private int shownState = -1; // 0 idle, 1 selected, 2 disabled

        private void Update()
        {
            if (target == null) return;
            var events = EventSystem.current;
            bool selected = events != null && events.currentSelectedGameObject == gameObject;
            int state = !target.IsInteractable() ? 2 : selected ? 1 : 0;
            if (state != shownState)
            {
                shownState = state;
                var faceColor = fill;
                var text = state == 1 ? Mustard : textColor;
                if (state == 2)
                {
                    faceColor.a *= DisabledAlpha;
                    text.a *= DisabledAlpha;
                }
                if (face != null) face.color = faceColor;
                if (label != null) label.color = text;
            }

            float goal = state == 1 ? 0f : tilt;
            float z = Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
            if (Mathf.Abs(z - goal) < .02f) return;
            z = Mathf.Abs(z - goal) < .1f ? goal : Mathf.Lerp(z, goal, 1f - Mathf.Exp(-TurnSpeed * Time.unscaledDeltaTime));
            transform.localEulerAngles = new Vector3(0f, 0f, z);
        }
    }
}
