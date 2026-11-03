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

        // Next / previous choice of a slot, wrapping around; "nothing" (empty id) is one more choice.
        public string Step(CharacterPartSlot slot, string current, int step)
        {
            var choices = new List<string> { string.Empty };
            foreach (var part in InSlot(slot)) choices.Add(part.Id);
            int index = Mathf.Max(0, choices.IndexOf(current ?? string.Empty));
            index = ((index + step) % choices.Count + choices.Count) % choices.Count;
            return choices[index];
        }

        // Bots get dressed at random.
        public CharacterLoadout RandomLoadout()
        {
            var loadout = new CharacterLoadout();
            foreach (CharacterPartSlot slot in System.Enum.GetValues(typeof(CharacterPartSlot)))
                loadout = loadout.With(slot, Step(slot, string.Empty, Random.Range(0, 8)));
            return loadout;
        }
    }
}
