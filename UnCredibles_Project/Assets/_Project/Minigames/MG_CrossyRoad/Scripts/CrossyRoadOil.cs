using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Oil trucks (see CrossyRoadTraffic) leak oil slicks as they cross. Players on them lose grip and slide.
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

        private CrossyRoadSettings Settings => board.Settings;

        public void Initialize()
        {
            for (int i = 0; i < Settings.MaxOilSlicks; i++) slicks.Add(CreateSlick());
        }

        public void Tick(float deltaTime)
        {
            while (traffic.TryTakeDrop(out var leakPosition)) Drop(leakPosition);

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
                ApplyScale(slick);
            }
        }

        private void ApplyScale(Slick slick)
        {
            float grow = Mathf.Clamp01(slick.Age / GrowSeconds);
            float shrink = Mathf.Clamp01((Settings.OilLifetime - slick.Age) / ShrinkSeconds);
            slick.Transform.localScale = slick.FullScale * Mathf.Min(grow, shrink);
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
            if (free == null) free = Oldest(); // at the maximum: the oldest slick dries up early

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

        // ---------- Online ----------

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)slicks.Count);
            foreach (var slick in slicks)
            {
                writer.Write(slick.Active);
                if (!slick.Active) continue;
                var position = slick.Transform.position;
                writer.Write(position.x);
                writer.Write(position.z);
                writer.Write(slick.Transform.eulerAngles.y);
                writer.Write(slick.FullScale.x);
                writer.Write(slick.FullScale.z);
                writer.Write(slick.Age);
            }
        }

        public void ReadState(BinaryReader reader)
        {
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                bool active = reader.ReadBoolean();
                if (i >= slicks.Count) slicks.Add(CreateSlick());
                var slick = slicks[i];
                slick.Active = active;
                if (!active)
                {
                    if (slick.Transform.gameObject.activeSelf) slick.Transform.gameObject.SetActive(false);
                    continue;
                }
                float x = reader.ReadSingle();
                float z = reader.ReadSingle();
                float yaw = reader.ReadSingle();
                slick.FullScale = new Vector3(reader.ReadSingle(), 1f, reader.ReadSingle());
                slick.Age = reader.ReadSingle();
                slick.Transform.SetPositionAndRotation(new Vector3(x, board.transform.position.y, z), Quaternion.Euler(0f, yaw, 0f));
                ApplyScale(slick);
                if (!slick.Transform.gameObject.activeSelf) slick.Transform.gameObject.SetActive(true);
            }
        }

        // Client: only grow and shrink the slicks; the host drops and removes them.
        public void TickRemote(float deltaTime)
        {
            foreach (var slick in slicks)
            {
                if (!slick.Active) continue;
                slick.Age = Mathf.Min(slick.Age + deltaTime, Settings.OilLifetime);
                ApplyScale(slick);
            }
        }

        private Slick Oldest()
        {
            Slick oldest = slicks[0];
            foreach (var slick in slicks)
                if (slick.Age > oldest.Age) oldest = slick;
            return oldest;
        }

        private Slick CreateSlick()
        {
            var instance = Instantiate(slickPrefab, transform);
            instance.gameObject.SetActive(false);
            return new Slick { Transform = instance };
        }
    }
}
