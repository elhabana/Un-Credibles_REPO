using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    public enum LaneType { Pickup, Spawn, Road, Goal }

    // Playable area: free movement along X, lanes stacked along Z, centred on this transform.
    public sealed class CrossyRoadBoard : MonoBehaviour
    {
        public const int PickupLane = 0;
        public const int SpawnLane = 1;
        public const int FirstRoadLane = 2;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CrossyRoadSettings settings;
        [SerializeField, Tooltip("Shared material for the lane tiles; colors come from the settings.")]
        private Material laneMaterial;

        private bool built;

        public CrossyRoadSettings Settings => settings;
        public int LaneCount => settings.LaneCount;
        public int GoalLane => settings.LaneCount - 1;
        public float CellSize => settings.CellSize;
        public float HalfWidth => settings.BoardWidth * 0.5f;
        public float MinZ => LaneToZ(PickupLane) - CellSize * 0.5f;
        public float MaxZ => LaneToZ(GoalLane) + CellSize * 0.5f;

        public LaneType GetLaneType(int lane)
        {
            if (lane <= PickupLane) return LaneType.Pickup;
            if (lane == SpawnLane) return LaneType.Spawn;
            return lane >= GoalLane ? LaneType.Goal : LaneType.Road;
        }

        // Road lanes map to settings.GetRoad(roadIndex); -1 when the lane is not a road.
        public int RoadIndex(int lane) => GetLaneType(lane) == LaneType.Road ? lane - FirstRoadLane : -1;

        public float LaneToZ(int lane) => transform.position.z + lane * CellSize;
        public int WorldToLane(float z) => Mathf.RoundToInt((z - transform.position.z) / CellSize);

        // X of the i-th of `count` points spread evenly across the board (grandmas, spawns...).
        public float SlotToX(int index, int count) =>
            transform.position.x - HalfWidth + (index + 0.5f) * (settings.BoardWidth / count);

        public Vector3 ClampInside(Vector3 position, float radius)
        {
            position.x = Mathf.Clamp(position.x, transform.position.x - HalfWidth + radius, transform.position.x + HalfWidth - radius);
            position.z = Mathf.Clamp(position.z, MinZ + radius, MaxZ - radius);
            return position;
        }

        // Creates one tile per lane, once. Replace with real art later without touching the logic.
        public void Build()
        {
            if (built) return;
            built = true;

            var block = new MaterialPropertyBlock();
            for (int lane = 0; lane < LaneCount; lane++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(tile.GetComponent<Collider>());
                tile.name = $"Lane_{lane}_{GetLaneType(lane)}";
                tile.transform.SetParent(transform, false);
                tile.transform.position = new Vector3(transform.position.x, transform.position.y - 0.1f, LaneToZ(lane));
                tile.transform.localScale = new Vector3(settings.BoardWidth, 0.2f, CellSize);

                var tileRenderer = tile.GetComponent<Renderer>();
                if (laneMaterial != null) tileRenderer.sharedMaterial = laneMaterial;
                block.SetColor(BaseColorId, LaneColor(lane));
                tileRenderer.SetPropertyBlock(block);
            }
        }

        public Color LaneColor(int lane) => GetLaneType(lane) switch
        {
            LaneType.Pickup => settings.PickupLaneColor,
            LaneType.Spawn => settings.SpawnLaneColor,
            LaneType.Goal => settings.GoalLaneColor,
            _ => settings.RoadColor,
        };

        private void OnDrawGizmos()
        {
            if (settings == null) return;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                var color = LaneColor(lane);
                color.a = 0.5f;
                Gizmos.color = color;
                Gizmos.DrawCube(new Vector3(transform.position.x, transform.position.y - 0.05f, LaneToZ(lane)),
                    new Vector3(settings.BoardWidth, 0.1f, CellSize * 0.96f));
            }
        }
    }
}
