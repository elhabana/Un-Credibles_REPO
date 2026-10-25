using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames.GarageMice
{
    // The player's avatar: the controller owns its state (stun, swing); this only draws it.
    public sealed class GarageMicePlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Shakes while stunned; holds the body and the broom.")] private Transform visual;
        [SerializeField, Tooltip("Rotates on each swing.")] private Transform broom;
        [SerializeField, Tooltip("Shown and spinning above the head while stunned.")] private Transform stars;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];

        public void Setup(PlayerSlot slot)
        {
            stars.gameObject.SetActive(false);
            PlayerPresentation.Attach(this, slot, bodyRenderers);
        }

        // swing: 1 when the swing starts, 0 when it is over.
        public void Animate(float swing, bool stunned, float deltaTime)
        {
            broom.localRotation = Quaternion.Euler(0f, swing > 0f ? -85f * Mathf.Sin(swing * Mathf.PI) : 0f, -20f);
            stars.gameObject.SetActive(stunned);
            if (stunned)
            {
                stars.Rotate(Vector3.up, 290f * deltaTime, Space.Self);
                visual.localPosition = new Vector3(Mathf.Sin(Time.time * 38f) * .055f, 0f, 0f);
            }
            else visual.localPosition = Vector3.zero;
        }
    }
}
