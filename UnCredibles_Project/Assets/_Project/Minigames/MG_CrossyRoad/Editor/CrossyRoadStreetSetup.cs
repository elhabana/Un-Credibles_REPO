using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad.EditorTools
{
    // Puts the art team's street (SM_CR_Street.fbx) into Crossy Road:
    //  1. PF_CR_Street: three copies of the street in a row (it is shorter than the camera is wide), painted
    //     with URP materials (the artist's named colours; a palette for the parts that came without one),
    //     plus a plaza in front of the near sidewalk, where the camera still sees ground.
    //  2. In SCN_MG_CrossyRoad it is turned, scaled and moved so the asphalt covers exactly the road lanes
    //     and the buildings stand behind the goal lane. The code-built placeholder scenery and the coloured
    //     lane tiles are switched off; gameplay is unchanged.
    // Measured from the model, so a new export of the FBX only needs this menu again.
    // Also started by creating Temp/build-cr-street.request, which then takes a screenshot of a bot game.
    [InitializeOnLoad]
    public static class CrossyRoadStreetSetup
    {
        private const string Folder = "Assets/_Project/Minigames/MG_CrossyRoad/";
        private const string ModelPath = Folder + "Art/SM_CR_Street.fbx";
        private const string BaseMaterialPath = Folder + "Art/M_CR_Shared.mat";
        private const string PrefabPath = Folder + "Prefabs/PF_CR_Street.prefab";
        private const string ScenePath = Folder + "Scenes/SCN_MG_CrossyRoad.unity";
        private const string RootName = "Street";
        private const int Copies = 3;
        private const float PlazaDepth = 15f; // metres of ground in front of the near sidewalk (before scaling)
        private const float CameraPitch = 38f;  // degrees down (it was 55: the facades hid behind the top edge)
        private const float CameraHeight = 14f; // metres above the board
        private const float NearMargin = .75f;  // ground shown in front of the grandmas' lane

        private const string RequestFile = "Temp/build-cr-street.request";
        private const string ShotKey = "CrossyRoadStreet.Shot";
        private static readonly float[] ShotTimes = { 6f, 12f };
        private static float playStart;
        private static int shot;

        // Colours of the parts that had no material in the FBX.
        private static readonly Color Asphalt = new Color(.22f, .22f, .25f);
        private static readonly Color Sidewalk = new Color(.74f, .72f, .68f);
        private static readonly Color Plaza = new Color(.68f, .66f, .62f);
        private static readonly Color Stripe = new Color(.95f, .95f, .92f);
        private static readonly Color Trim = new Color(.93f, .89f, .8f);
        private static readonly Color Leaf = new Color(.3f, .58f, .28f);
        private static readonly Color Pot = new Color(.62f, .36f, .24f);
        private static readonly Color[] Walls = { new Color(.86f, .6f, .45f), new Color(.6f, .74f, .82f), new Color(.93f, .82f, .52f) };
        private static readonly string[] Stripes = { "Plane.035", "Plane.036", "Plane.037", "Plane.038", "Plane.039", "Plane.040", "Plane.041" };

        static CrossyRoadStreetSetup()
        {
            EditorApplication.playModeStateChanged += HandlePlayMode;
            EditorApplication.update += () =>
            {
                if (!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                File.Delete(RequestFile);
                if (Build()) StartShots();
            };
        }

        [MenuItem("UnCredibles/Crossy Road/Build street scenery")]
        private static void BuildFromMenu() => Build();

        // Command line: Unity -batchmode -projectPath <project> -executeMethod
        //   UnCredibles.Minigames.CrossyRoad.EditorTools.CrossyRoadStreetSetup.BuildBatch -quit
        public static void BuildBatch()
        {
            if (!Build()) EditorApplication.Exit(1);
            CaptureCamera("Temp/CR_Street_Camera.png");
        }

        // The scene's Main Camera rendered in edit mode (scenery and lighting; no cars or players).
        public static void CaptureCamera(string path)
        {
            Camera camera = null;
            foreach (var candidate in InScene<Camera>(EditorSceneManager.GetActiveScene()))
                if (candidate.CompareTag("MainCamera")) camera = candidate;
            if (camera == null) return;
            var target = new RenderTexture(1920, 1080, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            Debug.Log("[Street] " + path);
        }

        public static bool Build()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Street] Save the open scenes first.");
                    return false;
                }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[Street] Missing " + ModelPath);
                return false;
            }
            var prefab = BuildPrefab(model);
            PlaceInScene(prefab);
            Debug.Log("[Street] Built " + PrefabPath + " and placed it in " + ScenePath);
            return true;
        }

        // ---------- Prefab ----------

        private static GameObject BuildPrefab(GameObject model)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(PrefabPath));
            try
            {
                var center = Instantiate(model, root.transform, "Street_0");
                var road = Measure(center, "Carretera");
                // The street runs along its longer side; copies continue it both ways.
                bool alongX = road.size.x > road.size.z;
                var along = alongX ? Vector3.right : Vector3.forward;
                float length = alongX ? road.size.x : road.size.z;
                for (int i = 1; i <= Copies / 2; i++)
                {
                    Instantiate(model, root.transform, "Street_+" + i).transform.localPosition = along * (length * i);
                    Instantiate(model, root.transform, "Street_-" + i).transform.localPosition = -along * (length * i);
                }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    Paint(renderer, along, road.center);
                    // The buildings are tall: their shadows covered the whole street and darkened the game.
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                BuildPlaza(root, center, road, along, length * Copies);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Instantiate(GameObject model, Transform parent, string name)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            copy.name = name;
            return copy;
        }

        // Ground in front of the sidewalk without buildings, as long as all the copies together.
        private static void BuildPlaza(GameObject root, GameObject street, Bounds road, Vector3 along, float totalLength)
        {
            var across = Vector3.Cross(Vector3.up, along);
            float buildingsSide = Mathf.Sign(Vector3.Dot(Measure(street, "building_").center - road.center, across));
            Bounds near = default;
            foreach (var renderer in street.GetComponentsInChildren<Renderer>())
                if (renderer.name.StartsWith("Acera") && Mathf.Sign(Vector3.Dot(renderer.bounds.center - road.center, across)) != buildingsSide)
                    near = renderer.bounds;

            float outer = Vector3.Dot(near.center - road.center, across) - buildingsSide * Vector3.Dot(near.extents, Abs(across));
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(plaza.GetComponent<Collider>());
            plaza.name = "Plaza";
            plaza.transform.SetParent(root.transform, false);
            float top = near.max.y;
            plaza.transform.localPosition = road.center + across * (outer - buildingsSide * PlazaDepth * .5f) + Vector3.up * (top - .25f - road.center.y);
            plaza.transform.localScale = Abs(along) * totalLength + Abs(across) * PlazaDepth + Vector3.up * .5f;
            plaza.GetComponent<Renderer>().sharedMaterial = GetMaterial("Plaza", Plaza);
        }

        // Searched through the scene itself: the global Find calls see nothing in batch mode.
        private static List<T> InScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // Encapsulates every renderer whose name starts with `prefix`, in the parent's space (the root sits at the origin).
        private static Bounds Measure(GameObject street, string prefix)
        {
            bool any = false;
            var bounds = default(Bounds);
            foreach (var renderer in street.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.name.StartsWith(prefix)) continue;
                if (any) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                any = true;
            }
            if (!any) throw new System.InvalidOperationException("[Street] No '" + prefix + "' in " + ModelPath);
            return bounds;
        }

        private static void Paint(Renderer renderer, Vector3 along, Vector3 roadCenter)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string original = materials[i] != null ? materials[i].name : string.Empty;
                materials[i] = NamedMaterial(original) ?? GetMaterial(PartName(renderer, along, roadCenter, out var color), color);
            }
            renderer.sharedMaterials = materials;
        }

        // The artist's own materials, in their colours (the uv_checker ones were placeholders for textures).
        private static Material NamedMaterial(string name)
        {
            if (name.StartsWith("uv_checker")) return GetMaterial("Trim", Trim);
            if (name.StartsWith("Cartel")) return GetMaterial("Sign", new Color(.8f, .09f, .75f));
            if (name.StartsWith("AireAcondicionado")) return GetMaterial("AirConditioner", new Color(.16f, .05f, .8f));
            if (name.StartsWith("Tuberias")) return GetMaterial("Pipes", new Color(.02f, .01f, .13f));
            if (name.StartsWith("Escalera")) return GetMaterial("Ladder", new Color(.11f, .21f, .27f));
            if (name.StartsWith("Pomo")) return GetMaterial("Knob", new Color(.8f, .06f, .37f));
            if (name.StartsWith("Toldo")) return GetMaterial("Awning", new Color(.03f, .19f, .07f));
            return null;
        }

        // Parts that came without a material: by name, and walls by which building they belong to.
        private static string PartName(Renderer renderer, Vector3 along, Vector3 roadCenter, out Color color)
        {
            string name = renderer.name;
            if (name == "Carretera") { color = Asphalt; return "Asphalt"; }
            if (name.StartsWith("Acera")) { color = Sidewalk; return "Sidewalk"; }
            if (System.Array.IndexOf(Stripes, name) >= 0) { color = Stripe; return "Crossing"; }
            if (name.StartsWith("Sphere") || name == "Plane.032" || name == "Plane.034") { color = Leaf; return "Leaves"; }
            if (name == "Plane.031" || name == "Plane.033" || name == "Cylinder.008") { color = Pot; return "Pot"; }
            var size = renderer.bounds.size;
            if (name.StartsWith("building_") && size.y > 4f && Mathf.Max(size.x, size.z) > 4f)
            {
                // Three buildings along the street: which third of its copy the wall stands in.
                float t = Vector3.Dot(renderer.transform.parent.InverseTransformPoint(renderer.bounds.center) - roadCenter, along);
                int index = t > 4.7f ? 0 : t < -5.8f ? 2 : 1;
                color = Walls[index];
                return "Wall" + "ABC"[index];
            }
            color = Trim;
            return "Trim";
        }

        // M_CR_Street<Name>, a copy of the minigame's shared lit material in that colour.
        private static Material GetMaterial(string name, Color color)
        {
            string path = Folder + "Art/M_CR_Street" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Scene ----------

        private static void PlaceInScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var board = InScene<CrossyRoadBoard>(scene)[0];
            GameObject street = null;
            foreach (var root in scene.GetRootGameObjects())
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab || root.name == RootName) street = root;
            if (street != null) Object.DestroyImmediate(street);
            street = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            street.name = RootName;
            street.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            street.transform.localScale = Vector3.one;

            var road = Measure(street, "Carretera");
            var buildings = Measure(street, "building_");
            // Turn it so the street runs along X (like the cars) with the buildings towards +Z (behind the goal).
            var toBuildings = buildings.center - road.center;
            toBuildings.y = 0f;
            toBuildings = Mathf.Abs(toBuildings.x) > Mathf.Abs(toBuildings.z)
                ? new Vector3(Mathf.Sign(toBuildings.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toBuildings.z));
            // A turn around Y only (FromToRotation may flip it upside down for opposite directions).
            var rotation = Quaternion.Euler(0f, Vector3.SignedAngle(toBuildings, Vector3.forward, Vector3.up), 0f);
            float roadWidth = Mathf.Abs(Vector3.Dot(road.size, toBuildings));

            // The asphalt covers the road lanes exactly.
            float cell = board.CellSize;
            float roadMin = board.LaneToZ(CrossyRoadBoard.FirstRoadLane) - cell * .5f;
            float roadMax = board.LaneToZ(board.GoalLane - 1) + cell * .5f;
            float scale = (roadMax - roadMin) / roadWidth;
            var target = new Vector3(board.transform.position.x, board.transform.position.y, (roadMin + roadMax) * .5f);
            var roadTop = new Vector3(road.center.x, road.max.y, road.center.z);
            street.transform.SetPositionAndRotation(target - rotation * (roadTop * scale), rotation);
            street.transform.localScale = Vector3.one * scale;

            FrameCamera(scene, board);

            // Only the builder is switched off: the scene's Directional Light hangs from the same object.
            foreach (var environment in InScene<CrossyRoadEnvironment>(scene))
            {
                environment.gameObject.SetActive(true);
                environment.enabled = false;
            }
            var serializedBoard = new SerializedObject(board);
            serializedBoard.FindProperty("drawLaneTiles").boolValue = false;
            serializedBoard.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Street] Scale {scale:F3}, rotation {rotation.eulerAngles.y:F0}°, road {roadMin:F2}..{roadMax:F2}");
        }

        // Flatter than the old top-down view so the facades behind the goal show: the lowest edge of the picture
        // lands just in front of the grandmas' lane and the rest of the height goes to the street and the buildings.
        private static void FrameCamera(UnityEngine.SceneManagement.Scene scene, CrossyRoadBoard board)
        {
            Camera camera = null;
            foreach (var candidate in InScene<Camera>(scene))
                if (candidate.CompareTag("MainCamera")) camera = candidate;
            if (camera == null) return;
            float lowestRay = (CameraPitch + camera.fieldOfView * .5f) * Mathf.Deg2Rad;
            float nearEdge = board.MinZ - NearMargin;
            var position = new Vector3(board.transform.position.x, board.transform.position.y + CameraHeight,
                nearEdge - CameraHeight / Mathf.Tan(lowestRay));
            camera.transform.SetPositionAndRotation(position, Quaternion.Euler(CameraPitch, 0f, 0f));
            EditorUtility.SetDirty(camera.transform);
        }

        // ---------- Screenshot of a bot game ----------

        private static void StartShots()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var bootstrap in root.GetComponentsInChildren<DebugBootstrap>(true))
            {
                var serialized = new SerializedObject(bootstrap);
                var players = serialized.FindProperty("players");
                players.arraySize = 4;
                for (int i = 0; i < 4; i++)
                    players.GetArrayElementAtIndex(i).FindPropertyRelative("inputSource").intValue = (int)UnCredibles.Players.InputSourceType.AI;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            SessionState.SetBool(ShotKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ShotKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                playStart = Time.realtimeSinceStartup;
                shot = 0;
                EditorApplication.update += Shoot;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Shoot;
                SessionState.SetBool(ShotKey, false);
                EditorSceneManager.OpenScene(ScenePath); // drop the in-memory bot setup
                Debug.Log("[Street] DONE");
            }
        }

        private static void Shoot()
        {
            if (shot >= ShotTimes.Length)
            {
                EditorApplication.update -= Shoot;
                EditorApplication.ExitPlaymode();
                return;
            }
            if (Time.realtimeSinceStartup - playStart < ShotTimes[shot]) return;
            string path = $"Temp/CR_Street_{shot + 1}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Street] " + path);
            shot++;
        }
    }
}
