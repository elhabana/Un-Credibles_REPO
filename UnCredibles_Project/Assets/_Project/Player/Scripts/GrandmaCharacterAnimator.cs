using UnityEngine;

namespace UnCredibles.Players
{
    // Drives the animated grandma: a small bounce while she waits, a side-to-side sway while
    // she is carried by a player or walks away, and a ragdoll (floppy head and arms) when she
    // is knocked flying.
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
        [SerializeField, Tooltip("Ragdoll bodies (torso first). Off and kinematic until she is knocked flying.")]
        private Rigidbody[] ragdollBodies = new Rigidbody[0];

        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Collider[][] ragdollColliders;
        private float extraGravity;

        public bool IsCarried { get; private set; }
        public bool IsSpinning { get; private set; }
        public bool IsWalking { get; private set; }
        public bool IsRagdoll { get; private set; }
        public bool HasRagdoll => ragdollBodies.Length > 0;
        public Renderer[] TintedRenderers => tintedRenderers;
        // Where the ragdoll is now (the prefab root stays where she was launched).
        public Vector3 RagdollPosition => HasRagdoll ? ragdollBodies[0].position : transform.position;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            restPositions = new Vector3[ragdollBodies.Length];
            restRotations = new Quaternion[ragdollBodies.Length];
            ragdollColliders = new Collider[ragdollBodies.Length][];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                var bone = ragdollBodies[i].transform;
                restPositions[i] = bone.localPosition;
                restRotations[i] = bone.localRotation;
                ragdollColliders[i] = bone.GetComponents<Collider>();
            }
            ApplyRagdoll(false);
        }

        private void OnEnable()
        {
            // Animator resets its parameters when re-enabled: restore the current state.
            animator.SetBool(CarriedId, IsCarried);
            animator.SetBool(SpinningId, IsSpinning);
            animator.SetBool(WalkingId, IsWalking);
            if (!IsCarried && !IsSpinning && !IsWalking && !IsRagdoll && randomizeIdlePhase) animator.Play(IdleState, 0, Random.value);
        }

        private void FixedUpdate()
        {
            if (!IsRagdoll || extraGravity <= 0f) return;
            foreach (var body in ragdollBodies) body.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
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

        // Goes limp and flies with this velocity. `gravity` is the fall acceleration wanted
        // (the minigame uses a stronger one than the physics default).
        public void Launch(Vector3 velocity, Vector3 angularVelocity, float gravity)
        {
            if (!HasRagdoll) return;
            ApplyRagdoll(true);
            extraGravity = Mathf.Max(0f, gravity - Mathf.Abs(Physics.gravity.y));
            foreach (var body in ragdollBodies)
            {
                body.linearVelocity = velocity;
                body.angularVelocity = angularVelocity;
            }
        }

        // Stops the physics where she lies and moves `pivot` (a parent of the ragdoll) onto her,
        // so it can be scaled down around her instead of around where she was launched.
        public void FreezeAround(Transform pivot)
        {
            if (!HasRagdoll || !IsRagdoll) return;
            var positions = new Vector3[ragdollBodies.Length];
            var rotations = new Quaternion[ragdollBodies.Length];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                var body = ragdollBodies[i];
                body.isKinematic = true;
                foreach (var part in ragdollColliders[i]) part.enabled = false;
                positions[i] = body.transform.position;
                rotations[i] = body.transform.rotation;
            }
            pivot.position = positions[0];
            for (int i = 0; i < ragdollBodies.Length; i++)
                ragdollBodies[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
        }

        // Back to the animated pose, ready to be reused.
        public void ResetRagdoll()
        {
            if (!HasRagdoll || !IsRagdoll) return;
            ApplyRagdoll(false);
            for (int i = 0; i < ragdollBodies.Length; i++)
                ragdollBodies[i].transform.SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
        }

        private void ApplyRagdoll(bool on)
        {
            IsRagdoll = on;
            animator.enabled = !on;
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                var body = ragdollBodies[i];
                if (!on && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = !on;
                body.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                foreach (var part in ragdollColliders[i]) part.enabled = on;
            }
        }
    }
}
