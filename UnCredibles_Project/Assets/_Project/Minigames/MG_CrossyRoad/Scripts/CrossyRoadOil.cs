using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Every now and then a car leaks an oil slick under itself. Players on it lose grip and slide.
    // Slicks are pooled, grow in, stay a while and shrink out. Ticked by CrossyRoadController.
    public sealed class CrossyRoadOil : MonoBehaviour
    {
        private const float GrowSeconds = 0.3f;
        private const float ShrinkSeconds = 0.6f;

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private CrossyRoadTraffic traffic;
        [SerializeField] private Transform slickPrefab;

        private sealed class Slick
        {
            public Transform Transform;
            public Vector3 FullScale;
            public float Age;
            public bool Active;
        }

        private readonly List<Slick> slicks = new List<Slick>();
        private float dropTimer;

        private CrossyRoadSettings Settings => board.Settings;

        public void Initialize()
        {
            for (int i = 0; i < Settings.MaxOilSlicks; i++) slicks.Add(CreateSlick());
            ScheduleNextDrop();
        }

        public void Tick(float deltaTime)
        {
            dropTimer -= deltaTime;
            if (dropTimer <= 0f)
            {
                ScheduleNextDrop();
                if (traffic.TryGetRandomCarOnBoard(out var carPosition)) Drop(carPosition);
            }

            foreach (var slick in slicks)
            {
                if (!slick.Active) continue;
                slick.Age += deltaTime;
                float remaining = Settings.OilLifetime - slick.Age;
                if (remaining <= 0f)
                {
                    slick.Active = false;
                    slick.Transform.gameObject.SetActive(false);
                    continue;
                }
                float grow = Mathf.Clamp01(slick.Age / GrowSeconds);
                float shrink = Mathf.Clamp01(remaining / ShrinkSeconds);
                slick.Transform.localScale = slick.FullScale * Mathf.Min(grow, shrink);
            }
        }

        public bool IsOnOil(Vector3 position)
        {
            float radius = Settings.OilRadius;
            foreach (var slick in slicks)
            {
                if (!slick.Active) continue;
                var offset = slick.Transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        private void Drop(Vector3 position)
        {
            Slick free = null;
            foreach (var slick in slicks)
                if (!slick.Active) { free = slick; break; }
            if (free == null) return; // already at the maximum; skip this drop

            free.Age = 0f;
            free.Active = true;
            float size = Settings.OilRadius * 2f;
            free.FullScale = new Vector3(size * Random.Range(0.9f, 1.15f), 1f, size * Random.Range(0.8f, 1f));
            free.Transform.SetPositionAndRotation(
                new Vector3(position.x, board.transform.position.y, position.z),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            free.Transform.localScale = Vector3.zero;
            free.Transform.gameObject.SetActive(true);
        }

        private Slick CreateSlick()
        {
            var instance = Instantiate(slickPrefab, transform);
            instance.gameObject.SetActive(false);
            return new Slick { Transform = instance };
        }

        private void ScheduleNextDrop() => dropTimer = Random.Range(Settings.OilDropInterval.x, Settings.OilDropInterval.y);
    }
}
