using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    public enum LaneType { Pickup, Spawn, Road, Goal }

    // Grid of the minigame: columns along X, lanes along Z, centred on this transform.
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
        public int Columns => settings.Columns;
        public int LaneCount => settings.LaneCount;
        public int GoalLane => settings.LaneCount - 1;
        public float CellSize => settings.CellSize;
        public float HalfWidth => settings.Columns * settings.CellSize * 0.5f;

        public LaneType GetLaneType(int lane)
        {
            if (lane <= PickupLane) return LaneType.Pickup;
            if (lane == SpawnLane) return LaneType.Spawn;
            return lane >= GoalLane ? LaneType.Goal : LaneType.Road;
        }

        // Road lanes map to settings.GetRoad(roadIndex); -1 when the lane is not a road.
        public int RoadIndex(int lane) => GetLaneType(lane) == LaneType.Road ? lane - FirstRoadLane : -1;

        public bool IsInside(Vector2Int cell) =>
            cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < LaneCount;

        public float ColumnToX(int column) => transform.position.x + (column - (Columns - 1) * 0.5f) * CellSize;
        public float LaneToZ(int lane) => transform.position.z + lane * CellSize;
        public Vector3 CellToWorld(Vector2Int cell) => new Vector3(ColumnToX(cell.x), transform.position.y, LaneToZ(cell.y));

        public int WorldToLane(float z) => Mathf.RoundToInt((z - transform.position.z) / CellSize);

        public Vector2Int WorldToCell(Vector3 world)
        {
            int column = Mathf.RoundToInt((world.x - transform.position.x) / CellSize + (Columns - 1) * 0.5f);
            return new Vector2Int(Mathf.Clamp(column, 0, Columns - 1), Mathf.Clamp(WorldToLane(world.z), 0, LaneCount - 1));
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
                tile.transform.localScale = new Vector3(Columns * CellSize, 0.2f, CellSize);

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
                    new Vector3(Columns * CellSize, 0.1f, CellSize * 0.96f));
            }
        }
    }
}
