using UnityEngine;

namespace UnCredibles.Players
{
    // The player figure shown outside the minigames (garage lobby, results podium), one prefab per slot
    // already in its colour, so nothing has to tint it.
    [CreateAssetMenu(fileName = "SO_PlayerFigures", menuName = "UnCredibles/Player/Figure Set")]
    public sealed class PlayerFigureSet : ScriptableObject
    {
        [SerializeField, Tooltip("One per slot: 0 red, 1 blue, 2 green, 3 yellow.")]
        private GameObject[] figures = new GameObject[PlayerRegistry.MaxPlayers];

        public GameObject For(int slotIndex) => figures[slotIndex];
    }
}
