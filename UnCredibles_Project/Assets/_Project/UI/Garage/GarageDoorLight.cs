using UnityEngine;

namespace UnCredibles.UI.Garage
{
    // Daylight pouring in through the open garage door (the wall behind it has no hole, so this hides it):
    // an overexposed opening, white in the middle and warm at the edges; a halo spilling over the bricks
    // around the frame; a bright patch on the floor; dust motes drifting in the beam; and a warm light that
    // throws the players' shadows back into the garage. Sprites only: their shader is always in the build.
    public sealed class GarageDoorLight : MonoBehaviour
    {
        private static readonly Color Warm = new Color(1f, .86f, .6f);
        private const float LightIntensity = 14f;
        private const float HaloAlpha = .85f;
        private const float FloorAlpha = .7f;
        private const int MoteCount = 16;
        private const int TextureSize = 64;

        private SpriteRenderer opening;
        private SpriteRenderer halo;
        private SpriteRenderer floor;
        private SpriteRenderer[] motes;
        private Vector3[] moteHomes;
        private Light sun;
        private readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();
        private float amount;

        // `door` is the box the door panel fills; `planeX` the X where the opening is drawn (behind the
        // panel, in front of the wall). The garage interior is towards +X.
        public static GarageDoorLight Build(Bounds door, float planeX)
        {
            var light = new GameObject("Garage door light").AddComponent<GarageDoorLight>();
            light.Create(door, planeX);
            return light;
        }

        private void Create(Bounds door, float planeX)
        {
            var facing = Quaternion.Euler(0f, 90f, 0f); // sprite plane across X, seen from inside
            opening = Quad("Opening", Texture((u, v) =>
            {
                float d = Mathf.Clamp01(Ellipse(u, v) * 1.1f);
                return Color.Lerp(Color.white, Warm, d * d);
            }));
            opening.transform.SetPositionAndRotation(new Vector3(planeX, door.center.y, door.center.z), facing);
            opening.transform.localScale = new Vector3(door.size.z, door.size.y, 1f);

            halo = Quad("Halo", Texture((u, v) =>
            {
                float d = Mathf.Clamp01(Ellipse(u, v));
                return new Color(1f, 1f, 1f, (1f - d) * (1f - d));
            }));
            halo.transform.SetPositionAndRotation(new Vector3(planeX + .3f, door.center.y, door.center.z), facing);
            halo.transform.localScale = new Vector3(door.size.z * 2.2f, door.size.y * 1.9f, 1f);

            floor = Quad("Floor patch", Texture((u, v) =>
            {
                float side = 1f - Mathf.Pow(Mathf.Abs(v * 2f - 1f), 4f);
                float fade = 1f - u;
                return new Color(1f, 1f, 1f, fade * fade * side);
            }));
            const float length = 4.5f;
            // Lies on the floor; u runs into the garage, away from the door.
            floor.transform.SetPositionAndRotation(new Vector3(planeX + length * .5f, door.min.y + .015f, door.center.z),
                Quaternion.Euler(90f, 0f, 0f));
            floor.transform.localScale = new Vector3(length, door.size.z * 1.1f, 1f);

            var dot = Texture((u, v) => Color.white, 2);
            motes = new SpriteRenderer[MoteCount];
            moteHomes = new Vector3[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                motes[i] = Quad("Mote", dot);
                motes[i].transform.rotation = facing;
                motes[i].transform.localScale = Vector3.one * Random.Range(.02f, .045f);
                moteHomes[i] = new Vector3(planeX + Random.Range(.3f, 3f), door.min.y + Random.Range(.2f, door.size.y),
                    door.center.z + Random.Range(-.45f, .45f) * door.size.z);
            }

            sun = new GameObject("Door sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Spot;
            sun.color = Warm;
            sun.range = 14f;
            sun.spotAngle = 95f;
            sun.innerSpotAngle = 40f;
            sun.shadows = LightShadows.Soft;
            // From just outside, slightly above the opening, down into the garage.
            var from = new Vector3(planeX - .8f, door.max.y + .2f, door.center.z);
            sun.transform.SetPositionAndRotation(from, Quaternion.LookRotation(new Vector3(planeX + 4f, door.min.y, door.center.z) - from));
            Set(0f);
        }

        public void Set(float value)
        {
            amount = Mathf.Clamp01(value);
            bool on = amount > 0f;
            opening.enabled = halo.enabled = floor.enabled = sun.enabled = on;
            opening.color = new Color(1f, 1f, 1f, amount);
            halo.color = new Color(Warm.r, Warm.g, Warm.b, HaloAlpha * amount);
            floor.color = new Color(Warm.r, Warm.g, Warm.b, FloorAlpha * amount);
            sun.intensity = LightIntensity * amount;
            foreach (var mote in motes) mote.enabled = on;
        }

        private void Update()
        {
            if (amount <= 0f) return;
            float t = Time.time;
            for (int i = 0; i < motes.Length; i++)
            {
                // Slow rise and sway, wrapping round; twinkling as they turn in the light.
                var home = moteHomes[i];
                float rise = Mathf.Repeat(t * .12f + i * .37f, 1f) - .5f;
                motes[i].transform.position = home + new Vector3(Mathf.Sin(t * .7f + i) * .15f, rise * .8f, Mathf.Cos(t * .5f + i * 2f) * .1f);
                float twinkle = .45f + .55f * Mathf.Abs(Mathf.Sin(t * 2.3f + i * 1.7f));
                motes[i].color = new Color(1f, .95f, .8f, amount * twinkle * (1f - Mathf.Abs(rise) * 2f));
            }
        }

        private void OnDestroy()
        {
            foreach (var item in owned) Destroy(item);
        }

        private SpriteRenderer Quad(string name, Sprite sprite)
        {
            var quad = new GameObject(name).AddComponent<SpriteRenderer>();
            quad.transform.SetParent(transform, false);
            quad.sprite = sprite;
            return quad;
        }

        // A sprite one unit wide, painted per texel from (u, v) in 0..1.
        private Sprite Texture(System.Func<float, float, Color> paint, int size = TextureSize)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = paint((x + .5f) / size, (y + .5f) / size);
            texture.SetPixels(pixels);
            texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        // 0 in the middle, 1 at the edge of the inscribed ellipse.
        private static float Ellipse(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            return Mathf.Sqrt(x * x + y * y);
        }
    }
}
