using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Look of the oil truck: black tank with a hazard stripe, oil dripping from the back and dark
    // smoke from the exhaust, so players see from far away which vehicle will leave oil.
    // Leak() makes a big gush the moment a slick is dropped. Animated by CrossyRoadTraffic.
    public sealed class CrossyRoadOilTruck : MonoBehaviour
    {
        private const float DripCycle = 0.55f;
        private const float SmokeCycle = 1.1f;

        [SerializeField, Tooltip("Child that cancels the truck scale so the effects keep their real size.")]
        private Transform effects;
        [SerializeField] private Transform[] drips = new Transform[0];
        [SerializeField] private Transform[] smoke = new Transform[0];
        [SerializeField, Min(0.01f)] private float dripSize = 0.13f;
        [SerializeField, Min(0.01f)] private float smokeSize = 0.5f;

        private float time;
        private float gush;

        public void Leak() => gush = 1f;

        public void Animate(float deltaTime)
        {
            if (effects == null) return;
            time += deltaTime;
            gush = Mathf.Max(0f, gush - deltaTime * 1.5f);

            // The truck is a stretched unit cube; undo that so drips and smoke stay round.
            var scale = transform.lossyScale;
            effects.localScale = new Vector3(1f / Mathf.Max(0.01f, scale.x), 1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));
            float halfLength = scale.x * 0.5f;
            float height = scale.y;

            for (int i = 0; i < drips.Length; i++)
            {
                float t = Mathf.Repeat(time / DripCycle + (float)i / drips.Length, 1f);
                float size = dripSize * (1f + gush * 1.5f) * (1f - t * 0.4f);
                var side = (i % 2 == 0 ? -1f : 1f) * 0.12f;
                drips[i].localPosition = new Vector3(-halfLength * 0.7f + i * 0.12f, height * 0.25f * (1f - t * t), side);
                drips[i].localScale = new Vector3(size, size * 1.4f, size);
            }

            for (int i = 0; i < smoke.Length; i++)
            {
                float t = Mathf.Repeat(time / SmokeCycle + (float)i / smoke.Length, 1f);
                float size = smokeSize * Mathf.Lerp(0.3f, 1f, t) * (t > 0.8f ? (1f - t) / 0.2f : 1f);
                smoke[i].localPosition = new Vector3(halfLength * 0.55f - t * 0.9f, height * 1.25f + t * 1.1f, Mathf.Sin(t * 6f + i) * 0.1f);
                smoke[i].localScale = new Vector3(size, size, size);
            }
        }
    }
}
