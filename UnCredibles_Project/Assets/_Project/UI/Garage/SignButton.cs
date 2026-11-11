using TMPro;
using UnCredibles.Minigames;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Art bible look of a garage menu button, applied at runtime over the scene's plain buttons: a
    // paper-kit pill (panels and lobby; the home options use MenuTab). Selected (pad, keys or mouse hover,
    // which SelectionHighlight turns into a selection) it straightens and its text turns TV mustard;
    // SelectionHighlight still grows it and plays the sound. Disabled, it fades.
    public sealed class SignButton : MonoBehaviour
    {
        public static readonly Color Mustard = new Color(.851f, .698f, .416f);    // #D9B26A
        public static readonly Color Brick = new Color(.698f, .294f, .235f);      // #B24B3C
        public static readonly Color SofaBlue = new Color(.302f, .49f, .722f);    // #4D7DB8
        public static readonly Color SoftInk = new Color(.114f, .114f, .106f, .6f);
        private static readonly Color ShadowColor = new Color(.114f, .114f, .106f, .9f);
        private static readonly Vector2 ShadowOffset = new Vector2(6f, -6f);
        private const float TurnSpeed = 14f;  // same feel as SelectionHighlight's grow
        private const float DisabledAlpha = .5f;

        private const int PillShape = 64;     // texture height of PaperUI.PillSprite
        private const int DashSize = 64;
        private const int DashRadius = 16;

        private static Sprite dashed;
        private static Material shadowMaterial;
        private static TMP_FontAsset shadowFont;

        [SerializeField] private Selectable target;
        [SerializeField] private Graphic face;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color fill = Color.white;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private float tilt;
        private int shownState = -1; // 0 idle, 1 selected, 2 disabled

        // Paper-kit pill of the given fill; text in paper (or ink on a light fill).
        public static void Pill(Selectable button, TMP_FontAsset font, Color fill, Color text, float tilt = 0f)
        {
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            image.sprite = PaperUI.PillSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PillShape / Mathf.Max(1f, ((RectTransform)button.transform).sizeDelta.y);
            Setup(button, image, font, fill, text, tilt, false);
        }

        private static void Setup(Selectable button, Image image, TMP_FontAsset font, Color fill, Color text, float tilt, bool shadowText)
        {
            button.transition = Selectable.Transition.None; // the look below replaces the pink tint
            AddShadow(image);
            var look = button.GetComponent<SignButton>();
            if (look == null) look = button.gameObject.AddComponent<SignButton>();
            look.target = button;
            look.face = image;
            look.label = button.GetComponentInChildren<TMP_Text>(true);
            look.fill = fill;
            look.textColor = text;
            look.tilt = tilt;
            look.shownState = -1;
            button.transform.localEulerAngles = new Vector3(0f, 0f, tilt);
            if (look.label != null) Text(look.label, font, text, shadowText);
        }

        // Bangers in the given colour; shadowed = hard ink drop shadow (TMP underlay).
        public static void Text(TMP_Text label, TMP_FontAsset font, Color color, bool shadowed)
        {
            if (font != null) label.font = font;
            label.fontStyle &= ~FontStyles.Bold; // Bangers is already heavy
            label.color = color;
            if (!shadowed || font == null) return;
            label.fontSharedMaterial = ShadowMaterial(font);
            label.extraPadding = true; // room for the offset underlay
        }

        public static void AddShadow(Graphic graphic)
        {
            var shadow = graphic.GetComponent<Shadow>();
            if (shadow == null) shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowOffset;
        }

        // Rounded frame with a dashed ink border, for empty places; use Image.Type.Tiled so the dashes repeat.
        public static Sprite DashedSprite => dashed != null ? dashed : dashed = BuildDashed();

        private static Material ShadowMaterial(TMP_FontAsset font)
        {
            if (shadowMaterial != null && shadowFont == font) return shadowMaterial;
            shadowFont = font;
            shadowMaterial = new Material(font.material) { name = font.name + " hard shadow", hideFlags = HideFlags.DontSave };
            shadowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            shadowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, PaperUI.Ink);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .8f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.8f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            ShaderUtilities.UpdateShaderRatios(shadowMaterial);
            return shadowMaterial;
        }

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

        // White fill, ink dashes on the straight edges (period 16 so the tiled edges repeat cleanly).
        private static Sprite BuildDashed()
        {
            var texture = NewTexture("Dashed frame", DashSize, DashSize);
            var pixels = new Color32[DashSize * DashSize];
            Color ink = PaperUI.Ink;
            for (int y = 0; y < DashSize; y++)
            for (int x = 0; x < DashSize; x++)
            {
                float distance = RoundedBox(x + .5f, y + .5f, DashSize, DashSize, DashRadius);
                bool cornerX = x < DashRadius || x >= DashSize - DashRadius;
                bool cornerY = y < DashRadius || y >= DashSize - DashRadius;
                // Along a top/bottom edge the dash follows x, along a side it follows y; corners stay solid.
                int along = cornerX && cornerY ? 0 : cornerX ? y - DashRadius : x - DashRadius;
                bool dash = cornerX && cornerY || along % 16 < 10;
                float border = dash ? Mathf.Clamp01(.5f - (distance + 4f)) : 1f;
                Color color = Color.Lerp(ink, Color.white, border);
                color.a = Mathf.Clamp01(.5f - distance);
                pixels[y * DashSize + x] = color;
            }
            return Finish(texture, pixels, new Vector4(DashRadius, DashRadius, DashRadius, DashRadius));
        }

        // Signed distance to a rounded box filling the texture (negative inside).
        private static float RoundedBox(float x, float y, float width, float height, float radius)
        {
            float qx = Mathf.Abs(x - width * .5f) - (width * .5f - radius);
            float qy = Mathf.Abs(y - height * .5f) - (height * .5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D NewTexture(string name, int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

        private static Sprite Finish(Texture2D texture, Color32[] pixels, Vector4 border)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
