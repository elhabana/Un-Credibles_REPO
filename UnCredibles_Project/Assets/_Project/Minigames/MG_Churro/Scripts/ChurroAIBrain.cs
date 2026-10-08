using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Jumps a little before the churro arrives and ducks a little before a beach ball does. The
    // lead times are rolled per approach, so sometimes it reacts too early or too late and falls,
    // like a person would.
    public sealed class ChurroAIBrain
    {
        private const float BallMinLead = 0.12f;
        private const float BallMaxLead = 0.45f;

        private readonly ChurroPlayer player;
        private readonly AIInput input;
        private readonly ChurroSpinner spinner;
        private readonly ChurroBalls balls;
        private readonly ChurroSettings settings;
        private readonly float halfWidth;
        private float lead;
        private float ballLead;
        private float lastTimeToArrival = float.MaxValue;
        private bool ducking;

        public ChurroPlayer Player => player;

        public ChurroAIBrain(ChurroPlayer player, AIInput input, ChurroSpinner spinner, ChurroBalls balls,
            ChurroSettings settings, float halfWidth)
        {
            this.player = player;
            this.input = input;
            this.spinner = spinner;
            this.balls = balls;
            this.settings = settings;
            this.halfWidth = halfWidth;
            RollLead();
            RollBallLead();
        }

        public void Tick()
        {
            // Beach ball first: duck shortly before it arrives, stand up once it is gone.
            float timeToBall = balls.TimeUntilHit(player);
            bool shouldDuck = timeToBall <= ballLead;
            if (ducking && !shouldDuck) RollBallLead();
            ducking = shouldDuck;
            input.SetHeld(PlayerAction.Crouch, ducking);
            if (ducking || !player.IsGrounded) return;

            float timeToArrival = spinner.TimeUntilArrival(player.Angle, halfWidth);
            // A new arm is on its way (the time jumped up): decide again how early to react.
            if (timeToArrival > lastTimeToArrival + 0.05f) RollLead();
            lastTimeToArrival = timeToArrival;

            if (timeToArrival <= lead)
            {
                input.Press(PlayerAction.Jump);
                RollLead();
            }
        }

        private void RollLead() => lead = Random.Range(settings.AIMinLead, settings.AIMaxLead);
        private void RollBallLead() => ballLead = Random.Range(BallMinLead, BallMaxLead);
    }
}
