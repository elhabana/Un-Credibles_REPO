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
        [SerializeField, Min(.1f), Tooltip("Same for the run-while-carrying clip (its steps are shorter).")]
        private float runCarryPlaybackSpeed = 1f;
        [SerializeField, Tooltip("Start the idle at a random point so several characters do not breathe in sync.")]
        private bool randomizeIdlePhase = true;

        [SerializeField, Tooltip("Chest bone the carried grandma is attached to.")]
        private Transform backSocket;

        [Header("Heavy carry posture (added on top of the carry clips)")]
        [SerializeField, Range(0f, 45f), Tooltip("Degrees the torso leans forward under the grandma's weight.")]
        private float carryLean = 16f;
        [SerializeField, Range(0f, 40f), Tooltip("Extra knee bend while carrying; the hips drop so the planted foot stays on the floor.")]
        private float carryKneeBend = 18f;
        [SerializeField, Min(.01f)] private float carryBlendTime = .2f;

        public bool IsRunning { get; private set; }
        public bool IsCarrying { get; private set; }
        public Transform BackSocket => backSocket;

        private Transform hips, torso, neck;
        private Transform[] thighs, shins, feet;
        private float heavy;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            animator.SetFloat(RunSpeedId, RunSpeed);
            FindCarryBones();
        }

        // Rigify bone names of SK_Shared_PJ. Missing bones simply turn the posture off.
        private void FindCarryBones()
        {
            var bones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in animator.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;
            hips = Bone("spine");
            torso = Bone("spine.001");
            neck = Bone("spine.004");
            thighs = new[] { Bone("thigh.L"), Bone("thigh.R") };
            shins = new[] { Bone("shin.L"), Bone("shin.R") };
            feet = new[] { Bone("foot.L"), Bone("foot.R") };
            bool complete = hips && torso && neck;
            for (int i = 0; i < 2; i++) complete &= thighs[i] && shins[i] && feet[i];
            if (!complete) hips = null;
        }

        private void LateUpdate()
        {
            heavy = Mathf.MoveTowards(heavy, IsCarrying ? 1f : 0f, Time.deltaTime / carryBlendTime);
            if (heavy <= 0f || hips == null) return;

            var axis = transform.right; // positive angle tips the bone forward
            Bend(torso, carryLean * heavy, axis);
            Bend(neck, -carryLean * .5f * heavy, axis); // keep looking ahead

            float knee = carryKneeBend * heavy, lowestFoot = float.MaxValue, drop = 0f;
            for (int i = 0; i < 2; i++)
            {
                float before = feet[i].position.y;
                Bend(thighs[i], -knee, axis);
                Bend(shins[i], 2f * knee, axis);
                Bend(feet[i], -knee, axis); // sole keeps its angle
                if (before < lowestFoot) { lowestFoot = before; drop = feet[i].position.y - before; }
            }
            hips.position -= Vector3.up * drop; // the foot on the ground stays put
        }

        private static void Bend(Transform bone, float degrees, Vector3 axis) =>
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;

        private void OnEnable()
        {
            // Animator resets its parameters when re-enabled: restore the current state.
            animator.SetFloat(RunSpeedId, RunSpeed);
            animator.SetBool(RunningId, IsRunning);
            animator.SetBool(CarryingId, IsCarrying);
            if (!IsRunning && !IsCarrying && randomizeIdlePhase) animator.Play(IdleState, 0, Random.value);
        }

        private float RunSpeed => IsCarrying ? runCarryPlaybackSpeed : runPlaybackSpeed;

        public void SetCarrying(bool carrying)
        {
            if (IsCarrying == carrying) return;
            IsCarrying = carrying;
            if (!animator.isActiveAndEnabled) return;
            animator.SetBool(CarryingId, carrying);
            animator.SetFloat(RunSpeedId, RunSpeed); // Run and RunCarry share the parameter
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
            if (animator.isActiveAndEnabled) animator.SetFloat(RunSpeedId, RunSpeed);
        }
    }
}
