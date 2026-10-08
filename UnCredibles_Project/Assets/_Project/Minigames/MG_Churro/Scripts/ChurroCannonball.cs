using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // A kid running in from outside the pool and jumping in as a cannonball. A pulsing ring on the
    // water warns where he will land; the landing throws water up and splashes the screen.
    // Only visual (it hurts nobody), the same on host and clients. Ticked by ChurroController.
    public sealed class ChurroCannonball : MonoBehaviour
    {
        private enum Phase { Idle, Warning, Flying, Sinking }

        private const int DropCount = 14;
        private const float SinkSeconds = 0.5f;

        [SerializeField, Tooltip("The jumping kid, curled up in a ball.")] private Transform kid;
        [SerializeField, Tooltip("Ring on the water where he will land.")] private Transform marker;
        [SerializeField, Tooltip("Water drops thrown up by the landing.")] private Transform dropPrefab;
        [SerializeField] private ChurroSplashScreen splashScreen;
        [SerializeField, Min(0.1f)] private float jumpHeight = 4f;

        private Transform[] drops;
        private Vector3[] dropVelocity;
        private ChurroSettings settings;
        private Phase phase = Phase.Idle;
        private Vector3 from;
        private Vector3 to;
        private float timer;
        private float dropTime = float.MaxValue;
        private Vector3 markerScale;

        public bool IsBusy => phase != Phase.Idle;

        public void Initialize(ChurroSettings churroSettings)
        {
            settings = churroSettings;
            markerScale = marker.localScale;
            kid.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            drops = new Transform[DropCount];
            dropVelocity = new Vector3[DropCount];
            for (int i = 0; i < DropCount; i++)
            {
                drops[i] = Instantiate(dropPrefab, transform);
                drops[i].gameObject.SetActive(false);
            }
        }

        // Runs from `start` (outside the pool) and lands at `landing` (on the water).
        public void Play(Vector3 start, Vector3 landing)
        {
            from = start;
            to = landing;
            phase = Phase.Warning;
            timer = settings.CannonWarnSeconds;
            kid.gameObject.SetActive(true);
            kid.position = from;
            marker.gameObject.SetActive(true);
            marker.position = new Vector3(to.x, to.y + 0.02f, to.z);
        }

        public void Tick(float deltaTime)
        {
            TickDrops(deltaTime);
            if (phase == Phase.Idle) return;
            timer -= deltaTime;

            switch (phase)
            {
                case Phase.Warning:
                    // Bouncing on the spot, getting ready; the ring pulses faster and faster.
                    kid.position = from + Vector3.up * Mathf.Abs(Mathf.Sin(timer * 14f)) * 0.25f;
                    float urgency = 1f - Mathf.Clamp01(timer / settings.CannonWarnSeconds);
                    float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(5f, 16f, urgency))) * 0.25f;
                    marker.localScale = new Vector3(markerScale.x * pulse, markerScale.y, markerScale.z * pulse);
                    if (timer <= 0f)
                    {
                        phase = Phase.Flying;
                        timer = settings.CannonFlightSeconds;
                    }
                    break;

                case Phase.Flying:
                    float t = 1f - Mathf.Clamp01(timer / settings.CannonFlightSeconds);
                    kid.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * jumpHeight);
                    kid.Rotate(Vector3.right, 720f * deltaTime, Space.Self);
                    if (timer <= 0f) Land();
                    break;

                case Phase.Sinking:
                    kid.position += Vector3.down * (2f * deltaTime);
                    if (timer <= 0f)
                    {
                        kid.gameObject.SetActive(false);
                        phase = Phase.Idle;
                    }
                    break;
            }
        }

        private void Land()
        {
            phase = Phase.Sinking;
            timer = SinkSeconds;
            marker.gameObject.SetActive(false);
            if (splashScreen != null) splashScreen.Splash();

            dropTime = 0f;
            for (int i = 0; i < drops.Length; i++)
            {
                var outward = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                dropVelocity[i] = new Vector3(outward.x, Random.Range(5f, 9f), outward.y);
                drops[i].position = to + Vector3.up * 0.2f;
                drops[i].localScale = Vector3.one * Random.Range(0.2f, 0.45f);
                drops[i].gameObject.SetActive(true);
            }
        }

        private void TickDrops(float deltaTime)
        {
            if (dropTime > 2f) return;
            dropTime += deltaTime;
            for (int i = 0; i < drops.Length; i++)
            {
                if (!drops[i].gameObject.activeSelf) continue;
                dropVelocity[i] += Vector3.down * (18f * deltaTime);
                drops[i].position += dropVelocity[i] * deltaTime;
                if (drops[i].position.y < to.y - 0.3f) drops[i].gameObject.SetActive(false);
            }
        }
    }
}
