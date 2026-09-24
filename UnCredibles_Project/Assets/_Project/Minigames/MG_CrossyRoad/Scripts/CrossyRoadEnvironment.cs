using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Pure decoration so the board fills the camera: lanes continue outside the playable area,
    // extra grass rows, road markings, curbs, trees and rocks. Built once and never updated.
    // Gameplay does not depend on it; real art can replace it entirely.
    public sealed class CrossyRoadEnvironment : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private Material sharedMaterial;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;

        [Header("Size")]
        [SerializeField, Min(0f), Tooltip("World units of fake ground on each side of the board.")]
        private float sideExtent = 24f;
        [SerializeField, Min(0)] private int rowsBelow = 5;
        [SerializeField, Min(0)] private int rowsAbove = 10;

        [Header("Look")]
        [SerializeField, Range(0.3f, 1f), Tooltip("Darkens lanes outside the playable columns.")]
        private float outsideTint = 0.75f;
        [SerializeField] private Color grassA = new Color(0.52f, 0.74f, 0.4f);
        [SerializeField] private Color grassB = new Color(0.47f, 0.68f, 0.36f);
        [SerializeField] private Color laneMarkingColor = new Color(0.92f, 0.92f, 0.85f);
        [SerializeField] private Color curbColor = new Color(0.7f, 0.7f, 0.72f);
        [SerializeField] private Color trunkColor = new Color(0.45f, 0.3f, 0.2f);
        [SerializeField] private Color[] foliageColors =
        {
            new Color(0.25f, 0.55f, 0.25f), new Color(0.3f, 0.62f, 0.28f), new Color(0.2f, 0.47f, 0.22f),
        };
        [SerializeField] private Color rockColor = new Color(0.55f, 0.55f, 0.58f);

        [Header("Props")]
        [SerializeField, Range(0f, 1f)] private float treeDensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float rockDensity = 0.08f;
        [SerializeField] private int seed = 1234;

        private MaterialPropertyBlock block;
        private System.Random random;
        private Mesh cubeMesh;

        private void Start() => Build();

        public void Build()
        {
            block = new MaterialPropertyBlock();
            random = new System.Random(seed); // same layout every time
            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            BuildLaneExtensions();
            BuildExtraRows();
            BuildRoadDetails();
            BuildProps();
        }

        private void BuildLaneExtensions()
        {
            float offset = board.HalfWidth + sideExtent * 0.5f;
            for (int lane = 0; lane < board.LaneCount; lane++)
            {
                var color = board.LaneColor(lane) * outsideTint;
                color.a = 1f;
                var size = new Vector3(sideExtent, 0.2f, board.CellSize);
                CreateBlock($"Lane_{lane}_Left", At(-offset, -0.1f, board.LaneToZ(lane)), size, color);
                CreateBlock($"Lane_{lane}_Right", At(offset, -0.1f, board.LaneToZ(lane)), size, color);
            }
        }

        private void BuildExtraRows()
        {
            float width = (board.HalfWidth + sideExtent) * 2f;
            for (int i = 1; i <= rowsBelow; i++) CreateGrassRow(-i, width);
            for (int i = 1; i <= rowsAbove; i++) CreateGrassRow(board.GoalLane + i, width);
        }

        private void CreateGrassRow(int lane, float width) =>
            CreateBlock($"Grass_{lane}", At(0f, -0.1f, board.LaneToZ(lane)),
                new Vector3(width, 0.2f, board.CellSize), lane % 2 == 0 ? grassA : grassB);

        private void BuildRoadDetails()
        {
            float cell = board.CellSize;
            float width = (board.HalfWidth + sideExtent) * 2f;

            // Curbs where the grass meets the road.
            float firstRoadZ = board.LaneToZ(CrossyRoadBoard.FirstRoadLane) - cell * 0.5f;
            float lastRoadZ = board.LaneToZ(board.GoalLane - 1) + cell * 0.5f;
            CreateBlock("Curb_Bottom", At(0f, 0.04f, firstRoadZ), new Vector3(width, 0.08f, 0.2f), curbColor);
            CreateBlock("Curb_Top", At(0f, 0.04f, lastRoadZ), new Vector3(width, 0.08f, 0.2f), curbColor);

            // Dashed lines between two consecutive road lanes.
            float dashLength = cell * 0.5f;
            int dashes = Mathf.CeilToInt(width / (cell * 1.5f));
            for (int lane = CrossyRoadBoard.FirstRoadLane; lane < board.GoalLane - 1; lane++)
            {
                float z = board.LaneToZ(lane) + cell * 0.5f;
                for (int d = 0; d < dashes; d++)
                {
                    float x = -width * 0.5f + d * cell * 1.5f;
                    CreateBlock("Dash", At(x, 0.005f, z), new Vector3(dashLength, 0.01f, 0.1f), laneMarkingColor);
                }
            }
        }

        private void BuildProps()
        {
            int extraColumns = Mathf.CeilToInt(sideExtent / board.CellSize);
            for (int lane = -rowsBelow; lane <= board.GoalLane + rowsAbove; lane++)
            {
                bool insideBoard = lane >= 0 && lane <= board.GoalLane;
                if (insideBoard && board.GetLaneType(lane) == LaneType.Road) continue;

                for (int column = -extraColumns; column < board.Columns + extraColumns; column++)
                {
                    bool playableCell = insideBoard && column >= 0 && column < board.Columns;
                    if (playableCell) continue;

                    // Rows in front of the camera only get low props so they never hide the players.
                    bool lowOnly = lane < 0;
                    var position = new Vector3(board.ColumnToX(column), board.transform.position.y, board.LaneToZ(lane));
                    double roll = random.NextDouble();
                    if (!lowOnly && roll < treeDensity) PlaceTree(position);
                    else if (roll < treeDensity + rockDensity) PlaceRock(position);
                }
            }
        }

        private void PlaceTree(Vector3 position)
        {
            var tree = Instantiate(treePrefab, position, Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f), transform);
            tree.transform.localScale = Vector3.one * Range(0.8f, 1.3f);
            var foliage = foliageColors.Length > 0 ? foliageColors[random.Next(foliageColors.Length)] : Color.green;
            foreach (var propRenderer in tree.GetComponentsInChildren<Renderer>())
                Paint(propRenderer, propRenderer.name == "Foliage" ? foliage : trunkColor);
        }

        private void PlaceRock(Vector3 position)
        {
            var rock = Instantiate(rockPrefab, position, Quaternion.Euler(0f, Range(0f, 360f), 0f), transform);
            rock.transform.localScale = Vector3.one * Range(0.7f, 1.3f);
            foreach (var propRenderer in rock.GetComponentsInChildren<Renderer>()) Paint(propRenderer, rockColor);
        }

        // Cube without collider, sharing one material; the color goes in a property block.
        private void CreateBlock(string blockName, Vector3 position, Vector3 scale, Color color)
        {
            var go = new GameObject(blockName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            var blockRenderer = go.AddComponent<MeshRenderer>();
            blockRenderer.sharedMaterial = sharedMaterial;
            Paint(blockRenderer, color);
        }

        private void Paint(Renderer target, Color color)
        {
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }

        // X and Y relative to the board, Z already in world space (LaneToZ).
        private Vector3 At(float x, float y, float z) =>
            new Vector3(board.transform.position.x + x, board.transform.position.y + y, z);

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
