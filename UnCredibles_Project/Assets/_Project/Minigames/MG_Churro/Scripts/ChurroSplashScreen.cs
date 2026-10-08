using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames.Churro
{
    // Water splashed onto the "camera" by a cannonball: soft blue-white blobs cover part of the
    // screen, slide down slowly and fade away (like the squid ink in kart games). Only visual and
    // local to each machine. Builds its own overlay canvas; the blob sprite is generated in code.
    public sealed class ChurroSplashScreen : MonoBehaviour
    {
        private const int BlobCount = 16;
        private const int SpriteSize = 128;

        [SerializeField] private Color waterColor = new Color(0.72f, 0.88f, 1f, 0.82f);
        [SerializeField, Min(0.5f), Tooltip("Seconds the splash stays on screen in total.")] private float seconds = 3.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Part of the time it stays fully visible before fading.")] private float holdPart = 0.45f;
        [SerializeField, Tooltip("Smallest and largest blob size, in reference pixels (1920x1080).")]
        private Vector2 blobSize = new Vector2(220f, 520f);

        private RectTransform[] blobs;
        private Image[] images;
        private Vector2[] slide;
        private float[] delay;
        private Texture2D texture;
        private Sprite sprite;
        private float age = float.MaxValue;

        private void Awake() => Build();

        public void Splash()
        {
            age = 0f;
            for (int i = 0; i < blobs.Length; i++)
            {
                float size = Random.Range(blobSize.x, blobSize.y);
                blobs[i].sizeDelta = new Vector2(size * Random.Range(0.85f, 1.2f), size);
                blobs[i].anchoredPosition = new Vector2(Random.Range(-880f, 880f), Random.Range(-460f, 460f));
                blobs[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                slide[i] = new Vector2(Random.Range(-10f, 10f), -Random.Range(25f, 70f));
                delay[i] = Random.Range(0f, 0.18f);
                images[i].enabled = true;
            }
        }

        private void Update()
        {
            if (age > seconds) return;
            age += Time.deltaTime;
            float fadeStart = seconds * holdPart;
            float fade = age < fadeStart ? 1f : 1f - Mathf.Clamp01((age - fadeStart) / (seconds - fadeStart));

            for (int i = 0; i < blobs.Length; i++)
            {
                float local = age - delay[i];
                if (local < 0f)
                {
                    images[i].enabled = false;
                    continue;
                }
                images[i].enabled = fade > 0f;
                // Splat in with a little overshoot, then drip down.
                float pop = local < 0.12f ? Mathf.Lerp(0.2f, 1.15f, local / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((local - 0.12f) * 5f));
                blobs[i].localScale = new Vector3(pop, pop, 1f);
                blobs[i].anchoredPosition += slide[i] * Time.deltaTime;
                var color = waterColor;
                color.a *= fade;
                images[i].color = color;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1; // under the HUD texts, over the 3D view
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            sprite = CreateBlobSprite();
            blobs = new RectTransform[BlobCount];
            images = new Image[BlobCount];
            slide = new Vector2[BlobCount];
            delay = new float[BlobCount];
            for (int i = 0; i < BlobCount; i++)
            {
                var go = new GameObject($"Splash_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                blobs[i] = (RectTransform)go.transform;
                images[i] = go.GetComponent<Image>();
                images[i].sprite = sprite;
                images[i].raycastTarget = false;
                images[i].enabled = false;
            }
        }

        // A soft-edged, slightly lumpy round blob with a lighter "shine" spot.
        private Sprite CreateBlobSprite()
        {
            texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false) { name = "SplashBlob", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[SpriteSize * SpriteSize];
            float half = SpriteSize * 0.5f;
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float angle = Mathf.Atan2(dy, dx);
                    float radius = 0.82f + Mathf.Sin(angle * 5f) * 0.06f + Mathf.Sin(angle * 9f + 1.3f) * 0.03f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((radius - distance) / 0.12f);
                    float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.3f, 0.35f)) / 0.3f) * 0.6f;
                    byte light = (byte)(Mathf.Lerp(0.82f, 1f, shine) * 255f);
                    pixels[y * SpriteSize + x] = new Color32(light, light, light, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
