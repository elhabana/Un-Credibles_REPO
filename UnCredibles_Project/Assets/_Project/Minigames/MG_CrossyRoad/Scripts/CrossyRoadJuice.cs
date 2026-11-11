using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Pure feedback, no gameplay: camera shake, hit-stop and dizzy stars when run over,
    // a squash pop when picking up or delivering a grandma, and a soft red edge plus a closer
    // camera in the final rush.
    // Reads only the avatars' state, so it looks the same on the host and on online clients.
    // Added and ticked by CrossyRoadController.
    public sealed class CrossyRoadJuice : MonoBehaviour
    {
        private const float StarLife = 1.1f;
        private const float StarSize = 0.22f;
        private static readonly Color StarColor = new Color(1f, 0.88f, 0.2f);
        private const float HitStopScale = 0.05f;
        private const float HitStopSeconds = 0.07f;
        private const float ShakeAmplitude = 0.45f;
        private const float RushZoom = 0.07f;
        // Final rush red edge: a soft hint at the corners, barely pulsing.
        private const float VignetteAlpha = 0.18f;
        private const float VignettePulse = 0.05f;

        private struct Star
        {
            public Vector3 Center;
            public float Age, Angle;
        }

        private struct Watch
        {
            public bool Alive, Carrying;
            public byte TimesShoved;
            public Vector3 Position, BaseScale;
            public float Punch;
        }

        // Impact sparks of a shove: little cubes bursting out and falling.
        private struct Spark
        {
            public Vector3 Position, Velocity;
            public float Age;
        }

        private const float SparkLife = 0.4f;
        private const float SparkSize = 0.14f;
        private const int SparksPerShove = 10;
        private const float ShoveStopSeconds = 0.05f;

        private readonly List<Star> stars = new List<Star>();
        private readonly List<Spark> sparks = new List<Spark>();
        private Watch[] watches;
        private readonly TintedMaterials tints = new TintedMaterials();
        private Mesh cube;
        private Material material;
        private Transform cameraTransform;
        private Camera viewCamera;
        private Vector3 cameraBase;
        private float baseFov, baseOrtho;
        private float trauma;
        private float hitStopEnd;
        private float rush;
        private RawImage vignette;
        private Texture2D vignetteTexture;

        public void Setup(CrossyRoadBoard gameBoard)
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            // Same lit material as the board tiles, tinted yellow.
            material = gameBoard.GoalTile != null ? tints.Get(gameBoard.GoalTile.sharedMaterial, StarColor) : null;
            viewCamera = Camera.main;
            if (viewCamera != null)
            {
                cameraTransform = viewCamera.transform;
                cameraBase = cameraTransform.localPosition;
                baseFov = viewCamera.fieldOfView;
                baseOrtho = viewCamera.orthographicSize;
            }
            BuildVignette();
        }

        public void Tick(IReadOnlyList<CrossyRoadPlayer> avatars, bool finalRush)
        {
            float deltaTime = Time.deltaTime; // the stars freeze during the hit-stop
            float realDelta = Time.unscaledDeltaTime; // the shake keeps going
            EndHitStop(false);

            if (watches == null || watches.Length != avatars.Count) Reset(avatars);
            for (int i = 0; i < avatars.Count; i++) Observe(avatars[i], ref watches[i], deltaTime);

            TickStars(deltaTime);
            TickSparks(deltaTime);
            TickCamera(realDelta, finalRush);
        }

        private void Reset(IReadOnlyList<CrossyRoadPlayer> avatars)
        {
            watches = new Watch[avatars.Count];
            for (int i = 0; i < avatars.Count; i++)
                watches[i] = new Watch
                {
                    Alive = avatars[i].IsAlive, Carrying = avatars[i].IsCarrying, TimesShoved = avatars[i].TimesShoved,
                    Position = avatars[i].Position, BaseScale = avatars[i].transform.localScale,
                };
        }

        private void Observe(CrossyRoadPlayer avatar, ref Watch watch, float deltaTime)
        {
            if (avatar.TimesShoved != watch.TimesShoved)
            {
                watch.TimesShoved = avatar.TimesShoved;
                Shoved(avatar.Position);
                watch.Punch = 1f;
            }

            if (watch.Alive && !avatar.IsAlive) Hit(avatar.Position);
            else if (avatar.IsAlive && !watch.Carrying && avatar.IsCarrying) watch.Punch = 1f;
            else if (avatar.IsAlive && watch.Carrying && !avatar.IsCarrying)
            {
                GameAudio.Play(GameSfx.Deliver); // alive and no longer carrying: she was delivered
                trauma = Mathf.Max(trauma, 0.3f);
                watch.Punch = 1f;
            }

            // Squash-and-stretch pop on pickup and delivery.
            watch.Punch = Mathf.Max(0f, watch.Punch - deltaTime * 5f);
            float wave = Mathf.Sin(watch.Punch * Mathf.PI) * 0.3f;
            avatar.transform.localScale = Vector3.Scale(watch.BaseScale, new Vector3(1f + wave, 1f - wave * 0.6f, 1f + wave));

            watch.Alive = avatar.IsAlive;
            watch.Carrying = avatar.IsCarrying;
            watch.Position = avatar.Position;
        }

        private void Hit(Vector3 position)
        {
            GameAudio.Play(GameSfx.RunOver); // just the thud: a long "wah-wah" on every hit got tiring with four players
            trauma = 1f;
            if (Mathf.Approximately(Time.timeScale, 1f)) // never fight the pause menu
            {
                Time.timeScale = HitStopScale;
                hitStopEnd = Time.unscaledTime + HitStopSeconds;
            }
            for (int i = 0; i < 5; i++)
                stars.Add(new Star { Center = position + Vector3.up * 1.1f, Angle = i * Mathf.PI * 0.4f });
        }

        // Shove: a small shake, a tiny freeze and a burst of sparks at the body.
        private void Shoved(Vector3 position)
        {
            GameAudio.Play(GameSfx.HitRival);
            trauma = Mathf.Max(trauma, 0.5f);
            if (hitStopEnd <= 0f && Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = HitStopScale;
                hitStopEnd = Time.unscaledTime + ShoveStopSeconds;
            }
            var center = position + Vector3.up * 0.8f;
            for (int i = 0; i < SparksPerShove; i++)
            {
                float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / SparksPerShove;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(4f, 7f);
                sparks.Add(new Spark { Position = center, Velocity = outward + Vector3.up * Random.Range(2f, 5f) });
            }
        }

        private void TickSparks(float deltaTime)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var spark = sparks[i];
                spark.Age += deltaTime;
                if (spark.Age >= SparkLife) { sparks.RemoveAt(i); continue; }
                spark.Velocity += Vector3.down * (20f * deltaTime);
                spark.Position += spark.Velocity * deltaTime;
                sparks[i] = spark;

                if (material == null) continue;
                float size = SparkSize * (1f - spark.Age / SparkLife);
                Graphics.DrawMesh(cube, Matrix4x4.TRS(spark.Position, Quaternion.Euler(spark.Age * 900f, spark.Age * 600f, 0f), Vector3.one * size), material, 0);
            }
        }

        // Dizzy stars circling the head of whoever was run over.
        private void TickStars(float deltaTime)
        {
            for (int i = stars.Count - 1; i >= 0; i--)
            {
                var star = stars[i];
                star.Age += deltaTime;
                if (star.Age >= StarLife) { stars.RemoveAt(i); continue; }
                star.Angle += deltaTime * 7f;
                stars[i] = star;

                if (material == null) continue;
                var position = star.Center + new Vector3(Mathf.Cos(star.Angle) * 0.6f, Mathf.Sin(star.Angle * 2f) * 0.1f, Mathf.Sin(star.Angle) * 0.6f);
                var rotation = Quaternion.Euler(0f, star.Angle * Mathf.Rad2Deg, 45f);
                float size = StarSize * Mathf.Clamp01((StarLife - star.Age) * 4f); // shrink away at the end
                Graphics.DrawMesh(cube, Matrix4x4.TRS(position, rotation, Vector3.one * size), material, 0);
            }
        }

        private void TickCamera(float realDelta, bool finalRush)
        {
            rush = Mathf.MoveTowards(rush, finalRush ? 1f : 0f, realDelta * 2f);
            if (vignette != null)
            {
                var color = vignette.color;
                color.a = rush * (VignetteAlpha + VignettePulse * Mathf.Sin(Time.unscaledTime * 4f));
                vignette.color = color;
                vignette.enabled = color.a > 0.001f;
            }

            if (viewCamera == null) return;
            trauma = Mathf.Max(0f, trauma - realDelta * 2.5f);
            float shake = trauma * trauma * ShakeAmplitude;
            cameraTransform.localPosition = cameraBase + new Vector3(
                (Mathf.PerlinNoise(Time.unscaledTime * 30f, 0f) - 0.5f) * 2f * shake,
                (Mathf.PerlinNoise(0f, Time.unscaledTime * 30f) - 0.5f) * 2f * shake, 0f);
            float zoom = 1f - RushZoom * rush;
            if (viewCamera.orthographic) viewCamera.orthographicSize = baseOrtho * zoom;
            else viewCamera.fieldOfView = baseFov * zoom;
        }

        private void EndHitStop(bool force)
        {
            if (hitStopEnd <= 0f || (!force && Time.unscaledTime < hitStopEnd)) return;
            hitStopEnd = 0f;
            if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
        }

        // Red edges built in code: a radial alpha texture on a full-screen overlay, under the HUD.
        private void BuildVignette()
        {
            const int size = 128;
            vignetteTexture = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.05f, 1.45f, Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
                }
            vignetteTexture.SetPixels32(pixels);
            vignetteTexture.Apply();

            var canvasObject = new GameObject("Final Rush Vignette", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;
            vignette = new GameObject("Edge", typeof(RawImage)).GetComponent<RawImage>();
            vignette.transform.SetParent(canvasObject.transform, false);
            vignette.texture = vignetteTexture;
            vignette.raycastTarget = false;
            vignette.color = new Color(1f, 0.1f, 0.05f, 0f);
            var rect = vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            vignette.enabled = false;
        }

        private void OnDestroy()
        {
            EndHitStop(true);
            if (vignetteTexture != null) Destroy(vignetteTexture);
            tints.DestroyAll();
        }
    }
}
