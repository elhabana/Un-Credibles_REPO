using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // The kid in the middle turning the churro clockwise (seen from above).
    // Angles follow Unity's yaw: 0 = +Z, 90 = +X, growing clockwise.
    public sealed class ChurroSpinner : MonoBehaviour
    {
        [SerializeField, Tooltip("Rotates with the kid; the churro arms hang from it.")]
        private Transform pivot;
        [SerializeField, Tooltip("Arms of the churro, enabled according to the round.")]
        private GameObject[] arms = new GameObject[0];

        private ChurroSettings.Round round;

        public float Angle { get; private set; }
        public float PreviousAngle { get; private set; }
        public float Speed { get; private set; }
        public int ArmCount { get; private set; } = 1;
        public float ArmSpacing => 360f / ArmCount;
        public Vector3 Center => transform.position;

        public void ResetForRound(ChurroSettings.Round roundSettings, float startAngle)
        {
            round = roundSettings;
            ArmCount = Mathf.Clamp(roundSettings.arms, 1, Mathf.Max(1, arms.Length));
            for (int i = 0; i < arms.Length; i++) arms[i].SetActive(i < ArmCount);

            Speed = roundSettings.startSpeed;
            Angle = PreviousAngle = startAngle;
            ApplyRotation();
        }

        public void Tick(float deltaTime)
        {
            PreviousAngle = Angle;
            Speed = Mathf.Min(Speed + round.acceleration * deltaTime, round.maxSpeed);
            Angle += Speed * deltaTime;
            // Keep both angles small without breaking the Angle - PreviousAngle sweep.
            if (Angle >= 360f)
            {
                Angle -= 360f;
                PreviousAngle -= 360f;
            }
            ApplyRotation();
        }

        // Did any arm touch [targetAngle - halfWidth, targetAngle + halfWidth] during the last Tick?
        public bool SweptThrough(float targetAngle, float halfWidth)
        {
            float swept = Angle - PreviousAngle;
            for (int arm = 0; arm < ArmCount; arm++)
            {
                float armStart = PreviousAngle + arm * ArmSpacing;
                // Distance from the arm to the far edge of the zone, in the direction of rotation.
                float toFarEdge = Mathf.Repeat(targetAngle + halfWidth - armStart, 360f);
                if (toFarEdge <= halfWidth * 2f || toFarEdge - halfWidth * 2f <= swept) return true;
            }
            return false;
        }

        // Seconds until the next arm reaches the near edge of the zone (used by the AI).
        public float TimeUntilArrival(float targetAngle, float halfWidth)
        {
            float best = float.MaxValue;
            for (int arm = 0; arm < ArmCount; arm++)
            {
                float distance = Mathf.Repeat(targetAngle - halfWidth - (Angle + arm * ArmSpacing), 360f);
                best = Mathf.Min(best, distance / Mathf.Max(Speed, 1f));
            }
            return best;
        }

        public static float AngleOf(Vector3 center, Vector3 position)
        {
            var offset = position - center;
            return Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);
        }

        private void ApplyRotation() => pivot.localRotation = Quaternion.Euler(0f, Angle, 0f);
    }
}
