using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Cars of every road lane. Pooled, moved from a single Tick and hit-tested without physics.
    public sealed class CrossyRoadTraffic : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private Transform carPrefab;

        private sealed class Car
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public float X;
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
        private MaterialPropertyBlock block;
        private Lane[] lanes = new Lane[0];
        private float spawnX;
        private float despawnX;
        private int colorIndex;

        public void Initialize()
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
                Prefill(lane);
            }
        }

        public void Tick(float deltaTime)
        {
            foreach (var lane in lanes)
            {
                lane.Timer -= deltaTime;
                if (lane.Timer <= 0f && EntryIsClear(lane))
                {
                    Spawn(lane, -Mathf.Sign(lane.Velocity) * spawnX);
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
                }
            }
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

        // Start with cars already on the road instead of empty lanes.
        private void Prefill(Lane lane)
        {
            float direction = Mathf.Sign(lane.Velocity);
            float x = -direction * spawnX;
            while (Mathf.Abs(x) <= spawnX || Mathf.Sign(x) != direction)
            {
                Spawn(lane, x);
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

        private void Spawn(Lane lane, float x)
        {
            var car = pool.Count > 0 ? pool.Pop() : CreateCar();
            car.X = x;
            car.Transform.localScale = new Vector3(lane.HalfLength * 2f, 0.8f, board.CellSize * 0.7f);
            car.Transform.position = new Vector3(board.transform.position.x + x, board.transform.position.y, lane.Z);
            block.SetColor(BaseColorId, board.Settings.GetCarColor(colorIndex++));
            foreach (var carRenderer in car.Renderers) carRenderer.SetPropertyBlock(block);
            car.Transform.gameObject.SetActive(true);
            lane.Cars.Add(car);
        }

        private void Release(Lane lane, int index)
        {
            var car = lane.Cars[index];
            lane.Cars.RemoveAt(index);
            car.Transform.gameObject.SetActive(false);
            pool.Push(car);
        }

        private Car CreateCar()
        {
            var instance = Instantiate(carPrefab, transform);
            return new Car { Transform = instance, Renderers = instance.GetComponentsInChildren<Renderer>() };
        }
    }
}
