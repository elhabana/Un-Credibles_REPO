using System.Collections.Generic;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // A ride-on mower. It always drives forward and steers towards the stick, cuts the grass
    // under its blade and drags its bags behind it like a tail, following the path it drove.
    // Ticked by MowTheLawnController, it has no Update of its own.
    public sealed class MowerPlayer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float PathStep = 0.1f;          // distance between recorded path points
        private const float RemoteSnapDistance = 3f;

        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField, Tooltip("Spins while driving.")] private Transform blade;
        [SerializeField, Tooltip("Grows on Y as the next bag fills up.")] private Transform fillIndicator;

        private readonly List<Vector3> path = new List<Vector3>(256); // [0] = newest
        private readonly List<MowBagView> tail = new List<MowBagView>(32);
        private readonly List<Vector3> tailPositions = new List<Vector3>(32);
        private MowTheLawnSettings settings;
        private MowBags bags;
        private Color color;
        private float clippings;
        private float bobTime;
        private Vector3 fillScale;
        private Vector3 remotePosition;
        private float remoteYaw;
        private bool hasRemote;

        public PlayerSlot Slot { get; private set; }
        public int BagCount => tail.Count;
        public float Fill => clippings / settings.CellsPerBag;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 BladePosition => transform.position + transform.forward * settings.BladeOffset;
        public IReadOnlyList<Vector3> BagPositions => tailPositions;

        public void Setup(PlayerSlot slot, MowTheLawnSettings mowSettings, MowBags bagPool, Color playerColor)
        {
            Slot = slot;
            settings = mowSettings;
            bags = bagPool;
            color = playerColor;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var body in bodyRenderers) body.SetPropertyBlock(block);
            if (fillIndicator != null) fillScale = fillIndicator.localScale;

            path.Clear();
            path.Add(transform.position);
            UpdateFillIndicator();
        }

        // Host: drive with the input, cut grass and fill bags.
        public void Tick(float deltaTime, MowLawn lawn)
        {
            var input = Slot.Input != null ? Slot.Input.Move : Vector2.zero;
            bool steering = input.sqrMagnitude > 0.04f;
            if (steering)
            {
                var target = Quaternion.LookRotation(new Vector3(input.x, 0f, input.y));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, settings.TurnSpeed * deltaTime);
            }

            float speed = settings.Speed * settings.SpeedFactor(BagCount) * (steering ? 1f : settings.IdleSpeedFactor);
            var next = lawn.ClampInside(transform.position + transform.forward * (speed * deltaTime), settings.MowerRadius);
            transform.position = next;

            clippings += lawn.Cut(BladePosition, settings.BladeRadius);
            while (clippings >= settings.CellsPerBag)
            {
                clippings -= settings.CellsPerBag;
                AddBags(1);
            }

            AfterMove(deltaTime);
        }

        // Pushed by another mower.
        public void MoveTo(Vector3 position) => transform.position = position;

        public void AddBags(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var view = bags.Rent();
                view.SetTint(color);
                // A new bag starts where the tail ends and slides into place.
                var end = tailPositions.Count > 0 ? tailPositions[tailPositions.Count - 1] : transform.position;
                view.transform.position = end;
                tail.Add(view);
            }
            PlaceTail();
        }

        // A rival cut the tail at `index`: that bag and all behind it fall off. Returns how many.
        public int CutTailAt(int index, List<Vector3> droppedPositions)
        {
            int count = 0;
            for (int i = tail.Count - 1; i >= index; i--)
            {
                droppedPositions?.Add(tail[i].transform.position);
                bags.Release(tail[i]);
                tail.RemoveAt(i);
                count++;
            }
            PlaceTail();
            return count;
        }

        // ---------- Online client ----------

        public void ApplyRemote(Vector3 position, float yaw, int bagCount, float fill)
        {
            if (!hasRemote || (position - transform.position).sqrMagnitude > RemoteSnapDistance * RemoteSnapDistance)
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                path.Clear();
                path.Add(position);
            }
            remotePosition = position;
            remoteYaw = yaw;
            hasRemote = true;
            clippings = fill * settings.CellsPerBag;

            if (bagCount > tail.Count) AddBags(bagCount - tail.Count);
            else if (bagCount < tail.Count) CutTailAt(bagCount, null);
            UpdateFillIndicator();
        }

        // Follows the host smoothly and mows the grass it drives over (only visual here).
        public void TickRemote(float deltaTime, MowLawn lawn)
        {
            if (hasRemote)
            {
                float blend = Mathf.Min(1f, 12f * deltaTime);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, remotePosition, blend),
                    Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, remoteYaw, 0f), blend));
                lawn.Cut(BladePosition, settings.BladeRadius);
            }
            AfterMove(deltaTime);
        }

        // ---------- Shared ----------

        private void AfterMove(float deltaTime)
        {
            RecordPath();
            PlaceTail();
            UpdateFillIndicator();

            // A little engine rumble and a spinning blade.
            bobTime += deltaTime * 18f;
            if (visual != null) visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobTime)) * 0.04f, 0f);
            if (blade != null) blade.Rotate(0f, 900f * deltaTime, 0f, Space.Self);
        }

        private void RecordPath()
        {
            var position = transform.position;
            if (path.Count == 0 || (position - path[0]).sqrMagnitude >= PathStep * PathStep) path.Insert(0, position);
            else path[0] = position;

            // Only keep the path the tail needs.
            int needed = Mathf.CeilToInt((tail.Count + 2) * settings.BagSpacing / PathStep) + 4;
            if (path.Count > needed) path.RemoveRange(needed, path.Count - needed);
        }

        // Bag i sits (i + 1) * spacing behind the mower, measured along the path it drove.
        private void PlaceTail()
        {
            tailPositions.Clear();
            if (tail.Count == 0) return;

            int segment = 0;
            float walked = 0f;
            var current = transform.position;
            var direction = -transform.forward;
            for (int i = 0; i < tail.Count; i++)
            {
                float target = (i + 1) * settings.BagSpacing;
                while (segment < path.Count && walked + Vector3.Distance(current, path[segment]) < target)
                {
                    walked += Vector3.Distance(current, path[segment]);
                    if (path[segment] != current) direction = (path[segment] - current).normalized;
                    current = path[segment];
                    segment++;
                }

                Vector3 position;
                if (segment < path.Count)
                {
                    var toNext = path[segment] - current;
                    float remaining = target - walked;
                    if (toNext.sqrMagnitude > 0.0001f) direction = toNext.normalized;
                    position = current + direction * remaining;
                }
                else position = current + direction * (target - walked); // path too short yet (start)

                position.y = transform.position.y;
                tailPositions.Add(position);
                var bag = tail[i].transform;
                bag.position = Vector3.Lerp(bag.position, position, 0.5f);
                if (direction.sqrMagnitude > 0.0001f) bag.rotation = Quaternion.LookRotation(-direction);
            }
        }

        private void UpdateFillIndicator()
        {
            if (fillIndicator == null) return;
            float fill = Mathf.Clamp01(Fill);
            fillIndicator.gameObject.SetActive(fill > 0.02f);
            fillIndicator.localScale = new Vector3(fillScale.x, fillScale.y * fill, fillScale.z);
        }
    }
}
