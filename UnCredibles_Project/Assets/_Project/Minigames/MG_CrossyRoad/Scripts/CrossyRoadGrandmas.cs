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
            public Vector3 Velocity;
            public Vector3 SpinAxis;
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

        // A grandma knocked out of a player's hands: flies off spinning in a random direction.
        // Only visual; the real grandma already went back to her spot through Return().
        public void Launch(Vector3 from)
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
                if (animated != null) animated.SetSpinning(true);
                flyer = new Flyer { Transform = instance.transform };
                flyers.Add(flyer);
            }

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(settings.GrandmaLaunchSpeed.x, settings.GrandmaLaunchSpeed.y);
            flyer.Velocity = new Vector3(Mathf.Cos(angle) * speed, Random.Range(settings.GrandmaLaunchUp.x, settings.GrandmaLaunchUp.y), Mathf.Sin(angle) * speed);
            flyer.SpinAxis = Random.onUnitSphere;
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
            walker.Transform.SetPositionAndRotation(new Vector3(from.x, walker.BaseHeight, from.z), Quaternion.LookRotation(walker.Direction));
            walker.Transform.gameObject.SetActive(true);
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
