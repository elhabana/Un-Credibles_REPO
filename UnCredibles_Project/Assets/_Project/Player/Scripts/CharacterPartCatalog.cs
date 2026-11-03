using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Players
{
    // Every wearable part of the game. The character editor lists them by slot and the
    // loadouts refer to them by id.
    [CreateAssetMenu(menuName = "UnCredibles/Personaje/Catalogo de piezas", fileName = "SO_PJ_PartCatalog")]
    public sealed class CharacterPartCatalog : ScriptableObject
    {
        [SerializeField] private List<CharacterPart> parts = new List<CharacterPart>();

        public IReadOnlyList<CharacterPart> Parts => parts;

        public CharacterPart Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var part in parts)
                if (part != null && part.Id == id) return part;
            return null;
        }

        public IEnumerable<CharacterPart> InSlot(CharacterPartSlot slot)
        {
            foreach (var part in parts)
                if (part != null && part.Slot == slot) yield return part;
        }
    }
}
