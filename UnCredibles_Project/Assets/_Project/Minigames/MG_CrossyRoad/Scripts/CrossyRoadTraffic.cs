using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Cars of every road lane. Pooled, moved from a single Tick and hit-tested without physics.
    // Some of them are oil trucks: easy to tell apart, and the only vehicles that leak oil slicks.
    public sealed class CrossyRoadTraffic : MonoBehaviour
    {
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
            public Vector3 ModelSize;  // custom model size at scale 1; zero = the box prefab
            public float ModelScale = 1f;
            public Wheel[] Wheels = new Wheel[0];
            public Transform Body;        // the model without its wheels; null = box prefab
            public Vector3 BodyRestPosition;
            public Quaternion BodyRestRotation;
            public float BodyTime;        // starts at a random phase so cars do not bounce in sync
            public float HalfLength;   // hit box, set on every spawn
            public float HalfDepth;
        }

        private struct Wheel
        {
            public Transform Pivot;      // at the wheel centre, axes aligned with the car root
            public Quaternion Rest;
            public float Radius;         // at scale 1
            public float Angle;
        }

        private sealed class Lane
        {
            public CrossyRoadSettings.RoadLane Config;
            public float Z;
            public float HalfLength;
            public float BaseVelocity; // world units per second, signed, at multiplier 1
            public float Velocity;     // current one, grows with the difficulty
            public float Timer;
            public CrossyRoadSettings.VehicleModel Model; // no model = box prefab
            public float Gap;          // minimum distance between two vehicles, fits the longest one
            public readonly Stack<Car> Pool = new Stack<Car>(); // per lane, as each lane can have its own model
            public readonly List<Car> Cars = new List<Car>();
        }

        private float MaxCarDepth => board.CellSize * CarDepthRatio * board.Settings.VehicleScale;

        private readonly Stack<Car> truckPool = new Stack<Car>();
        private readonly Queue<Vector3> pendingDrops = new Queue<Vector3>();
        private readonly List<bool> receivedTrucks = new List<bool>();
        private readonly TintedMaterials tints = new TintedMaterials();
        private Lane[] lanes = new Lane[0];
        private float spawnX;
        private float despawnX;
        private readonly List<int> receivedIds = new List<int>();
        private readonly List<float> receivedX = new List<float>();
        private int nextId;
        private float difficulty;
        private float speedMultiplier = 1f;
        private float spawnMultiplier = 1f;

        // An online client starts empty: its cars come from the host.
        public void Initialize(bool prefill = true)
        {
            var settings = board.Settings;
            speedMultiplier = settings.SpeedMultiplier(0f);
            spawnMultiplier = settings.SpawnMultiplier(0f);
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
                    HalfLength = config.carLength * settings.CellSize * 0.5f * settings.VehicleScale,
                    BaseVelocity = config.direction * config.speed * settings.CellSize,
                    Model = settings.CarModelFor(config.carLength),
                };
                lane.Velocity = lane.BaseVelocity * speedMultiplier;
                float longest = Mathf.Max(1f, ScaleOf(lane.Model), ScaleOf(settings.OilTruck));
                lane.Gap = lane.HalfLength * longest * 2f + settings.CellSize;
                lanes[i] = lane;
                if (prefill) Prefill(lane);
            }
        }

        // 0 = calm start (few, slow cars), 1 = end of the game (many, fast cars). Set by the host
        // every frame and sent to the clients with the cars.
        public void SetDifficulty(float value)
        {
            difficulty = Mathf.Clamp01(value);
            var settings = board.Settings;
            speedMultiplier = settings.SpeedMultiplier(difficulty);
            spawnMultiplier = settings.SpawnMultiplier(difficulty);
            foreach (var lane in lanes) lane.Velocity = lane.BaseVelocity * speedMultiplier;
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
                    lane.Timer = Random.Range(lane.Config.minSpawnInterval, lane.Config.maxSpawnInterval) * spawnMultiplier;
                }

                for (int i = lane.Cars.Count - 1; i >= 0; i--)
                {
                    var car = lane.Cars[i];
                    car.X += lane.Velocity * deltaTime;
                    AnimateModel(car, Mathf.Abs(lane.Velocity) * deltaTime, deltaTime);
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
            // No trucks at the very start; more likely as the game gets harder.
            float chance = board.Settings.OilTruckChance * Mathf.Clamp01(difficulty * 1.5f);
            if (oilTruckPrefab == null || Random.value >= chance) return false;
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
            foreach (var car in lane.Cars)
            {
                float reach = car.HalfLength + halfWidth;
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

        // Signed speed of the cars in the road closest to this Z (0 when there is no road).
        public float VelocityNear(float z)
        {
            float best = float.MaxValue, velocity = 0f;
            foreach (var lane in lanes)
            {
                if (lane == null) continue;
                float distance = Mathf.Abs(z - lane.Z);
                if (distance >= best) continue;
                best = distance;
                velocity = lane.Velocity;
            }
            return velocity;
        }

        // Same test with every car moved `seconds` into the future (used by the AI to plan).
        public bool OverlapsAt(Vector3 worldPosition, float radius, float seconds)
        {
            float x = worldPosition.x - board.transform.position.x;
            foreach (var lane in lanes)
            {
                // Per car only: a scaled truck can be wider than the lane's cars.
                float dz = Mathf.Abs(worldPosition.z - lane.Z);
                float travel = lane.Velocity * seconds;
                foreach (var car in lane.Cars)
                    if (dz < car.HalfDepth + radius && Mathf.Abs(car.X + travel - x) < car.HalfLength + radius) return true;
            }
            return false;
        }

        // ---------- Online ----------

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)Mathf.RoundToInt(difficulty * 255f));
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
            SetDifficulty(reader.ReadByte() / 255f);
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
                    AnimateModel(car, Mathf.Abs(lane.Velocity) * deltaTime, deltaTime);
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
                float interval = Random.Range(lane.Config.minSpawnInterval, lane.Config.maxSpawnInterval) * spawnMultiplier;
                x += direction * Mathf.Max(Mathf.Abs(lane.Velocity) * interval, lane.Gap);
            }
            // Keep "last in the list = closest to the entry", as Tick appends new cars at the end.
            lane.Cars.Reverse();
            lane.Timer = Random.Range(0f, lane.Config.minSpawnInterval) * spawnMultiplier;
        }

        private bool EntryIsClear(Lane lane)
        {
            if (lane.Cars.Count == 0) return true;
            var last = lane.Cars[lane.Cars.Count - 1];
            float travelled = Mathf.Abs(last.X - (-Mathf.Sign(lane.Velocity) * spawnX));
            return travelled > lane.Gap;
        }

        private int NextId() => nextId++ & 0xFFFF;

        private void Spawn(Lane lane, float x, int id, bool oilTruck)
        {
            var source = oilTruck ? truckPool : lane.Pool;
            var car = source.Count > 0 ? source.Pop() : CreateCar(oilTruck ? board.Settings.OilTruck : lane.Model, oilTruck);
            car.X = x;
            car.Id = id;
            float maxDepth = MaxCarDepth;
            if (car.ModelSize == Vector3.zero)
            {
                car.HalfLength = lane.HalfLength;
                car.HalfDepth = maxDepth * 0.5f;
                car.Transform.localScale = new Vector3(lane.HalfLength * 2f, 0.8f * board.Settings.VehicleScale, maxDepth);
            }
            else
            {
                // Uniform scale, as long as the lane's cars but never wider than the box; the hit box follows the model.
                float scale = Mathf.Min(lane.HalfLength * 2f / car.ModelSize.x, maxDepth / car.ModelSize.z) * car.ModelScale;
                car.HalfLength = car.ModelSize.x * scale * 0.5f;
                car.HalfDepth = car.ModelSize.z * scale * 0.5f;
                car.Transform.localScale = Vector3.one * scale;
            }
            // Facing the way it drives (the truck has a front; plain cars look the same both ways).
            car.Transform.SetPositionAndRotation(
                new Vector3(board.transform.position.x + x, board.transform.position.y, lane.Z),
                Quaternion.Euler(0f, lane.Velocity >= 0f ? 0f : 180f, 0f));
            if (oilTruck)
            {
                car.DropsLeft = board.Settings.OilDropsPerTruck;
                PickNextDrop(car, Mathf.Sign(lane.Velocity));
            }
            else if (car.ModelSize == Vector3.zero || board.Settings.TintCarModel)
            {
                var color = board.Settings.GetCarColor(car.Id);
                foreach (var carRenderer in car.Renderers) tints.Apply(carRenderer, color);
            }
            car.Transform.gameObject.SetActive(true);
            lane.Cars.Add(car);
        }

        private void Release(Lane lane, int index)
        {
            var car = lane.Cars[index];
            lane.Cars.RemoveAt(index);
            car.Transform.gameObject.SetActive(false);
            (car.IsOilTruck ? truckPool : lane.Pool).Push(car);
        }

        private void OnDestroy() => tints.DestroyAll();

        private Car CreateCar(CrossyRoadSettings.VehicleModel model, bool oilTruck)
        {
            var size = Vector3.zero;
            Transform instance;
            var wheels = new Wheel[0];
            Transform body = null;
            if (model.model == null) instance = Instantiate(oilTruck ? oilTruckPrefab : carPrefab, transform);
            else
            {
                // The truck keeps its prefab (drips, smoke, Leak) with the box parts hidden under the model.
                instance = oilTruck ? Instantiate(oilTruckPrefab) : new GameObject(model.model.name).transform;
                if (oilTruck)
                {
                    var effects = instance.GetComponent<CrossyRoadOilTruck>().Effects;
                    foreach (var part in instance.GetComponentsInChildren<Renderer>())
                        if (effects == null || !part.transform.IsChildOf(effects)) part.enabled = false;
                }
                size = AttachModel(instance, model, out wheels, out body);
                instance.SetParent(transform, false);
            }
            var truck = oilTruck ? instance.GetComponent<CrossyRoadOilTruck>() : null;
            if (truck != null && size != Vector3.zero) truck.BodySize = size;
            return new Car
            {
                Transform = instance,
                Renderers = instance.GetComponentsInChildren<Renderer>(),
                IsOilTruck = oilTruck,
                Truck = truck,
                ModelSize = size,
                ModelScale = ScaleOf(model),
                Wheels = wheels,
                Body = oilTruck ? null : body, // only cars bounce; the truck stays steady
                BodyRestPosition = body != null ? body.localPosition : Vector3.zero,
                BodyRestRotation = body != null ? body.localRotation : Quaternion.identity,
                BodyTime = Random.value * 10f,
            };
        }

        // Parts named "wheel" / "rueda" spin around their own centre (a mesh with both wheels of an
        // axle works too: its centre is on the axle). They hang from the root, not from the body, so
        // the body can bounce on top of them. Called with the root still at the origin, unscaled.
        private static Wheel[] FindWheels(Transform root, Renderer[] renderers)
        {
            var found = new List<Wheel>();
            foreach (var part in renderers)
            {
                string name = part.name.ToLowerInvariant();
                if (!name.Contains("wheel") && !name.Contains("rueda")) continue;
                var bounds = part.bounds;
                var pivot = new GameObject(part.name + "_Spin").transform;
                pivot.position = bounds.center; // world axes = car root axes here
                pivot.SetParent(root, true);
                part.transform.SetParent(pivot, true);
                found.Add(new Wheel { Pivot = pivot, Rest = pivot.localRotation, Radius = Mathf.Max(0.01f, bounds.extents.y) });
            }
            return found.ToArray();
        }

        // Small suspension bounce of the body: up and down plus a little pitch and roll.
        private const float BodyBob = 0.025f;   // of the model height
        private const float BodyPitch = 1.2f;   // degrees
        private const float BodyRoll = 0.8f;    // degrees

        private static void AnimateModel(Car car, float distance, float deltaTime)
        {
            SpinWheels(car, distance);
            if (car.Body == null) return;
            float t = car.BodyTime += deltaTime;
            float bob = (Mathf.Sin(t * 9f) * 0.7f + Mathf.Sin(t * 14.3f) * 0.3f) * BodyBob * car.ModelSize.y;
            // Root space: the car drives along +X, so pitch turns around Z and roll around X.
            var sway = Quaternion.Euler(Mathf.Sin(t * 5.3f) * BodyRoll, 0f, Mathf.Sin(t * 7.1f + 1f) * BodyPitch);
            car.Body.localPosition = car.BodyRestPosition + Vector3.up * bob;
            car.Body.localRotation = sway * car.BodyRestRotation;
        }

        // Rolls forward: the root faces the way it drives (+X), so the wheels turn around its Z.
        private static void SpinWheels(Car car, float distance)
        {
            for (int i = 0; i < car.Wheels.Length; i++)
            {
                ref var wheel = ref car.Wheels[i];
                float radius = wheel.Radius * car.Transform.lossyScale.y;
                wheel.Angle = Mathf.Repeat(wheel.Angle - distance / radius * Mathf.Rad2Deg, 360f);
                wheel.Pivot.localRotation = wheel.Rest * Quaternion.AngleAxis(wheel.Angle, Vector3.forward);
            }
        }

        private static float ScaleOf(CrossyRoadSettings.VehicleModel model) =>
            model.model != null && model.scale > 0f ? model.scale : 1f;

        // Adds the model to `root` (still at the world origin, unscaled) centred on it and resting
        // on the ground, like the box prefabs. Returns its size, used for the hit box.
        private static Vector3 AttachModel(Transform root, CrossyRoadSettings.VehicleModel model, out Wheel[] wheels, out Transform body)
        {
            wheels = new Wheel[0];
            var visual = Instantiate(model.model, root).transform;
            body = visual;
            visual.localRotation = Quaternion.Euler(model.rotation);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"Vehicle model '{model.model.name}' has no renderers; using a 1 m hit box.", model.model);
                return Vector3.one;
            }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            visual.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            wheels = FindWheels(root, renderers);
            return bounds.size;
        }
    }
}
