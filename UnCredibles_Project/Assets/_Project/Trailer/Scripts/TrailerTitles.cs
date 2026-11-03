using TMPro;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Trailer
{
    // Previs title cards on white: shot 03 (the challenge question) and shot 18 (game title, slogan and
    // the four heroes, then fade to black). One take: 0-2 s is shot 03, 2-5 s is shot 18.
    public sealed class TrailerTitles : MonoBehaviour
    {
        public TMP_FontAsset font;
        public PlayerFigureSet figures;
        public Texture studioLogo;
        public string challenge = "¿Tienes lo necesario\npara ser un héroe?";
        public string title = "UN-CREDIBLES";
        public string slogan = "Héroes de nombre. Desastres de profesión.";
        public float logoStart = 2f, length = 5f;

        private static readonly Color Ink = new(.106f, .133f, .2f);
        private static readonly Color Gold = new(1f, .76f, .1f);
        private RectTransform challengeText, titleText, sloganText, logo;
        private CanvasGroup sloganGroup, logoGroup;
        private Image fade;
        private GameObject heroes;
        private float startTime;

        private void Start()
        {
            var output = Camera.main;
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = Color.white;
            output.transform.SetPositionAndRotation(new Vector3(0f, 1.25f, -11f), Quaternion.Euler(0f, 0f, 0f));
            output.fieldOfView = 28f;

            heroes = new GameObject("Trailer heroes");
            for (int i = 0; i < 4; i++)
            {
                var figure = Instantiate(figures.For(i), heroes.transform);
                figure.transform.SetPositionAndRotation(new Vector3(-1.95f + i * 1.3f, -1.35f, 0f), Quaternion.Euler(0f, 180f, 0f));
            }
            heroes.SetActive(false);

            var canvas = new GameObject("Trailer titles", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            challengeText = Text(canvas.transform, "Challenge", challenge, 104f, Ink, new Vector2(0f, 0f), new Vector2(1700f, 400f), false);
            titleText = Text(canvas.transform, "Title", title, 190f, Gold, new Vector2(0f, 330f), new Vector2(1800f, 260f), true);
            sloganText = Text(canvas.transform, "Slogan", slogan, 64f, Ink, new Vector2(0f, 175f), new Vector2(1800f, 120f), false);
            sloganGroup = sloganText.gameObject.AddComponent<CanvasGroup>();

            if (studioLogo != null)
            {
                var image = new GameObject("Studio logo", typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(canvas.transform, false);
                image.texture = studioLogo;
                image.color = new Color(.25f, .27f, .32f);
                logo = image.rectTransform;
                logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(1f, 0f);
                logo.anchoredPosition = new Vector2(-50f, 36f);
                logo.sizeDelta = new Vector2(260f, 260f * studioLogo.height / studioLogo.width);
                logoGroup = image.gameObject.AddComponent<CanvasGroup>();
            }

            fade = new GameObject("Fade", typeof(Image)).GetComponent<Image>();
            fade.transform.SetParent(canvas.transform, false);
            fade.color = new Color(0f, 0f, 0f, 0f);
            var fadeRect = fade.rectTransform;
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;

            startTime = Time.time;
            Update();
        }

        private RectTransform Text(Transform parent, string objectName, string value, float size, Color colour, Vector2 position, Vector2 box, bool outline)
        {
            var text = new GameObject(objectName, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (outline)
            {
                text.fontMaterial.EnableKeyword("OUTLINE_ON");
                text.outlineColor = Ink;
                text.outlineWidth = .22f;
            }
            var rect = text.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            return rect;
        }

        // Comic "pop": overshoots then settles to full size.
        private static float Pop(float t, float duration = .35f)
        {
            if (t <= 0f) return 0f;
            float k = Mathf.Clamp01(t / duration);
            return 1f + Mathf.Sin(k * Mathf.PI) * .18f * (1f - k) - (1f - k) * (1f - k) * (1f - k);
        }

        // Seeks the cards to a given time (to review framing).
        public void Seek(float seconds) => startTime = Time.time - seconds;

        private void Update()
        {
            if (challengeText == null) return;
            float t = Time.time - startTime;
            bool logoPart = t >= logoStart;
            challengeText.gameObject.SetActive(!logoPart);
            challengeText.localScale = Vector3.one * (Pop(t) * (1f + .04f * t));

            heroes.SetActive(logoPart);
            titleText.gameObject.SetActive(logoPart);
            sloganText.gameObject.SetActive(logoPart);
            float l = t - logoStart;
            titleText.localScale = Vector3.one * Pop(l - .1f, .45f);
            sloganGroup.alpha = Mathf.Clamp01((l - .7f) / .3f);
            if (logo != null)
            {
                logo.gameObject.SetActive(logoPart);
                logoGroup.alpha = Mathf.Clamp01((l - 1.2f) / .3f);
            }
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - (length - .5f)) / .45f));
        }
    }
}
