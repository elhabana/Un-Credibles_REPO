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
        private const string RunCarryClipName = "AN_Shared_PJ_RunCarry";
        private const string IdleCarryClipName = "AN_Shared_PJ_IdleCarry";
        private const string BackSocketBone = "Socket_Espalda";
        private const string PlayerMaterials = "Assets/_Project/Player/Materials/M_Player_";
        private const string CrossyMaterials = "Assets/_Project/Minigames/MG_CrossyRoad/Art/M_CR_Player_";
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
        private const float CarriedGrandmaHeight = 1f; // a bit smaller than the waiting ones (1.18) so she fits on the back
        private const float CarriedGrandmaSitMin = -.1f; // range for her bottom relative to the hands
        private const float CarriedGrandmaSitMax = .3f;
        private const float CarriedGrandmaSink = .04f; // pressed slightly into the back so no gap shows while bouncing

        static PJCharacterSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ModelPath) && (!File.Exists(PrefabPath) || !File.Exists(GrandmaCharacterSetup.PrefabPath))) Run();
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
            var grandma = GrandmaCharacterSetup.Build(neutral);
            if (grandma == null) return;

            var figureColors = VariantColors(FigurePath, PlayerMaterials);
            var crossyColors = VariantColors(CrossyPath, CrossyMaterials);

            IntegrateFigure(character);
            IntegrateCrossy(character, grandma);
            PaintVariants(FigurePath, figureColors);
            PaintVariants(CrossyPath, crossyColors);

            AssetDatabase.SaveAssets();
            Debug.Log("[PJ] Listo: personaje integrado en figuras (lobby/podio) y en PF_CR_Player, abuela en PF_CR_Grandma y en la espalda.");
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
                if (clip.takeName.EndsWith("RunCarry")) clip.name = RunCarryClipName;
                else if (clip.takeName.EndsWith("IdleCarry")) clip.name = IdleCarryClipName;
                else if (clip.takeName.EndsWith("Run")) clip.name = RunClipName;
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
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
            });

            // Four loops, one per (running, carrying) pair; every state can go straight to any other.
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var states = new (AnimatorState state, bool running, bool carrying)[]
            {
                (machine.AddState("Idle", new Vector3(250f, 0f)), false, false),
                (machine.AddState("Run", new Vector3(250f, 90f)), true, false),
                (machine.AddState("IdleCarry", new Vector3(520f, 0f)), false, true),
                (machine.AddState("RunCarry", new Vector3(520f, 90f)), true, true),
            };
            states[0].state.motion = Clip(IdleClipName);
            states[1].state.motion = Clip(RunClipName);
            states[2].state.motion = Clip(IdleCarryClipName);
            states[3].state.motion = Clip(RunCarryClipName);
            machine.defaultState = states[0].state;
            foreach (var (state, running, _) in states)
            {
                if (!running) continue;
                state.speedParameterActive = true;
                state.speedParameter = "RunSpeed";
            }
            foreach (var from in states)
                foreach (var to in states)
                {
                    if (from.state == to.state) continue;
                    var transition = from.state.AddTransition(to.state);
                    transition.hasExitTime = false;
                    transition.hasFixedDuration = true;
                    transition.duration = to.running ? .15f : .2f;
                    transition.AddCondition(to.running ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Running");
                    transition.AddCondition(to.carrying ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Carrying");
                }

            EditorUtility.SetDirty(controller);
            if (states.Any(s => s.state.motion == null)) Debug.LogError("[PJ] Faltan clips en el FBX");
            else Debug.Log("[PJ] AC_Shared_PJ: Idle / Run / IdleCarry / RunCarry (parametros Running, Carrying, RunSpeed)");
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
                var socket = FindDeep(model.transform, BackSocketBone);
                if (socket == null) Debug.LogError("[PJ] El FBX no tiene el hueso " + BackSocketBone);
                so.FindProperty("backSocket").objectReferenceValue = socket;
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

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
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

        private static void IntegrateCrossy(GameObject character, GameObject grandma)
        {
            var riding = PlaceOnBack(character, grandma);
            var contents = PrefabUtility.LoadPrefabContents(CrossyPath + ".prefab");
            try
            {
                var visual = contents.transform.Find("Visual");
                RemoveChildren(visual, OldCrossyParts.Append(CharacterChild).Append("CarriedGrandma"));
                var instance = AddCharacter(character, visual, CrossyHeight);
                var driver = instance.GetComponent<PlayerCharacterAnimator>();
                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("runPlaybackSpeed").floatValue = CrossyRunSpeed;
                driverSo.ApplyModifiedPropertiesWithoutUndo();

                // The carried grandma rides on the back: child of the back bone, so she follows
                // the bounce and the lean of the carry loops.
                var carried = (GameObject)PrefabUtility.InstantiatePrefab(grandma, driver.BackSocket);
                carried.name = "CarriedGrandma";
                carried.transform.SetLocalPositionAndRotation(riding.position, riding.rotation);
                carried.transform.localScale = riding.scale;
                carried.SetActive(false);

                var player = contents.GetComponent("CrossyRoadPlayer");
                var so = new SerializedObject(player);
                so.FindProperty("character").objectReferenceValue = driver;
                so.FindProperty("carriedGrandma").objectReferenceValue = carried;
                var renderers = so.FindProperty("bodyRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = Find(instance, "Cuerpo");
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, CrossyPath + ".prefab");
                Debug.Log("[PJ] PF_CR_Player: personaje de " + CrossyHeight + " de alto, carrera x" + CrossyRunSpeed + ", abuela en la espalda");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // Where the grandma sits relative to the back bone: measured on a temporary copy posed
        // with the carry loop, her front against the back and her bottom on the hands.
        private static (Vector3 position, Quaternion rotation, Vector3 scale) PlaceOnBack(GameObject character, GameObject grandma)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(character);
                SceneManager.MoveGameObjectToScene(hero, scene);
                hero.transform.localScale = Vector3.one * CrossyHeight;
                var driver = hero.GetComponent<PlayerCharacterAnimator>();
                var model = hero.GetComponentInChildren<Animator>().gameObject;
                Clip(RunCarryClipName).SampleAnimation(model, 0f);

                float centerX = driver.BackSocket.position.x;
                var heroPoints = BakedPoints(hero).Where(p => Mathf.Abs(p.x - centerX) < .35f).ToList();
                float hands = (FindDeep(model.transform, "hand.L").position.y + FindDeep(model.transform, "hand.R").position.y) / 2f;

                // Piggyback: sitting on the hands and resting on the back. Tries a few leans and
                // heights and keeps the one where her whole front stays closest to his back
                // (the big head would otherwise keep her body away).
                var rider = (GameObject)PrefabUtility.InstantiatePrefab(grandma);
                SceneManager.MoveGameObjectToScene(rider, scene);
                rider.transform.localScale = Vector3.one * CarriedGrandmaHeight;
                const float Slice = .05f;
                var heroBack = heroPoints.GroupBy(p => Mathf.FloorToInt(p.y / Slice)).ToDictionary(g => g.Key, g => g.Min(p => p.z));

                float bestScore = float.MaxValue, bestLean = 0f, bestLift = 0f, bestGap = 0f;
                for (float lean = 0f; lean <= 16.1f; lean += 4f) // forward, following the lean of the carry loops
                {
                    rider.transform.rotation = Quaternion.Euler(lean, 0f, 0f);
                    var riderPoints = BakedPoints(rider).Where(p => Mathf.Abs(p.x) < .35f).ToList();
                    for (float sit = CarriedGrandmaSitMin; sit <= CarriedGrandmaSitMax + .001f; sit += .05f)
                    {
                        float lift = hands + sit - riderPoints.Min(p => p.y);
                        // Distance between both silhouettes, slice by slice.
                        var gaps = new List<float>();
                        foreach (var group in riderPoints.GroupBy(p => Mathf.FloorToInt((p.y + lift) / Slice)))
                            if (heroBack.TryGetValue(group.Key, out float back)) gaps.Add(back - group.Max(p => p.z));
                        if (gaps.Count < 4) continue;
                        float gap = gaps.Min();                       // closest without going through
                        float score = gaps.Average(g => g - gap);     // how far the rest of her stays
                        if (score >= bestScore) continue;
                        bestScore = score; bestLean = lean; bestLift = lift; bestGap = gap;
                    }
                }
                rider.transform.SetPositionAndRotation(new Vector3(centerX, bestLift, bestGap + CarriedGrandmaSink), Quaternion.Euler(bestLean, 0f, 0f));

                rider.transform.SetParent(driver.BackSocket, true);
                Debug.Log($"[PJ] Abuela en la espalda: inclinacion {bestLean:F0}, altura {bestLift:F2}, separacion media {bestScore:F2}");
                return (rider.transform.localPosition, rider.transform.localRotation, rider.transform.localScale);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        // World positions of every vertex in the current (sampled) pose.
        private static List<Vector3> BakedPoints(GameObject root)
        {
            var points = new List<Vector3>();
            var mesh = new Mesh();
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(mesh, true);
                var matrix = skinned.transform.localToWorldMatrix; // the baked vertices come without the transform scale
                points.AddRange(mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)));
            }
            Object.DestroyImmediate(mesh);
            return points;
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

        // Skin of each variant by name (e.g. M_CR_Player_Red), so it does not depend on what
        // the previous run left in the variant.
        private static Dictionary<string, Material> VariantColors(string basePath, string materialPrefix)
        {
            var result = new Dictionary<string, Material>();
            foreach (var color in Colors)
            {
                if (!File.Exists($"{basePath}_{color}.prefab")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPrefix + color + ".mat");
                if (material == null) Debug.LogError("[PJ] Falta el material " + materialPrefix + color);
                else result[color] = material;
            }
            return result;
        }

        private static void PaintVariants(string basePath, Dictionary<string, Material> colors)
        {
            foreach (var pair in colors)
            {
                string path = $"{basePath}_{pair.Key}.prefab";
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Each run rebuilds the character, so overrides of the previous one point nowhere.
                    PrefabUtility.RemoveUnusedOverrides(new[] { contents }, InteractionMode.AutomatedAction);
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
