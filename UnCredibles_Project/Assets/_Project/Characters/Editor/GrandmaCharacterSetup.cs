using System.Linq;
using UnCredibles.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Characters.Editor
{
    // Sets up the animated grandma (SK_Shared_Grandma.fbx, one bone) and puts her into the
    // CrossyRoad grandma prefab. Called by PJCharacterSetup, which also puts her on top of the character.
    public static class GrandmaCharacterSetup
    {
        private const string Folder = "Assets/_Project/Characters/Grandma";
        private const string ModelPath = Folder + "/SK_Shared_Grandma.fbx";
        private const string ControllerPath = Folder + "/AC_Shared_Grandma.controller";
        public const string PrefabPath = Folder + "/PF_Shared_Grandma.prefab";
        private const string CrossyGrandmaPath = "Assets/_Project/Minigames/MG_CrossyRoad/Prefabs/PF_CR_Grandma.prefab";
        private const string IdleClipName = "AN_Shared_Grandma_Idle";
        private const string SwayClipName = "AN_Shared_Grandma_Sway";
        private const string SpinClipName = "AN_Shared_Grandma_Spin";
        private const string CharacterChild = "Grandma";
        private const string TintedPart = "CHAQUETA";
        private const float CrossyHeight = 1.18f; // same height as the old capsule grandma

        private static readonly string[] OldParts = { "Body", "Head" };

        // Colour of each part of the model (the jacket is painted later with the minigame colour).
        private static readonly (string part, Color color)[] PartColors =
        {
            ("CUERPO", new Color(.96f, .80f, .68f)),
            ("PELO", new Color(.86f, .86f, .90f)),
            ("CHAQUETA", new Color(.60f, .45f, .78f)),
            ("CAMISA", new Color(.96f, .94f, .88f)),
            ("FALDA", new Color(.32f, .26f, .38f)),
            ("ZAPATO", new Color(.26f, .18f, .13f)),
            ("GAFAS", new Color(.16f, .16f, .19f)),
        };

        public static GameObject Build(Material template)
        {
            if (!ConfigureModel()) return null;
            var controller = BuildController();
            var prefab = BuildPrefab(controller, template);
            IntegrateCrossy(prefab);
            return prefab;
        }

        private static bool ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[Abuela] No se encuentra " + ModelPath);
                return false;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;

            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                importer.SaveAndReimport();
                clips = importer.defaultClipAnimations;
            }
            foreach (var clip in clips)
            {
                if (clip.takeName.EndsWith("Idle")) clip.name = IdleClipName;
                else if (clip.takeName.EndsWith("Balanceo")) clip.name = SwayClipName;
                else if (clip.takeName.EndsWith("Giro")) clip.name = SpinClipName;
                clip.loopTime = true;
                clip.loopPose = false; // first and last frames are already identical
                clip.lockRootRotation = false;
                clip.lockRootHeightY = false;
                clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log("[Abuela] FBX configurado (clips: " + string.Join(", ", clips.Select(c => c.name)) + ")");
            return true;
        }

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            controller.AddParameter("Carried", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Spinning", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var idle = machine.AddState("Idle", new Vector3(250f, 0f));
            idle.motion = Clip(IdleClipName);
            var sway = machine.AddState("Sway", new Vector3(250f, 90f));
            sway.motion = Clip(SwayClipName);
            var spin = machine.AddState("Spin", new Vector3(520f, 45f));
            spin.motion = Clip(SpinClipName);
            machine.defaultState = idle;

            void Link(AnimatorState from, AnimatorState to, float duration, params (AnimatorConditionMode mode, string parameter)[] conditions)
            {
                var transition = from.AddTransition(to);
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = duration;
                foreach (var (mode, parameter) in conditions) transition.AddCondition(mode, 0f, parameter);
            }
            // The sway is used both while carried and when she walks away after a delivery.
            Link(idle, sway, .15f, (AnimatorConditionMode.If, "Carried"));
            Link(idle, sway, .15f, (AnimatorConditionMode.If, "Walking"));
            Link(sway, idle, .15f, (AnimatorConditionMode.IfNot, "Carried"), (AnimatorConditionMode.IfNot, "Walking"));
            Link(idle, spin, 0f, (AnimatorConditionMode.If, "Spinning"));
            Link(sway, spin, 0f, (AnimatorConditionMode.If, "Spinning"));
            Link(spin, idle, .1f, (AnimatorConditionMode.IfNot, "Spinning"));

            EditorUtility.SetDirty(controller);
            if (idle.motion == null || sway.motion == null || spin.motion == null) Debug.LogError("[Abuela] Faltan clips en el FBX");
            else Debug.Log("[Abuela] AC_Shared_Grandma: Idle (rebote) / Sway (cargada o yendose) / Spin (sale volando)");
            return controller;
        }

        // ---------- Prefab (1 unit tall, feet at 0, facing +Z) ----------

        private static GameObject BuildPrefab(AnimatorController controller, Material template)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PF_Shared_Grandma");
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                model.name = "Model";

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    var color = PartColors.FirstOrDefault(p => p.part == renderer.name).color;
                    if (color == default) color = Color.white;
                    var material = MakeMaterial($"{Folder}/M_Shared_Grandma_{Pretty(renderer.name)}.mat", template, color);
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = false;
                }

                // Face +Z (the glasses are in front of the body).
                var body = renderers.First(r => r.name == "CUERPO").bounds;
                var glasses = renderers.First(r => r.name == "GAFAS").bounds;
                if (glasses.center.z < body.center.z) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var bounds = Measure(renderers);
                model.transform.localScale *= 1f / bounds.size.y;
                bounds = Measure(renderers);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var driver = root.AddComponent<GrandmaCharacterAnimator>();
                var so = new SerializedObject(driver);
                so.FindProperty("animator").objectReferenceValue = animator;
                var tinted = so.FindProperty("tintedRenderers");
                tinted.arraySize = 1;
                tinted.GetArrayElementAtIndex(0).objectReferenceValue = renderers.First(r => r.name == TintedPart);
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Abuela] PF_Shared_Grandma creado (altura 1, mira a +Z)");
                return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static string Pretty(string part) => char.ToUpper(part[0]) + part.Substring(1).ToLower();

        private static Material MakeMaterial(string path, Material template, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds Measure(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // The waiting (and flying) grandmas of CrossyRoad use the animated model.
        private static void IntegrateCrossy(GameObject grandma)
        {
            var contents = PrefabUtility.LoadPrefabContents(CrossyGrandmaPath);
            try
            {
                foreach (var name in OldParts.Append(CharacterChild))
                {
                    var child = contents.transform.Find(name);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(grandma, contents.transform);
                instance.name = CharacterChild;
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one * CrossyHeight;
                PrefabUtility.SaveAsPrefabAsset(contents, CrossyGrandmaPath);
                Debug.Log("[Abuela] PF_CR_Grandma: abuela animada de " + CrossyHeight + " de alto");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
