using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Orthographic camera tilted over the garden, sized every frame so the whole playable lawn
    // fills the screen whatever the window shape. The lawn's extra grass covers what is left.
    [RequireComponent(typeof(Camera))]
    public sealed class MowCameraFit : MonoBehaviour
    {
        [SerializeField] private MowTheLawnSettings settings;
        [SerializeField] private Transform lawnCenter;
        [SerializeField, Range(30f, 90f), Tooltip("Degrees the camera looks down.")] private float pitch = 58f;
        [SerializeField, Min(0f), Tooltip("Extra space around the playable area, in metres.")] private float padding = 0.6f;
        [SerializeField, Min(1f)] private float distance = 40f;

        private Camera view;
        private float shakeAmount;

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = true;
        }

        // A short jolt (rams). Fades out by itself.
        public void Shake(float amount) => shakeAmount = Mathf.Max(shakeAmount, amount);

        private void LateUpdate()
        {
            if (settings == null || lawnCenter == null) return;

            // Seen from above at an angle, the ground looks shorter in depth by sin(pitch).
            float width = settings.LawnWidth + padding * 2f;
            float depth = (settings.LawnDepth + padding * 2f) * Mathf.Sin(pitch * Mathf.Deg2Rad);
            view.orthographicSize = Mathf.Max(depth * 0.5f, width * 0.5f / Mathf.Max(0.1f, view.aspect));

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var position = lawnCenter.position - rotation * Vector3.forward * distance;
            if (shakeAmount > 0.001f)
            {
                position += rotation * (Vector3)(Random.insideUnitCircle * shakeAmount);
                shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.unscaledDeltaTime * 1.5f);
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
