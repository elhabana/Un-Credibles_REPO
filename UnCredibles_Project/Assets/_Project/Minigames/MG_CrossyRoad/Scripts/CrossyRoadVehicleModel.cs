using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Fits a modelled vehicle inside the stretched unit cube that CrossyRoadTraffic scales to the lane
    // (x = car length, y = 0.8, z = car depth). The models are built facing +Z; they are turned to drive
    // along +X, kept in proportion (only slightly stretched to fill long lanes) and put on the road.
    // With several models, one is picked each time the vehicle is spawned.
    public sealed class CrossyRoadVehicleModel : MonoBehaviour
    {
        [SerializeField, Tooltip("Child that cancels the lane scale; the models live under it.")]
        private Transform fit;
        [SerializeField] private Transform[] models = new Transform[0];
        [SerializeField, Tooltip("Renderers tinted with the car colour (the body panels).")]
        private Renderer[] paint = new Renderer[0];
        [SerializeField, Min(.1f), Tooltip("Widest the model may be, in metres (a lane is 1.5 m deep).")]
        private float maxWidth = 1.3f;
        [SerializeField, Min(1f), Tooltip("How much the model may be stretched lengthwise to fill a long lane.")]
        private float maxStretch = 1.3f;

        private readonly Dictionary<Transform, Bounds> sizes = new();
        private Transform current;

        public Renderer[] Paint => paint;

        private void OnEnable()
        {
            if (models.Length == 0 || fit == null) return;
            current = models[Random.Range(0, models.Length)];
            foreach (var model in models) model.gameObject.SetActive(model == current);
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (current == null) return;
            var lane = transform.lossyScale;
            var size = SizeOf(current);
            // Model space: z is the length, x the width (turned 90 degrees to drive along +X).
            float byLength = lane.x / Mathf.Max(.01f, size.size.z);
            float byWidth = maxWidth / Mathf.Max(.01f, size.size.x);
            float scale = Mathf.Min(byLength, byWidth);
            float stretch = Mathf.Clamp(byLength / scale, 1f, maxStretch);
            fit.localScale = new Vector3(scale * stretch / Mathf.Max(.01f, lane.x), scale / Mathf.Max(.01f, lane.y),
                scale / Mathf.Max(.01f, lane.z));
            current.localRotation = Quaternion.Euler(0f, 90f, 0f);
            current.localScale = Vector3.one;
            // Centre it lengthwise and sit it on the road; z of the model becomes x after the turn.
            current.localPosition = new Vector3(-size.center.z, -size.min.y, size.center.x);
        }

        // Bounds of the model in its own space, measured once.
        private Bounds SizeOf(Transform model)
        {
            if (sizes.TryGetValue(model, out var bounds)) return bounds;
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var toModel = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                    var point = toModel.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            // Measured with the model unscaled and unrotated relative to its parent.
            sizes[model] = bounds;
            return bounds;
        }
    }
}
