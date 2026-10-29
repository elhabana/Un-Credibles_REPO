using UnityEngine;

namespace UnCredibles.Players
{
    // Drives the animated player character (Idle / Run). Whoever moves the avatar only says
    // whether it is running; the Animator Controller blends between both loops.
    public sealed class PlayerCharacterAnimator : MonoBehaviour
    {
        private static readonly int RunningId = Animator.StringToHash("Running");
        private static readonly int RunSpeedId = Animator.StringToHash("RunSpeed");
        private const string IdleState = "Idle";

        [SerializeField] private Animator animator;
        [SerializeField, Min(.1f), Tooltip("Run clip playback multiplier, to match how fast the avatar moves.")]
        private float runPlaybackSpeed = 1f;
        [SerializeField, Tooltip("Start the idle at a random point so several characters do not breathe in sync.")]
        private bool randomizeIdlePhase = true;

        public bool IsRunning { get; private set; }

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
            if (!IsRunning && randomizeIdlePhase) animator.Play(IdleState, 0, Random.value);
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
