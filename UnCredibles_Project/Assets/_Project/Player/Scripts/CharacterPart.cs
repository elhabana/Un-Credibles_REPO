using UnityEngine;

namespace UnCredibles.Players
{
    // One wearable part (helmet, outfit or boots). The model shares the character skeleton:
    // skinned parts are rebound to the character bones, rigid ones (helmets) hang from the
    // bone they are attached to in the model (Socket_Cabeza).
    [CreateAssetMenu(menuName = "UnCredibles/Personaje/Pieza", fileName = "SO_PJ_Part_")]
    public sealed class CharacterPart : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CharacterPartSlot slot;
        [SerializeField, Tooltip("Imported model of the part (same skeleton as the character).")]
        private GameObject model;
        [SerializeField, Tooltip("One per material slot of the model.")]
        private Material[] materials = new Material[0];
        [SerializeField, Tooltip("Body zones hidden under this part.")]
        private BodyZone hiddenZones;
        [SerializeField, Tooltip("Hides eyes, eyelids and brows (helmets with a visor).")]
        private bool coversFace;
        [SerializeField, Tooltip("The first material takes the player's colour, so players stay easy to tell apart.")]
        private bool usePlayerColor;
        [SerializeField, Tooltip("Helmets: how much higher than the bare head it reaches (character 1 unit tall).")]
        private float extraHeadHeight;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public CharacterPartSlot Slot => slot;
        public GameObject Model => model;
        public Material[] Materials => materials;
        public BodyZone HiddenZones => hiddenZones;
        public bool CoversFace => coversFace;
        public bool UsePlayerColor => usePlayerColor;
        public float ExtraHeadHeight => extraHeadHeight;
        public Sprite Icon => icon;
    }
}
