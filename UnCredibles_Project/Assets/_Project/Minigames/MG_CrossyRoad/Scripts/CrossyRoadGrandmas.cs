using System.Collections.Generic;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Grandmas spread evenly across the pickup lane. Taken grandmas come back after a delay.
    public sealed class CrossyRoadGrandmas : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CrossyRoadBoard board;
        [SerializeField] private GameObject grandmaPrefab;

        private GameObject[] grandmas = new GameObject[0];
        private float[] respawnTimers = new float[0];
        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly List<Walker> walkers = new List<Walker>();
        private Camera viewCamera;
        private MaterialPropertyBlock colorBlock;

        private sealed class Flyer
        {
            public Transform Transform;
            public GrandmaCharacterAnimator Animated; // ragdoll; without it she just spins in the air
            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float Age;
        }

        // A delivered grandma: walks off to the nearest side waddling, gone once off screen.
        private sealed class Walker
        {
            public Transform Transform;
            public Vector3 Direction;
            public float Age;
            public float BaseHeight;
        }

        private const float WalkBobHeight = 0.06f;
        private const float WalkBobFrequency = 9f;
        private const float WalkMaxSeconds = 12f;
        private const float DropBackOffset = 0.25f; // fraction of a lane past the goal lane centre
        private const float WalkerSpacing = 1.5f;   // centre to centre; a grandma is about 1.1 wide
        private const float RagdollSeconds = 3.6f;  // flying and tumbling on the ground before vanishing
        private const float RagdollVanish = 0.35f;  // last seconds spent shrinking away
        private const float RagdollSpin = 18f;      // radians per second when she is knocked off
        private const float RagdollFlail = 14f;     // extra random spin of the head and arms
        private const float LaunchSpread = 0.45f;   // radians around the car direction

        public int Count => grandmas.Length;

        // Paints the grandma colour: only the jacket on the animated grandma, everything on the old one.
        public static void Tint(GameObject grandma, MaterialPropertyBlock block)
        {
            var animated = grandma.GetComponentInChildren<GrandmaCharacterAnimator>(true);
            var renderers = animated != null ? animated.TintedRenderers : grandma.GetComponentsInChildren<Renderer>(true);
            foreach (var grandmaRenderer in renderers)
                if (grandmaRenderer != null) grandmaRenderer.SetPropertyBlock(block);
        }

        public void Initialize()
        {
            int count = board.Settings.GrandmaCount;
            grandmas = new GameObject[count];
            respawnTimers = new float[count];

            colorBlock = new MaterialPropertyBlock();
            colorBlock.SetColor(BaseColorId, board.Settings.GrandmaColor);
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3(board.SlotToX(i, count), board.transform.position.y, board.LaneToZ(CrossyRoadBoard.PickupLane));
                var grandma = Instantiate(grandmaPrefab, position, Quaternion.Euler(0f, 180f, 0f), transform);
                grandma.name = $"Grandma_{i}";
                Tint(grandma, colorBlock);
                grandmas[i] = grandma;
            }
            BuildRagdollFloor();
        }

        // Nothing else in the minigame uses physics: an invisible floor (much bigger than the
        // board) just for the ragdoll grandmas to land, bounce and roll on.
        private void BuildRagdollFloor()
        {
            var floor = new GameObject("GrandmaRagdollFloor").AddComponent<BoxCollider>();
            floor.transform.SetParent(transform, false);
            float depth = board.MaxZ - board.MinZ;
            floor.transform.position = new Vector3(board.transform.position.x, board.transform.position.y - 0.5f, (board.MinZ + board.MaxZ) * 0.5f);
            floor.size = new Vector3(board.Settings.BoardWidth * 4f, 1f, depth * 4f);
        }

        // Bit i set = grandma i waiting in her spot (sent by the host to the clients).
        public int AvailableMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < grandmas.Length && i < 32; i++)
                    if (grandmas[i].activeSelf) mask |= 1 << i;
                return mask;
            }
        }

        public void ApplyAvailableMask(int mask)
        {
            for (int i = 0; i < grandmas.Length && i < 32; i++)
            {
                bool available = (mask & (1 << i)) != 0;
                if (grandmas[i].activeSelf != available) grandmas[i].SetActive(available);
            }
        }

        public bool IsAvailable(int index) =>
            index >= 0 && index < grandmas.Length && grandmas[index].activeSelf;

        public Vector3 GetPosition(int index) => grandmas[index].transform.position;

        // Closest available grandma within `radius` of the position (flat distance), or -1.
        public int FindAvailableNear(Vector3 position, float radius)
        {
            int best = -1;
            float bestDistance = radius * radius;
            for (int i = 0; i < grandmas.Length; i++)
            {
                if (!IsAvailable(i)) continue;
                var offset = grandmas[i].transform.position - position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        public int NearestAvailable(Vector3 position) => FindAvailableNear(position, float.MaxValue / 4f);

        public bool TryTake(int index)
        {
            if (!IsAvailable(index)) return false;
            grandmas[index].SetActive(false);
            respawnTimers[index] = -1f; // travelling with a player
            return true;
        }

        // Called when the grandma is delivered or dropped: she reappears in her spot later.
        public void Return(int index)
        {
            if (index < 0 || index >= grandmas.Length || grandmas[index].activeSelf) return;
            respawnTimers[index] = board.Settings.GrandmaRespawnDelay;
        }

        // A grandma knocked out of a player's hands: flies off spinning, pushed along `pushX`
        // (the way the car was going, +1 / -1; 0 = any direction).
        // Only visual; the real grandma already went back to her spot through Return().
        public void Launch(Vector3 from, float pushX = 0f)
        {
            var settings = board.Settings;
            Flyer flyer = null;
            foreach (var candidate in flyers)
                if (!candidate.Transform.gameObject.activeSelf) { flyer = candidate; break; }
            if (flyer == null)
            {
                var instance = Instantiate(grandmaPrefab, transform);
                instance.name = "FlyingGrandma";
                Tint(instance, colorBlock);
                var animated = instance.GetComponentInChildren<GrandmaCharacterAnimator>();
                if (animated != null && !animated.HasRagdoll) { animated.SetSpinning(true); animated = null; }
                flyer = new Flyer { Transform = instance.transform, Animated = animated };
                flyers.Add(flyer);
            }

            float angle = pushX > 0f ? Random.Range(-LaunchSpread, LaunchSpread)
                : pushX < 0f ? Mathf.PI + Random.Range(-LaunchSpread, LaunchSpread)
                : Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(settings.GrandmaLaunchSpeed.x, settings.GrandmaLaunchSpeed.y);
            flyer.Velocity = new Vector3(Mathf.Cos(angle) * speed, Random.Range(settings.GrandmaLaunchUp.x, settings.GrandmaLaunchUp.y), Mathf.Sin(angle) * speed);
            flyer.SpinAxis = Random.onUnitSphere;
            flyer.Age = 0f;
            flyer.Transform.localScale = Vector3.one;
            if (flyer.Animated != null)
            {
                // Limp ragdoll: flops head and arms, lands, bounces and rolls, then vanishes.
                flyer.Animated.ResetRagdoll();
                flyer.Transform.SetPositionAndRotation(from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                flyer.Transform.gameObject.SetActive(true);
                flyer.Animated.Launch(flyer.Velocity, flyer.SpinAxis * RagdollSpin, settings.GrandmaGravity, RagdollFlail);
                return;
            }
            flyer.Transform.SetPositionAndRotation(from, Random.rotation);
            flyer.Transform.gameObject.SetActive(true);
        }

        // Only visual, like Launch: the real grandma already went back to her spot through Return().
        public void SendOff(Vector3 from)
        {
            Walker walker = null;
            foreach (var candidate in walkers)
                if (!candidate.Transform.gameObject.activeSelf) { walker = candidate; break; }
            if (walker == null)
            {
                var instance = Instantiate(grandmaPrefab, transform);
                instance.name = "LeavingGrandma";
                Tint(instance, colorBlock);
                var animated = instance.GetComponentInChildren<GrandmaCharacterAnimator>();
                if (animated != null) animated.SetWalking(true);
                walker = new Walker { Transform = instance.transform };
                walkers.Add(walker);
            }

            // Towards the closer side of the board; from the middle, either side.
            float offset = from.x - board.transform.position.x;
            float side = Mathf.Abs(offset) < 0.5f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(offset);
            walker.Direction = new Vector3(side, 0f, 0f);
            walker.Age = 0f;
            walker.BaseHeight = board.transform.position.y;
            // Dropped on the far half of the goal lane, clear of the cars on the last road.
            float z = Mathf.Max(from.z, board.LaneToZ(board.GoalLane) + board.CellSize * DropBackOffset);
            float x = KeepApart(from.x, side, walker);
            walker.Transform.SetPositionAndRotation(new Vector3(x, walker.BaseHeight, z), Quaternion.LookRotation(walker.Direction));
            walker.Transform.gameObject.SetActive(true);
        }

        // Grandmas leaving at the same time must not overlap: the new one starts behind any
        // grandma that is too close. All walk at the same speed, so the gap stays.
        private float KeepApart(float x, float side, Walker self)
        {
            for (int attempt = 0; attempt < walkers.Count; attempt++)
            {
                bool moved = false;
                foreach (var other in walkers)
                {
                    if (other == self || !other.Transform.gameObject.activeSelf) continue;
                    float otherX = other.Transform.position.x;
                    if (Mathf.Abs(otherX - x) >= WalkerSpacing) continue;
                    x = otherX - side * WalkerSpacing;
                    moved = true;
                }
                if (!moved) break;
            }
            return x;
        }

        private void TickRagdoll(Flyer flyer, float deltaTime)
        {
            flyer.Age += deltaTime;
            float left = RagdollSeconds - flyer.Age;
            if (left < RagdollVanish)
            {
                // Shrinks away where she lies (not towards where she was launched).
                if (left + deltaTime >= RagdollVanish) flyer.Animated.FreezeAround(flyer.Transform);
                flyer.Transform.localScale = Vector3.one * Mathf.Max(0f, left / RagdollVanish);
            }
            if (left <= 0f || flyer.Animated.RagdollPosition.y < board.transform.position.y - 3f)
            {
                flyer.Transform.gameObject.SetActive(false);
                flyer.Animated.ResetRagdoll();
                flyer.Transform.localScale = Vector3.one;
            }
        }

        private bool IsOffScreen(Vector3 position)
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return Mathf.Abs(position.x - board.transform.position.x) > board.HalfWidth + 6f;
            var viewport = viewCamera.WorldToViewportPoint(position + Vector3.up * 0.6f);
            return viewport.z < 0f || viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < grandmas.Length; i++)
            {
                if (respawnTimers[i] <= 0f) continue;
                respawnTimers[i] -= deltaTime;
                if (respawnTimers[i] <= 0f) grandmas[i].SetActive(true);
            }

            var settings = board.Settings;
            foreach (var flyer in flyers)
            {
                if (!flyer.Transform.gameObject.activeSelf) continue;
                if (flyer.Animated != null)
                {
                    TickRagdoll(flyer, deltaTime);
                    continue;
                }
                flyer.Velocity.y -= settings.GrandmaGravity * deltaTime;
                flyer.Transform.position += flyer.Velocity * deltaTime;
                flyer.Transform.Rotate(flyer.SpinAxis, settings.GrandmaSpinSpeed * deltaTime, Space.World);
                // Gone once she falls below the ground (usually far off screen).
                if (flyer.Transform.position.y < board.transform.position.y - 2f) flyer.Transform.gameObject.SetActive(false);
            }

            foreach (var walker in walkers)
            {
                if (!walker.Transform.gameObject.activeSelf) continue;
                walker.Age += deltaTime;
                var position = walker.Transform.position + walker.Direction * (board.Settings.GrandmaWalkSpeed * deltaTime);
                position.y = walker.BaseHeight + Mathf.Abs(Mathf.Sin(walker.Age * WalkBobFrequency)) * WalkBobHeight;
                walker.Transform.position = position;
                if (walker.Age > WalkMaxSeconds || (walker.Age > 0.5f && IsOffScreen(position)))
                    walker.Transform.gameObject.SetActive(false);
            }
        }
    }
}
