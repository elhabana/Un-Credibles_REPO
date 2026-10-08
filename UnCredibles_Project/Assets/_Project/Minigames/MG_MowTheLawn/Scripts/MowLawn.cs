using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // The grass: a grid of cells with a height from 0 (just cut) to 1 (fully grown).
    // Each cell is drawn twice: a colour on the ground texture (brown soil when cut) and a tuft of
    // real 3D blades, GPU instanced, as tall as the cell. Cut grass disappears and grows back.
    // The grid also covers a margin around the playable area so grass reaches the screen edges.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MowLawn : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private const float RedrawInterval = 0.1f; // regrowth is slow; cuts redraw at once
        private const int StripeCells = 6;
        private const int BatchSize = 1023;        // max instances per instanced draw
        private const float MinVisibleHeight = 0.06f;

        [SerializeField, Tooltip("Material of the 3D grass blades (GPU instancing on).")]
        private Material bladeMaterial;

        private MowTheLawnSettings settings;
        private float[] heights;
        private Color32[] pixels;
        private Color32[] baseColors;   // per-cell grass colour with a little noise
        private Matrix4x4[] tuftBases;  // per-cell position, random turn and width
        private Matrix4x4[] tuftMatrices;
        private int tuftCount;
        private Texture2D texture;
        private Mesh groundMesh;
        private Mesh tuftMesh;
        private RenderParams bladeParams;
        private int columns;
        private int rows;
        private float gridHalfWidth;
        private float gridHalfDepth;
        private float halfWidth;
        private float halfDepth;
        private float redrawTimer;
        private bool dirty;

        // Playable area (mowers stay inside it).
        public float HalfWidth => halfWidth;
        public float HalfDepth => halfDepth;
        public Vector3 Center => transform.position;

        public void Initialize(MowTheLawnSettings lawnSettings)
        {
            settings = lawnSettings;
            halfWidth = settings.LawnWidth * 0.5f;
            halfDepth = settings.LawnDepth * 0.5f;
            columns = Mathf.Max(2, Mathf.RoundToInt((settings.LawnWidth + settings.VisualMargin * 2f) / settings.CellSize));
            rows = Mathf.Max(2, Mathf.RoundToInt((settings.LawnDepth + settings.VisualMargin * 2f) / settings.CellSize));
            gridHalfWidth = columns * settings.CellSize * 0.5f;
            gridHalfDepth = rows * settings.CellSize * 0.5f;

            int cells = columns * rows;
            heights = new float[cells];
            for (int i = 0; i < cells; i++) heights[i] = 1f;
            pixels = new Color32[cells];
            BuildCellLooks();

            texture = new Texture2D(columns, rows, TextureFormat.RGBA32, false)
            {
                name = "LawnTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            BuildGroundMesh();
            tuftMesh = BuildTuftMesh(settings.BladesPerCell);
            tuftMatrices = new Matrix4x4[cells];
            if (bladeMaterial != null)
                bladeParams = new RenderParams(bladeMaterial)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = new Bounds(Center, new Vector3(gridHalfWidth * 2f, 2f, gridHalfDepth * 2f)),
                };

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
            DrawBlades();
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

        // Keeps a circle of the given radius inside the playable area.
        public Vector3 ClampInside(Vector3 position, float radius)
        {
            var center = Center;
            position.x = Mathf.Clamp(position.x, center.x - halfWidth + radius, center.x + halfWidth - radius);
            position.z = Mathf.Clamp(position.z, center.z - halfDepth + radius, center.z + halfDepth - radius);
            return position;
        }

        // Random point of the playable area, kept `margin` away from its edges.
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
            cx = (local.x + gridHalfWidth) / settings.CellSize;
            cz = (local.z + gridHalfDepth) / settings.CellSize;
        }

        // Ground colour: mown stripes of green with some noise, soil where it was just cut.
        // Tufts: one instance per cell that is tall enough to be seen, scaled by its height.
        private void Redraw()
        {
            tuftCount = 0;
            float bladeHeight = settings.BladeHeight;
            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                float shaped = height * height * (3f - 2f * height);
                pixels[i] = Color32.Lerp(settings.CutColor, baseColors[i], shaped);
                if (height < MinVisibleHeight) continue;
                tuftMatrices[tuftCount++] = tuftBases[i] * Matrix4x4.Scale(new Vector3(1f, bladeHeight * shaped, 1f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            dirty = false;
            redrawTimer = RedrawInterval;
        }

        private void DrawBlades()
        {
            if (bladeMaterial == null || tuftCount == 0) return;
            for (int start = 0; start < tuftCount; start += BatchSize)
                Graphics.RenderMeshInstanced(bladeParams, tuftMesh, 0, tuftMatrices, Mathf.Min(BatchSize, tuftCount - start), start);
        }

        // Fixed random look per cell (same on every machine is not needed: it is only visual).
        private void BuildCellLooks()
        {
            int cells = columns * rows;
            baseColors = new Color32[cells];
            tuftBases = new Matrix4x4[cells];
            var origin = Center;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = z * columns + x;
                    var stripe = (x / StripeCells) % 2 == 0 ? settings.TallGrassColor : settings.StripeGrassColor;
                    float noise = Random.Range(-0.05f, 0.05f);
                    baseColors[index] = new Color(stripe.r + noise, stripe.g + noise * 1.4f, stripe.b + noise, 1f);

                    var jitter = Random.insideUnitCircle * settings.CellSize * 0.25f;
                    var position = new Vector3(
                        origin.x - gridHalfWidth + (x + 0.5f) * settings.CellSize + jitter.x,
                        origin.y,
                        origin.z - gridHalfDepth + (z + 0.5f) * settings.CellSize + jitter.y);
                    float width = settings.CellSize * Random.Range(0.9f, 1.25f);
                    tuftBases[index] = Matrix4x4.TRS(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(width, 1f, width));
                }
            }
        }

        private void BuildGroundMesh()
        {
            groundMesh = new Mesh { name = "LawnGround" };
            groundMesh.vertices = new[]
            {
                new Vector3(-gridHalfWidth, 0f, -gridHalfDepth), new Vector3(-gridHalfWidth, 0f, gridHalfDepth),
                new Vector3(gridHalfWidth, 0f, gridHalfDepth), new Vector3(gridHalfWidth, 0f, -gridHalfDepth),
            };
            groundMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            groundMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            groundMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            groundMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = groundMesh;
        }

        // A tuft of thin triangular blades inside a 1x1 cell, 1 unit tall (scaled per instance).
        private static Mesh BuildTuftMesh(int blades)
        {
            var vertices = new Vector3[blades * 3];
            var normals = new Vector3[blades * 3];
            var triangles = new int[blades * 3];
            for (int b = 0; b < blades; b++)
            {
                var root = new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f));
                float angle = Random.Range(0f, Mathf.PI);
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.06f;
                var lean = new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
                float tall = Random.Range(0.7f, 1f);

                int v = b * 3;
                vertices[v] = root - side;
                vertices[v + 1] = root + lean + Vector3.up * tall;
                vertices[v + 2] = root + side;
                normals[v] = normals[v + 1] = normals[v + 2] = Vector3.up; // lit like the ground below
                triangles[v] = v;
                triangles[v + 1] = v + 1;
                triangles[v + 2] = v + 2;
            }
            var mesh = new Mesh { name = "GrassTuft", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (groundMesh != null) Destroy(groundMesh);
            if (tuftMesh != null) Destroy(tuftMesh);
        }
    }
}
