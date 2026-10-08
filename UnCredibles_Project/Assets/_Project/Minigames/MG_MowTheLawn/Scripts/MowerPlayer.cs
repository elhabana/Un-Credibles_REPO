using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // A ride-on mower driven like a little kart: the stick sets direction and speed, it speeds
    // up and brakes progressively and slides a bit in tight turns. Jump gives a short turbo;
    // ramming another mower with it knocks bags off and leaves it spinning for a moment.
    // It cuts the grass under its blade and drags its bags behind like a tail that follows
    // exactly the path it drove. Ticked by MowTheLawnController, it has no Update of its own.
    public sealed class MowerPlayer : MonoBehaviour
    {
        private const float PathStep = 0.12f;         // distance between recorded path points
        private const float RemoteSnapDistance = 3f;
        private const float StickDeadZone = 0.15f;
        private const float KnockDecay = 22f;     // how fast a crash push fades (m/s per second)
        private const float CrashSpeedKept = 0.85f; // speed kept after crashing into a mower

        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField, Tooltip("Spins while driving.")] private Transform blade;
        [SerializeField, Tooltip("Grows on Y as the next bag fills up.")] private Transform fillIndicator;
        [SerializeField, Tooltip("Exhaust flames, shown while the turbo lasts.")] private Transform boostFlame;
        [SerializeField, Tooltip("Ring on the ground, shown when the turbo is ready.")] private Transform turboRing;

        // Recorded points of the drive, newest first. The mower itself is always the start of the
        // tail; points are only added once it moved PathStep away from the newest one.
        private readonly List<Vector3> path = new List<Vector3>(256);
        private readonly List<MowBagView> tail = new List<MowBagView>(16);
        private readonly List<Vector3> tailPositions = new List<Vector3>(16);
        private MowTheLawnSettings settings;
        private MowBags bags;
        private Color color => PlayerIdentity.ColorFor(Slot.SlotIndex, Slot.IsAI);
        private float clippings;
        private float bobTime;
        private Vector3 fillScale;
        private float forwardSpeed;
        private Vector3 drift;
        private Vector3 knock;
        private float boostTimer;
        private float boostCooldown;
        private float protectedTimer;
        private float stunTimer;
        private float ramImmunityTimer;
        private float spin;
        private float lean;
        private Vector3 remotePosition;
        private float remoteYaw;
        private bool remoteBoosting;
        private bool remoteProtected;
        private bool remoteStunned;
        private bool remoteBoostReady;
        private bool wasBoostReady;
        private float ringPop;
        private Vector3 flameScale;
        private Vector3 ringScale;
        private bool hasRemote;

        public PlayerSlot Slot { get; private set; }
        public Color Color => color;
        public int BagCount => tail.Count;
        public float Fill => clippings / settings.CellsPerBag;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 BladePosition => transform.position + transform.forward * settings.BladeOffset;
        public IReadOnlyList<Vector3> BagPositions => tailPositions;
        public bool IsBoosting => boostTimer > 0f;
        public bool CanBoost => boostCooldown <= 0f;
        // Right after losing bags the rest of the tail cannot be cut, so it is not lost at once.
        public bool IsTailProtected => protectedTimer > 0f;
        public bool IsStunned => stunTimer > 0f;
        public bool CanBeRammed => ramImmunityTimer <= 0f;
        // Global speed change (the final frenzy makes everybody faster).
        public float SpeedMultiplier { get; set; } = 1f;

        public void Setup(PlayerSlot slot, MowTheLawnSettings mowSettings, MowBags bagPool)
        {
            Slot = slot;
            settings = mowSettings;
            bags = bagPool;
            PlayerPresentation.Attach(this, slot, bodyRenderers);
            if (fillIndicator != null) fillScale = fillIndicator.localScale;
            if (boostFlame != null) flameScale = boostFlame.localScale;
            if (turboRing != null) ringScale = turboRing.localScale;

            path.Clear();
            path.Add(transform.position);
            UpdateFillIndicator();
        }

        // Host: drive with the input, cut grass and fill bags.
        public void Tick(float deltaTime, MowLawn lawn)
        {
            var input = Slot.Input != null ? Slot.Input.Move : Vector2.zero;
            float throttle = Mathf.Clamp01(input.magnitude);
            if (throttle < StickDeadZone) throttle = 0f;

            if (boostCooldown > 0f) boostCooldown -= deltaTime;
            if (boostTimer > 0f) boostTimer -= deltaTime;
            if (protectedTimer > 0f) protectedTimer -= deltaTime;
            if (ramImmunityTimer > 0f) ramImmunityTimer -= deltaTime;
            if (stunTimer > 0f)
            {
                stunTimer -= deltaTime;
                throttle = 0f; // spinning: no control until it recovers
            }
            else if (CanBoost && Slot.Input != null && Slot.Input.WasPressed(PlayerAction.Jump)) Boost();

            // Turn towards the stick; slow mowers turn faster, like a kart.
            if (throttle > 0f)
            {
                float speedRatio = forwardSpeed / Mathf.Max(0.01f, settings.Speed);
                float turnSpeed = settings.TurnSpeed * Mathf.Lerp(1.5f, 1f, speedRatio);
                var target = Quaternion.LookRotation(new Vector3(input.x, 0f, input.y));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
            }
            // A mower does not change speed instantly; turbo ignores the stick.
            float maxSpeed = MaxSpeed;
            float wanted = IsBoosting ? maxSpeed * settings.BoostMultiplier : maxSpeed * throttle;
            float rate = wanted > forwardSpeed ? settings.Acceleration : settings.Braking;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, wanted, rate * deltaTime);

            // Grip: the drive catches up with the heading, so tight turns slide a little.
            // Knocks from crashes are a separate push that fades fast and never eats the speed.
            drift = Vector3.Lerp(drift, transform.forward * forwardSpeed, Mathf.Min(1f, settings.Grip * deltaTime));
            knock = Vector3.MoveTowards(knock, Vector3.zero, KnockDecay * deltaTime);
            var intended = transform.position + (drift + knock) * deltaTime;
            var next = lawn.ClampInside(intended, settings.MowerRadius);
            // Walls stop the movement along that axis only.
            if (!Mathf.Approximately(next.x, intended.x)) drift.x = knock.x = 0f;
            if (!Mathf.Approximately(next.z, intended.z)) drift.z = knock.z = 0f;
            transform.position = next;

            clippings += lawn.Cut(BladePosition, settings.BladeRadius);
            while (clippings >= settings.CellsPerBag)
            {
                clippings -= settings.CellsPerBag;
                AddBags(1);
            }

            AfterMove(deltaTime, IsBoosting, IsTailProtected, IsStunned, CanBoost && !IsStunned, drift.magnitude);
        }

        private float MaxSpeed => settings.Speed * settings.SpeedFactor(BagCount) * SpeedMultiplier;

        // The turbo kicks in at once: no waiting for the engine to speed up.
        public void Boost()
        {
            boostTimer = settings.BoostSeconds;
            boostCooldown = settings.BoostCooldown;
            forwardSpeed = Mathf.Max(forwardSpeed, MaxSpeed * settings.BoostMultiplier);
            drift = transform.forward * forwardSpeed;
        }

        // Rammed: spins out of control for a while and cannot be rammed again for a moment.
        public void Stun(float seconds, float immunitySeconds)
        {
            stunTimer = seconds;
            ramImmunityTimer = immunitySeconds;
            boostTimer = 0f;
        }

        // Crashed into another mower: pushed out and knocked away, keeping most of its speed.
        public void Bump(Vector3 position, Vector3 push)
        {
            transform.position = position;
            knock += push;
            if (!IsBoosting) forwardSpeed *= CrashSpeedKept;
        }

        // Pushed out of something solid (a bin) without any knock.
        public void MoveTo(Vector3 position) => transform.position = position;

        public void AddBags(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var view = bags.Rent();
                view.SetTint(color);
                // A new bag starts where the tail ends and slides into place.
                var end = tailPositions.Count > 0 ? tailPositions[tailPositions.Count - 1] : transform.position;
                view.transform.position = end;
                tail.Add(view);
            }
            PlaceTail(1f);
        }

        // A rival cut the tail at `index`: that bag and all behind it fall off. Returns how many.
        public int CutTailAt(int index, List<Vector3> droppedPositions)
        {
            int count = 0;
            for (int i = tail.Count - 1; i >= index; i--)
            {
                droppedPositions?.Add(tail[i].transform.position);
                bags.Release(tail[i]);
                tail.RemoveAt(i);
                count++;
            }
            if (count > 0 && droppedPositions != null) protectedTimer = settings.TailProtectSeconds;
            PlaceTail(1f);
            return count;
        }

        // Unloading into the bin: the last bag of the tail leaves it. Returns where it was.
        public Vector3 TakeLastBag()
        {
            int last = tail.Count - 1;
            var position = tail[last].transform.position;
            bags.Release(tail[last]);
            tail.RemoveAt(last);
            PlaceTail(1f);
            return position;
        }

        // Where a bag thrown from this tail starts flying (clients use it for the animation).
        public Vector3 TailEnd => tail.Count > 0 ? tail[tail.Count - 1].transform.position : transform.position;

        // ---------- Online client ----------

        public void ApplyRemote(Vector3 position, float yaw, int bagCount, float fill, bool boosting, bool tailProtected, bool stunned, bool boostReady)
        {
            if (!hasRemote || (position - transform.position).sqrMagnitude > RemoteSnapDistance * RemoteSnapDistance)
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                path.Clear();
                path.Add(position);
            }
            remotePosition = position;
            remoteYaw = yaw;
            remoteBoosting = boosting;
            remoteProtected = tailProtected;
            remoteStunned = stunned;
            remoteBoostReady = boostReady;
            hasRemote = true;
            clippings = fill * settings.CellsPerBag;

            if (bagCount > tail.Count) AddBags(bagCount - tail.Count);
            else if (bagCount < tail.Count) CutTailAt(bagCount, null);
            UpdateFillIndicator();
        }

        // Follows the host smoothly and mows the grass it drives over (only visual here).
        public void TickRemote(float deltaTime, MowLawn lawn)
        {
            float speed = 0f;
            if (hasRemote)
            {
                var previous = transform.position;
                float blend = Mathf.Min(1f, 12f * deltaTime);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(previous, remotePosition, blend),
                    Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, remoteYaw, 0f), blend));
                speed = (transform.position - previous).magnitude / Mathf.Max(deltaTime, 0.0001f);
                lawn.Cut(BladePosition, settings.BladeRadius);
            }
            AfterMove(deltaTime, remoteBoosting, remoteProtected, remoteStunned, remoteBoostReady, speed);
        }

        // ---------- Shared ----------

        private void AfterMove(float deltaTime, bool boosting, bool tailProtected, bool stunned, bool boostReady, float speed)
        {
            RecordPath();
            PlaceTail(Mathf.Min(1f, 20f * deltaTime));
            UpdateFillIndicator();
            AnimateVisual(deltaTime, boosting, stunned, speed);
            AnimateTail(tailProtected);
            AnimateTurbo(deltaTime, boosting, boostReady);
        }

        // Flames out of the exhaust during the turbo; a ring under the mower when it is ready again,
        // with a little pop the moment it comes back.
        private void AnimateTurbo(float deltaTime, bool boosting, bool ready)
        {
            if (boostFlame != null)
            {
                boostFlame.gameObject.SetActive(boosting);
                if (boosting)
                {
                    float flicker = Random.Range(0.75f, 1.3f);
                    boostFlame.localScale = new Vector3(flameScale.x, flameScale.y, flameScale.z * flicker);
                }
            }
            if (turboRing == null) return;
            if (ready && !wasBoostReady) ringPop = 1f;
            wasBoostReady = ready;
            ringPop = Mathf.Max(0f, ringPop - deltaTime * 4f);
            turboRing.gameObject.SetActive(ready);
            float pulse = 1f + ringPop * 0.6f + Mathf.Sin(Time.time * 5f) * 0.04f;
            turboRing.localScale = new Vector3(ringScale.x * pulse, ringScale.y, ringScale.z * pulse);
        }

        private void RecordPath()
        {
            var position = transform.position;
            if ((position - path[0]).sqrMagnitude >= PathStep * PathStep) path.Insert(0, position);

            // Only keep the path the tail needs.
            int needed = Mathf.CeilToInt((tail.Count + 2) * settings.BagSpacing / PathStep) + 4;
            if (path.Count > needed) path.RemoveRange(needed, path.Count - needed);
        }

        // Bag i sits (i + 1) * spacing behind the mower, measured along the driven path.
        private void PlaceTail(float follow)
        {
            tailPositions.Clear();
            if (tail.Count == 0) return;

            var previous = transform.position;
            var direction = -transform.forward;
            float walked = 0f;
            int next = 0;
            for (int i = 0; i < tail.Count; i++)
            {
                float target = (i + 1) * settings.BagSpacing;
                Vector3 position;
                while (true)
                {
                    if (next >= path.Count)
                    {
                        // Path too short yet (start of the game): pile up straight behind.
                        position = previous + direction * (target - walked);
                        break;
                    }
                    var segment = path[next] - previous;
                    float length = segment.magnitude;
                    if (length > 0.0001f) direction = segment / length;
                    if (walked + length >= target)
                    {
                        position = previous + direction * (target - walked);
                        break;
                    }
                    walked += length;
                    previous = path[next];
                    next++;
                }

                position.y = transform.position.y;
                tailPositions.Add(position);
                var bag = tail[i].transform;
                bag.position = Vector3.Lerp(bag.position, position, follow);
                if (direction.sqrMagnitude > 0.0001f)
                    bag.rotation = Quaternion.Slerp(bag.rotation, Quaternion.LookRotation(-direction), follow);
            }
        }

        // Engine rumble, a lean into the turbo, a spin when rammed and a spinning blade.
        private void AnimateVisual(float deltaTime, bool boosting, bool stunned, float speed)
        {
            bobTime += deltaTime * (boosting ? 30f : 18f);
            float targetLean = boosting ? -8f : -speed * 0.6f;
            lean = Mathf.Lerp(lean, targetLean, Mathf.Min(1f, 8f * deltaTime));
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobTime)) * (boosting ? 0.08f : 0.04f), 0f);
                spin = stunned ? spin + 900f * deltaTime : Mathf.LerpAngle(spin, 0f, Mathf.Min(1f, 10f * deltaTime));
                visual.localRotation = Quaternion.Euler(lean, spin, stunned ? Mathf.Sin(bobTime) * 10f : 0f);
            }
            if (blade != null) blade.Rotate(0f, 900f * deltaTime, 0f, Space.Self);
        }

        // Protected tails pulse so everybody sees they cannot be cut right now.
        private void AnimateTail(bool tailProtected)
        {
            float scale = tailProtected ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.2f : 1f;
            foreach (var bag in tail) bag.transform.localScale = new Vector3(scale, scale, scale);
        }

        private void UpdateFillIndicator()
        {
            if (fillIndicator == null) return;
            float fill = Mathf.Clamp01(Fill);
            fillIndicator.gameObject.SetActive(fill > 0.02f);
            fillIndicator.localScale = new Vector3(fillScale.x, fillScale.y * fill, fillScale.z);
        }
    }
}
