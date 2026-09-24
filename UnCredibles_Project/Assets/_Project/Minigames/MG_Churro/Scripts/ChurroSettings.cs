using System;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    [CreateAssetMenu(fileName = "CH_Settings", menuName = "UnCredibles/Churro/Settings")]
    public sealed class ChurroSettings : ScriptableObject
    {
        [Serializable]
        public struct Round
        {
            [Min(1), Tooltip("Churro arms, evenly spaced. More arms = less time between jumps.")]
            public int arms;
            [Min(1f), Tooltip("Degrees per second when the round starts.")] public float startSpeed;
            [Min(0f), Tooltip("Degrees per second gained every second.")] public float acceleration;
            [Min(1f)] public float maxSpeed;
        }

        [Header("Rounds")]
        [SerializeField] private Round[] rounds =
        {
            new Round { arms = 1, startSpeed = 100f, acceleration = 10f, maxSpeed = 360f },
            new Round { arms = 1, startSpeed = 140f, acceleration = 14f, maxSpeed = 420f },
            new Round { arms = 2, startSpeed = 90f, acceleration = 6f, maxSpeed = 230f },
        };
        [SerializeField, Min(0f)] private float roundIntroSeconds = 1.5f;
        [SerializeField, Min(0f)] private float roundOutroSeconds = 2.5f;

        [Header("Churro")]
        [SerializeField, Min(0.05f), Tooltip("Height of the churro above the float, in metres.")]
        private float churroHeight = 0.35f;
        [SerializeField, Min(0.01f)] private float churroRadius = 0.15f;

        [Header("Players")]
        [SerializeField, Min(0.1f)] private float jumpVelocity = 6.5f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField, Min(0.05f), Tooltip("Player body radius used for the hit test.")]
        private float playerRadius = 0.35f;
        [SerializeField] private float knockoutSpeed = 7f;
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f),
            new Color(0.3f, 0.8f, 0.35f), new Color(0.95f, 0.8f, 0.2f),
        };

        [Header("AI")]
        [SerializeField, Min(0f), Tooltip("Earliest a bot jumps before the churro arrives (s).")]
        private float aiMinLead = 0.05f;
        [SerializeField, Min(0f), Tooltip("Latest a bot jumps before the churro arrives (s).")]
        private float aiMaxLead = 0.45f;

        public int RoundCount => rounds.Length;
        public Round GetRound(int index) => rounds[Mathf.Clamp(index, 0, rounds.Length - 1)];
        public float RoundIntroSeconds => roundIntroSeconds;
        public float RoundOutroSeconds => roundOutroSeconds;

        public float ChurroHeight => churroHeight;
        public float ChurroRadius => churroRadius;
        // Feet must be above this to clear the churro.
        public float ClearHeight => churroHeight + churroRadius;

        public float JumpVelocity => jumpVelocity;
        public float Gravity => gravity;
        public float PlayerRadius => playerRadius;
        public float KnockoutSpeed => knockoutSpeed;
        public Color GetPlayerColor(int slotIndex) =>
            playerColors.Length > 0 ? playerColors[slotIndex % playerColors.Length] : Color.white;

        public float AIMinLead => aiMinLead;
        public float AIMaxLead => aiMaxLead;

        private void OnValidate()
        {
            if (aiMaxLead < aiMinLead) aiMaxLead = aiMinLead;
            for (int i = 0; i < rounds.Length; i++)
                if (rounds[i].maxSpeed < rounds[i].startSpeed) rounds[i].maxSpeed = rounds[i].startSpeed;
        }
    }
}
