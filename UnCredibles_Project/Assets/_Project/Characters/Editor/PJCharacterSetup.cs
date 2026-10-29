using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnCredibles.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Characters.Editor
{
    // Sets up the animated PJ character (SK_Shared_PJ.fbx) and puts it into the existing player
    // prefabs: the garage/podium figures and the CrossyRoad player. Safe to run again.
    // Runs once by itself when the character prefab does not exist yet.
    [InitializeOnLoad]
    public static class PJCharacterSetup
    {
        private const string Folder = "Assets/_Project/Characters/PJ";
        private const string ModelPath = Folder + "/SK_Shared_PJ.fbx";
        private const string ControllerPath = Folder + "/AC_Shared_PJ.controller";
        private const string PrefabPath = Folder + "/PF_Shared_PJ.prefab";
        private const string EyesMaterialPath = Folder + "/M_Shared_PJ_Eyes.mat";
        private const string BrowsMaterialPath = Folder + "/M_Shared_PJ_Brows.mat";
        private const string RunClipName = "AN_Shared_PJ_Run";
        private const string IdleClipName = "AN_Shared_PJ_Idle";
        private const string PlayerMaterials = "Assets/_Project/Player/Materials/M_Player_";
        private const string FigurePath = "Assets/_Project/Player/Prefabs/PF_PlayerFigure";
        private const string CrossyPath = "Assets/_Project/Minigames/MG_CrossyRoad/Prefabs/PF_CR_Player";
        private const string CharacterChild = "PJ";

        private static readonly string[] Colors = { "Red", "Blue", "Green", "Yellow" };
        private static readonly string[] SkinMeshes = { "Cuerpo", "Parpado_Sup_L", "Parpado_Inf_L", "Parpado_Sup_R", "Parpado_Inf_R" };
        private static readonly string[] OldFigureParts = { "Head", "Body", "Right eye", "Left eye" };
        private static readonly string[] OldCrossyParts = { "Body", "Nose" };

        private const float FigureHeight = 2.55f;   // same height as the old garage/podium figure
        private const float CrossyHeight = 1.7f;    // clearly taller than the 1.18 grandmas and 1.0 cars
        private const float CrossyRunSpeed = 2.2f;  // small avatar moving fast: quicker legs

        static PJCharacterSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ModelPath) && !File.Exists(PrefabPath)) Run();
            };
        }

        private const string ReportPath = "Logs/PJCharacterSetup.log";

        [MenuItem("UnCredibles/Personaje PJ/Configurar e integrar")]
        public static void Run()
        {
            // Everything logged during the setup also goes to Logs/PJCharacterSetup.log.
            var report = new List<string>();
            void Collect(string message, string stack, LogType type) =>
                report.Add($"{type}: {message}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : ""));
            Application.logMessageReceived += Collect;
            try { Setup(); }
            catch (System.Exception exception) { Debug.LogException(exception); }
            finally
            {
                Application.logMessageReceived -= Collect;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines(ReportPath, report);
            }
        }

        private static void Setup()
        {
            Debug.Log("[PJ] Configurando el personaje animado...");
            if (!ConfigureModel()) return;
            var neutral = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterials + "Neutral.mat");
            var eyes = MakeMaterial(EyesMaterialPath, neutral, new Color(.96f, .96f, .96f));
            var brows = MakeMaterial(BrowsMaterialPath, neutral, new Color(.25f, .15f, .1f));
            var controller = BuildController();
            var character = BuildCharacterPrefab(controller, neutral, eyes, brows);

            // Colours of each variant, read before the old body parts are removed.
            var figureColors = ReadVariantColors(FigurePath, "Body", null);
            var crossyColors = ReadVariantColors(CrossyPath, "Body", "Visual");

            IntegrateFigure(character);
            IntegrateCrossy(character);
            PaintVariants(FigurePath, figureColors);
            PaintVariants(CrossyPath, crossyColors);

            AssetDatabase.SaveAssets();
            Debug.Log("[PJ] Listo: personaje integrado en figuras (lobby/podio) y en PF_CR_Player.");
        }

        // ---------- Model import ----------

        private static bool ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[PJ] No se encuentra " + ModelPath);
                return false;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off; // keep the loops smooth

            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                importer.SaveAndReimport();
                clips = importer.defaultClipAnimations;
            }
            foreach (var clip in clips)
            {
                if (clip.takeName.EndsWith("Run")) clip.name = RunClipName;
                else if (clip.takeName.EndsWith("Idle")) clip.name = IdleClipName;
                clip.loopTime = true;
                clip.loopPose = false; // first and last frames are already identical
                clip.lockRootRotation = false;
                clip.lockRootHeightY = false;
                clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log("[PJ] FBX configurado (Generic, sin compresion, clips: " + string.Join(", ", clips.Select(c => c.name)) + ")");
            return true;
        }

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name);

        // ---------- Materials / controller ----------

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

        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            controller.AddParameter("Running", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
            });

            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var idle = machine.AddState("Idle", new Vector3(250f, 0f));
            idle.motion = Clip(IdleClipName);
            var run = machine.AddState("Run", new Vector3(250f, 90f));
            run.motion = Clip(RunClipName);
            run.speedParameterActive = true;
            run.speedParameter = "RunSpeed";
            machine.defaultState = idle;

            var toRun = idle.AddTransition(run);
            toRun.hasExitTime = false; toRun.hasFixedDuration = true; toRun.duration = .15f;
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "Running");
            var toIdle = run.AddTransition(idle);
            toIdle.hasExitTime = false; toIdle.hasFixedDuration = true; toIdle.duration = .2f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Running");

            EditorUtility.SetDirty(controller);
            if (idle.motion == null || run.motion == null) Debug.LogError("[PJ] Faltan clips en el FBX");
            else Debug.Log("[PJ] AC_Shared_PJ: Idle <-> Run (parametros Running, RunSpeed)");
            return controller;
        }

        // ---------- Character prefab (1 unit tall, feet at 0, facing +Z) ----------

        private static GameObject BuildCharacterPrefab(AnimatorController controller, Material skin, Material eyes, Material brows)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PF_Shared_PJ");
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                model.name = "Model";

                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var material = renderer.name == "Ojos" ? eyes : renderer.name == "Cejas" ? brows : skin;
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = false;
                }

                // Face +Z like the old figures (the eyes are in front of the body).
                var body = Find(model, "Cuerpo").bounds;
                var eyeBounds = Find(model, "Ojos").bounds;
                if (eyeBounds.center.z < body.center.z) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var bounds = MeasureBounds(model);
                model.transform.localScale *= 1f / bounds.size.y;
                bounds = MeasureBounds(model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var driver = root.AddComponent<PlayerCharacterAnimator>();
                var so = new SerializedObject(driver);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[PJ] PF_Shared_PJ creado (altura 1, mira a +Z)");
                return prefab;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Renderer Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Renderer>(true).First(r => r.name == name);

        private static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ---------- Integration into the existing prefabs ----------

        private static void IntegrateFigure(GameObject character)
        {
            var contents = PrefabUtility.LoadPrefabContents(FigurePath + ".prefab");
            try
            {
                RemoveChildren(contents.transform, OldFigureParts.Append(CharacterChild));
                AddCharacter(character, contents.transform, FigureHeight);
                PrefabUtility.SaveAsPrefabAsset(contents, FigurePath + ".prefab");
                Debug.Log("[PJ] PF_PlayerFigure: personaje de " + FigureHeight + " de alto");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void IntegrateCrossy(GameObject character)
        {
            var contents = PrefabUtility.LoadPrefabContents(CrossyPath + ".prefab");
            try
            {
                var visual = contents.transform.Find("Visual");
                RemoveChildren(visual, OldCrossyParts.Append(CharacterChild));
                var instance = AddCharacter(character, visual, CrossyHeight);
                var driver = instance.GetComponent<PlayerCharacterAnimator>();
                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("runPlaybackSpeed").floatValue = CrossyRunSpeed;
                driverSo.ApplyModifiedPropertiesWithoutUndo();

                // The carried grandma rides on top of the head.
                var carried = visual.Find("CarriedGrandma");
                if (carried != null) carried.localPosition = new Vector3(0f, CrossyHeight + .08f, 0f);

                var player = contents.GetComponent("CrossyRoadPlayer");
                var so = new SerializedObject(player);
                so.FindProperty("character").objectReferenceValue = driver;
                var renderers = so.FindProperty("bodyRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = Find(instance, "Cuerpo");
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, CrossyPath + ".prefab");
                Debug.Log("[PJ] PF_CR_Player: personaje de " + CrossyHeight + " de alto, carrera x" + CrossyRunSpeed);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static GameObject AddCharacter(GameObject character, Transform parent, float height)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(character, parent);
            instance.name = CharacterChild;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * height;
            return instance;
        }

        private static void RemoveChildren(Transform parent, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                var child = parent.Find(name);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }

        // ---------- Colour variants ----------

        private static Dictionary<string, Material> ReadVariantColors(string basePath, string bodyName, string under)
        {
            var result = new Dictionary<string, Material>();
            foreach (var color in Colors)
            {
                string path = $"{basePath}_{color}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var parent = under != null ? prefab.transform.Find(under) : prefab.transform;
                var oldBody = parent != null ? parent.Find(bodyName) : null;
                var skin = oldBody != null ? oldBody.GetComponent<Renderer>() : FindSkin(prefab);
                result[color] = skin != null && skin.sharedMaterial != null
                    ? skin.sharedMaterial
                    : AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterials + color + ".mat");
            }
            return result;
        }

        private static Renderer FindSkin(GameObject prefab) =>
            prefab.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Cuerpo");

        private static void PaintVariants(string basePath, Dictionary<string, Material> colors)
        {
            foreach (var pair in colors)
            {
                string path = $"{basePath}_{pair.Key}.prefab";
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                        if (SkinMeshes.Contains(renderer.name))
                            renderer.sharedMaterials = Enumerable.Repeat(pair.Value, renderer.sharedMaterials.Length).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Debug.Log($"[PJ] {Path.GetFileName(path)}: piel {pair.Value.name}");
            }
        }
    }
}
