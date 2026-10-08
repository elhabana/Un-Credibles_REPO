using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // A player's bin in a corner of the lawn. Bags only score once they are thrown in here.
    // Solid for every mower; only its owner can unload into it. It bounces when it gets a bag.
    public sealed class MowBin : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float BounceSeconds = 0.25f;

        [SerializeField] private Transform visual;
        [SerializeField, Tooltip("Tinted with the owner's colour.")] private Renderer[] tintedRenderers = new Renderer[0];
        [SerializeField, Tooltip("Where thrown bags land.")] private Transform mouth;

        private float bounceTimer;

        public int OwnerSlot { get; private set; }
        public Vector3 Position => transform.position;
        public Vector3 MouthPosition => mouth != null ? mouth.position : transform.position + Vector3.up;

        public void Setup(int ownerSlot, Color color)
        {
            OwnerSlot = ownerSlot;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var tinted in tintedRenderers) tinted.SetPropertyBlock(block);
        }

        public void Bounce() => bounceTimer = BounceSeconds;

        // Ticked by MowTheLawnController.
        public void Tick(float deltaTime)
        {
            if (visual == null) return;
            if (bounceTimer > 0f) bounceTimer -= deltaTime;
            float t = Mathf.Clamp01(bounceTimer / BounceSeconds);
            float squash = Mathf.Sin(t * Mathf.PI) * 0.15f;
            visual.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
        }
    }
}
