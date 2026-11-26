using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Turbo charge as a round dial beside the mower, facing the camera. Using the turbo drains it
    // quickly, then it fills up clockwise; once full it turns yellow, pops and makes a sound.
    // Built in code (world-space canvas with a radial filled image); the mower feeds it every frame.
    public sealed class MowTurboDial : MonoBehaviour
    {
        private const float Diameter = .85f;      // world units
        private const float PixelsPerUnit = 100f;
        private const float Side = 1.25f;          // to the right of the mower, on screen
        private const float Height = .9f;
        private const float DrainPerSecond = 4f;   // fill lost per second when the turbo is used
        private const float PopSeconds = .3f;

        private static readonly Color RimColor = new Color(0f, 0f, 0f, .8f);
        private static readonly Color BackColor = new Color(.15f, .15f, .15f, .85f);
        private static readonly Color ChargingColor = new Color(.95f, .55f, .1f);
        private static readonly Color ReadyColor = new Color(1f, .9f, .15f);
        private static Sprite disc;

        private Image fill;
        private Transform owner;
        private Camera viewCamera;
        private float shown = 1f;
        private bool full = true;
        private float popTimer;

        public float Shown => shown;

        public static MowTurboDial Create(Transform owner)
        {
            // Child of the mower root (not of its Visual, which rumbles and spins); placed in world space.
            var go = new GameObject("TurboDial", typeof(RectTransform));
            go.transform.SetParent(owner, false);
            var dial = go.AddComponent<MowTurboDial>();
            dial.owner = owner;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(Diameter, Diameter) * PixelsPerUnit;
            rect.localScale = Vector3.one / PixelsPerUnit;

            dial.Disc("Rim", 1.18f, RimColor);
            dial.Disc("Back", 1f, BackColor);
            dial.fill = dial.Disc("Charge", .86f, ReadyColor);
            dial.fill.type = Image.Type.Filled;
            dial.fill.fillMethod = Image.FillMethod.Radial360;
            dial.fill.fillOrigin = (int)Image.Origin360.Top;
            dial.fill.fillClockwise = true;
            return dial;
        }

        // charge: 0 just used, 1 ready. Called every frame by the mower.
        public void Show(float charge, float deltaTime)
        {
            charge = Mathf.Clamp01(charge);
            shown = charge < shown ? Mathf.Max(charge, shown - DrainPerSecond * deltaTime) : charge;
            bool nowFull = shown >= 1f;
            if (nowFull && !full)
            {
                popTimer = PopSeconds;
                GameAudio.Play(GameSfx.UiReady);
            }
            full = nowFull;
            if (popTimer > 0f) popTimer -= deltaTime;
            fill.fillAmount = shown;
            fill.color = full ? Color.Lerp(ReadyColor, Color.white, Mathf.Abs(Mathf.Sin(Time.time * 5f)) * .3f) : ChargingColor;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            if (viewCamera == null) return;
            var view = viewCamera.transform;
            transform.SetPositionAndRotation(owner.position + view.right * Side + Vector3.up * Height, view.rotation);
            float pop = 1f + popTimer / PopSeconds * .4f;
            transform.localScale = Vector3.one * (pop / PixelsPerUnit);
        }

        private Image Disc(string discName, float size, Color color)
        {
            if (disc == null) disc = CreateDisc(64);
            var go = new GameObject(discName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.sizeDelta = new Vector2(Diameter, Diameter) * (PixelsPerUnit * size);
            var image = go.AddComponent<Image>();
            image.sprite = disc;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // White disc with a soft edge.
        private static Sprite CreateDisc(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float radius = size * .5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + .5f - radius, y + .5f - radius).magnitude;
                byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        }
    }
}
