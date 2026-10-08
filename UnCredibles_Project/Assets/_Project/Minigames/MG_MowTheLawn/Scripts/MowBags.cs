using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Pool of bag visuals for every tail, plus the loose bags lying on the lawn after a tail
    // was cut. Loose bags hop to where they land and can then be picked up by any mower.
    public sealed class MowBags : MonoBehaviour
    {
        private const float HopSeconds = 0.45f;
        private const float HopHeight = 1.3f;
        private const float BlinkSeconds = 2f;

        [SerializeField] private MowBagView bagPrefab;

        private sealed class LooseBag
        {
            public MowBagView View;
            public Vector3 From;
            public Vector3 To;
            public float Age;
        }

        private readonly Stack<MowBagView> pool = new Stack<MowBagView>();
        private readonly List<LooseBag> loose = new List<LooseBag>();
        private readonly List<Vector3> received = new List<Vector3>();
        private readonly List<float> receivedAges = new List<float>();
        private MowTheLawnSettings settings;
        private MowLawn lawn;

        public int LooseCount => loose.Count;
        public Vector3 GetLoosePosition(int index) => loose[index].To;
        public bool CanPickUp(int index) => loose[index].Age >= settings.PickupDelay;

        public void Initialize(MowTheLawnSettings bagSettings, MowLawn mowLawn)
        {
            settings = bagSettings;
            lawn = mowLawn;
        }

        public MowBagView Rent()
        {
            var view = pool.Count > 0 ? pool.Pop() : Instantiate(bagPrefab, transform);
            view.transform.localScale = Vector3.one;
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(MowBagView view)
        {
            view.gameObject.SetActive(false);
            pool.Push(view);
        }

        // A bag knocked out of a tail: it jumps a little away and lands loose on the lawn.
        public void Drop(Vector3 from)
        {
            var offset = Random.insideUnitCircle * settings.ScatterDistance;
            var to = lawn.ClampInside(new Vector3(from.x + offset.x, lawn.Center.y, from.z + offset.y), settings.BagRadius);
            AddLoose(from, to, 0f);
            // Too many on the lawn: the oldest one is gone.
            if (loose.Count > settings.MaxLooseBags) RemoveLoose(0);
        }

        // Picks up every ready loose bag touching the circle; returns how many.
        public int PickUp(Vector3 position, float radius)
        {
            int count = 0;
            float reach = radius + settings.BagRadius;
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                if (!CanPickUp(i)) continue;
                var offset = loose[i].To - position;
                offset.y = 0f;
                if (offset.sqrMagnitude > reach * reach) continue;
                RemoveLoose(i);
                count++;
            }
            return count;
        }

        // Only the host removes old bags; clients follow its snapshots.
        public void Tick(float deltaTime, bool removeExpired)
        {
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                var bag = loose[i];
                bag.Age += deltaTime;
                float left = settings.LooseBagLifetime - bag.Age;
                if (removeExpired && left <= 0f)
                {
                    RemoveLoose(i);
                    continue;
                }
                bag.View.gameObject.SetActive(left > BlinkSeconds || Mathf.FloorToInt(left * 8f) % 2 == 0);
                float t = Mathf.Clamp01(bag.Age / HopSeconds);
                var position = Vector3.Lerp(bag.From, bag.To, t);
                position.y += Mathf.Sin(t * Mathf.PI) * HopHeight;
                bag.View.transform.position = position;
            }
        }

        // ---------- Online ----------

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)loose.Count);
            foreach (var bag in loose)
            {
                writer.Write(bag.To.x);
                writer.Write(bag.To.z);
                writer.Write((byte)Mathf.Clamp(Mathf.RoundToInt(bag.Age * 10f), 0, 255));
            }
        }

        // Client: same loose bags as the host. New ones hop in from where they were.
        public void ReadState(BinaryReader reader)
        {
            received.Clear();
            int count = reader.ReadByte();
            receivedAges.Clear();
            for (int i = 0; i < count; i++)
            {
                received.Add(new Vector3(reader.ReadSingle(), lawn.Center.y, reader.ReadSingle()));
                receivedAges.Add(reader.ReadByte() / 10f);
            }

            while (loose.Count > received.Count) RemoveLoose(loose.Count - 1);
            for (int i = 0; i < received.Count; i++)
            {
                if (i >= loose.Count) AddLoose(received[i], received[i], Mathf.Max(HopSeconds, receivedAges[i]));
                else if ((loose[i].To - received[i]).sqrMagnitude > 0.01f)
                {
                    loose[i].From = loose[i].To = received[i];
                    loose[i].Age = Mathf.Max(HopSeconds, receivedAges[i]);
                }
                else loose[i].Age = Mathf.Max(loose[i].Age, receivedAges[i]);
            }
        }

        private void AddLoose(Vector3 from, Vector3 to, float age)
        {
            var view = Rent();
            view.SetTint(Color.white);
            view.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            loose.Add(new LooseBag { View = view, From = from, To = to, Age = age });
        }

        private void RemoveLoose(int index)
        {
            Release(loose[index].View);
            loose.RemoveAt(index);
        }
    }
}
