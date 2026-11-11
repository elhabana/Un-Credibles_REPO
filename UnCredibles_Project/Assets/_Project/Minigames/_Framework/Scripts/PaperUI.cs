using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames
{
    // Hand-drawn look of the art bible, built in code: cream paper, black ink border, hard offset
    // shadow, a slight tilt and sometimes a strip of tape. The shapes are generated once and cached as
    // sprites with a white fill and an ink border, so Image.color tints the paper and the ink stays black.
    public static class PaperUI
    {
        public static readonly Color Paper = new Color(.957f, .922f, .867f);      // #F4EBDD
        public static readonly Color Ink = new Color(.114f, .114f, .106f);        // #1D1D1B
        public static readonly Color TimerTeal = new Color(.169f, .647f, .604f);  // #2BA59A
        public static readonly Color Alarm = new Color(.902f, .231f, .180f);      // #E63B2E (hero red)
        private static readonly Color ShadowColor = new Color(.114f, .114f, .106f, .9f);
        private static readonly Color TapeColor = new Color(1f, .97f, .85f, .55f);
        private static readonly Vector2 ShadowOffset = new Vector2(6f, -6f);

        private const int ShapeSize = 64;    // texture side of the panel and pill shapes
        // The shapes are assets in UI/Common/Resources/PaperUI so scenes and prefabs can reference them.
        private static Sprite panel;
        private static Sprite pill;
        private static Sprite disc;

        public static Sprite PanelSprite => panel != null ? panel : panel = Load("T_UI_Panel");
        public static Sprite PillSprite => pill != null ? pill : pill = Load("T_UI_Pill");
        public static Sprite DiscSprite => disc != null ? disc : disc = Load("T_UI_Disc");

        private static Sprite Load(string name) => Resources.Load<Sprite>("PaperUI/" + name);

        // Screen-space canvas at the 1920x1080 reference every minigame HUD uses.
        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        // Paper card: 9-sliced rounded frame with ink border and hard shadow; tilt in degrees.
        public static Image Panel(string name, Transform parent, Color fill, float tilt = 0f, bool tape = false)
        {
            var image = Shape(name, parent, PanelSprite, fill, Image.Type.Sliced);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt);
            if (tape)
            {
                var strip = Shape("Tape", image.transform, null, TapeColor, Image.Type.Simple);
                Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -6f), new Vector2(76f, 24f));
                strip.rectTransform.pivot = new Vector2(.5f, .5f);
                strip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);
            }
            return image;
        }

        // Rounded-end pill of the given height (the caps stay round at any size).
        public static Image Pill(string name, Transform parent, Color fill, float height)
        {
            var image = Shape(name, parent, PillSprite, fill, Image.Type.Sliced);
            image.pixelsPerUnitMultiplier = ShapeSize / height;
            return image;
        }

        public static Image Disc(string name, Transform parent, Color fill) =>
            Shape(name, parent, DiscSprite, fill, Image.Type.Simple);

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        // Anchor and pivot on the same point, then offset from it.
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image Shape(string name, Transform parent, Sprite sprite, Color fill, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = fill;
            image.raycastTarget = false;
            if (sprite != null)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = ShadowColor;
                shadow.effectDistance = ShadowOffset;
            }
            return image;
        }
    }
}
