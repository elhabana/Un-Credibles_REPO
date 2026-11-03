using System.IO;
using System.Linq;
using UnCredibles.Players;
using UnCredibles.UI.CharacterEditor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace UnCredibles.Characters.Editor
{
    // Character editor ("probador") support: part icons, its UI texts and a test scene.
    public static class CharacterEditorSetup
    {
        private const string IconFolder = CharacterPartsSetup.Folder + "/Icons";
        private const string ScenePath = "Assets/_Project/UI/CharacterEditor/SCN_CharacterEditor.unity";
        private const string CharacterPrefabPath = "Assets/_Project/Characters/PJ/PF_Shared_PJ.prefab";
        private const string CatalogPath = CharacterPartsSetup.Folder + "/SO_PJ_PartCatalog.asset";
        private const string PlayerMaterials = "Assets/_Project/Player/Materials/M_Player_";
        private const int IconSize = 256;

        private static readonly (string key, string es, string en)[] Texts =
        {
            ("probador.title", "Personaje", "Character"),
            ("probador.tab.helmet", "Casco", "Helmet"),
            ("probador.tab.outfit", "Ropa", "Outfit"),
            ("probador.tab.boots", "Botas", "Boots"),
            ("probador.none", "Nada", "None"),
            ("probador.run", "Correr", "Run"),
            ("probador.stop", "Parar", "Stop"),
            ("probador.back", "Volver", "Back"),
            ("probador.save", "Guardar", "Save"),
            ("probador.hint", "Arrastra para girar  ·  Rueda para acercar  ·  Q / E cambia de pestaña",
                "Drag to rotate  ·  Wheel to zoom  ·  Q / E switch tab"),
            ("part.Casco_Abierto", "Casco abierto", "Open helmet"),
            ("part.Casco_Cerrado", "Casco con visera", "Visor helmet"),
            ("part.Traje_Mono", "Mono", "Jumpsuit"),
            ("part.Traje_Tunica", "Túnica", "Tunic"),
            ("part.Botas_Altas", "Botas altas", "Tall boots"),
        };

        [MenuItem("UnCredibles/Personaje PJ/Probador: iconos, textos y escena")]
        public static void Run()
        {
            var character = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(CatalogPath);
            if (character == null || catalog == null)
            {
                Debug.LogError("[Probador] Falta el personaje o el catalogo: ejecuta antes 'Configurar e integrar'");
                return;
            }
            RenderIcons(character, catalog);
            AddTexts();
            BuildScene(character);
            AssetDatabase.SaveAssets();
        }

        // ---------- Icons: each part worn by the character, framed by slot, transparent ----------

        public static void RenderIcons(GameObject characterPrefab, CharacterPartCatalog catalog)
        {
            Directory.CreateDirectory(IconFolder);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                SceneManager.MoveGameObjectToScene(character, scene);
                var outfit = character.GetComponent<PlayerCharacterOutfit>();
                // Rest pose (the model comes in whatever pose the file was saved in).
                var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Characters/PJ/SK_Shared_PJ.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => c.name == "AN_Shared_PJ_Idle");
                var model = character.GetComponentInChildren<Animator>().gameObject;
                if (idle != null) idle.SampleAnimation(model, 0f);
                // Shown in a player colour, so clothes that take it read as clothes.
                var skin = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterials + "Blue.mat");
                if (skin != null)
                    foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                        if (renderer.name.StartsWith("Cuerpo_") || renderer.name.StartsWith("Parpado_"))
                            renderer.sharedMaterials = Enumerable.Repeat(skin, renderer.sharedMaterials.Length).ToArray();

                var lightObject = new GameObject("Light");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.3f;
                lightObject.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
                var cameraObject = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.fieldOfView = 26f;
                camera.nearClipPlane = .02f;
                camera.targetTexture = target;

                foreach (var part in catalog.Parts)
                {
                    if (part == null) continue;
                    outfit.Apply(new CharacterLoadout().With(part.Slot, part.Id));
                    var (focus, distance) = part.Slot switch
                    {
                        CharacterPartSlot.Helmet => (new Vector3(0f, .8f, 0f), 1.6f),
                        CharacterPartSlot.Boots => (new Vector3(0f, .1f, 0f), 1.05f),
                        _ => (new Vector3(0f, .5f, 0f), 2.1f),
                    };
                    var eye = focus + Quaternion.Euler(12f, 25f, 0f) * Vector3.forward * distance;
                    cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
                    camera.Render();

                    RenderTexture.active = target;
                    var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                    texture.Apply();
                    RenderTexture.active = null;
                    string path = $"{IconFolder}/ICO_PJ_Part_{part.Id}.png";
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);

                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();

                    var so = new SerializedObject(part);
                    so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log($"[Probador] Iconos de {catalog.Parts.Count} piezas en {IconFolder}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        // ---------- UI texts (Spanish and English) ----------

        private static void AddTexts()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            if (collection == null)
            {
                Debug.LogWarning("[Probador] No hay tabla de textos UI");
                return;
            }
            foreach (var (key, es, en) in Texts)
            {
                if (collection.SharedData.GetEntry(key) == null) collection.SharedData.AddKey(key);
                foreach (var table in collection.StringTables)
                {
                    string value = table.LocaleIdentifier.Code.StartsWith("es") ? es : en;
                    var entry = table.GetEntry(key);
                    if (entry == null) table.AddEntry(key, value);
                    else entry.Value = value;
                    EditorUtility.SetDirty(table);
                }
            }
            EditorUtility.SetDirty(collection.SharedData);
            Debug.Log($"[Probador] {Texts.Length} textos en la tabla UI (es / en)");
        }

        // ---------- Test scene ----------

        private static void BuildScene(GameObject characterPrefab)
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                Debug.Log("[Probador] La escena de prueba esta abierta: no se rehace");
                return;
            }
            // Additive, so whatever scene is open stays as it is.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            var root = new GameObject("CharacterEditor");
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.AddComponent<CharacterEditorController>();
            var so = new SerializedObject(controller);
            so.FindProperty("characterPrefab").objectReferenceValue = characterPrefab;
            so.FindProperty("stagePosition").vector3Value = Vector3.zero;
            string[] colors = { "Red", "Blue", "Green", "Yellow" };
            var list = so.FindProperty("playerColors");
            list.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterials + colors[i] + ".mat");
            so.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Probador] Escena de prueba: " + ScenePath);
        }
    }
}
