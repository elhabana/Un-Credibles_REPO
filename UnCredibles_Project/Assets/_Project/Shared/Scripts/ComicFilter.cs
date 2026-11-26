using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnCredibles.Shared
{
    // Comic look over this object (and its children) without touching their materials: before each
    // camera renders, every mesh is drawn once more with the ComicFilter material on top of itself.
    // Disable or remove the component and everything looks as before. Works in the editor too.
    // Animated characters (SkinnedMeshRenderer): in Play Mode each one gets an overlay, a child SkinnedMeshRenderer on
    // the same bones and mesh drawn with the filter, so the GPU skins it like any character. Baking every part to a
    // mesh on the CPU each frame (still used for edit-mode previews) cost up to ~25 ms per frame in Crossy Road.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ComicFilter : MonoBehaviour
    {
        [SerializeField, Tooltip("M_Shared_ComicFilter, or a copy with other settings.")]
        private Material filter;
        [SerializeField, Tooltip("Also every child of this object.")]
        private bool includeChildren = true;

        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        private readonly List<MeshFilter> meshFilters = new List<MeshFilter>();
        private readonly List<SkinnedMeshRenderer> skinned = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> baked = new Dictionary<SkinnedMeshRenderer, Mesh>();
        private readonly Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> overlays = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();
        private readonly HashSet<SkinnedMeshRenderer> overlaySet = new HashSet<SkinnedMeshRenderer>();
        private const string OverlayName = "ComicFilter overlay";
        private int bakedFrame = -1;

        public Material Filter { get => filter; set => filter = value; }

        private void OnEnable()
        {
            Collect();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            foreach (var overlay in overlays.Values)
                if (overlay != null) overlay.enabled = false; // switched off with the filter, back in Collect
        }

        private void OnDestroy()
        {
            foreach (var mesh in baked.Values) Kill(mesh);
            baked.Clear();
            foreach (var overlay in overlays.Values)
                if (overlay != null) Kill(overlay.gameObject);
            overlays.Clear();
            overlaySet.Clear();
        }

        private void OnTransformChildrenChanged() => Collect();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Collect();
        }

        // Call it after adding props or parts under this object from code; the editor and new direct children do it alone.
        public void Collect()
        {
            renderers.Clear();
            meshFilters.Clear();
            var found = includeChildren ? GetComponentsInChildren<MeshRenderer>(true) : GetComponents<MeshRenderer>();
            foreach (var meshRenderer in found)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null) continue;
                renderers.Add(meshRenderer);
                meshFilters.Add(meshFilter);
            }

            skinned.Clear();
            foreach (var skin in includeChildren ? GetComponentsInChildren<SkinnedMeshRenderer>(true) : GetComponents<SkinnedMeshRenderer>())
                if (!overlaySet.Contains(skin) && skin.name != OverlayName) skinned.Add(skin);
            var stale = new List<SkinnedMeshRenderer>();
            foreach (var key in baked.Keys)
                if (!skinned.Contains(key)) stale.Add(key);
            foreach (var key in stale)
            {
                Kill(baked[key]);
                baked.Remove(key);
            }
            bakedFrame = -1;

            stale.Clear();
            foreach (var key in overlays.Keys)
                if (key == null || !skinned.Contains(key)) stale.Add(key);
            foreach (var key in stale)
            {
                if (overlays[key] != null) Kill(overlays[key].gameObject);
                overlaySet.Remove(overlays[key]);
                overlays.Remove(key);
            }
            if (Application.isPlaying && filter != null)
                foreach (var skin in skinned) EnsureOverlay(skin);
        }

        // Same mesh, bones and bounds as the part, every submesh drawn with the filter. A child of the part, so it
        // follows its GameObject on and off; its renderer follows the part's in Draw.
        private void EnsureOverlay(SkinnedMeshRenderer skin)
        {
            if (!overlays.TryGetValue(skin, out var overlay) || overlay == null)
            {
                var go = new GameObject(OverlayName) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(skin.transform, false);
                overlay = go.AddComponent<SkinnedMeshRenderer>();
                overlays[skin] = overlay;
                overlaySet.Add(overlay);
            }
            overlay.gameObject.layer = skin.gameObject.layer;
            overlay.sharedMesh = skin.sharedMesh;
            overlay.bones = skin.bones;
            overlay.rootBone = skin.rootBone;
            overlay.localBounds = skin.localBounds;
            overlay.updateWhenOffscreen = skin.updateWhenOffscreen;
            overlay.quality = skin.quality;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = true;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            int submeshes = skin.sharedMesh != null ? skin.sharedMesh.subMeshCount : 1;
            var materials = new Material[Mathf.Max(1, submeshes)];
            for (int i = 0; i < materials.Length; i++) materials[i] = filter;
            overlay.sharedMaterials = materials;
            if (skin.sharedMesh != null)
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    overlay.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            overlay.enabled = skin.enabled;
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (filter == null) return;
            var parameters = new RenderParams(filter)
            {
                camera = camera,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };
            for (int i = 0; i < renderers.Count; i++)
            {
                var meshRenderer = renderers[i];
                if (!Visible(meshRenderer)) continue;
                var mesh = meshFilters[i] != null ? meshFilters[i].sharedMesh : null;
                if (mesh == null) continue;
                parameters.layer = meshRenderer.gameObject.layer;

                if (meshRenderer.isPartOfStaticBatch)
                {
                    // Static batching merged it into one big mesh, already in world space.
                    int count = meshRenderer.sharedMaterials.Length;
                    for (int s = 0; s < count; s++)
                        Graphics.RenderMesh(parameters, mesh, meshRenderer.subMeshStartIndex + s, Matrix4x4.identity);
                }
                else
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        Graphics.RenderMesh(parameters, mesh, s, meshRenderer.localToWorldMatrix);
                }
            }

            // Every camera this frame (game, portraits, minimap) shares one bake.
            bool bake = bakedFrame != Time.renderedFrameCount;
            bakedFrame = Time.renderedFrameCount;
            foreach (var skin in skinned)
            {
                if (skin != null && overlays.TryGetValue(skin, out var overlay) && overlay != null)
                {
                    overlay.enabled = skin.enabled; // the GPU draws it; parts switched off by the outfit stay off
                    continue;
                }
                if (!Visible(skin) || skin.sharedMesh == null) continue;
                if (!baked.TryGetValue(skin, out var mesh) || mesh == null)
                {
                    mesh = new Mesh { name = skin.name + "_Comic", hideFlags = HideFlags.HideAndDontSave };
                    baked[skin] = mesh;
                    bake = true;
                }
                if (bake) skin.BakeMesh(mesh, false);
                parameters.layer = skin.gameObject.layer;
                // Measured: without useScale the bake already comes out at world size (bones included),
                // so it only needs position and rotation; useScale: true applied the scale twice.
                var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                for (int s = 0; s < mesh.subMeshCount; s++)
                    Graphics.RenderMesh(parameters, mesh, s, matrix);
            }
        }

        private static bool Visible(Renderer renderer) =>
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;

        private static void Kill(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
