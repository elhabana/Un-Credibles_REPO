using System.Collections.Generic;
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
        private MaterialPropertyBlock colorBlock;

        private sealed class Flyer
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 SpinAxis;
        }

        public int Count => grandmas.Length;

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
                foreach (var grandmaRenderer in grandma.GetComponentsInChildren<Renderer>())
                    grandmaRenderer.SetPropertyBlock(colorBlock);
                grandmas[i] = grandma;
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
                foreach (var flyerRenderer in instance.GetComponentsInChildren<Renderer>()) flyerRenderer.SetPropertyBlock(colorBlock);
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
        }
    }
}
