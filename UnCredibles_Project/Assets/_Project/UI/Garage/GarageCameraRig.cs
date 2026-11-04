using UnCredibles.Core;
using UnityEngine;

namespace UnCredibles.UI.Garage
{
    public enum GarageView { Home, Lobby, Settings, Credits, Gallery }

    // Viewpoints are authored in the scene. Each computer owns its camera independently.
    // Turning to the sides is a quick dash (snappy ease, FOV kick, lean into the turn, whoosh);
    // zooming into the TV for the settings keeps the calm move.
    public sealed class GarageCameraRig : MonoBehaviour
    {
        public Camera output;
        public Transform[] viewpoints;
        [Min(0.05f)] public float duration = 0.8f;
        [Min(0.05f)] public float dashDuration = 0.45f;
        public float dashFovKick = 14f;
        public float dashRoll = 5f;
        public AudioClip dashSound;
        public GarageView View { get; private set; }
        public bool IsTransitioning { get; private set; }
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private float elapsed;
        private bool dashing;
        private float roll;
        private float baseFov;

        private void Awake() => baseFov = output.fieldOfView;

        public void Show(GarageView view, bool immediate = false)
        {
            dashing = !immediate && view != View && view != GarageView.Settings && View != GarageView.Settings;
            View = view;
            fromPosition = output.transform.position;
            fromRotation = output.transform.rotation;
            elapsed = immediate ? duration : 0f;
            IsTransitioning = !immediate;
            if (dashing)
            {
                var target = viewpoints[(int)view];
                float turn = Vector3.SignedAngle(fromRotation * Vector3.forward, target.forward, Vector3.up);
                roll = -Mathf.Sign(turn) * dashRoll;
                if (dashSound != null && CoreRoot.Instance != null) CoreRoot.Instance.Audio.PlaySfx(dashSound);
            }
            Apply();
        }

        private void Update()
        {
            if (!IsTransitioning) return;
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            var target = viewpoints[(int)View];
            float length = dashing ? dashDuration : duration;
            float x = Mathf.Clamp01(elapsed / length);
            float t = dashing ? EaseInOutQuart(x) : Mathf.SmoothStep(0f, 1f, x);
            var rotation = Quaternion.Slerp(fromRotation, target.rotation, t);
            float pulse = dashing ? Mathf.Sin(x * Mathf.PI) : 0f;
            if (dashing) rotation *= Quaternion.Euler(0f, 0f, roll * pulse);
            output.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, target.position, t), rotation);
            if (baseFov > 0f) output.fieldOfView = baseFov + dashFovKick * pulse;
            IsTransitioning = elapsed < length;
        }

        // Slow start, fast middle, soft landing: reads as a burst of speed.
        private static float EaseInOutQuart(float x) =>
            x < .5f ? 8f * x * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 4f) / 2f;
    }
}
