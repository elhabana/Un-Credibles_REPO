using UnityEngine;

namespace UnCredibles.Players
{
    // Drives the animated grandma: a small bounce while she waits, a side-to-side sway while
    // she is carried by a player or walks away, and a spin (around the vertical axis) when she flies off.
    public sealed class GrandmaCharacterAnimator : MonoBehaviour
    {
        private static readonly int CarriedId = Animator.StringToHash("Carried");
        private static readonly int SpinningId = Animator.StringToHash("Spinning");
        private static readonly int WalkingId = Animator.StringToHash("Walking");
        private const string IdleState = "Idle";

        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Parts painted with the grandma colour of the minigame (the jacket).")]
        private Renderer[] tintedRenderers = new Renderer[0];
        [SerializeField, Tooltip("Start the bounce at a random point so the grandmas in a row do not hop in sync.")]
        private bool randomizeIdlePhase = true;

        public bool IsCarried { get; private set; }
        public bool IsSpinning { get; private set; }
        public bool IsWalking { get; private set; }
        public Renderer[] TintedRenderers => tintedRenderers;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            // Animator resets its parameters when re-enabled: restore the current state.
            animator.SetBool(CarriedId, IsCarried);
            animator.SetBool(SpinningId, IsSpinning);
            animator.SetBool(WalkingId, IsWalking);
            if (!IsCarried && !IsSpinning && !IsWalking && randomizeIdlePhase) animator.Play(IdleState, 0, Random.value);
        }

        public void SetCarried(bool carried)
        {
            IsCarried = carried;
            if (animator.isActiveAndEnabled) animator.SetBool(CarriedId, carried);
        }

        public void SetWalking(bool walking)
        {
            IsWalking = walking;
            if (animator.isActiveAndEnabled) animator.SetBool(WalkingId, walking);
        }

        public void SetSpinning(bool spinning)
        {
            IsSpinning = spinning;
            if (animator.isActiveAndEnabled) animator.SetBool(SpinningId, spinning);
        }
    }
}
