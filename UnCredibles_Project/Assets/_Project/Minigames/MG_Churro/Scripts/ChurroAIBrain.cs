using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Jumps a little before the churro arrives. The lead time is rolled once per approach,
    // so sometimes it jumps too early or too late and falls, like a person would.
    public sealed class ChurroAIBrain
    {
        private readonly ChurroPlayer player;
        private readonly AIInput input;
        private readonly ChurroSpinner spinner;
        private readonly ChurroSettings settings;
        private readonly float halfWidth;
        private float lead;
        private float lastTimeToArrival = float.MaxValue;

        public ChurroAIBrain(ChurroPlayer player, AIInput input, ChurroSpinner spinner, ChurroSettings settings, float halfWidth)
        {
            this.player = player;
            this.input = input;
            this.spinner = spinner;
            this.settings = settings;
            this.halfWidth = halfWidth;
            RollLead();
        }

        public void Tick()
        {
            if (!player.IsGrounded) return;

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
    }
}
