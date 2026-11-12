using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.GarageMice
{
    // The player's avatar: the controller owns its state (stun, swing); this only draws it.
    public sealed class GarageMicePlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Shakes while stunned; holds the body and the broom.")] private Transform visual;
        [SerializeField, Tooltip("Rotates on each swing.")] private Transform broom;
        [SerializeField, Tooltip("Shown and spinning above the head while stunned.")] private Transform stars;
        [SerializeField, Tooltip("Placeholder body, hidden once the slot's figure is mounted.")]
        private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField, Tooltip("Lobby figures: the slot's one, dressed the same, stands inside the visual.")]
        private PlayerFigureSet figures;

        private const float FigureScale = .72f;
        // With the figure the broom shrinks to its size, sits in front of its belly and tips its top
        // back towards it, so the brush ends up ahead near the floor, as when sweeping.
        private const float BroomScale = .75f;
        private const float BroomTilt = -20f;
        private static readonly Vector3 BroomPivot = new Vector3(.07f, .65f, -.06f);
        // Where each hand holds the Handle (which runs along the broom's Y at z .55): right one high, left one lower.
        private static readonly Vector3 RightGrip = new Vector3(0f, .3f, .55f);
        private static readonly Vector3 LeftGrip = new Vector3(0f, -.1f, .55f);
        private const float SwingTwist = .4f; // part of the swing done turning the shoulders
        private PlayerCharacterAnimator character;
        private float broomTilt;
        private const float RunGrace = .15f;
        private Vector3 lastPosition;
        private float runGrace;

        public void Setup(PlayerSlot slot)
        {
            stars.gameObject.SetActive(false);
            var outfit = figures != null ? figures.Mount(slot, visual, Vector3.zero, FigureScale) : null;
            if (outfit != null)
            {
                foreach (var placeholder in bodyRenderers) placeholder.gameObject.SetActive(false);
                character = outfit.GetComponent<PlayerCharacterAnimator>();
                if (character != null)
                {
                    broom.localScale = Vector3.one * BroomScale;
                    broom.localPosition = BroomPivot;
                    broomTilt = BroomTilt;
                    // The arms follow every swing.
                    character.Hold(PlayerCharacterAnimator.AddPoint(broom, "Grip_L", LeftGrip),
                        PlayerCharacterAnimator.AddPoint(broom, "Grip_R", RightGrip));
                }
            }
            PlayerPresentation.Attach(this, slot, outfit != null ? outfit.VisibleRenderers().ToArray() : bodyRenderers);
            lastPosition = transform.position;
        }

        // swing: 1 when the swing starts, 0 when it is over.
        public void Animate(float swing, bool stunned, float deltaTime)
        {
            if (character != null && deltaTime > 0f)
            {
                // Same for host and replicas: run while the avatar moves. The grace bridges the frames
                // without a new snapshot, so the legs do not drop to idle in between.
                float speed = (transform.position - lastPosition).magnitude / deltaTime;
                runGrace = speed > .5f ? RunGrace : runGrace - deltaTime;
                character.SetRunning(runGrace > 0f && !stunned);
            }
            lastPosition = transform.position;
            float yaw = swing > 0f ? -85f * Mathf.Sin(swing * Mathf.PI) : 0f;
            broom.localRotation = Quaternion.Euler(broomTilt, yaw, -20f);
            if (character != null) character.SetTorsoTwist(yaw * SwingTwist);
            stars.gameObject.SetActive(stunned);
            if (stunned)
            {
                stars.Rotate(Vector3.up, 290f * deltaTime, Space.Self);
                visual.localPosition = new Vector3(Mathf.Sin(Time.time * 38f) * .055f, 0f, 0f);
            }
            else visual.localPosition = Vector3.zero;
        }
    }
}
