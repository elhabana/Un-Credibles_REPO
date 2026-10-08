using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Avatar standing on a float. It can jump (churro) or duck while holding Crouch (beach balls);
    // never both at once. When hit it flies into the water.
    // Ticked by ChurroController, it has no Update of its own.
    public sealed class ChurroPlayer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float SinkDepth = 1.5f;

        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];

        private ChurroSettings settings;
        private Vector3 floatPosition;
        private Quaternion floatRotation;
        private Vector3 knockVelocity;
        private float height;
        private float verticalSpeed;
        private Vector3 remotePosition;
        private Quaternion remoteRotation;
        private float remoteHeight;
        private bool hasRemote;
        private bool remoteDucking;
        private float duck; // 0 standing .. 1 fully crouched, only visual

        public PlayerSlot Slot { get; private set; }
        public float Angle { get; private set; }
        public bool IsIn { get; private set; }            // still playing this round
        public bool IsGrounded => IsIn && height <= 0f;
        public float FeetHeight => height;
        public Vector3 FloatPosition => floatPosition;
        public bool IsDucking { get; private set; }

        public void Setup(PlayerSlot slot, ChurroSettings churroSettings, Vector3 center, Color color)
        {
            Slot = slot;
            settings = churroSettings;
            floatPosition = transform.position;
            Angle = ChurroSpinner.AngleOf(center, floatPosition);

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var body in bodyRenderers) body.SetPropertyBlock(block);

            // Face the kid in the middle.
            var toCenter = center - floatPosition;
            toCenter.y = 0f;
            floatRotation = toCenter.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCenter) : transform.rotation;
            ResetOnFloat();
        }

        public void ResetOnFloat()
        {
            IsIn = true;
            height = 0f;
            verticalSpeed = 0f;
            transform.SetPositionAndRotation(floatPosition, floatRotation);
            visual.localPosition = Vector3.zero;
            IsDucking = false;
            duck = 0f;
            visual.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        public void Tick(float deltaTime, bool canJump)
        {
            if (IsIn) TickOnFloat(deltaTime, canJump);
            else TickKnockout(deltaTime);
        }

        public void KnockOut(Vector3 awayFromCenter)
        {
            IsIn = false;
            awayFromCenter.y = 0f;
            knockVelocity = awayFromCenter.normalized * settings.KnockoutSpeed + Vector3.up * settings.KnockoutSpeed * 0.6f;
            transform.position = floatPosition + Vector3.up * height;
            visual.localPosition = Vector3.zero;
            IsDucking = false;
            visual.localScale = Vector3.one;
        }

        private void TickOnFloat(float deltaTime, bool canJump)
        {
            var input = Slot.Input;
            // Ducking only on the float; while ducking there is no jump.
            IsDucking = canJump && height <= 0f && input != null && input.IsHeld(PlayerAction.Crouch);
            AnimateDuck(deltaTime, IsDucking);
            if (!IsDucking && canJump && height <= 0f && input != null && input.WasPressed(PlayerAction.Jump))
                verticalSpeed = settings.JumpVelocity;

            if (height <= 0f && verticalSpeed <= 0f) return;
            verticalSpeed -= settings.Gravity * deltaTime;
            height = Mathf.Max(0f, height + verticalSpeed * deltaTime);
            if (height <= 0f) verticalSpeed = 0f;
            visual.localPosition = new Vector3(0f, height, 0f);
        }

        // ---------- Online ----------

        // Bytes written by WriteState (lets a client skip an avatar it does not have).
        public const int StateSize = 1 + 1 + 4 + 1 + 3 * 4 + 4 * 4;

        // Host: what a client needs to draw this avatar.
        public void WriteState(BinaryWriter writer)
        {
            var position = transform.position;
            var rotation = transform.rotation;
            writer.Write(IsIn);
            writer.Write(gameObject.activeSelf);
            writer.Write(FeetHeight);
            writer.Write(IsDucking);
            writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
            writer.Write(rotation.x); writer.Write(rotation.y); writer.Write(rotation.z); writer.Write(rotation.w);
        }

        // Client: the host decides; we only follow its position smoothly.
        public void ReadState(BinaryReader reader)
        {
            bool isIn = reader.ReadBoolean();
            bool visible = reader.ReadBoolean();
            float feetHeight = reader.ReadSingle();
            bool ducking = reader.ReadBoolean();
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            bool appeared = visible && !gameObject.activeSelf;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            IsIn = isIn;
            remoteDucking = ducking;
            remoteHeight = isIn ? feetHeight : 0f;
            remotePosition = position;
            remoteRotation = rotation;
            hasRemote = true;
            if (appeared || (transform.position - position).sqrMagnitude > 9f)
            {
                transform.SetPositionAndRotation(position, rotation);
                height = remoteHeight;
            }
        }

        public void TickRemote(float deltaTime)
        {
            if (!gameObject.activeSelf || !hasRemote) return;
            float blend = Mathf.Min(1f, 15f * deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, remotePosition, blend),
                Quaternion.Slerp(transform.rotation, remoteRotation, blend));
            height = Mathf.Lerp(height, remoteHeight, blend);
            visual.localPosition = new Vector3(0f, height, 0f);
            AnimateDuck(deltaTime, remoteDucking && IsIn);
        }

        // Squashed down while ducking, back up quickly when released.
        private void AnimateDuck(float deltaTime, bool ducking)
        {
            duck = Mathf.MoveTowards(duck, ducking ? 1f : 0f, deltaTime * 12f);
            visual.localScale = new Vector3(1f + duck * 0.2f, 1f - duck * 0.5f, 1f + duck * 0.2f);
        }

        // Simple ballistic flight into the pool, then hidden until the next round.
        private void TickKnockout(float deltaTime)
        {
            if (!gameObject.activeSelf) return;
            knockVelocity.y -= settings.Gravity * deltaTime;
            transform.position += knockVelocity * deltaTime;
            transform.Rotate(720f * deltaTime, 0f, 0f, Space.Self);
            if (transform.position.y < floatPosition.y - SinkDepth) gameObject.SetActive(false);
        }
    }
}
