using UnityEngine;

namespace UnCredibles.UI.Garage
{
    public enum GarageView { Home, Lobby, Settings, Credits, Gallery }

    // Viewpoints are authored in the scene. Each computer owns its camera independently.
    public sealed class GarageCameraRig : MonoBehaviour
    {
        public Camera output;
        public Transform[] viewpoints;
        [Min(0.05f)] public float duration = 0.8f;
        public GarageView View { get; private set; }
        public bool IsTransitioning { get; private set; }
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private float elapsed;

        public void Show(GarageView view, bool immediate = false)
        {
            View = view;
            fromPosition = output.transform.position;
            fromRotation = output.transform.rotation;
            elapsed = immediate ? duration : 0f;
            IsTransitioning = !immediate;
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
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            output.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, target.position, t),
                Quaternion.Slerp(fromRotation, target.rotation, t));
            IsTransitioning = elapsed < duration;
        }
    }
}
