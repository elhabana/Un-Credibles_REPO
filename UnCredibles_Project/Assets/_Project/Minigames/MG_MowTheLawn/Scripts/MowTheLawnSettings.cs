using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    [CreateAssetMenu(fileName = "MW_Settings", menuName = "UnCredibles/Mow The Lawn/Settings")]
    public sealed class MowTheLawnSettings : ScriptableObject
    {
        [Header("Lawn")]
        [SerializeField, Min(2f)] private float lawnWidth = 26f;
        [SerializeField, Min(2f)] private float lawnDepth = 16f;
        [SerializeField, Min(0.1f), Tooltip("Size of one grass cell. Smaller = finer trail, more work.")]
        private float cellSize = 0.4f;
        [SerializeField, Min(0.5f), Tooltip("Seconds for a cut cell to grow back completely.")]
        private float regrowSeconds = 18f;
        [SerializeField, Range(0f, 1f), Tooltip("Grass below this height is too short to be cut again.")]
        private float minCutHeight = 0.35f;
        [SerializeField] private Color tallGrassColor = new Color(0.27f, 0.62f, 0.22f);
        [SerializeField] private Color stripeGrassColor = new Color(0.33f, 0.7f, 0.27f);
        [SerializeField] private Color cutColor = new Color(0.45f, 0.3f, 0.16f);

        [Header("Mowers")]
        [SerializeField, Min(0.1f)] private float speed = 4.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Speed while the stick is released: mowers never stop.")]
        private float idleSpeedFactor = 0.55f;
        [SerializeField, Min(1f), Tooltip("Degrees per second.")] private float turnSpeed = 220f;
        [SerializeField, Min(0.1f), Tooltip("Body radius for collisions with other mowers and bags.")]
        private float mowerRadius = 0.55f;
        [SerializeField, Min(0.1f), Tooltip("Radius of grass cut around the blade.")]
        private float bladeRadius = 0.7f;
        [SerializeField, Min(0.1f), Tooltip("Distance of the blade in front of the mower centre.")]
        private float bladeOffset = 0.25f;
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f),
            new Color(0.95f, 0.55f, 0.15f), new Color(0.75f, 0.3f, 0.85f),
        };

        [Header("Bags")]
        [SerializeField, Min(1f), Tooltip("Grass cells (fully grown) needed to fill one bag.")]
        private float cellsPerBag = 70f;
        [SerializeField, Min(0.2f), Tooltip("Distance between bags in the tail.")] private float bagSpacing = 0.75f;
        [SerializeField, Min(0.1f)] private float bagRadius = 0.32f;
        [SerializeField, Range(0f, 0.05f), Tooltip("Speed lost per bag carried.")] private float slowdownPerBag = 0.012f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Never slower than this fraction of the speed.")]
        private float minSpeedFactor = 0.6f;
        [SerializeField, Min(0f), Tooltip("Seconds a dropped bag cannot be picked up.")]
        private float pickupDelay = 0.6f;
        [SerializeField, Min(0f), Tooltip("How far cut-off bags are scattered.")] private float scatterDistance = 1.6f;
        [SerializeField, Min(1), Tooltip("Loose bags on the lawn at the same time; older ones vanish.")]
        private int maxLooseBags = 48;

        [Header("AI")]
        [SerializeField, Min(0.05f), Tooltip("Seconds between AI decisions.")] private float aiThinkInterval = 0.35f;
        [SerializeField, Min(0f), Tooltip("Loose bags closer than this attract a bot.")] private float aiLooseBagRange = 6f;
        [SerializeField, Min(0f), Tooltip("Rival bags closer than this may be attacked.")] private float aiAttackRange = 5f;

        public float LawnWidth => lawnWidth;
        public float LawnDepth => lawnDepth;
        public float CellSize => cellSize;
        public float RegrowSeconds => regrowSeconds;
        public float MinCutHeight => minCutHeight;
        public Color TallGrassColor => tallGrassColor;
        public Color StripeGrassColor => stripeGrassColor;
        public Color CutColor => cutColor;

        public float Speed => speed;
        public float IdleSpeedFactor => idleSpeedFactor;
        public float TurnSpeed => turnSpeed;
        public float MowerRadius => mowerRadius;
        public float BladeRadius => bladeRadius;
        public float BladeOffset => bladeOffset;

        public float CellsPerBag => cellsPerBag;
        public float BagSpacing => bagSpacing;
        public float BagRadius => bagRadius;
        public float PickupDelay => pickupDelay;
        public float ScatterDistance => scatterDistance;
        public int MaxLooseBags => maxLooseBags;

        public float AIThinkInterval => aiThinkInterval;
        public float AILooseBagRange => aiLooseBagRange;
        public float AIAttackRange => aiAttackRange;

        public float SpeedFactor(int bags) => Mathf.Max(minSpeedFactor, 1f - slowdownPerBag * bags);

        public Color GetPlayerColor(int slotIndex) =>
            playerColors.Length > 0 ? playerColors[Mathf.Abs(slotIndex) % playerColors.Length] : Color.white;
    }
}
