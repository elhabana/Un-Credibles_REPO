using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Hit feedback for rams: a shock ring on the ground, a floating "-3" over the victim and a
    // camera shake. Pooled objects built in code; ticked by MowTheLawnController.
    public sealed class MowFx : MonoBehaviour
    {
        private const float RingSeconds = 0.4f;
        private const float TextSeconds = 1.1f;

        [SerializeField, Tooltip("Material of the shock ring.")] private Material ringMaterial;
        [SerializeField] private MowCameraFit cameraFit;
        [SerializeField, Min(0f)] private float ringSize = 4f;
        [SerializeField, Min(0f)] private float shake = 0.35f;
        [SerializeField] private Color lostColor = new Color(1f, 0.25f, 0.2f);

        private sealed class Effect
        {
            public Transform Transform;
            public TMP_Text Text;
            public float Age;
            public Vector3 Origin;
        }

        private readonly List<Effect> rings = new List<Effect>();
        private readonly List<Effect> texts = new List<Effect>();

        public void Ram(Vector3 position, int bagsLost)
        {
            var ring = Take(rings, CreateRing);
            ring.Origin = position + Vector3.up * 0.05f;
            ring.Transform.position = ring.Origin;

            if (bagsLost > 0)
            {
                var text = Take(texts, CreateText);
                text.Origin = position + Vector3.up * 1.6f;
                text.Text.text = $"-{bagsLost}";
            }
            if (cameraFit != null) cameraFit.Shake(shake);
        }

        public void Tick(float deltaTime)
        {
            foreach (var ring in rings)
            {
                if (!ring.Transform.gameObject.activeSelf) continue;
                ring.Age += deltaTime;
                float t = ring.Age / RingSeconds;
                if (t >= 1f)
                {
                    ring.Transform.gameObject.SetActive(false);
                    continue;
                }
                float size = Mathf.Lerp(0.5f, ringSize, 1f - (1f - t) * (1f - t));
                ring.Transform.localScale = new Vector3(size, 0.02f * (1f - t) + 0.005f, size);
            }

            var cameraRotation = cameraFit != null ? cameraFit.transform.rotation : Quaternion.identity;
            foreach (var text in texts)
            {
                if (!text.Transform.gameObject.activeSelf) continue;
                text.Age += deltaTime;
                float t = text.Age / TextSeconds;
                if (t >= 1f)
                {
                    text.Transform.gameObject.SetActive(false);
                    continue;
                }
                text.Transform.position = text.Origin + Vector3.up * (t * 1.2f);
                text.Transform.rotation = cameraRotation;
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.3f, t / 0.15f) : Mathf.Lerp(1.3f, 1f, (t - 0.15f) * 3f);
                text.Transform.localScale = Vector3.one * pop;
                var color = lostColor;
                color.a = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                text.Text.color = color;
            }
        }

        private static Effect Take(List<Effect> pool, System.Func<Effect> create)
        {
            foreach (var effect in pool)
            {
                if (effect.Transform.gameObject.activeSelf) continue;
                effect.Age = 0f;
                effect.Transform.gameObject.SetActive(true);
                return effect;
            }
            var created = create();
            pool.Add(created);
            return created;
        }

        private Effect CreateRing()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "RamRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            var ringRenderer = ring.GetComponent<MeshRenderer>();
            if (ringMaterial != null) ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Effect { Transform = ring.transform };
        }

        private Effect CreateText()
        {
            var go = new GameObject("RamText");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 9f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
            return new Effect { Transform = go.transform, Text = text };
        }
    }
}
