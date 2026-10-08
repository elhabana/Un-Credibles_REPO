using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // The kid in the middle turning the churro. Each round starts slow in a random direction and
    // speeds up; now and then the kid brakes, stops for a blink and spins the other way.
    // Angles follow Unity's yaw: 0 = +Z, 90 = +X, growing clockwise.
    public sealed class ChurroSpinner : MonoBehaviour
    {
        private enum Phase { Spinning, Braking, Paused, Reversing }

        [SerializeField, Tooltip("Rotates with the kid; the churro arms hang from it.")]
        private Transform pivot;
        [SerializeField, Tooltip("Arms of the churro, enabled according to the round.")]
        private GameObject[] arms = new GameObject[0];

        private ChurroSettings settings;
        private ChurroSettings.Round round;
        private Phase phase;
        private float phaseTimer;
        private float reverseTimer;
        private float resumeSpeed;
        private float remoteAngle;

        public float Angle { get; private set; }
        public float PreviousAngle { get; private set; }
        public float Speed { get; private set; }          // degrees per second, always positive
        public int Direction { get; private set; } = 1;   // 1 = clockwise, -1 = counter-clockwise
        public float AngularVelocity => Speed * Direction;
        public bool IsBraking => phase == Phase.Braking || phase == Phase.Paused;
        public bool IsChangingDirection => phase != Phase.Spinning;
        public float Acceleration => round.acceleration;
        public float MaxSpeed => round.maxSpeed;
        public int ArmCount { get; private set; } = 1;
        public float ArmSpacing => 360f / ArmCount;
        public Vector3 Center => transform.position;

        public void ResetForRound(ChurroSettings churroSettings, ChurroSettings.Round roundSettings, float startAngle, int direction)
        {
            settings = churroSettings;
            round = roundSettings;
            ArmCount = Mathf.Clamp(roundSettings.arms, 1, Mathf.Max(1, arms.Length));
            for (int i = 0; i < arms.Length; i++) arms[i].SetActive(i < ArmCount);

            Speed = roundSettings.startSpeed;
            Direction = direction >= 0 ? 1 : -1;
            phase = Phase.Spinning;
            reverseTimer = settings.FirstReverseDelay + RandomReverseInterval();
            Angle = PreviousAngle = Mathf.Repeat(startAngle, 360f);
            ApplyRotation();
        }

        public void Tick(float deltaTime)
        {
            PreviousAngle = Angle;
            switch (phase)
            {
                case Phase.Spinning:
                    Speed = Mathf.Min(Speed + round.acceleration * deltaTime, round.maxSpeed);
                    if (round.reverses && (reverseTimer -= deltaTime) <= 0f)
                    {
                        phase = Phase.Braking;
                        resumeSpeed = Speed;
                    }
                    break;

                case Phase.Braking:
                    Speed = Mathf.MoveTowards(Speed, 0f, resumeSpeed / settings.BrakeSeconds * deltaTime);
                    if (Speed <= 0f)
                    {
                        phase = Phase.Paused;
                        phaseTimer = settings.ReversePause;
                    }
                    break;

                case Phase.Paused:
                    if ((phaseTimer -= deltaTime) <= 0f)
                    {
                        Direction = -Direction;
                        phase = Phase.Reversing;
                    }
                    break;

                case Phase.Reversing:
                    Speed = Mathf.MoveTowards(Speed, resumeSpeed, resumeSpeed / settings.ReaccelerateSeconds * deltaTime);
                    if (Speed >= resumeSpeed)
                    {
                        phase = Phase.Spinning;
                        reverseTimer = RandomReverseInterval();
                    }
                    break;
            }

            Angle += AngularVelocity * deltaTime;
            // Keep both angles small without breaking the Angle - PreviousAngle sweep.
            if (Angle >= 360f)
            {
                Angle -= 360f;
                PreviousAngle -= 360f;
            }
            else if (Angle < 0f)
            {
                Angle += 360f;
                PreviousAngle += 360f;
            }
            ApplyRotation();
        }

        // No direction change for the next `seconds` (a beach ball is flying: its timing must hold).
        public void HoldDirection(float seconds)
        {
            if (phase == Phase.Spinning) reverseTimer = Mathf.Max(reverseTimer, seconds);
        }

        // Did any arm touch [targetAngle - halfWidth, targetAngle + halfWidth] during the last Tick?
        // Works for both directions: it looks at the arc each arm covered, whichever way it turned.
        public bool SweptThrough(float targetAngle, float halfWidth)
        {
            float swept = Angle - PreviousAngle;
            float from = Mathf.Min(PreviousAngle, Angle);
            float length = Mathf.Abs(swept);
            for (int arm = 0; arm < ArmCount; arm++)
            {
                float armStart = from + arm * ArmSpacing;
                // Distance from the start of the arc to the far edge of the zone, going clockwise.
                float toFarEdge = Mathf.Repeat(targetAngle + halfWidth - armStart, 360f);
                if (toFarEdge <= halfWidth * 2f || toFarEdge - halfWidth * 2f <= length) return true;
            }
            return false;
        }

        // Seconds until the next arm reaches the near edge of the zone, in the current direction
        // (used by the AI; a sudden reversal can fool it, like it fools people).
        public float TimeUntilArrival(float targetAngle, float halfWidth)
        {
            float best = float.MaxValue;
            for (int arm = 0; arm < ArmCount; arm++)
                best = Mathf.Min(best, DistanceToZone(arm, targetAngle, halfWidth) / Mathf.Max(Speed, 1f));
            return best;
        }

        // Degrees this arm still has to turn, in the current direction, to reach the near edge of
        // [targetAngle - halfWidth, targetAngle + halfWidth].
        public float DistanceToZone(int arm, float targetAngle, float halfWidth)
        {
            float armAngle = Angle + arm * ArmSpacing;
            return Direction > 0
                ? Mathf.Repeat(targetAngle - halfWidth - armAngle, 360f)
                : Mathf.Repeat(armAngle - (targetAngle + halfWidth), 360f);
        }

        // ---------- Online ----------

        // Host: angle, signed speed (for the client prediction) and arm count.
        public void WriteState(BinaryWriter writer)
        {
            writer.Write(Angle);
            writer.Write(AngularVelocity);
            writer.Write((byte)ArmCount);
        }

        public void ReadState(BinaryReader reader) => ApplyRemote(reader.ReadSingle(), reader.ReadSingle(), reader.ReadByte());

        // Client: between packets we predict with the received speed and blend towards the host.
        private void ApplyRemote(float angle, float angularVelocity, int armCount)
        {
            if (armCount != ArmCount)
            {
                ArmCount = Mathf.Clamp(armCount, 1, Mathf.Max(1, arms.Length));
                for (int i = 0; i < arms.Length; i++) arms[i].SetActive(i < ArmCount);
                Angle = angle; // new round: no blending from the old one
            }
            remoteAngle = angle;
            Speed = Mathf.Abs(angularVelocity);
            Direction = angularVelocity >= 0f ? 1 : -1;
        }

        public void TickRemote(float deltaTime)
        {
            remoteAngle += AngularVelocity * deltaTime;
            float predicted = Angle + AngularVelocity * deltaTime;
            float error = Mathf.DeltaAngle(predicted, remoteAngle);
            Angle = Mathf.Repeat(Mathf.Abs(error) > 45f ? remoteAngle : predicted + error * Mathf.Min(1f, 10f * deltaTime), 360f);
            PreviousAngle = Angle;
            ApplyRotation();
        }

        public static float AngleOf(Vector3 center, Vector3 position)
        {
            var offset = position - center;
            return Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);
        }

        private float RandomReverseInterval() =>
            settings != null ? Random.Range(settings.ReverseInterval.x, settings.ReverseInterval.y) : 999f;

        private void ApplyRotation() => pivot.localRotation = Quaternion.Euler(0f, Angle, 0f);
    }
}
