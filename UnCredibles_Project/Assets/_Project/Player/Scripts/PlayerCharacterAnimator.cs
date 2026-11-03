using UnityEngine;

namespace UnCredibles.Players
{
    // Drives the animated player character (Idle / Run, and IdleCarry / RunCarry with a grandma
    // held up over the head). Whoever moves the avatar only says whether it runs and whether it carries;
    // the Animator Controller blends between the loops.
    public sealed class PlayerCharacterAnimator : MonoBehaviour
    {
        private static readonly int RunningId = Animator.StringToHash("Running");
        private static readonly int CarryingId = Animator.StringToHash("Carrying");
        private static readonly int RunSpeedId = Animator.StringToHash("RunSpeed");
        private const string IdleState = "Idle";

        [SerializeField] private Animator animator;
        [SerializeField, Min(.1f), Tooltip("Run clip playback multiplier, to match how fast the avatar moves.")]
        private float runPlaybackSpeed = 1f;
        [SerializeField, Tooltip("Start the idle at a random point so several characters do not breathe in sync.")]
        private bool randomizeIdlePhase = true;

        [SerializeField, Tooltip("Chest bone the carried grandma is attached to.")]
        private Transform backSocket;

        public bool IsRunning { get; private set; }
        public bool IsCarrying { get; private set; }
        public Transform BackSocket => backSocket;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            animator.SetFloat(RunSpeedId, runPlaybackSpeed);
        }

        private void OnEnable()
        {
            // Animator resets its parameters when re-enabled: restore the current state.
            animator.SetFloat(RunSpeedId, runPlaybackSpeed);
            animator.SetBool(RunningId, IsRunning);
            animator.SetBool(CarryingId, IsCarrying);
            if (!IsRunning && !IsCarrying && randomizeIdlePhase) animator.Play(IdleState, 0, Random.value);
        }

        public void SetCarrying(bool carrying)
        {
            if (IsCarrying == carrying) return;
            IsCarrying = carrying;
            if (animator.isActiveAndEnabled) animator.SetBool(CarryingId, carrying);
        }

        public void SetRunning(bool running)
        {
            if (IsRunning == running) return;
            IsRunning = running;
            // While inactive just remember it: OnEnable applies it.
            if (animator.isActiveAndEnabled) animator.SetBool(RunningId, running);
        }

        public void SetRunPlaybackSpeed(float speed)
        {
            runPlaybackSpeed = Mathf.Max(.1f, speed);
            if (animator.isActiveAndEnabled) animator.SetFloat(RunSpeedId, runPlaybackSpeed);
        }
    }
}
