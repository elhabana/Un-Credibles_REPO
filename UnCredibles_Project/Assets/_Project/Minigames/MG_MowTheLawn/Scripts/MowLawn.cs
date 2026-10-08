using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // The grass: a grid of cells with a height from 0 (just cut) to 1 (fully grown), drawn as a
    // texture on a flat mesh built here. Cut cells grow back over time. No physics, no colliders.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MowLawn : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private const float RedrawInterval = 0.1f; // regrowth is slow; cuts redraw at once
        private const int StripeCells = 5;

        private MowTheLawnSettings settings;
        private float[] heights;
        private Color32[] pixels;
        private Texture2D texture;
        private Mesh mesh;
        private int columns;
        private int rows;
        private float halfWidth;
        private float halfDepth;
        private float redrawTimer;
        private bool dirty;

        public float HalfWidth => halfWidth;
        public float HalfDepth => halfDepth;
        public Vector3 Center => transform.position;

        public void Initialize(MowTheLawnSettings lawnSettings)
        {
            settings = lawnSettings;
            columns = Mathf.Max(2, Mathf.RoundToInt(settings.LawnWidth / settings.CellSize));
            rows = Mathf.Max(2, Mathf.RoundToInt(settings.LawnDepth / settings.CellSize));
            halfWidth = columns * settings.CellSize * 0.5f;
            halfDepth = rows * settings.CellSize * 0.5f;

            heights = new float[columns * rows];
            for (int i = 0; i < heights.Length; i++) heights[i] = 1f;
            pixels = new Color32[heights.Length];

            texture = new Texture2D(columns, rows, TextureFormat.RGBA32, false)
            {
                name = "LawnTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            BuildMesh();

            var block = new MaterialPropertyBlock();
            block.SetTexture(BaseMapId, texture);
            GetComponent<MeshRenderer>().SetPropertyBlock(block);
            Redraw();
        }

        public void Tick(float deltaTime)
        {
            float growth = deltaTime / settings.RegrowSeconds;
            for (int i = 0; i < heights.Length; i++)
                if (heights[i] < 1f) heights[i] = Mathf.Min(1f, heights[i] + growth);

            redrawTimer -= deltaTime;
            if (dirty || redrawTimer <= 0f) Redraw();
        }

        // Cuts every cell inside the circle and returns how much grass was collected
        // (1 = one fully grown cell). Grass that is still too short is left alone.
        public float Cut(Vector3 worldCenter, float radius)
        {
            ToCell(worldCenter, out float cx, out float cz);
            float cellRadius = radius / settings.CellSize;
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - cellRadius));
            int maxX = Mathf.Min(columns - 1, Mathf.CeilToInt(cx + cellRadius));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(cz - cellRadius));
            int maxZ = Mathf.Min(rows - 1, Mathf.CeilToInt(cz + cellRadius));

            float collected = 0f;
            float radiusSqr = cellRadius * cellRadius;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - cx, dz = z + 0.5f - cz;
                    if (dx * dx + dz * dz > radiusSqr) continue;
                    int index = z * columns + x;
                    if (heights[index] < settings.MinCutHeight) continue;
                    collected += heights[index];
                    heights[index] = 0f;
                    dirty = true;
                }
            }
            return collected;
        }

        public float GetHeight(Vector3 worldPosition)
        {
            ToCell(worldPosition, out float cx, out float cz);
            int x = Mathf.Clamp(Mathf.FloorToInt(cx), 0, columns - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(cz), 0, rows - 1);
            return heights[z * columns + x];
        }

        // Keeps a circle of the given radius inside the lawn.
        public Vector3 ClampInside(Vector3 position, float radius)
        {
            var center = Center;
            position.x = Mathf.Clamp(position.x, center.x - halfWidth + radius, center.x + halfWidth - radius);
            position.z = Mathf.Clamp(position.z, center.z - halfDepth + radius, center.z + halfDepth - radius);
            return position;
        }

        // Random point of the lawn, kept `margin` away from the edges.
        public Vector3 RandomPoint(float margin)
        {
            var center = Center;
            return new Vector3(
                center.x + Random.Range(-halfWidth + margin, halfWidth - margin),
                center.y,
                center.z + Random.Range(-halfDepth + margin, halfDepth - margin));
        }

        private void ToCell(Vector3 worldPosition, out float cx, out float cz)
        {
            var local = worldPosition - Center;
            cx = (local.x + halfWidth) / settings.CellSize;
            cz = (local.z + halfDepth) / settings.CellSize;
        }

        // Mown lawn look: alternating stripes of green, brown where it was just cut.
        private void Redraw()
        {
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = z * columns + x;
                    var grass = (x / StripeCells) % 2 == 0 ? settings.TallGrassColor : settings.StripeGrassColor;
                    float height = heights[index];
                    pixels[index] = Color.Lerp(settings.CutColor, grass, height * height * (3f - 2f * height));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            dirty = false;
            redrawTimer = RedrawInterval;
        }

        private void BuildMesh()
        {
            mesh = new Mesh { name = "LawnMesh" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfDepth), new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(halfWidth, 0f, halfDepth), new Vector3(halfWidth, 0f, -halfDepth),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (mesh != null) Destroy(mesh);
        }
    }
}
