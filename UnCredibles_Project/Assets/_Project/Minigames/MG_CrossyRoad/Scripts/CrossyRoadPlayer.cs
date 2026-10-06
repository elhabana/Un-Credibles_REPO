using System;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Avatar of one player: walks freely in any direction reading only IPlayerInput.
    // Ticked by CrossyRoadController, it has no Update of its own.
    public sealed class CrossyRoadPlayer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float BlinkInterval = 0.1f;

        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField] private GameObject carriedGrandma;

        private CrossyRoadBoard board;
        private CrossyRoadSettings settings;
        private float invulnerableTimer;
        private float bobTime;
        private float deadTime;
        private Vector3 velocity;
        private float slipTimer;
        private bool wasOnOil;
        private float wobbleTime;

        public PlayerSlot Slot { get; private set; }
        public Vector3 SpawnPosition { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsCarrying { get; private set; }
        public int CarriedGrandma { get; private set; } = -1;
        public bool IsInvulnerable => invulnerableTimer > 0f;
        public float CurrentSpeed => settings.SpeedFor(IsCarrying);
        public Vector3 Position => transform.position;
        // Where the carried grandma sits, used to launch her when the player is hit.
        public Vector3 CarryPosition => carriedGrandma != null ? carriedGrandma.transform.position : transform.position + Vector3.up;
        public int Lane => board.WorldToLane(transform.position.z);

        public void Setup(PlayerSlot slot, CrossyRoadBoard gameBoard, Vector3 spawnPosition, Color color, Color grandmaColor)
        {
            Slot = slot;
            board = gameBoard;
            settings = gameBoard.Settings;
            SpawnPosition = spawnPosition;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var body in bodyRenderers) body.SetPropertyBlock(block);
            if (carriedGrandma != null)
            {
                block.SetColor(BaseColorId, grandmaColor);
                foreach (var grandmaRenderer in carriedGrandma.GetComponentsInChildren<Renderer>(true))
                    grandmaRenderer.SetPropertyBlock(block);
            }

            SetCarrying(-1);
            Respawn(spawnPosition, 0f);
        }

        public bool IsSlipping => slipTimer > 0f;

        // Moves with the stick/keys. Walking into a car is blocked; the avatar slides along it.
        // On oil it keeps its momentum: it can barely steer and goes further than wanted.
        public void Tick(float deltaTime, Func<Vector3, CrossyRoadPlayer, bool> isBlocked, bool onOil)
        {
            if (!IsAlive) return;
            TickInvulnerability(deltaTime);

            var input = Slot.Input != null ? Slot.Input.Move : Vector2.zero;
            bool hasInput = input.sqrMagnitude >= settings.InputDeadZone * settings.InputDeadZone;
            var direction = hasInput ? new Vector3(input.x, 0f, input.y) : Vector3.zero;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            var desired = direction * CurrentSpeed;

            if (onOil && !wasOnOil) velocity *= settings.OilSlideBoost; // the "whoops" lurch
            wasOnOil = onOil;
            if (onOil) slipTimer = settings.OilAfterSlip;
            else if (slipTimer > 0f) slipTimer -= deltaTime;

            velocity = IsSlipping ? SlideOnIce(velocity, direction, deltaTime) : desired;

            var step = velocity * deltaTime;
            var current = transform.position;
            var target = board.ClampInside(current + step, settings.PlayerRadius);
            if (isBlocked(target, this))
            {
                // Try each axis on its own so the player slides instead of sticking.
                var alongX = board.ClampInside(current + new Vector3(step.x, 0f, 0f), settings.PlayerRadius);
                var alongZ = board.ClampInside(current + new Vector3(0f, 0f, step.z), settings.PlayerRadius);
                if (!isBlocked(alongX, this)) { target = alongX; velocity.z = 0f; }
                else if (!isBlocked(alongZ, this)) { target = alongZ; velocity.x = 0f; }
                else { target = current; velocity = Vector3.zero; }
            }
            // Board edges stop the slide on that axis.
            if (!Mathf.Approximately(target.x, current.x + step.x)) velocity.x = 0f;
            if (!Mathf.Approximately(target.z, current.z + step.z)) velocity.z = 0f;
            transform.position = target;

            if (hasInput)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), settings.TurnSpeed * deltaTime);

            AnimateVisual(deltaTime, hasInput);
        }

        // Ice physics: the stick only pushes, almost nothing slows you down, and you can
        // build up more speed than walking. Stopping or turning means pushing the other way.
        private Vector3 SlideOnIce(Vector3 current, Vector3 inputDirection, float deltaTime)
        {
            current += inputDirection * (settings.OilAcceleration * deltaTime);
            current *= Mathf.Max(0f, 1f - settings.OilFriction * deltaTime);
            return Vector3.ClampMagnitude(current, CurrentSpeed * settings.OilMaxSpeedMultiplier);
        }

        // Walking bob, plus a comic wobble while slipping on oil.
        private void AnimateVisual(float deltaTime, bool walking)
        {
            float height = 0f;
            if (walking && !IsSlipping)
            {
                bobTime += deltaTime * settings.WalkBobFrequency;
                height = Mathf.Abs(Mathf.Sin(bobTime)) * settings.WalkBobHeight;
            }
            else bobTime = 0f;
            visual.localPosition = new Vector3(0f, height, 0f);

            wobbleTime = IsSlipping ? wobbleTime + deltaTime : 0f;
            visual.localRotation = IsSlipping
                ? Quaternion.Euler(Mathf.Sin(wobbleTime * 17f) * 12f, 0f, Mathf.Sin(wobbleTime * 23f) * 20f)
                : Quaternion.identity;
        }

        // Used by the controller to keep avatars from overlapping each other.
        public void MoveTo(Vector3 position) => transform.position = position;

        public void SetCarrying(int grandmaIndex)
        {
            CarriedGrandma = grandmaIndex;
            IsCarrying = grandmaIndex >= 0;
            if (carriedGrandma != null) carriedGrandma.SetActive(IsCarrying);
        }

        // Cartoon death: flattened on the road, then shrinks away at the end of the respawn delay.
        public void Kill()
        {
            IsAlive = false;
            velocity = Vector3.zero;
            slipTimer = 0f;
            visual.localRotation = Quaternion.identity;
            deadTime = 0f;
            invulnerableTimer = 0f;
            visual.localPosition = Vector3.zero;
            visual.gameObject.SetActive(true);
        }

        public void TickDead(float deltaTime)
        {
            if (!visual.gameObject.activeSelf) return;
            deadTime += deltaTime;

            float squash = Mathf.Clamp01(deadTime / settings.SquashDuration);
            var scale = Vector3.Lerp(Vector3.one, settings.SquashScale, squash);

            float vanishStart = settings.RespawnDelay - settings.VanishDuration;
            if (deadTime > vanishStart)
            {
                float shrink = settings.VanishDuration > 0f ? 1f - (deadTime - vanishStart) / settings.VanishDuration : 0f;
                scale *= Mathf.Clamp01(shrink);
            }

            visual.localScale = scale;
            if (scale.x <= 0.001f) visual.gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position, float invulnerableSeconds)
        {
            IsAlive = true;
            bobTime = 0f;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            velocity = Vector3.zero;
            slipTimer = 0f;
            wasOnOil = false;
            invulnerableTimer = invulnerableSeconds;
            visual.gameObject.SetActive(true);
        }

        private void TickInvulnerability(float deltaTime)
        {
            if (invulnerableTimer <= 0f) return;
            invulnerableTimer -= deltaTime;
            bool visible = invulnerableTimer <= 0f || Mathf.FloorToInt(invulnerableTimer / BlinkInterval) % 2 == 0;
            visual.gameObject.SetActive(visible);
        }
    }
}
