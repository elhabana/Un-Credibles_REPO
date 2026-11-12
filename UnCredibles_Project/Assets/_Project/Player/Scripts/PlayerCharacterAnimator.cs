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

        [Header("Shove pose (both arms thrust forward)")]
        [SerializeField, Min(.05f)] private float shoveDuration = .32f;
        [SerializeField, Range(.05f, .9f), Tooltip("Part of the shove spent reaching out; the rest is pulling back.")]
        private float shoveReach = .25f;

        public bool IsRunning { get; private set; }
        public bool IsCarrying { get; private set; }
        public Transform BackSocket => backSocket;

        private Transform hips, torso, neck;
        private Transform[] thighs, shins, feet;
        private float heavy;
        private Transform[] upperArms, forearms, hands;
        private float shoveTimer;
        // Seated: hip joint this far above the seat (this object's origin), in model heights.
        private const float SeatHipHeight = .05f;
        // Procedural poses (there are no clips for them), blended in and out over this time.
        private const float PoseBlendTime = .15f;
        private const float JumpTuck = 50f;    // knee bend of the legs tucked in the air
        private const float CrouchKnee = 50f;
        private const float CrouchLean = 35f;
        // Index 0 = left, 1 = right, like the bone arrays.
        private readonly Transform[] seatFeet = new Transform[2];
        private readonly Transform[] holdTargets = new Transform[2];
        private bool armsUp, airborne, crouching;
        private float raise, tuck, crouch, torsoTwist;
        // Bones the poses touch, with the clip pose under them and what the poses left last frame.
        private Transform[] posed;
        private Vector3 hipsClip, hipsWritten = new(float.NaN, 0f, 0f);
        private Quaternion[] posedClip, posedWritten;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            // The poses below bend bones on top of the clip pose; a culled Animator stops resetting it and they pile up.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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

            upperArms = new[] { Bone("upper_arm.L"), Bone("upper_arm.R") };
            forearms = new[] { Bone("forearm.L"), Bone("forearm.R") };
            hands = new[] { Bone("hand.L"), Bone("hand.R") };
            for (int i = 0; i < 2; i++)
                if (!upperArms[i] || !forearms[i] || !hands[i]) upperArms = null;

            var list = new System.Collections.Generic.List<Transform>();
            foreach (var bone in new[] { hips, torso, neck }) if (bone) list.Add(bone);
            foreach (var set in new[] { thighs, shins, feet, upperArms, forearms, hands })
                if (set != null) foreach (var bone in set) if (bone) list.Add(bone);
            posed = list.ToArray();
            posedClip = new Quaternion[posed.Length];
            posedWritten = new Quaternion[posed.Length];
        }

        // The clips do not key every bone the poses bend: a bone nothing wrote since last frame still
        // holds last frame's pose, so it goes back to the clip pose first (otherwise the bends pile up).
        private void RestoreClipPose()
        {
            for (int i = 0; i < posed.Length; i++)
            {
                if (posed[i].localRotation == posedWritten[i]) posed[i].localRotation = posedClip[i];
                posedClip[i] = posed[i].localRotation;
            }
            if (hips == null) return;
            if (hips.localPosition == hipsWritten) hips.localPosition = hipsClip;
            hipsClip = hips.localPosition;
        }

        private void RememberPose()
        {
            for (int i = 0; i < posed.Length; i++) posedWritten[i] = posed[i].localRotation;
            if (hips != null) hipsWritten = hips.localPosition;
        }

        // Both arms thrust forward for a moment, on top of whatever loop is playing.
        public void PlayShove() => shoveTimer = shoveDuration;

        // Sitting on a seat at this object's origin (there is no sitting clip), the ankles resting on
        // these points (knees up if the legs are longer than that). Null stands up again.
        public void Sit(Transform leftFoot, Transform rightFoot)
        {
            seatFeet[0] = leftFoot;
            seatFeet[1] = rightFoot;
        }

        // Each hand reaches for its point on top of the loop, elbows bent, and follows it as it moves
        // (a broom handle, a steering wheel). Null lets that hand go.
        public void Hold(Transform leftHand, Transform rightHand)
        {
            holdTargets[0] = leftHand;
            holdTargets[1] = rightHand;
        }

        // Both arms up, swaying, like on a roller coaster (lets go of what it holds meanwhile).
        public void SetArmsUp(bool value) => armsUp = value;
        // In the air: legs tucked, arms up and open.
        public void SetAirborne(bool value) => airborne = value;
        // Ducking: knees bent, hips low, leaning forward with the arms over the head.
        public void SetCrouching(bool value) => crouching = value;
        // Upper body turned around the vertical (positive to the right), e.g. into a broom swing.
        public void SetTorsoTwist(float degrees) => torsoTwist = degrees;

        // Empty child used as a seat/hold point.
        public static Transform AddPoint(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        // Every pose starts again from this frame's clip pose, so nothing builds up.
        private void LateUpdate()
        {
            float blend = Time.deltaTime / PoseBlendTime;
            raise = Mathf.MoveTowards(raise, armsUp || airborne ? 1f : 0f, blend);
            tuck = Mathf.MoveTowards(tuck, airborne ? 1f : 0f, blend);
            crouch = Mathf.MoveTowards(crouch, crouching ? 1f : 0f, blend);

            RestoreClipPose();
            ApplySeated();
            ApplyCarryPosture();
            ApplyLegTuck();
            if (torsoTwist != 0f && hips != null) Bend(torso, torsoTwist, transform.up);
            ApplyHold();
            ApplyRaisedArms();
            ApplyCrouchArms();
            ApplyShovePose();
            RememberPose();
        }

        private void ApplySeated()
        {
            if (seatFeet[0] == null || seatFeet[1] == null || hips == null) return;
            hips.position = transform.position + transform.up * (SeatHipHeight * transform.lossyScale.y);
            for (int i = 0; i < 2; i++)
            {
                var sole = feet[i].rotation; // flat, as in the clip
                Reach(thighs[i], shins[i], feet[i], seatFeet[i].position, transform.forward + transform.up, 1f);
                feet[i].rotation = sole;
            }
        }

        private void ApplyHold()
        {
            if (upperArms == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (holdTargets[i] == null) continue;
                // Elbows down, out and a bit back.
                var pole = Side(i) * .6f - transform.up - transform.forward * .4f;
                Reach(upperArms[i], forearms[i], hands[i], holdTargets[i].position, pole, 1f);
            }
        }

        private void ApplyRaisedArms()
        {
            if (raise <= 0f || upperArms == null) return;
            float weight = Mathf.SmoothStep(0f, 1f, raise);
            // Roller coaster: straight up in a V, slightly forward so they never fold behind the head, swaying; jump: wide open and still.
            float spread = armsUp ? .6f : .75f;
            var sway = armsUp ? transform.right * (Mathf.Sin(Time.time * 6f) * .15f) + transform.forward * .2f : Vector3.zero;
            for (int i = 0; i < 2; i++)
            {
                var up = transform.up + Side(i) * spread + sway;
                Aim(upperArms[i], forearms[i].position, up, weight);
                Aim(forearms[i], hands[i].position, up, weight);
            }
        }

        private void ApplyLegTuck()
        {
            if (tuck <= 0f || hips == null) return;
            float knee = JumpTuck * Mathf.SmoothStep(0f, 1f, tuck);
            var axis = transform.right;
            for (int i = 0; i < 2; i++)
            {
                Bend(thighs[i], -knee, axis);
                Bend(shins[i], 2f * knee, axis);
                Bend(feet[i], -knee, axis);
            }
        }

        // Upper arms forward and up, forearms crossed in front of the head.
        private void ApplyCrouchArms()
        {
            if (crouch <= 0f || upperArms == null) return;
            float weight = Mathf.SmoothStep(0f, 1f, crouch);
            for (int i = 0; i < 2; i++)
            {
                var side = Side(i);
                Aim(upperArms[i], forearms[i].position, transform.forward * .7f + transform.up * .6f + side * .3f, weight);
                Aim(forearms[i], hands[i].position, -side + transform.up * .3f + transform.forward * .2f, weight);
            }
        }

        private Vector3 Side(int i) => i == 0 ? -transform.right : transform.right;

        // Two-bone IK: bends the middle joint towards `pole` so `end` lands on `target`
        // (arm straight towards it when out of reach).
        private static void Reach(Transform upper, Transform middle, Transform end, Vector3 target, Vector3 pole, float weight)
        {
            var root = upper.position;
            var toTarget = target - root;
            if (toTarget.sqrMagnitude < 1e-8f) return;
            float a = Vector3.Distance(root, middle.position), b = Vector3.Distance(middle.position, end.position);
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-4f, (a + b) * .999f);
            var direction = toTarget.normalized;
            float along = (a * a - b * b + d * d) / (2f * d);
            var joint = root + direction * along
                + Vector3.ProjectOnPlane(pole, direction).normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Aim(upper, middle.position, joint - root, weight);
            Aim(middle, end.position, root + direction * d - middle.position, weight);
        }

        private void ApplyShovePose()
        {
            if (shoveTimer <= 0f || upperArms == null) return;
            shoveTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(shoveTimer / shoveDuration);
            // Snap out fast, ease back slowly.
            float weight = progress < shoveReach
                ? Mathf.SmoothStep(0f, 1f, progress / shoveReach)
                : Mathf.SmoothStep(1f, 0f, (progress - shoveReach) / (1f - shoveReach));

            var target = (transform.forward - transform.up * .15f).normalized;
            for (int i = 0; i < 2; i++)
            {
                Aim(upperArms[i], forearms[i].position, target, weight);
                Aim(forearms[i], hands[i].position, target, weight); // straight arm
            }
        }

        // Turns `bone` so the segment towards `child` points along `direction`, blended by weight.
        private static void Aim(Transform bone, Vector3 child, Vector3 direction, float weight)
        {
            var current = child - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            var full = Quaternion.FromToRotation(current, direction) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, full, weight);
        }

        private void ApplyCarryPosture()
        {
            heavy = Mathf.MoveTowards(heavy, IsCarrying ? 1f : 0f, Time.deltaTime / carryBlendTime);
            if (hips == null) return;
            float ducked = Mathf.SmoothStep(0f, 1f, crouch);
            if (heavy > 0f || ducked > 0f)
                Squat(carryLean * heavy + CrouchLean * ducked, carryKneeBend * heavy + CrouchKnee * ducked);
        }

        // Leans the torso and bends the knees by these degrees, keeping the planted foot on the floor.
        private void Squat(float lean, float knee)
        {
            var axis = transform.right; // positive angle tips the bone forward
            Bend(torso, lean, axis);
            Bend(neck, -lean * .5f, axis); // keep looking ahead

            float lowestFoot = float.MaxValue, drop = 0f;
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
