using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Look of the settings on the garage TV: a retro pixel-art screen with a sky, clouds, hills, a grass
    // ground and a mouse running about; one orange row per setting with an icon tile, red volume bars and
    // a language picker. Lays out the scene's settings widgets in canvas units (170 x 100) and frames
    // the selected row or button in yellow.
    public sealed class TvSettingsOsd
    {
        public static readonly Color Background = new Color(.31f, .53f, .97f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color Ink = new Color(.16f, .07f, .03f);
        private static readonly Color Hint = new Color(.9f, .95f, 1f);
        private static readonly Color ButtonIdle = new Color(.88f, .88f, .88f);
        // Canvas units per art pixel: interface sprites, then the scenery (drawn a little bigger).
        private const float Pixel = .75f;
        private const float ScenePixel = 1f;
        private const float RowHeight = 10.5f;
        private const float RowWidth = 134f;
        private const float RowX = 1f;
        private const float GroundTop = -38f;
        private const float MouseSpeed = 9f;

        private readonly TvMenuSkin skin;
        private readonly Slider[] sliders;
        private readonly TextMeshProUGUI[] volumeLabels;
        private readonly Button back;
        private readonly TextMeshProUGUI backLabel;
        private readonly TextMeshProUGUI languageLabel;
        private readonly Button[] languageArrows;
        private readonly Image[] rowFrames = new Image[4];
        private Image backFrame;
        private Image[] arrowFrames;
        private TextMeshProUGUI title;
        private TextMeshProUGUI titleShadow;
        private RectTransform mouse;

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
            BuildScenery(root);
            BuildTitle(root, title);
            BuildBack();

            var icons = new[] { skin.general, skin.music, skin.effects, skin.language };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = RowY(i);
                var row = AddImage(root, "Row", skin.panel, Color.white, new Vector2(RowX, y), new Vector2(RowWidth, RowHeight));
                Sliced(row);
                var tile = AddImage(root, "Icon tile", skin.tile, Color.white, new Vector2(RowX - RowWidth * .5f + 1f, y), new Vector2(12f, 12f));
                Sliced(tile);
                AddImage(tile.transform, "Icon", icons[i], Color.white, Vector2.zero, Size(icons[i], .6f));
            }
            // Over the rows and their icon tiles, which stick out on the left.
            for (int i = 0; i < icons.Length; i++)
                rowFrames[i] = Frame(root, new Vector2(RowX - 2.5f, RowY(i)), new Vector2(RowWidth + 5f, 12f));

            for (int i = 0; i < sliders.Length; i++)
            {
                float y = RowY(i);
                // Name on the left, value right-aligned at the row's end (see VolumeText).
                // The scene's labels come before the rows made here: bring them in front.
                volumeLabels[i].transform.SetAsLastSibling();
                Style(volumeLabels[i], 5.5f, TextColor, TextAlignmentOptions.Left);
                Place(volumeLabels[i].rectTransform, new Vector2(6f, y), new Vector2(112f, 8f));
                StyleSlider(sliders[i], y);
            }

            float languageY = RowY(3);
            languageLabel.transform.SetAsLastSibling();
            Style(languageLabel, 5.5f, TextColor, TextAlignmentOptions.Left);
            Place(languageLabel.rectTransform, new Vector2(-30f, languageY), new Vector2(40f, 8f));
            var well = AddImage(root, "Language well", skin.well, Color.white, new Vector2(22f, languageY), new Vector2(48f, 8f));
            Sliced(well);
            languageValue.transform.SetAsLastSibling();
            Style(languageValue, 5.5f, TextColor, TextAlignmentOptions.Center);
            Place(languageValue.rectTransform, new Vector2(22f, languageY), new Vector2(48f, 8f));
            arrowFrames = new Image[languageArrows.Length];
            for (int i = 0; i < languageArrows.Length; i++)
            {
                var arrow = languageArrows[i];
                arrow.transform.SetAsLastSibling();
                var image = arrow.GetComponent<Image>();
                image.sprite = skin.tile;
                Sliced(image);
                arrow.GetComponentInChildren<TextMeshProUGUI>(true).text = string.Empty;
                Place((RectTransform)arrow.transform, new Vector2(i == 0 ? -8f : 52f, languageY), new Vector2(9f, 9f));
                var icon = i == 0 ? skin.previous : skin.next;
                AddImage(arrow.transform, "Icon", icon, Color.white, Vector2.zero, Size(icon, .6f));
                arrowFrames[i] = Frame(arrow.transform, Vector2.zero, new Vector2(9f, 9f));
            }

            Style(instruction, 4f, Hint, TextAlignmentOptions.Center);
            Place(instruction.rectTransform, new Vector2(0f, -25f), new Vector2(RowWidth, 7f));
            LinkNavigation();
        }

        // Called every frame while the TV shows the settings.
        public void Tick(GameObject selected)
        {
            for (int i = 0; i < sliders.Length; i++)
                rowFrames[i].enabled = selected == sliders[i].gameObject;
            bool language = false;
            for (int i = 0; i < languageArrows.Length; i++)
            {
                bool on = selected == languageArrows[i].gameObject;
                arrowFrames[i].enabled = on;
                languageArrows[i].targetGraphic.color = on ? Color.white : ButtonIdle;
                language |= on;
            }
            rowFrames[rowFrames.Length - 1].enabled = language;
            backFrame.enabled = selected == back.gameObject;
            back.targetGraphic.color = backFrame.enabled ? Color.white : ButtonIdle;

            // The shadow copies the title, which the scene localizes.
            if (titleShadow.text != title.text) titleShadow.text = title.text;

            // The mouse runs back and forth along the ground, facing where it goes.
            float run = Mathf.PingPong(Time.unscaledTime * MouseSpeed, 150f);
            bool right = Mathf.Repeat(Time.unscaledTime * MouseSpeed, 300f) < 150f;
            mouse.anchoredPosition = new Vector2(-75f + run, mouse.anchoredPosition.y);
            mouse.localScale = new Vector3(right ? 1f : -1f, 1f, 1f);
        }

        // Name on the left and the value right-aligned on the same line.
        public static string VolumeText(string label, float value) =>
            label + "<line-height=0>\n<align=right>" + Mathf.RoundToInt(value * 100f) + "%";

        private static float RowY(int row) => 22f - row * 12f;

        private void BuildScenery(RectTransform root)
        {
            // Behind everything else, back to front: clouds, hills, bush, ground and the mouse on it.
            int order = 0;
            Behind(AddImage(root, "Cloud", skin.cloud, Color.white, new Vector2(-80f, 22f), Size(skin.cloud, ScenePixel)), ref order);
            Behind(AddImage(root, "Cloud", skin.cloud, Color.white, new Vector2(10f, 44f), Size(skin.cloud, Pixel)), ref order);
            Behind(AddImage(root, "Cloud", skin.cloud, Color.white, new Vector2(80f, 8f), Size(skin.cloud, Pixel)), ref order);
            Behind(OnGround(root, "Hill", skin.hill, -62f, ScenePixel), ref order);
            Behind(OnGround(root, "Hill", skin.hill, 52f, .8f), ref order);
            Behind(OnGround(root, "Bush", skin.bush, 12f, Pixel), ref order);

            var ground = AddImage(root, "Ground", skin.ground, Color.white, Vector2.zero, Vector2.zero);
            ground.type = Image.Type.Tiled;
            ground.pixelsPerUnitMultiplier = 1f / ScenePixel;
            var groundRect = ground.rectTransform;
            groundRect.anchorMin = Vector2.zero;
            groundRect.anchorMax = new Vector2(1f, 0f);
            groundRect.pivot = new Vector2(.5f, 0f);
            groundRect.anchoredPosition = Vector2.zero;
            groundRect.sizeDelta = new Vector2(0f, root.rect.height * .5f + GroundTop);
            Behind(ground, ref order);

            var runner = OnGround(root, "Mouse", skin.mouse, -40f, Pixel);
            Behind(runner, ref order);
            mouse = runner.rectTransform;
        }

        private Image OnGround(RectTransform root, string name, Sprite sprite, float x, float pixel)
        {
            var size = Size(sprite, pixel);
            return AddImage(root, name, sprite, Color.white, new Vector2(x, GroundTop + size.y * .5f - .5f), size);
        }

        private static void Behind(Image image, ref int order) => image.transform.SetSiblingIndex(order++);

        private void BuildTitle(RectTransform root, TextMeshProUGUI sceneTitle)
        {
            title = sceneTitle;
            var position = new Vector2(-30f, 39f);
            var size = new Vector2(100f, 16f);
            titleShadow = new GameObject("Title shadow", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            titleShadow.transform.SetParent(root, false);
            Style(titleShadow, 13f, Ink, TextAlignmentOptions.Left);
            titleShadow.text = title.text;
            Place(titleShadow.rectTransform, position + new Vector2(1.2f, -1.2f), size);
            title.transform.SetAsLastSibling();
            Style(title, 13f, TextColor, TextAlignmentOptions.Left);
            Place(title.rectTransform, position, size);
        }

        // VOLVER on an orange button with a "go back" arrow, top right.
        private void BuildBack()
        {
            var backRect = (RectTransform)back.transform;
            back.transform.SetAsLastSibling();
            var image = back.GetComponent<Image>();
            image.sprite = skin.panel;
            Sliced(image);
            var size = new Vector2(40f, 11f);
            Place(backRect, new Vector2(56f, 39f), size);
            backRect.localEulerAngles = Vector3.zero;
            AddImage(backRect, "Icon", skin.back, Color.white, new Vector2(-13f, 0f), Size(skin.back, .6f));
            Style(backLabel, 5.5f, TextColor, TextAlignmentOptions.Center);
            Place(backLabel.rectTransform, new Vector2(4f, 0f), new Vector2(28f, 8f));
            backFrame = Frame(backRect, Vector2.zero, size);
        }

        // Black track with a dark outline, red fill and a white knob.
        private void StyleSlider(Slider slider, float y)
        {
            var track = (RectTransform)slider.transform;
            track.SetAsLastSibling();
            Place(track, new Vector2(12f, y), new Vector2(60f, 4.5f));
            var trackImage = track.GetComponent<Image>();
            trackImage.sprite = skin.track;
            trackImage.color = Color.white;
            Sliced(trackImage);

            if (slider.fillRect == null)
            {
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                fill.transform.SetParent(track, false);
                fill.transform.SetSiblingIndex(0);
                fill.raycastTarget = false;
                slider.fillRect = fill.rectTransform;
            }
            // Inside the track's one-pixel outline.
            slider.fillRect.offsetMin = new Vector2(Pixel, Pixel);
            slider.fillRect.offsetMax = new Vector2(-Pixel, -Pixel);
            var fillImage = slider.fillRect.GetComponent<Image>();
            fillImage.sprite = skin.fill;
            fillImage.color = Color.white;
            Sliced(fillImage);

            var knob = slider.handleRect.GetComponent<Image>();
            knob.enabled = true;
            knob.sprite = skin.knob;
            knob.type = Image.Type.Simple;
            knob.color = Color.white;
            // The handle's height follows the track: 4.5 + 3 makes a 7 x 10 pixel knob.
            slider.handleRect.sizeDelta = new Vector2(skin.knob.rect.width * Pixel, 3f);
            slider.handleRect.anchoredPosition = Vector2.zero;
        }

        // Yellow pixel frame around a row or button, shown while it is selected.
        private Image Frame(Transform parent, Vector2 position, Vector2 size)
        {
            var frame = AddImage(parent, "Selected frame", skin.select, Color.white, position, size + new Vector2(2f, 2f));
            Sliced(frame);
            frame.enabled = false;
            return frame;
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

        private static Vector2 Size(Sprite sprite, float pixel) => sprite.rect.size * pixel;

        // 9-sliced at the interface pixel size, so every border is drawn with the same chunky pixels.
        private static void Sliced(Image image)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / Pixel;
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, position, size);
            return image;
        }

        private void Style(TextMeshProUGUI text, float size, Color color, TextAlignmentOptions alignment)
        {
            text.font = skin.Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
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
