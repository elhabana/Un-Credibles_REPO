#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnCredibles.UI.Garage.Editor
{
    // Explicit authoring command: paints the graffiti logo on the brick wall seen from the home view (a quad just
    // off the bricks) and hides the old screen-space title. Run it again to re-place it; fine-tune the quad by hand.
    public static class GarageTitleWall
    {
        private const string TexturePath = "Assets/_Project/UI/Garage/Art/T_Title_Graffiti.png";
        private const string MaterialPath = "Assets/_Project/UI/Garage/Materials/M_Garage_TitleGraffiti.mat";
        private const string QuadName = "Title graffiti";
        private const string UiTitle = "Home options/Title"; // under the menu root
        private const string WallRoot = "SM_Garage";          // the garage shell model; not SM_GarageDoor
        // Where the logo's centre lands in the home view (viewport 0..1) and how much of the view's width it spans:
        // right of the diagonal panel, above the sofa and clear of the TV.
        private static readonly Vector2 ViewportCentre = new Vector2(.575f, .815f);
        private const float ViewWidth = .56f;
        private const float ReferenceAspect = 16f / 9f;
        private const float WallGap = .015f; // off the bricks: no z-fighting

        [MenuItem("UnCredibles/Garage/Paint title on wall")]
        public static void Paint()
        {
            var texture = ConfigureTexture(out float aspect);
            var front = Object.FindFirstObjectByType<GarageFrontend>();
            var rig = front != null ? front.cameras : null;
            if (texture == null || rig == null || rig.output == null || rig.viewpoints == null || rig.viewpoints.Length == 0)
            {
                Debug.LogError("[Title] Open SCN_MainMenu (needs the Garage frontend, its camera rig and " + TexturePath + ").");
                return;
            }

            var home = rig.viewpoints[(int)GarageView.Home];
            float tan = Mathf.Tan(rig.output.fieldOfView * .5f * Mathf.Deg2Rad);
            var direction = home.rotation * new Vector3((ViewportCentre.x * 2f - 1f) * tan * ReferenceAspect,
                (ViewportCentre.y * 2f - 1f) * tan, 1f);
            if (!RaycastWall(new Ray(home.position, direction), out var point, out var normal))
            {
                Debug.LogError("[Title] The home view does not hit the garage wall (" + WallRoot + "*): place the quad by hand.");
                return;
            }

            var quad = GameObject.Find("/" + QuadName);
            if (quad == null)
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = QuadName;
                Undo.RegisterCreatedObjectUndo(quad, "Paint title on wall");
            }
            else Undo.RecordObject(quad.transform, "Paint title on wall");

            float distance = Vector3.Dot(point - home.position, home.forward);
            float width = 2f * distance * tan * ReferenceAspect * ViewWidth;
            // A quad shows its -Z face: +Z goes into the wall, so the text reads from the room unmirrored.
            quad.transform.SetPositionAndRotation(point + normal * WallGap, Quaternion.LookRotation(-normal, Vector3.up));
            quad.transform.localScale = new Vector3(width, width / aspect, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            Undo.RecordObject(renderer, "Paint title on wall");
            renderer.sharedMaterial = TitleMaterial(texture);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            var title = front.menuRoot != null ? front.menuRoot.transform.Find(UiTitle) : null;
            if (title != null && title.gameObject.activeSelf)
            {
                Undo.RecordObject(title.gameObject, "Paint title on wall");
                title.gameObject.SetActive(false);
            }
            else if (title == null) Debug.LogWarning("[Title] No UI title at " + UiTitle + " under the menu root; nothing hidden.");

            EditorSceneManager.MarkSceneDirty(quad.scene);
            Selection.activeGameObject = quad;
            Debug.Log($"[Title] Graffiti on the wall at {quad.transform.position}, {width:0.00} m wide. Save the scene.");
        }

        // Plain sRGB colour texture with real transparency; clamped so the drips do not wrap to the top edge.
        private static Texture2D ConfigureTexture(out float aspect)
        {
            aspect = 1f;
            if (!(AssetImporter.GetAtPath(TexturePath) is TextureImporter importer)) return null;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; // no dark fringe around the cream outline
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.ToNearest; // 2048 x 512: compresses and mips cleanly; the quad keeps the real aspect
            importer.mipmapEnabled = true; // seen a few metres away and during the zoom: no shimmer
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;       // the wall is seen at an angle
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            aspect = (float)width / Mathf.Max(1, height);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        // URP Lit, so the garage lights shade it like the bricks; alpha blended, no depth or shadows of its own.
        private static Material TitleMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", .15f); // spray paint: a little sheen, not glossy
            material.SetFloat("_Surface", 1f);      // Transparent
            material.SetFloat("_Blend", 0f);        // Alpha
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ReceiveShadows", 1f);
            if (material.HasProperty("_CastShadows")) material.SetFloat("_CastShadows", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // The garage model has no colliders: temporary hidden mesh colliders on its pieces, removed right after.
        private static bool RaycastWall(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            var probes = new List<Collider>();
            try
            {
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (!root.activeInHierarchy || !root.name.StartsWith(WallRoot) || root.name.StartsWith(WallRoot + "Door")) continue;
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    {
                        if (filter.sharedMesh == null) continue;
                        var probe = EditorUtility.CreateGameObjectWithHideFlags("Title wall probe", HideFlags.HideAndDontSave, typeof(MeshCollider));
                        probe.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                        probe.transform.localScale = filter.transform.lossyScale;
                        var collider = probe.GetComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        probes.Add(collider);
                    }
                }
                Physics.SyncTransforms();
                float nearest = float.MaxValue;
                // Other colliders in the scene (props, old blockout) are ignored: only the wall counts.
                foreach (var hit in Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.distance >= nearest || !probes.Contains(hit.collider)) continue;
                    nearest = hit.distance;
                    point = hit.point;
                    normal = Vector3.Dot(hit.normal, ray.direction) > 0f ? -hit.normal : hit.normal;
                }
                return nearest < float.MaxValue;
            }
            finally
            {
                foreach (var probe in probes) Object.DestroyImmediate(probe.gameObject);
            }
        }
    }
}
#endif
