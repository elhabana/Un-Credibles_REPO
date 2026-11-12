using UnityEngine;

namespace UnCredibles.Players
{
    // The player figure (garage lobby, results podium, minigame avatars), one prefab per slot
    // already in its colour, so nothing has to tint it.
    [CreateAssetMenu(fileName = "SO_PlayerFigures", menuName = "UnCredibles/Player/Figure Set")]
    public sealed class PlayerFigureSet : ScriptableObject
    {
        [SerializeField, Tooltip("One per slot: 0 blue, 1 red, 2 green, 3 yellow.")]
        private GameObject[] figures = new GameObject[PlayerRegistry.MaxPlayers];

        public GameObject For(int slotIndex) => figures[slotIndex];

        // The slot's figure, dressed as in the lobby, standing at `localPosition` under a minigame avatar.
        // Null when the slot has no figure.
        public PlayerCharacterOutfit Mount(PlayerSlot slot, Transform parent, Vector3 localPosition, float scale)
        {
            var prefab = For(slot.SlotIndex);
            if (prefab == null) return null;
            var figure = Instantiate(prefab, parent, false);
            figure.transform.localPosition = localPosition;
            figure.transform.localScale *= scale;
            var outfit = figure.GetComponentInChildren<PlayerCharacterOutfit>();
            if (outfit != null) outfit.Apply(slot.Loadout);
            return outfit;
        }
    }
}
