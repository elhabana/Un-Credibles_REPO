using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // One grandma per column of the pickup lane. Taken grandmas come back after a delay.
    public sealed class CrossyRoadGrandmas : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private GameObject grandmaPrefab;

        private GameObject[] grandmas = new GameObject[0];
        private float[] respawnTimers = new float[0];

        public void Initialize()
        {
            int columns = board.Columns;
            grandmas = new GameObject[columns];
            respawnTimers = new float[columns];

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, board.Settings.GrandmaColor);
            for (int column = 0; column < columns; column++)
            {
                var position = board.CellToWorld(new Vector2Int(column, CrossyRoadBoard.PickupLane));
                var grandma = Instantiate(grandmaPrefab, position, Quaternion.Euler(0f, 180f, 0f), transform);
                grandma.name = $"Grandma_{column}";
                foreach (var grandmaRenderer in grandma.GetComponentsInChildren<Renderer>())
                    grandmaRenderer.SetPropertyBlock(block);
                grandmas[column] = grandma;
            }
        }

        public bool IsAvailable(int column) =>
            column >= 0 && column < grandmas.Length && grandmas[column].activeSelf;

        public bool TryTake(int column)
        {
            if (!IsAvailable(column)) return false;
            grandmas[column].SetActive(false);
            respawnTimers[column] = -1f; // travelling with a player
            return true;
        }

        // Called when the grandma is delivered or dropped: she reappears in her spot later.
        public void Return(int column)
        {
            if (column < 0 || column >= grandmas.Length || grandmas[column].activeSelf) return;
            respawnTimers[column] = board.Settings.GrandmaRespawnDelay;
        }

        public int NearestAvailable(int fromColumn)
        {
            for (int offset = 0; offset < grandmas.Length; offset++)
            {
                if (IsAvailable(fromColumn - offset)) return fromColumn - offset;
                if (IsAvailable(fromColumn + offset)) return fromColumn + offset;
            }
            return -1;
        }

        public void Tick(float deltaTime)
        {
            for (int column = 0; column < grandmas.Length; column++)
            {
                if (respawnTimers[column] <= 0f) continue;
                respawnTimers[column] -= deltaTime;
                if (respawnTimers[column] <= 0f) grandmas[column].SetActive(true);
            }
        }
    }
}
