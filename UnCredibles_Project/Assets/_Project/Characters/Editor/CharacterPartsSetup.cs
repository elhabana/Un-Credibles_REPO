using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnCredibles.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Characters.Editor
{
    // Turns the parts exported from Blender (SK_PJ_Part_*.fbx + piezas.json) into CharacterPart
    // assets and the part catalog. Called by PJCharacterSetup.
    public static class CharacterPartsSetup
    {
        public const string Folder = "Assets/_Project/Characters/PJ/Parts";
        private const string ManifestPath = Folder + "/piezas.json";
        private const string CatalogPath = Folder + "/SO_PJ_PartCatalog.asset";

        [Serializable] private sealed class Manifest { public List<Entry> parts = new List<Entry>(); }

        [Serializable]
        private sealed class Entry
        {
            public string id;
            public string slot;
            public string fbx;
            public string mesh;
            public List<string> hides = new List<string>();
            public bool coversFace;
            public bool usePlayerColor;
        }

        // Zone names used in Blender -> body zones in the game.
        private static readonly Dictionary<string, BodyZone> Zones = new Dictionary<string, BodyZone>
        {
            { "Cabeza", BodyZone.Head }, { "Orejas", BodyZone.Ears }, { "Torso", BodyZone.Torso },
            { "Brazos", BodyZone.Arms }, { "Manos", BodyZone.Hands }, { "Muslos", BodyZone.Thighs },
            { "Rodillas", BodyZone.Knees }, { "Tobillos", BodyZone.Ankles }, { "Pies", BodyZone.Feet },
        };

        public static BodyZone ZoneFromName(string name) =>
            Zones.TryGetValue(name, out var zone) ? zone : BodyZone.None;

        public static CharacterPartCatalog Build(Material template)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterPartCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (!File.Exists(ManifestPath))
            {
                Debug.LogWarning("[Piezas] No hay " + ManifestPath + ": catalogo vacio");
                return catalog;
            }

            string json = File.ReadAllText(ManifestPath);
            var manifest = JsonUtility.FromJson<Manifest>(json);
            var colors = ParseColors(json);
            var parts = new List<CharacterPart>();
            foreach (var entry in manifest.parts)
            {
                var part = BuildPart(entry, colors.TryGetValue(entry.id, out var c) ? c : new List<Color>(), template);
                if (part != null) parts.Add(part);
            }

            var so = new SerializedObject(catalog);
            var list = so.FindProperty("parts");
            list.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Piezas] Catalogo: " + string.Join(", ", parts.Select(p => $"{p.Id} ({p.Slot})")));
            return catalog;
        }

        private static CharacterPart BuildPart(Entry entry, List<Color> colors, Material template)
        {
            string modelPath = $"{Folder}/{entry.fbx}";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[Piezas] No se encuentra " + modelPath);
                return null;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var renderer = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == entry.mesh);
            int slots = renderer != null ? renderer.sharedMaterials.Length : 1;
            var materials = new Material[Mathf.Max(1, slots)];
            for (int i = 0; i < materials.Length; i++)
            {
                var color = i < colors.Count ? colors[i] : Color.white;
                materials[i] = MakeMaterial($"{Folder}/M_PJ_Part_{entry.id}_{i}.mat", template, color);
            }

            string path = $"{Folder}/SO_PJ_Part_{entry.id}.asset";
            var part = AssetDatabase.LoadAssetAtPath<CharacterPart>(path);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<CharacterPart>();
                AssetDatabase.CreateAsset(part, path);
            }
            var hidden = BodyZone.None;
            foreach (var zone in entry.hides) hidden |= ZoneFromName(zone);
            var slot = entry.slot switch { "Casco" => CharacterPartSlot.Helmet, "Botas" => CharacterPartSlot.Boots, _ => CharacterPartSlot.Outfit };

            var so = new SerializedObject(part);
            so.FindProperty("id").stringValue = entry.id;
            if (string.IsNullOrEmpty(so.FindProperty("displayName").stringValue))
                so.FindProperty("displayName").stringValue = entry.id.Replace('_', ' ');
            so.FindProperty("slot").enumValueIndex = (int)slot;
            so.FindProperty("model").objectReferenceValue = model;
            var mats = so.FindProperty("materials");
            mats.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++) mats.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            so.FindProperty("hiddenZones").intValue = (int)hidden;
            so.FindProperty("coversFace").boolValue = entry.coversFace;
            so.FindProperty("usePlayerColor").boolValue = entry.usePlayerColor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        // JsonUtility cannot read nested arrays: pull "colors" of each part by hand.
        private static Dictionary<string, List<Color>> ParseColors(string json)
        {
            var result = new Dictionary<string, List<Color>>();
            int at = 0;
            while ((at = json.IndexOf("\"id\":", at, StringComparison.Ordinal)) >= 0)
            {
                int q1 = json.IndexOf('"', at + 5) + 1, q2 = json.IndexOf('"', q1);
                string id = json.Substring(q1, q2 - q1);
                int c = json.IndexOf("\"colors\":", q2, StringComparison.Ordinal);
                int next = json.IndexOf("\"id\":", q2, StringComparison.Ordinal);
                var list = new List<Color>();
                if (c >= 0 && (next < 0 || c < next))
                {
                    int open = json.IndexOf('[', c), depth = 0, end = open;
                    for (; end < json.Length; end++)
                    {
                        if (json[end] == '[') depth++;
                        else if (json[end] == ']' && --depth == 0) break;
                    }
                    foreach (var chunk in json.Substring(open + 1, end - open - 1).Split(']'))
                    {
                        var numbers = chunk.Trim(' ', ',', '[', '\n', '\r', '\t').Split(',')
                            .Select(s => s.Trim()).Where(s => s.Length > 0)
                            .Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        if (numbers.Length >= 3) list.Add(new Color(numbers[0], numbers[1], numbers[2]));
                    }
                }
                result[id] = list;
                at = q2;
            }
            return result;
        }

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

        // Helmets: how far above the bare head they reach, measured on the character prefab
        // (1 unit tall). Things that sit on the head (the carried grandma) go up by this much.
        public static void MeasureHelmets(GameObject characterPrefab, CharacterPartCatalog catalog)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                SceneManager.MoveGameObjectToScene(character, scene);
                var outfit = character.GetComponent<PlayerCharacterOutfit>();
                float bare = Top(outfit.VisibleRenderers());
                foreach (var part in catalog.InSlot(CharacterPartSlot.Helmet))
                {
                    outfit.Apply(new CharacterLoadout().With(CharacterPartSlot.Helmet, part.Id));
                    float extra = Mathf.Max(0f, Top(outfit.VisibleRenderers()) - bare);
                    var so = new SerializedObject(part);
                    so.FindProperty("extraHeadHeight").floatValue = extra;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[Piezas] {part.Id}: sobresale {extra:F3} de la cabeza");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static float Top(List<Renderer> renderers)
        {
            float top = float.MinValue;
            foreach (var renderer in renderers) top = Mathf.Max(top, renderer.bounds.max.y);
            return top;
        }
    }
}
