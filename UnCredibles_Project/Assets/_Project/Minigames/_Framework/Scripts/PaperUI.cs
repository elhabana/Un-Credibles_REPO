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
        private const int PanelRadius = 16;
        private const int DiscSize = 128;
        private const float InkWidth = 4f;   // reference pixels, as drawn at 1:1

        private static Sprite panel;
        private static Sprite pill;
        private static Sprite disc;
        private static Texture2D panelTexture;
        private static Texture2D pillTexture;
        private static Texture2D discTexture;

        // Unity null check: the cached sprites are rebuilt if something destroyed them.
        public static Sprite PanelSprite => panel != null ? panel : panel = Build(ref panelTexture, ShapeSize, PanelRadius, InkWidth, PanelRadius);
        public static Sprite PillSprite => pill != null ? pill : pill = Build(ref pillTexture, ShapeSize, ShapeSize / 2, InkWidth, ShapeSize / 2);
        public static Sprite DiscSprite => disc != null ? disc : disc = Build(ref discTexture, DiscSize, DiscSize / 2, InkWidth * 1.5f, 0);

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

        // Rounded square with a white fill and an ink border, antialiased over one pixel. slice = 9-slice border.
        // texture: the previous one of this shape, destroyed so a rebuilt sprite leaves no orphan.
        private static Sprite Build(ref Texture2D texture, int size, float radius, float ink, int slice)
        {
            if (texture != null)
            {
                if (Application.isPlaying) Object.Destroy(texture);
                else Object.DestroyImmediate(texture); // the garage menu preview builds these in edit mode
            }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PaperUI shape",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            float half = size * .5f;
            Color inkColor = Ink;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to the rounded square (negative inside).
                float qx = Mathf.Abs(x + .5f - half) - (half - radius);
                float qy = Mathf.Abs(y + .5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float alpha = Mathf.Clamp01(.5f - distance);
                float paper = Mathf.Clamp01(.5f - (distance + ink));
                var color = Color.Lerp(inkColor, Color.white, paper);
                color.a = alpha;
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }
    }
}
