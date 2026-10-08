using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    [CreateAssetMenu(fileName = "MW_Settings", menuName = "UnCredibles/Mow The Lawn/Settings")]
    public sealed class MowTheLawnSettings : ScriptableObject
    {
        [Header("Lawn")]
        [SerializeField, Min(2f), Tooltip("Playable area. The camera fits it to the screen.")] private float lawnWidth = 30f;
        [SerializeField, Min(2f)] private float lawnDepth = 17f;
        [SerializeField, Min(0f), Tooltip("Extra grass drawn around the playable area so it reaches the screen edges.")]
        private float visualMargin = 6f;
        [SerializeField, Min(0.1f), Tooltip("Size of one grass cell. Smaller = finer trail, more work.")]
        private float cellSize = 0.4f;
        [SerializeField, Min(0.5f), Tooltip("Seconds for a cut cell to grow back completely.")]
        private float regrowSeconds = 20f;
        [SerializeField, Range(0f, 1f), Tooltip("Grass below this height is too short to be cut again.")]
        private float minCutHeight = 0.4f;
        [SerializeField] private Color tallGrassColor = new Color(0.13f, 0.33f, 0.09f);
        [SerializeField] private Color stripeGrassColor = new Color(0.17f, 0.4f, 0.12f);
        [SerializeField] private Color cutColor = new Color(0.45f, 0.3f, 0.16f);
        [SerializeField, Min(0.05f), Tooltip("Height of fully grown grass blades.")] private float bladeHeight = 0.4f;
        [SerializeField, Range(1, 12), Tooltip("Blades drawn per grass cell.")] private int bladesPerCell = 12;

        [Header("Mowers")]
        [SerializeField, Min(0.1f), Tooltip("Top speed with the stick fully pushed.")] private float speed = 8.5f;
        [SerializeField, Min(0.1f), Tooltip("Speed gained per second.")] private float acceleration = 24f;
        [SerializeField, Min(0.1f), Tooltip("Speed lost per second when releasing or slowing down.")] private float braking = 28f;
        [SerializeField, Min(1f), Tooltip("Degrees per second at top speed (faster when slow).")] private float turnSpeed = 380f;
        [SerializeField, Min(0.5f), Tooltip("How fast the mower stops sliding sideways. Lower = more drift.")]
        private float grip = 9f;
        [SerializeField, Min(1f), Tooltip("Speed multiplier while the turbo (Jump) lasts.")] private float boostMultiplier = 2.1f;
        [SerializeField, Min(0.05f)] private float boostSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds before the turbo can be used again.")] private float boostCooldown = 1.6f;
        [SerializeField, Min(0f), Tooltip("Push given to two mowers that crash into each other.")] private float bumpForce = 8f;
        [SerializeField, Min(0.1f), Tooltip("Body radius for collisions with other mowers and bags.")]
        private float mowerRadius = 0.55f;
        [SerializeField, Min(0.1f), Tooltip("Radius of grass cut around the blade.")]
        private float bladeRadius = 0.75f;
        [SerializeField, Min(0.1f), Tooltip("Distance of the blade in front of the mower centre.")]
        private float bladeOffset = 0.25f;
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f),
            new Color(0.95f, 0.55f, 0.15f), new Color(0.75f, 0.3f, 0.85f),
        };

        [Header("Bags")]
        [SerializeField, Min(1f), Tooltip("Grass cells (fully grown) needed to fill one bag.")]
        private float cellsPerBag = 200f;
        [SerializeField, Min(0.2f), Tooltip("Distance between bags in the tail.")] private float bagSpacing = 0.8f;
        [SerializeField, Min(0.1f)] private float bagRadius = 0.32f;
        [SerializeField, Range(0f, 0.05f), Tooltip("Speed lost per bag carried.")] private float slowdownPerBag = 0.02f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Never slower than this fraction of the speed.")]
        private float minSpeedFactor = 0.6f;
        [SerializeField, Min(0f), Tooltip("Seconds a dropped bag cannot be picked up.")]
        private float pickupDelay = 0.35f;
        [SerializeField, Min(0f), Tooltip("How far cut-off bags are scattered.")] private float scatterDistance = 1f;
        [SerializeField, Min(1), Tooltip("Loose bags on the lawn at the same time; older ones vanish.")]
        private int maxLooseBags = 48;
        [SerializeField, Min(1f), Tooltip("Seconds a loose bag stays on the lawn before vanishing.")]
        private float looseBagLifetime = 10f;
        [SerializeField, Min(0f), Tooltip("After losing bags, seconds the rest of the tail cannot be cut.")]
        private float tailProtectSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("Seconds a mower must wait before cutting another tail.")]
        private float cutCooldown = 1f;

        [Header("Ram (turbo into another mower)")]
        [SerializeField, Min(0), Tooltip("Bags knocked off the end of the tail of a rammed mower.")] private int ramBagsLost = 3;
        [SerializeField, Min(0f), Tooltip("Seconds a rammed mower spins out of control.")] private float stunSeconds = 1.1f;
        [SerializeField, Min(0f), Tooltip("Seconds a rammed mower cannot be rammed again.")] private float ramImmunitySeconds = 2f;
        [SerializeField, Range(10f, 90f), Tooltip("Half angle in front of the attacker that counts as a ram.")] private float ramAngle = 60f;
        [SerializeField, Min(0f)] private float ramKnockForce = 12f;

        [Header("Bins (one per player, in the corners)")]
        [SerializeField, Min(0.1f), Tooltip("Solid radius of a bin; mowers bump into it.")] private float binRadius = 0.95f;
        [SerializeField, Min(0.1f), Tooltip("Distance to your own bin to start unloading bags.")] private float deliverRadius = 2.1f;
        [SerializeField, Min(0.01f), Tooltip("Seconds between two bags thrown into the bin.")] private float unloadInterval = 0.12f;
        [SerializeField, Min(0f), Tooltip("Distance from the corner of the lawn to the bin centre.")] private float binInset = 1.1f;

        [Header("Final frenzy")]
        [SerializeField, Min(0f), Tooltip("Last seconds of the game: all grass grows back and bags are worth more.")]
        private float frenzySeconds = 30f;
        [SerializeField, Min(1)] private int frenzyPointsPerBag = 2;
        [SerializeField, Min(1f), Tooltip("Speed multiplier for everybody during the frenzy.")] private float frenzySpeed = 1.15f;

        [Header("AI")]
        [SerializeField, Min(0.05f), Tooltip("Seconds between AI decisions.")] private float aiThinkInterval = 0.35f;
        [SerializeField, Min(0f), Tooltip("Loose bags closer than this attract a bot.")] private float aiLooseBagRange = 6f;
        [SerializeField, Min(0f), Tooltip("Rival bags closer than this may be attacked.")] private float aiAttackRange = 5f;

        public float LawnWidth => lawnWidth;
        public float LawnDepth => lawnDepth;
        public float VisualMargin => visualMargin;
        public float BladeHeight => bladeHeight;
        public int BladesPerCell => bladesPerCell;
        public float CellSize => cellSize;
        public float RegrowSeconds => regrowSeconds;
        public float MinCutHeight => minCutHeight;
        public Color TallGrassColor => tallGrassColor;
        public Color StripeGrassColor => stripeGrassColor;
        public Color CutColor => cutColor;

        public float Speed => speed;
        public float Acceleration => acceleration;
        public float Braking => braking;
        public float Grip => grip;
        public float BoostMultiplier => boostMultiplier;
        public float BoostSeconds => boostSeconds;
        public float BoostCooldown => boostCooldown;
        public float BumpForce => bumpForce;
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
        public float LooseBagLifetime => looseBagLifetime;
        public float TailProtectSeconds => tailProtectSeconds;
        public float CutCooldown => cutCooldown;

        public int RamBagsLost => ramBagsLost;
        public float StunSeconds => stunSeconds;
        public float RamImmunitySeconds => ramImmunitySeconds;
        public float RamAngle => ramAngle;
        public float RamKnockForce => ramKnockForce;

        public float BinRadius => binRadius;
        public float DeliverRadius => deliverRadius;
        public float UnloadInterval => unloadInterval;
        public float BinInset => binInset;

        public float FrenzySeconds => frenzySeconds;
        public int FrenzyPointsPerBag => frenzyPointsPerBag;
        public float FrenzySpeed => frenzySpeed;

        public float AIThinkInterval => aiThinkInterval;
        public float AILooseBagRange => aiLooseBagRange;
        public float AIAttackRange => aiAttackRange;

        public float SpeedFactor(int bags) => Mathf.Max(minSpeedFactor, 1f - slowdownPerBag * bags);

        public Color GetPlayerColor(int slotIndex) =>
            playerColors.Length > 0 ? playerColors[Mathf.Abs(slotIndex) % playerColors.Length] : Color.white;
    }
}
