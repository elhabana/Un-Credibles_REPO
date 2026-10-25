using System;
using UnityEngine;

namespace UnCredibles.Minigames.GarageMice
{
    // Every tuning value of the minigame. Positions are on the floor: x across, y = world z.
    [CreateAssetMenu(fileName = "SO_GM_Settings", menuName = "UnCredibles/Garage Mice/Settings")]
    public sealed class GarageMiceSettings : ScriptableObject
    {
        [Serializable]
        public struct Shelf
        {
            public Vector2 center;
            [Tooltip("Half the footprint. Players walk around it; scared mice can hide under it.")]
            public Vector2 halfSize;
            public Shelf(float x, float z, float halfX, float halfZ)
            {
                center = new Vector2(x, z);
                halfSize = new Vector2(halfX, halfZ);
            }
        }

        [Header("Arena")]
        [SerializeField, Min(2f), Tooltip("Half the playable width. The arena prefab walls are built for this size.")]
        private float halfWidth = 10f;
        [SerializeField, Min(2f)] private float halfDepth = 6f;
        [SerializeField] private Shelf[] shelves =
        {
            new Shelf(-6.6f, -2.1f, 1.2f, .55f), new Shelf(-6.6f, 2.1f, 1.2f, .55f),
            new Shelf(6.6f, -2.1f, 1.2f, .55f), new Shelf(6.6f, 2.1f, 1.2f, .55f),
            new Shelf(-3.9f, .5f, 1.05f, .55f), new Shelf(3.9f, .5f, 1.05f, .55f)
        };
        [SerializeField, Tooltip("Points just inside the walls. A scared mouse that reaches one pops out of another later.")]
        private Vector2[] holes =
        {
            new Vector2(-9.7f, 0f), new Vector2(9.7f, 0f),
            new Vector2(-3f, 5.7f), new Vector2(3f, 5.7f),
            new Vector2(-3f, -5.7f), new Vector2(3f, -5.7f)
        };

        [Header("Players")]
        [SerializeField, Min(.1f)] private float playerRadius = .38f;
        [SerializeField, Min(.1f), Tooltip("Metres per second.")] private float moveSpeed = 4.4f;
        [SerializeField, Min(.1f), Tooltip("Metres in front of the player the broom reaches.")] private float swingReach = 1.5f;
        [SerializeField, Range(1f, 180f), Tooltip("Degrees to each side of where the player faces.")]
        private float swingHalfAngle = 65f;
        [SerializeField, Min(0f), Tooltip("Seconds between two swings.")] private float swingCooldown = .55f;
        [SerializeField, Min(.01f), Tooltip("Seconds the broom animation lasts.")] private float swingVisualSeconds = .22f;
        [SerializeField, Min(0f), Tooltip("Seconds a player hit by a broom cannot move.")] private float stunSeconds = 1.25f;
        [SerializeField, Min(0f), Tooltip("Seconds after a hit before the same player can be stunned again.")]
        private float stunImmunitySeconds = 1.8f;
        [SerializeField, Tooltip("One per slot, already in its colour: 0 red, 1 blue, 2 green, 3 yellow.")]
        private GarageMicePlayer[] playerPrefabs = new GarageMicePlayer[UnCredibles.Players.PlayerRegistry.MaxPlayers];

        [Header("Mice")]
        [SerializeField, Min(1)] private int mouseCount = 26;
        [SerializeField, Min(.05f)] private float mouseRadius = .2f;
        [SerializeField, Min(.1f)] private float mouseSpeed = 2.1f;
        [SerializeField, Min(.1f)] private float mouseFleeSpeed = 3.2f;
        [SerializeField, Min(0f), Tooltip("A mouse runs away from any player closer than this.")] private float mouseFleeRadius = 3f;
        [SerializeField, Min(0f), Tooltip("Seconds before a caught mouse comes back.")] private float mouseRespawnSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("Seconds a mouse stays hidden under a shelf.")] private float shelterSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("Seconds after leaving a shelf before it can hide again.")]
        private float shelterCooldownSeconds = 4f;
        [SerializeField, Min(0f), Tooltip("Seconds after leaving a hole before it can hide again.")]
        private float holeCooldownSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("A fleeing mouse heads for a shelf or hole closer than this.")]
        private float shelterSeekRadius = 3.5f;
        [SerializeField, Min(0f)] private float holeEnterDistance = .45f;

        [Header("Golden mouse")]
        [SerializeField, Min(.1f)] private float goldenSpeed = 2.6f;
        [SerializeField, Min(.1f)] private float goldenFleeSpeed = 3.8f;
        [SerializeField, Min(1)] private int goldenPoints = 5;
        [SerializeField, Min(0f), Tooltip("Seconds before it appears, and before it comes back once caught.")]
        private float goldenRespawnSeconds = 20f;

        public float HalfWidth => halfWidth;
        public float HalfDepth => halfDepth;
        public Shelf[] Shelves => shelves;
        public Vector2[] Holes => holes;

        public float PlayerRadius => playerRadius;
        public float MoveSpeed => moveSpeed;
        public float SwingReach => swingReach;
        public float SwingHalfAngle => swingHalfAngle;
        public float SwingCooldown => swingCooldown;
        public float SwingVisualSeconds => swingVisualSeconds;
        public float StunSeconds => stunSeconds;
        public float StunImmunitySeconds => stunImmunitySeconds;
        public GarageMicePlayer PlayerPrefabFor(int slotIndex) => playerPrefabs[slotIndex];

        public int MouseCount => mouseCount;
        public float MouseRadius => mouseRadius;
        public float MouseSpeed => mouseSpeed;
        public float MouseFleeSpeed => mouseFleeSpeed;
        public float MouseFleeRadius => mouseFleeRadius;
        public float MouseRespawnSeconds => mouseRespawnSeconds;
        public float ShelterSeconds => shelterSeconds;
        public float ShelterCooldownSeconds => shelterCooldownSeconds;
        public float HoleCooldownSeconds => holeCooldownSeconds;
        public float ShelterSeekRadius => shelterSeekRadius;
        public float HoleEnterDistance => holeEnterDistance;

        public float GoldenSpeed => goldenSpeed;
        public float GoldenFleeSpeed => goldenFleeSpeed;
        public int GoldenPoints => goldenPoints;
        public float GoldenRespawnSeconds => goldenRespawnSeconds;
    }
}
