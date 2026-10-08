using System;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Every tuning value of the minigame. Lanes, bottom to top:
    // 0 grandmas (pickup) | 1 player spawn | roads... | last = goal (drop-off).
    [CreateAssetMenu(fileName = "CR_Settings", menuName = "UnCredibles/Crossy Road/Settings")]
    public sealed class CrossyRoadSettings : ScriptableObject
    {
        [Serializable]
        public struct RoadLane
        {
            [Tooltip("1 = cars go right, -1 = cars go left.")]
            public int direction;
            [Min(0.1f), Tooltip("Lane depths per second.")] public float speed;
            [Min(0.2f)] public float minSpawnInterval;
            [Min(0.2f)] public float maxSpawnInterval;
            [Min(1f), Tooltip("Car length in lane depths.")] public float carLength;
        }

        [Header("Board")]
        [SerializeField, Min(4f), Tooltip("Playable width in metres.")] private float boardWidth = 12f;
        [SerializeField, Min(0.5f), Tooltip("Depth of every lane in metres.")] private float cellSize = 1.5f;
        [SerializeField] private RoadLane[] roadLanes =
        {
            new RoadLane { direction = 1, speed = 3f, minSpawnInterval = 1.6f, maxSpawnInterval = 3f, carLength = 1.5f },
            new RoadLane { direction = -1, speed = 4.5f, minSpawnInterval = 1.4f, maxSpawnInterval = 2.6f, carLength = 1.5f },
            new RoadLane { direction = 1, speed = 2.5f, minSpawnInterval = 2.2f, maxSpawnInterval = 3.5f, carLength = 2.5f },
            new RoadLane { direction = -1, speed = 5.5f, minSpawnInterval = 1.2f, maxSpawnInterval = 2.4f, carLength = 1.5f },
            new RoadLane { direction = 1, speed = 3.5f, minSpawnInterval = 1.5f, maxSpawnInterval = 2.8f, carLength = 2f },
            new RoadLane { direction = -1, speed = 4f, minSpawnInterval = 1.5f, maxSpawnInterval = 2.8f, carLength = 2f },
        };

        [Header("Players")]
        [SerializeField, Min(0.5f), Tooltip("Metres per second while carrying a grandma.")] private float moveSpeed = 5f;
        [SerializeField, Min(1f), Tooltip("Speed multiplier when not carrying a grandma.")]
        private float emptyHandedSpeedMultiplier = 1.3f;
        [SerializeField, Min(0f), Tooltip("Degrees per second the avatar turns towards where it walks.")]
        private float turnSpeed = 900f;
        [SerializeField, Range(0f, 0.9f)] private float inputDeadZone = 0.2f;
        [SerializeField, Min(0.1f), Tooltip("Body radius in metres for cars, players and pickups.")]
        private float playerRadius = 0.4f;
        [SerializeField, Min(0f)] private float walkBobHeight = 0.12f;
        [SerializeField, Min(0f)] private float walkBobFrequency = 12f;
        [SerializeField, Min(0f)] private float respawnDelay = 1f;
        [SerializeField, Min(0f)] private float invulnerableTime = 1.5f;
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f),
            new Color(0.3f, 0.8f, 0.35f), new Color(0.95f, 0.8f, 0.2f),
        };

        [Header("Hit by a car (cartoon)")]
        [SerializeField, Min(1f), Tooltip("How wide the flattened player gets.")] private float squashWidth = 1.6f;
        [SerializeField, Range(0.01f, 1f), Tooltip("How tall the flattened player stays.")] private float squashHeight = 0.08f;
        [SerializeField, Min(0.01f)] private float squashDuration = 0.08f;
        [SerializeField, Min(0f), Tooltip("Seconds at the end of the respawn delay spent shrinking away.")]
        private float vanishDuration = 0.25f;
        [SerializeField, Tooltip("Horizontal speed range of a grandma knocked out of a player's hands.")]
        private Vector2 grandmaLaunchSpeed = new Vector2(9f, 14f);
        [SerializeField, Tooltip("Upward speed range of a knocked-out grandma.")]
        private Vector2 grandmaLaunchUp = new Vector2(9f, 13f);
        [SerializeField, Min(0f), Tooltip("Degrees per second she spins while flying.")] private float grandmaSpinSpeed = 900f;
        [SerializeField, Min(0.1f)] private float grandmaGravity = 20f;

        [Header("Oil slicks")]
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a new vehicle is an oil truck at full difficulty (none at the start).")]
        private float oilTruckChance = 0.08f;
        [SerializeField, Min(0), Tooltip("Oil trucks on the road at the same time.")] private int maxOilTrucks = 1;
        [SerializeField, Min(1), Tooltip("Oil slicks each truck leaks while crossing the board.")] private int oilDropsPerTruck = 2;
        [SerializeField, Min(0.5f)] private float oilLifetime = 8f;
        [SerializeField, Min(0.1f)] private float oilRadius = 1.5f;
        [SerializeField, Min(1)] private int maxOilSlicks = 4;
        [SerializeField, Min(0.1f), Tooltip("Push the stick gives on oil (m/s²). Lower = harder to steer or stop.")]
        private float oilAcceleration = 4f;
        [SerializeField, Range(0f, 5f), Tooltip("Fraction of speed lost per second on oil. 0 = slides forever, like ice.")]
        private float oilFriction = 0.15f;
        [SerializeField, Min(1f), Tooltip("Top speed on oil relative to the normal walking speed.")]
        private float oilMaxSpeedMultiplier = 1.4f;
        [SerializeField, Min(1f), Tooltip("Speed multiplier the moment a player steps on oil.")]
        private float oilSlideBoost = 1.25f;
        [SerializeField, Min(0f), Tooltip("Seconds a player keeps sliding after leaving the oil.")]
        private float oilAfterSlip = 0.6f;

        [Header("Difficulty (grows during the game)")]
        [SerializeField, Min(0.05f), Tooltip("Car speed multiplier at the start of the game.")] private float startSpeedMultiplier = 0.5f;
        [SerializeField, Min(0.05f), Tooltip("Car speed multiplier at the end of the game.")] private float endSpeedMultiplier = 1.2f;
        [SerializeField, Min(0.1f), Tooltip("Time between cars multiplier at the start (higher = fewer cars).")] private float startSpawnMultiplier = 3.5f;
        [SerializeField, Min(0.1f), Tooltip("Time between cars multiplier at the end (lower = more cars).")] private float endSpawnMultiplier = 0.75f;
        [SerializeField, Min(0.1f), Tooltip("1 = linear. Higher = stays easy longer and gets hard near the end.")] private float difficultyRamp = 1f;

        [Header("Final rush")]
        [SerializeField, Min(0f), Tooltip("Last seconds of the game where deliveries are worth more.")] private float finalRushSeconds = 30f;
        [SerializeField, Min(1)] private int finalRushMultiplier = 2;

        [Header("Grandmas")]
        [SerializeField, Min(1), Tooltip("Grandmas spread evenly across the pickup lane.")] private int grandmaCount = 6;
        [SerializeField, Min(0.1f), Tooltip("Distance at which a player grabs a grandma.")] private float pickupRadius = 0.9f;
        [SerializeField, Min(1)] private int pointsPerDelivery = 1;
        [SerializeField, Min(0f)] private float grandmaRespawnDelay = 2f;
        [SerializeField, Tooltip("Teleport back to the spawn lane after a delivery instead of walking back.")]
        private bool returnToSpawnAfterDelivery;
        [SerializeField] private Color grandmaColor = new Color(0.75f, 0.55f, 0.85f);

        [Header("Traffic")]
        [SerializeField, Min(1f), Tooltip("Lane depths outside the board where cars appear and disappear.")]
        private float offscreenMargin = 9f;
        [SerializeField, Tooltip("Plain cars. Avoid black and yellow: those are the oil trucks.")]
        private Color[] carColors =
        {
            new Color(0.95f, 0.45f, 0.1f), new Color(0.15f, 0.7f, 0.85f),
            new Color(0.95f, 0.95f, 0.95f), new Color(0.9f, 0.22f, 0.25f),
        };

        [Header("Lane colors")]
        [SerializeField] private Color pickupLaneColor = new Color(0.55f, 0.75f, 0.45f);
        [SerializeField] private Color spawnLaneColor = new Color(0.45f, 0.65f, 0.4f);
        [SerializeField] private Color roadColor = new Color(0.22f, 0.22f, 0.25f);
        [SerializeField] private Color goalLaneColor = new Color(0.95f, 0.85f, 0.45f);

        public float BoardWidth => boardWidth;
        public float CellSize => cellSize;
        public int RoadCount => roadLanes.Length;
        public int LaneCount => roadLanes.Length + 3;
        public RoadLane GetRoad(int roadIndex) => roadLanes[roadIndex];

        public float MoveSpeed => moveSpeed;
        public float SpeedFor(bool carrying) => carrying ? moveSpeed : moveSpeed * emptyHandedSpeedMultiplier;
        public float TurnSpeed => turnSpeed;
        public float InputDeadZone => inputDeadZone;
        public float PlayerRadius => playerRadius;
        public float WalkBobHeight => walkBobHeight;
        public float WalkBobFrequency => walkBobFrequency;
        public float RespawnDelay => respawnDelay;
        public float InvulnerableTime => invulnerableTime;
        public Color GetPlayerColor(int slotIndex) =>
            playerColors.Length > 0 ? playerColors[slotIndex % playerColors.Length] : Color.white;

        public Vector3 SquashScale => new Vector3(squashWidth, squashHeight, squashWidth);
        public float SquashDuration => squashDuration;
        public float VanishDuration => vanishDuration;
        public Vector2 GrandmaLaunchSpeed => grandmaLaunchSpeed;
        public Vector2 GrandmaLaunchUp => grandmaLaunchUp;
        public float GrandmaSpinSpeed => grandmaSpinSpeed;
        public float GrandmaGravity => grandmaGravity;

        public float OilTruckChance => oilTruckChance;
        public int MaxOilTrucks => maxOilTrucks;
        public int OilDropsPerTruck => oilDropsPerTruck;
        public float OilLifetime => oilLifetime;
        public float OilRadius => oilRadius;
        public int MaxOilSlicks => maxOilSlicks;
        public float OilAcceleration => oilAcceleration;
        public float OilFriction => oilFriction;
        public float OilMaxSpeedMultiplier => oilMaxSpeedMultiplier;
        public float OilSlideBoost => oilSlideBoost;
        public float OilAfterSlip => oilAfterSlip;

        public int GrandmaCount => grandmaCount;
        public float PickupRadius => pickupRadius;
        public int PointsPerDelivery => pointsPerDelivery;
        public float FinalRushSeconds => finalRushSeconds;
        public int FinalRushMultiplier => finalRushMultiplier;

        // 0 = start of the game, 1 = end; returns the curved difficulty used by the traffic.
        public float Difficulty(float progress) => Mathf.Pow(Mathf.Clamp01(progress), difficultyRamp);
        public float SpeedMultiplier(float difficulty) => Mathf.Lerp(startSpeedMultiplier, endSpeedMultiplier, difficulty);
        public float SpawnMultiplier(float difficulty) => Mathf.Lerp(startSpawnMultiplier, endSpawnMultiplier, difficulty);
        public float GrandmaRespawnDelay => grandmaRespawnDelay;
        public bool ReturnToSpawnAfterDelivery => returnToSpawnAfterDelivery;
        public Color GrandmaColor => grandmaColor;

        public float OffscreenMargin => offscreenMargin * cellSize;
        public Color GetCarColor(int index) => carColors.Length > 0 ? carColors[index % carColors.Length] : Color.white;

        public Color PickupLaneColor => pickupLaneColor;
        public Color SpawnLaneColor => spawnLaneColor;
        public Color RoadColor => roadColor;
        public Color GoalLaneColor => goalLaneColor;

        private void OnValidate()
        {
            for (int i = 0; i < roadLanes.Length; i++)
            {
                if (roadLanes[i].direction == 0) roadLanes[i].direction = 1;
                roadLanes[i].direction = Math.Sign(roadLanes[i].direction);
                if (roadLanes[i].maxSpawnInterval < roadLanes[i].minSpawnInterval)
                    roadLanes[i].maxSpawnInterval = roadLanes[i].minSpawnInterval;
            }
        }
    }
}
