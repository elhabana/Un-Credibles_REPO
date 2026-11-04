using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnCredibles.Shared
{
    // Comic look over this object (and its children) without touching their materials: before each
    // camera renders, every mesh is drawn once more with the ComicFilter material on top of itself.
    // Disable or remove the component and everything looks as before. Works in the editor too.
    // shortcut: only MeshRenderers (props, walls, furniture); animated characters are left out.
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

        private void OnEnable()
        {
            Collect();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        private void OnTransformChildrenChanged() => Collect();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Collect();
        }

        // Call it after adding props under this object from code; the editor and new children do it alone.
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
                if (meshRenderer == null || !meshRenderer.enabled || !meshRenderer.gameObject.activeInHierarchy) continue;
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
        }
    }
}
