using System;

namespace UnCredibles.Players
{
    public enum CharacterPartSlot { Helmet, Outfit, Boots }

    // Zones the base body is split into. A part hides the zones it covers so the skin
    // never pokes through it.
    [Flags]
    public enum BodyZone
    {
        None = 0,
        Head = 1 << 0,
        Ears = 1 << 1,
        Torso = 1 << 2,
        Arms = 1 << 3,
        Hands = 1 << 4,
        Thighs = 1 << 5,
        Knees = 1 << 6,
        Ankles = 1 << 7,
        Feet = 1 << 8,
    }

    // What a player wears: one part id per slot (empty = nothing). Small enough to send
    // over the network or save as it is.
    [Serializable]
    public struct CharacterLoadout
    {
        public string Helmet;
        public string Outfit;
        public string Boots;

        public string Get(CharacterPartSlot slot) => slot switch
        {
            CharacterPartSlot.Helmet => Helmet,
            CharacterPartSlot.Outfit => Outfit,
            _ => Boots,
        };

        public CharacterLoadout With(CharacterPartSlot slot, string partId)
        {
            var copy = this;
            switch (slot)
            {
                case CharacterPartSlot.Helmet: copy.Helmet = partId; break;
                case CharacterPartSlot.Outfit: copy.Outfit = partId; break;
                default: copy.Boots = partId; break;
            }
            return copy;
        }
    }
}
