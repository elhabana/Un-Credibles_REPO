using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Look of the settings on the garage TV: a cartoon title with doodles, one rounded row per setting
    // with a coloured icon tile, pink sliders and a brush-stroke VOLVER. Lays out the scene's settings
    // widgets in canvas units (170 x 100) and outlines the selected row in pink.
    public sealed class TvSettingsOsd
    {
        public static readonly Color Background = new Color(.15f, .07f, .17f);
        private static readonly Color Cream = new Color(1f, .96f, .9f);
        private static readonly Color Pink = new Color(1f, .42f, .82f);
        private static readonly Color DeepPurple = new Color(.3f, .12f, .55f);
        private static readonly Color Lilac = new Color(.8f, .7f, 1f);
        private static readonly Color Ink = new Color(.18f, .1f, .38f);
        private static readonly Color RowFill = new Color(1f, 1f, 1f, .05f);
        private static readonly Color RowOutline = new Color(.62f, .5f, 1f, .28f);
        private static readonly Color Selected = new Color(1f, .45f, .85f, .95f);
        private static readonly Color Well = new Color(.04f, .02f, .12f, .65f);
        private static readonly Color Doodle = new Color(.62f, .5f, 1f, .6f);
        private static readonly Color Hint = new Color(.66f, .58f, .98f);
        private static readonly Color[] TileColors =
        {
            new Color(.42f, .3f, .9f), new Color(.95f, .27f, .43f), new Color(.47f, .36f, .95f), new Color(.22f, .48f, 1f)
        };
        private const float RowHeight = 12.5f;
        private const float RowWidth = 148f;

        private readonly TvMenuSkin skin;
        private readonly Slider[] sliders;
        private readonly TextMeshProUGUI[] volumeLabels;
        private readonly Button back;
        private readonly TextMeshProUGUI backLabel;
        private readonly TextMeshProUGUI languageLabel;
        private readonly Button[] languageArrows;
        private readonly Image[] rowOutlines = new Image[4];
        private TextMeshProUGUI title;
        private TextMeshProUGUI[] titleShadows;

        public TvSettingsOsd(TvMenuSkin skin, Slider[] sliders, TextMeshProUGUI[] volumeLabels, RectTransform back,
            TextMeshProUGUI languageLabel, Button[] languageArrows)
        {
            this.skin = skin;
            this.sliders = sliders;
            this.volumeLabels = volumeLabels;
            this.back = back.GetComponent<Button>();
            backLabel = back.GetComponentInChildren<TextMeshProUGUI>(true);
            this.languageLabel = languageLabel;
            this.languageArrows = languageArrows;
        }

        public void Build(RectTransform root, TextMeshProUGUI title, TextMeshProUGUI languageValue, TextMeshProUGUI instruction)
        {
            root.GetComponent<Image>().color = Background;
            BuildTitle(root, title);
            BuildBack();

            var icons = new[] { skin.general, skin.music, skin.effects, skin.language };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = 24f - i * 17f;
                bool languageRow = i == icons.Length - 1;
                // The language row stops after its "next" arrow.
                rowOutlines[i] = Row(root, y, languageRow ? 132f : RowWidth, languageRow ? -8f : 0f);
                var tile = AddImage(root, "Icon tile", skin.tile, TileColors[i], new Vector2(-66f, y), new Vector2(9.5f, 9.5f), 2.4f);
                AddImage(tile.transform, "Icon", icons[i], Color.white, Vector2.zero, new Vector2(6f, 6f));
            }

            for (int i = 0; i < sliders.Length; i++)
            {
                float y = 24f - i * 17f;
                // Name on the left, value right-aligned at the row's end (see VolumeText).
                Style(volumeLabels[i], 5.5f, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(volumeLabels[i].rectTransform, new Vector2(7f, y), new Vector2(126f, 8f));
                StyleSlider(sliders[i], y);
            }

            float languageY = -27f;
            Style(languageLabel, 5.5f, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(languageLabel.rectTransform, new Vector2(-36f, languageY), new Vector2(40f, 8f));
            AddImage(root, "Language well", skin.round, Well, new Vector2(22f, languageY), new Vector2(40f, 8f), 2.4f);
            languageValue.transform.SetAsLastSibling();
            Style(languageValue, 5.5f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(languageValue.rectTransform, new Vector2(22f, languageY), new Vector2(40f, 8f));
            for (int i = 0; i < languageArrows.Length; i++)
            {
                var arrow = languageArrows[i];
                arrow.transform.SetAsLastSibling();
                Sliced(arrow.GetComponent<Image>(), skin.round, 2.4f).color = new Color(1f, 1f, 1f, .08f);
                arrow.GetComponentInChildren<TextMeshProUGUI>(true).text = string.Empty;
                Place((RectTransform)arrow.transform, new Vector2(i == 0 ? -6f : 50f, languageY), new Vector2(9f, 8f));
                AddImage(arrow.transform, "Icon", i == 0 ? skin.previous : skin.next, Cream, Vector2.zero, new Vector2(4.5f, 4.5f));
            }

            AddImage(root, "Divider", skin.round, new Color(.62f, .5f, 1f, .3f), new Vector2(0f, -38f), new Vector2(90f, .5f), .25f);
            Style(instruction, 4f, Hint, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(instruction.rectTransform, new Vector2(0f, -44f), new Vector2(RowWidth, 7f));

            AddImage(root, "Star doodle", skin.star, Doodle, new Vector2(-72f, -43f), new Vector2(11f, 11f)).transform.localEulerAngles = new Vector3(0f, 0f, 12f);
            AddImage(root, "Crown doodle", skin.crown, Doodle, new Vector2(70f, -42f), new Vector2(12f, 9f)).transform.localEulerAngles = new Vector3(0f, 0f, -10f);
            Dash(root, new Vector2(64f, -33f), 70f, Doodle);
            Dash(root, new Vector2(75f, -35f), 40f, Doodle);
            LinkNavigation();
        }

        // Called every frame while the TV shows the settings.
        public void Tick(GameObject selected)
        {
            for (int i = 0; i < sliders.Length; i++)
                rowOutlines[i].color = selected == sliders[i].gameObject ? Selected : RowOutline;
            for (int i = 0; i < languageArrows.Length; i++)
                languageArrows[i].targetGraphic.color = selected == languageArrows[i].gameObject
                    ? new Color(Pink.r, Pink.g, Pink.b, .45f) : new Color(1f, 1f, 1f, .08f);
            bool language = selected == languageArrows[0].gameObject || selected == languageArrows[1].gameObject;
            rowOutlines[rowOutlines.Length - 1].color = language ? Selected : RowOutline;
            back.targetGraphic.color = selected == back.gameObject ? Pink : Lilac;

            // The shadows copy the title, which the scene localizes.
            foreach (var shadow in titleShadows)
                if (shadow.text != title.text) shadow.text = title.text;
        }

        // Name on the left and the value right-aligned on the same line.
        public static string VolumeText(string label, float value) =>
            label + "<line-height=0>\n<align=right>" + Mathf.RoundToInt(value * 100f) + "%";

        private void BuildTitle(RectTransform root, TextMeshProUGUI sceneTitle)
        {
            title = sceneTitle;
            var position = new Vector2(-34f, 42f);
            var size = new Vector2(84f, 20f);
            var offsets = new[] { new Vector2(1.6f, -1.6f), new Vector2(.8f, -.8f) };
            var colors = new[] { DeepPurple, Pink };
            titleShadows = new TextMeshProUGUI[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var shadow = new GameObject("Title shadow", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                shadow.transform.SetParent(root, false);
                TitleStyle(shadow, colors[i]);
                shadow.text = title.text;
                Place(shadow.rectTransform, position + offsets[i], size);
                titleShadows[i] = shadow;
            }
            title.transform.SetAsLastSibling();
            TitleStyle(title, Cream);
            Place(title.rectTransform, position, size);

            var crown = AddImage(root, "Crown doodle", skin.crown, Pink, new Vector2(-24f, 54f), new Vector2(10f, 8f));
            crown.transform.localEulerAngles = new Vector3(0f, 0f, -8f);
            Dash(root, new Vector2(-78f, 42f), 0f, Pink);
            Dash(root, new Vector2(-44f, 55f), 70f, Pink);
            Dash(root, new Vector2(-6f, 51f), -35f, Pink);
            Dash(root, new Vector2(-16f, 31f), -65f, Pink);
        }

        private void TitleStyle(TextMeshProUGUI text, Color color)
        {
            text.font = skin.titleFont;
            text.fontSize = 17f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 2f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        // VOLVER on a lilac brush stroke, with a "go back" arrow.
        private void BuildBack()
        {
            var backRect = (RectTransform)back.transform;
            back.transform.SetAsLastSibling();
            var stroke = back.GetComponent<Image>();
            stroke.sprite = skin.brush;
            stroke.type = Image.Type.Simple;
            stroke.color = Lilac;
            Place(backRect, new Vector2(52f, 42f), new Vector2(46f, 14f));
            backRect.localEulerAngles = new Vector3(0f, 0f, 3f);
            AddImage(backRect, "Icon", skin.back, Ink, new Vector2(-13f, 0f), new Vector2(5.5f, 5.5f));
            Style(backLabel, 5.5f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(backLabel.rectTransform, new Vector2(4.5f, 0f), new Vector2(28f, 10f));
        }

        // Dark rounded track, pink gradient fill and a white knob.
        private void StyleSlider(Slider slider, float y)
        {
            var track = (RectTransform)slider.transform;
            track.SetAsLastSibling();
            Place(track, new Vector2(17f, y), new Vector2(58f, 3f));
            Sliced(track.GetComponent<Image>(), skin.round, 1.5f).color = Well;

            if (slider.fillRect == null)
            {
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                fill.transform.SetParent(track, false);
                fill.transform.SetSiblingIndex(0);
                fill.raycastTarget = false;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                slider.fillRect = fill.rectTransform;
            }
            Sliced(slider.fillRect.GetComponent<Image>(), skin.fill, 1.5f).color = Color.white;

            var knob = slider.handleRect.GetComponent<Image>();
            knob.enabled = true;
            knob.sprite = skin.knob;
            knob.type = Image.Type.Simple;
            knob.color = Color.white;
            // The handle's height follows the track: 3 + 3 makes a 6 x 6 knob.
            slider.handleRect.sizeDelta = new Vector2(6f, 3f);
            slider.handleRect.anchoredPosition = Vector2.zero;
        }

        private Image Row(RectTransform root, float y, float width, float x)
        {
            var fill = AddImage(root, "Row", skin.round, RowFill, new Vector2(x, y), new Vector2(width, RowHeight), 3f);
            fill.transform.SetSiblingIndex(0);
            return AddImage(fill.transform, "Outline", skin.roundOutline, RowOutline, Vector2.zero, new Vector2(width, RowHeight), 3f);
        }

        private void Dash(Transform parent, Vector2 position, float angle, Color color)
        {
            var dash = AddImage(parent, "Doodle dash", skin.round, color, position, new Vector2(3.4f, .8f), .4f);
            dash.transform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        // Pad and keys go down the menu row by row (automatic navigation skips the bars from VOLVER).
        // Left/right stay empty on the bars so they change the volume, and swap the two language arrows.
        private void LinkNavigation()
        {
            Link(back, null, sliders[0], null, null);
            for (int i = 0; i < sliders.Length; i++)
                Link(sliders[i], i == 0 ? back : sliders[i - 1],
                    i == sliders.Length - 1 ? languageArrows[0] : sliders[i + 1], null, null);
            var last = sliders[sliders.Length - 1];
            Link(languageArrows[0], last, null, null, languageArrows[1]);
            Link(languageArrows[1], last, null, languageArrows[0], null);
        }

        private static void Link(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }

        // A 9-sliced sprite's corners keep the given radius in canvas units, whatever the image size.
        private static Image Sliced(Image image, Sprite sprite, float radius)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = sprite.border.x / radius;
            return image;
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, float radius = 0f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f) Sliced(image, sprite, radius);
            else image.sprite = sprite;
            Place(image.rectTransform, position, size);
            return image;
        }

        private static void Style(TextMeshProUGUI text, float size, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
