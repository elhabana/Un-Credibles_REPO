using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // One grass bag on screen: in a mower's tail (tie in the owner's colour) or loose on the lawn.
    public sealed class MowBagView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Tinted with the owner's colour; white when the bag is loose.")]
        private Renderer tie;

        private MaterialPropertyBlock block;

        public void SetTint(Color color)
        {
            if (tie == null) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            tie.SetPropertyBlock(block);
        }
    }
}
