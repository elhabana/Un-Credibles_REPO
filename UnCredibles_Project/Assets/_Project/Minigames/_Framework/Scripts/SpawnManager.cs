using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Players are never created from scratch: the minigame asks for the registered players
    // and places their avatar on SpawnPoint_<slot>.
    public sealed class SpawnManager : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints = new Transform[PlayerRegistry.MaxPlayers];

        public Transform GetSpawnPoint(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= spawnPoints.Length || spawnPoints[slotIndex] == null)
            {
                Debug.LogError($"Missing SpawnPoint_{slotIndex}.", this);
                return transform;
            }
            return spawnPoints[slotIndex];
        }

        public T Spawn<T>(T prefab, PlayerSlot player, Transform parent = null) where T : Component
        {
            var point = GetSpawnPoint(player.SlotIndex);
            var instance = Instantiate(prefab, point.position, point.rotation, parent);
            instance.name = $"{prefab.name}_{player.SlotIndex}";
            return instance;
        }
    }
}
