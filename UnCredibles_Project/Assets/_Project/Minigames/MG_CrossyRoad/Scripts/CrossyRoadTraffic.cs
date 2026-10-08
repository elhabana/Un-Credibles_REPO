using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Cars of every road lane. Pooled, moved from a single Tick and hit-tested without physics.
    // Some of them are oil trucks: easy to tell apart, and the only vehicles that leak oil slicks.
    public sealed class CrossyRoadTraffic : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float CarDepthRatio = 0.7f; // car depth relative to the lane depth

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private Transform carPrefab;
        [SerializeField, Tooltip("Leaking oil truck. Same size as a car of its lane, its own look.")]
        private Transform oilTruckPrefab;

        private sealed class Car
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public float X;
            public int Id; // same on host and clients, also picks the colour
            public bool IsOilTruck;
            public CrossyRoadOilTruck Truck;
            public int DropsLeft;
            public float NextDropX;
        }

        private sealed class Lane
        {
            public CrossyRoadSettings.RoadLane Config;
            public float Z;
            public float HalfLength;
            public float Velocity; // world units per second, signed
            public float Timer;
            public readonly List<Car> Cars = new List<Car>();
        }

        private readonly Stack<Car> pool = new Stack<Car>();
        private readonly Stack<Car> truckPool = new Stack<Car>();
        private readonly Queue<Vector3> pendingDrops = new Queue<Vector3>();
        private readonly List<bool> receivedTrucks = new List<bool>();
        private MaterialPropertyBlock block;
        private Lane[] lanes = new Lane[0];
        private float spawnX;
        private float despawnX;
        private readonly List<int> receivedIds = new List<int>();
        private readonly List<float> receivedX = new List<float>();
        private int nextId;

        // An online client starts empty: its cars come from the host.
        public void Initialize(bool prefill = true)
        {
            var settings = board.Settings;
            block = new MaterialPropertyBlock();
            spawnX = board.HalfWidth + settings.OffscreenMargin;
            despawnX = spawnX + settings.CellSize * 3f;

            lanes = new Lane[settings.RoadCount];
            for (int i = 0; i < lanes.Length; i++)
            {
                var config = settings.GetRoad(i);
                var lane = new Lane
                {
                    Config = config,
                    Z = board.LaneToZ(CrossyRoadBoard.FirstRoadLane + i),
                    HalfLength = config.carLength * settings.CellSize * 0.5f,
                    Velocity = config.direction * config.speed * settings.CellSize,
                };
                lanes[i] = lane;
                if (prefill) Prefill(lane);
            }
        }

        // `leak`: trucks only drop oil while the game is being played.
        public void Tick(float deltaTime, bool leak)
        {
            foreach (var lane in lanes)
            {
                lane.Timer -= deltaTime;
                if (lane.Timer <= 0f && EntryIsClear(lane))
                {
                    Spawn(lane, -Mathf.Sign(lane.Velocity) * spawnX, NextId(), RollOilTruck());
                    lane.Timer = Random.Range(lane.Config.minSpawnInterval, lane.Config.maxSpawnInterval);
                }

                for (int i = lane.Cars.Count - 1; i >= 0; i--)
                {
                    var car = lane.Cars[i];
                    car.X += lane.Velocity * deltaTime;
                    if (Mathf.Abs(car.X) > despawnX && Mathf.Sign(car.X) == Mathf.Sign(lane.Velocity))
                    {
                        Release(lane, i);
                        continue;
                    }
                    car.Transform.position = new Vector3(board.transform.position.x + car.X, board.transform.position.y, lane.Z);
                    if (car.IsOilTruck) TickTruck(car, lane, deltaTime, leak);
                }
            }
        }

        // Host: next oil slick leaked by a truck, if any (consumed by CrossyRoadOil).
        public bool TryTakeDrop(out Vector3 position)
        {
            if (pendingDrops.Count == 0)
            {
                position = default;
                return false;
            }
            position = pendingDrops.Dequeue();
            return true;
        }

        // A truck leaks at a couple of random points while it crosses the board.
        private void TickTruck(Car car, Lane lane, float deltaTime, bool leak)
        {
            car.Truck.Animate(deltaTime);
            if (!leak || car.DropsLeft <= 0) return;
            float direction = Mathf.Sign(lane.Velocity);
            if ((car.X - car.NextDropX) * direction < 0f) return;

            pendingDrops.Enqueue(new Vector3(board.transform.position.x + car.X, board.transform.position.y, lane.Z));
            car.Truck.Leak();
            car.DropsLeft--;
            PickNextDrop(car, direction);
        }

        // Somewhere ahead of the truck, still over the board; none if there is no room left.
        private void PickNextDrop(Car car, float direction)
        {
            float limit = board.HalfWidth - board.Settings.OilRadius;
            float from = direction > 0f ? Mathf.Max(car.X, -limit) : Mathf.Min(car.X, limit);
            float to = direction * limit;
            if ((to - from) * direction < board.CellSize)
            {
                car.DropsLeft = 0;
                return;
            }
            car.NextDropX = Random.Range(from, to);
        }

        private bool RollOilTruck()
        {
            if (oilTruckPrefab == null || Random.value >= board.Settings.OilTruckChance) return false;
            int trucks = 0;
            foreach (var lane in lanes)
            {
                if (lane == null) continue; // lanes still being filled at start
                foreach (var car in lane.Cars)
                    if (car.IsOilTruck) trucks++;
            }
            return trucks < board.Settings.MaxOilTrucks;
        }

        public bool IsHit(int roadIndex, float worldX, float halfWidth) =>
            IsDangerous(roadIndex, worldX, halfWidth, 0f);

        // True if any car overlaps worldX now or within the next `lookAhead` seconds (used by the AI).
        public bool IsDangerous(int roadIndex, float worldX, float halfWidth, float lookAhead)
        {
            if (roadIndex < 0 || roadIndex >= lanes.Length) return false;
            var lane = lanes[roadIndex];
            float x = worldX - board.transform.position.x;
            float reach = lane.HalfLength + halfWidth;
            foreach (var car in lane.Cars)
            {
                float future = car.X + lane.Velocity * lookAhead;
                float min = Mathf.Min(car.X, future) - reach;
                float max = Mathf.Max(car.X, future) + reach;
                if (x > min && x < max) return true;
            }
            return false;
        }

        // Does a circle (player) on the ground overlap any car? Checks every lane it touches,
        // so a player standing between two lanes is tested against both.
        public bool Overlaps(Vector3 worldPosition, float radius) => OverlapsAt(worldPosition, radius, 0f);

        // Same test with every car moved `seconds` into the future (used by the AI to plan).
        public bool OverlapsAt(Vector3 worldPosition, float radius, float seconds)
        {
            float halfDepth = board.CellSize * CarDepthRatio * 0.5f;
            float x = worldPosition.x - board.transform.position.x;
            foreach (var lane in lanes)
            {
                if (Mathf.Abs(worldPosition.z - lane.Z) >= halfDepth + radius) continue;
                float reach = lane.HalfLength + radius;
                float travel = lane.Velocity * seconds;
                foreach (var car in lane.Cars)
                    if (Mathf.Abs(car.X + travel - x) < reach) return true;
            }
            return false;
        }

        // ---------- Online ----------

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)lanes.Length);
            foreach (var lane in lanes)
            {
                writer.Write((byte)lane.Cars.Count);
                foreach (var car in lane.Cars)
                {
                    writer.Write((ushort)car.Id);
                    writer.Write(car.X);
                    writer.Write(car.IsOilTruck);
                }
            }
        }

        // Client: same cars as the host (matched by id), gently pulled to the host position.
        public void ReadState(BinaryReader reader)
        {
            int laneCount = reader.ReadByte();
            for (int l = 0; l < laneCount; l++)
            {
                receivedIds.Clear();
                receivedX.Clear();
                receivedTrucks.Clear();
                int count = reader.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    receivedIds.Add(reader.ReadUInt16());
                    receivedX.Add(reader.ReadSingle());
                    receivedTrucks.Add(reader.ReadBoolean());
                }
                if (l >= lanes.Length) continue;

                var lane = lanes[l];
                for (int i = lane.Cars.Count - 1; i >= 0; i--)
                    if (!receivedIds.Contains(lane.Cars[i].Id)) Release(lane, i);
                for (int i = 0; i < receivedIds.Count; i++)
                {
                    var car = FindCar(lane, receivedIds[i]);
                    if (car == null) Spawn(lane, receivedX[i], receivedIds[i], receivedTrucks[i]);
                    else car.X = Mathf.Lerp(car.X, receivedX[i], 0.5f);
                }
            }
        }

        // Client: cars keep driving between snapshots; the host spawns and removes them.
        public void TickRemote(float deltaTime)
        {
            foreach (var lane in lanes)
                foreach (var car in lane.Cars)
                {
                    car.X += lane.Velocity * deltaTime;
                    car.Transform.position = new Vector3(board.transform.position.x + car.X, board.transform.position.y, lane.Z);
                    if (car.IsOilTruck) car.Truck.Animate(deltaTime);
                }
        }

        private static Car FindCar(Lane lane, int id)
        {
            foreach (var car in lane.Cars)
                if (car.Id == id) return car;
            return null;
        }

        // Start with cars already on the road instead of empty lanes.
        private void Prefill(Lane lane)
        {
            float direction = Mathf.Sign(lane.Velocity);
            float x = -direction * spawnX;
            while (Mathf.Abs(x) <= spawnX || Mathf.Sign(x) != direction)
            {
                Spawn(lane, x, NextId(), RollOilTruck());
                float interval = Random.Range(lane.Config.minSpawnInterval, lane.Config.maxSpawnInterval);
                x += direction * Mathf.Max(Mathf.Abs(lane.Velocity) * interval, lane.HalfLength * 2f + board.CellSize);
            }
            // Keep "last in the list = closest to the entry", as Tick appends new cars at the end.
            lane.Cars.Reverse();
            lane.Timer = Random.Range(0f, lane.Config.minSpawnInterval);
        }

        private bool EntryIsClear(Lane lane)
        {
            if (lane.Cars.Count == 0) return true;
            var last = lane.Cars[lane.Cars.Count - 1];
            float travelled = Mathf.Abs(last.X - (-Mathf.Sign(lane.Velocity) * spawnX));
            return travelled > lane.HalfLength * 2f + board.CellSize;
        }

        private int NextId() => nextId++ & 0xFFFF;

        private void Spawn(Lane lane, float x, int id, bool oilTruck)
        {
            var source = oilTruck ? truckPool : pool;
            var car = source.Count > 0 ? source.Pop() : CreateCar(oilTruck);
            car.X = x;
            car.Id = id;
            car.Transform.localScale = new Vector3(lane.HalfLength * 2f, 0.8f, board.CellSize * CarDepthRatio);
            // Facing the way it drives (the truck has a front; plain cars look the same both ways).
            car.Transform.SetPositionAndRotation(
                new Vector3(board.transform.position.x + x, board.transform.position.y, lane.Z),
                Quaternion.Euler(0f, lane.Velocity >= 0f ? 0f : 180f, 0f));
            if (oilTruck)
            {
                car.DropsLeft = board.Settings.OilDropsPerTruck;
                PickNextDrop(car, Mathf.Sign(lane.Velocity));
            }
            else
            {
                block.SetColor(BaseColorId, board.Settings.GetCarColor(car.Id));
                foreach (var carRenderer in car.Renderers) carRenderer.SetPropertyBlock(block);
            }
            car.Transform.gameObject.SetActive(true);
            lane.Cars.Add(car);
        }

        private void Release(Lane lane, int index)
        {
            var car = lane.Cars[index];
            lane.Cars.RemoveAt(index);
            car.Transform.gameObject.SetActive(false);
            (car.IsOilTruck ? truckPool : pool).Push(car);
        }

        private Car CreateCar(bool oilTruck)
        {
            var instance = Instantiate(oilTruck ? oilTruckPrefab : carPrefab, transform);
            return new Car
            {
                Transform = instance,
                Renderers = instance.GetComponentsInChildren<Renderer>(),
                IsOilTruck = oilTruck,
                Truck = oilTruck ? instance.GetComponent<CrossyRoadOilTruck>() : null,
            };
        }
    }
}
