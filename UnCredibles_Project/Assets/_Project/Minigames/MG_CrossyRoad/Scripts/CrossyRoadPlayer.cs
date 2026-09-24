using System;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Avatar of one player: hops cell by cell reading only IPlayerInput.
    // Ticked by CrossyRoadController, it has no Update of its own.
    public sealed class CrossyRoadPlayer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float BlinkInterval = 0.1f;

        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField] private GameObject carriedGrandma;

        private CrossyRoadBoard board;
        private CrossyRoadSettings settings;
        private Vector3 hopFrom;
        private Vector3 hopTo;
        private float hopProgress;
        private float invulnerableTimer;

        public PlayerSlot Slot { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int SpawnColumn { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsHopping { get; private set; }
        public bool IsCarrying { get; private set; }
        public int CarriedGrandmaColumn { get; private set; } = -1;
        public bool IsInvulnerable => invulnerableTimer > 0f;

        public event Action<CrossyRoadPlayer> CellReached;

        public void Setup(PlayerSlot slot, CrossyRoadBoard gameBoard, Vector2Int spawnCell, Color color, Color grandmaColor)
        {
            Slot = slot;
            board = gameBoard;
            settings = gameBoard.Settings;
            SpawnColumn = spawnCell.x;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var body in bodyRenderers) body.SetPropertyBlock(block);
            if (carriedGrandma != null)
            {
                block.SetColor(BaseColorId, grandmaColor);
                foreach (var grandmaRenderer in carriedGrandma.GetComponentsInChildren<Renderer>(true))
                    grandmaRenderer.SetPropertyBlock(block);
            }

            SetCarrying(-1);
            Respawn(spawnCell, 0f);
        }

        public void Tick(float deltaTime, Func<Vector2Int, CrossyRoadPlayer, bool> isMoveBlocked)
        {
            if (!IsAlive) return;
            TickInvulnerability(deltaTime);

            if (IsHopping)
            {
                hopProgress += deltaTime / settings.HopDuration;
                float t = Mathf.Clamp01(hopProgress);
                transform.position = Vector3.Lerp(hopFrom, hopTo, t);
                visual.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * settings.HopHeight, 0f);
                if (t < 1f) return;
                IsHopping = false;
                CellReached?.Invoke(this);
                return;
            }

            var direction = ReadDirection();
            if (direction == Vector2Int.zero) return;

            transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
            var target = Cell + direction;
            if (!board.IsInside(target) || isMoveBlocked(target, this)) return;

            // The cell is claimed when the hop starts so nobody else can jump into it.
            Cell = target;
            hopFrom = transform.position;
            hopTo = board.CellToWorld(target);
            hopProgress = 0f;
            IsHopping = true;
        }

        public void SetCarrying(int grandmaColumn)
        {
            CarriedGrandmaColumn = grandmaColumn;
            IsCarrying = grandmaColumn >= 0;
            if (carriedGrandma != null) carriedGrandma.SetActive(IsCarrying);
        }

        public void Kill()
        {
            IsAlive = false;
            IsHopping = false;
            SetVisible(false);
        }

        public void Respawn(Vector2Int cell, float invulnerableSeconds)
        {
            Cell = cell;
            IsHopping = false;
            IsAlive = true;
            transform.SetPositionAndRotation(board.CellToWorld(cell), Quaternion.identity);
            visual.localPosition = Vector3.zero;
            invulnerableTimer = invulnerableSeconds;
            SetVisible(true);
        }

        // Dominant axis of the stick/keys, so the avatar never moves diagonally.
        private Vector2Int ReadDirection()
        {
            var move = Slot.Input != null ? Slot.Input.Move : Vector2.zero;
            if (move.sqrMagnitude < settings.MoveThreshold * settings.MoveThreshold) return Vector2Int.zero;
            return Mathf.Abs(move.x) > Mathf.Abs(move.y)
                ? new Vector2Int(move.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, move.y > 0f ? 1 : -1);
        }

        private void TickInvulnerability(float deltaTime)
        {
            if (invulnerableTimer <= 0f) return;
            invulnerableTimer -= deltaTime;
            bool visible = invulnerableTimer <= 0f || Mathf.FloorToInt(invulnerableTimer / BlinkInterval) % 2 == 0;
            visual.gameObject.SetActive(visible);
        }

        private void SetVisible(bool visible) => visual.gameObject.SetActive(visible);
    }
}
