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
            [Min(0.1f), Tooltip("Cells per second.")] public float speed;
            [Min(0.2f)] public float minSpawnInterval;
            [Min(0.2f)] public float maxSpawnInterval;
            [Min(1f), Tooltip("Car length in cells.")] public float carLength;
        }

        [Header("Board")]
        [SerializeField, Min(5)] private int columns = 13;
        [SerializeField, Min(0.5f)] private float cellSize = 1.5f;
        [SerializeField] private RoadLane[] roadLanes =
        {
            new RoadLane { direction = 1, speed = 3f, minSpawnInterval = 1.6f, maxSpawnInterval = 3f, carLength = 1.5f },
            new RoadLane { direction = -1, speed = 4.5f, minSpawnInterval = 1.4f, maxSpawnInterval = 2.6f, carLength = 1.5f },
            new RoadLane { direction = 1, speed = 2.5f, minSpawnInterval = 2.2f, maxSpawnInterval = 3.5f, carLength = 2.5f },
            new RoadLane { direction = -1, speed = 5.5f, minSpawnInterval = 1.2f, maxSpawnInterval = 2.4f, carLength = 1.5f },
            new RoadLane { direction = 1, speed = 3.5f, minSpawnInterval = 1.5f, maxSpawnInterval = 2.8f, carLength = 2f },
        };

        [Header("Players")]
        [SerializeField, Min(0.05f)] private float hopDuration = 0.15f;
        [SerializeField, Min(0f)] private float hopHeight = 0.4f;
        [SerializeField, Range(0.1f, 0.9f)] private float moveThreshold = 0.5f;
        [SerializeField, Range(0.1f, 0.5f), Tooltip("Player hit box half width, in cells.")] private float playerHalfWidth = 0.3f;
        [SerializeField, Min(0f)] private float respawnDelay = 1f;
        [SerializeField, Min(0f)] private float invulnerableTime = 1.5f;
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f),
            new Color(0.3f, 0.8f, 0.35f), new Color(0.95f, 0.8f, 0.2f),
        };

        [Header("Grandmas")]
        [SerializeField, Min(1)] private int pointsPerDelivery = 1;
        [SerializeField, Min(0f)] private float grandmaRespawnDelay = 2f;
        [SerializeField, Tooltip("Teleport back to the spawn lane after a delivery instead of walking back.")]
        private bool returnToSpawnAfterDelivery;
        [SerializeField] private Color grandmaColor = new Color(0.75f, 0.55f, 0.85f);

        [Header("Traffic")]
        [SerializeField, Min(1f), Tooltip("Cells outside the board where cars appear and disappear.")]
        private float offscreenMargin = 6f;
        [SerializeField] private Color[] carColors =
        {
            new Color(0.95f, 0.45f, 0.1f), new Color(0.15f, 0.7f, 0.85f),
            new Color(0.95f, 0.95f, 0.95f), new Color(0.2f, 0.2f, 0.25f),
        };

        [Header("Lane colors")]
        [SerializeField] private Color pickupLaneColor = new Color(0.55f, 0.75f, 0.45f);
        [SerializeField] private Color spawnLaneColor = new Color(0.45f, 0.65f, 0.4f);
        [SerializeField] private Color roadColor = new Color(0.22f, 0.22f, 0.25f);
        [SerializeField] private Color goalLaneColor = new Color(0.95f, 0.85f, 0.45f);

        public int Columns => columns;
        public float CellSize => cellSize;
        public int RoadCount => roadLanes.Length;
        public int LaneCount => roadLanes.Length + 3;
        public RoadLane GetRoad(int roadIndex) => roadLanes[roadIndex];

        public float HopDuration => hopDuration;
        public float HopHeight => hopHeight;
        public float MoveThreshold => moveThreshold;
        public float PlayerHalfWidth => playerHalfWidth * cellSize;
        public float RespawnDelay => respawnDelay;
        public float InvulnerableTime => invulnerableTime;
        public Color GetPlayerColor(int slotIndex) =>
            playerColors.Length > 0 ? playerColors[slotIndex % playerColors.Length] : Color.white;

        public int PointsPerDelivery => pointsPerDelivery;
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
