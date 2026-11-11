#if UNITY_EDITOR
using System;
using TMPro;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnCredibles.UI.Results.Editor
{
    // Temporary: bakes the paper standings sheet (formerly built at runtime by ResultsSheet) into
    // SCN_Results and PF_ResultsRow. Safe to run again; delete once the scene is baked.
    public static class ResultsSheetBake
    {
        private const string ScenePath = "Assets/_Project/Core/Scenes/SCN_Results.unity";
        private const string RowPrefabPath = "Assets/_Project/UI/Results/Prefabs/PF_ResultsRow.prefab";
        private const string SpriteFolder = "Assets/_Project/UI/Common/Resources/PaperUI/";
        private const string TitleFontGuid = "0f4999180db1cd14f8de3280d6998060"; // Bangers SDF
        private const string SheetName = "Results Sheet";

        private static readonly Color Gold = new Color(1f, .78f, .2f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1f);
        private static readonly Vector2 Left = new Vector2(0f, .5f);
        private static readonly Vector2 Right = new Vector2(1f, .5f);
        private static readonly Vector2 SheetSize = new Vector2(1300f, 860f);
        private static readonly Vector2 RowSize = new Vector2(1180f, 124f);
        private const float FirstRowY = -240f;
        private const float RowStep = 142f;

        [MenuItem("UnCredibles/UI Bake/Results")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // The scene must reference the sprite assets, not something PaperUI made up.
            CheckSprite("T_UI_Panel", PaperUI.PanelSprite);
            CheckSprite("T_UI_Pill", PaperUI.PillSprite);
            CheckSprite("T_UI_Disc", PaperUI.DiscSprite);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TitleFontGuid));
            if (font == null) throw new InvalidOperationException("Bangers SDF not found (guid " + TitleFontGuid + ").");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var screen = Object.FindFirstObjectByType<ResultsScreen>(FindObjectsInactive.Include);
            var canvas = FindRoot(scene, "Canvas_Results");
            if (screen == null || canvas == null)
                throw new InvalidOperationException("SCN_Results needs ResultsScreen and Canvas_Results.");

            // Previous bake, plus the flat list, header and hint the sheet replaces (only ResultsScreen used them).
            foreach (var old in new[] { SheetName, "Header", "Rows", "Hint" })
            {
                var child = canvas.Find(old);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }

            var sheet = BuildSheet(canvas, font);
            Assign(screen, "sheet", sheet);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Results sheet baked into " + ScenePath + " and " + RowPrefabPath);
        }

        private static ResultsSheet BuildSheet(Transform canvas, TMP_FontAsset font)
        {
            var root = new GameObject(SheetName, typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            root.transform.SetAsLastSibling(); // drawn over the scene's backdrop, like its old sorting order 10
            PaperUI.Stretch((RectTransform)root.transform);

            var paper = PaperUI.Panel("Paper", root.transform, PaperUI.Paper, -1f, true);
            Center(paper.rectTransform, Middle, new Vector2(0f, 30f), SheetSize);
            // The kit's tape is sized for small badges; a sheet this big needs a wider strip.
            if (paper.transform.Find("Tape") is RectTransform tape) tape.sizeDelta = new Vector2(170f, 50f);

            var title = Label("Title", paper.transform, font, 110f, 50f, PaperUI.Ink, TextAlignmentOptions.Center);
            Center(title.rectTransform, Top, new Vector2(0f, -95f), new Vector2(1000f, 130f));

            var tab = PaperUI.Pill("Round Tab", paper.transform, PaperUI.Ink, 52f);
            Center(tab.rectTransform, Top, new Vector2(0f, -190f), new Vector2(320f, 52f));
            tab.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.5f);
            var round = Label("Label", tab.transform, font, 36f, 18f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(round.rectTransform, 6f);

            var rowsRoot = new GameObject("Rows", typeof(RectTransform)).transform;
            rowsRoot.SetParent(paper.transform, false);
            PaperUI.Stretch((RectTransform)rowsRoot); // same frame as the paper, so row offsets stay from its top

            var template = BuildRow(rowsRoot, font);
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, RowPrefabPath);
            Object.DestroyImmediate(template);
            var rows = new ResultsRow[PlayerRegistry.MaxPlayers];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rowsRoot);
                row.name = "Row " + (i + 1);
                var rect = (RectTransform)row.transform;
                Center(rect, Top, new Vector2(0f, FirstRowY - i * RowStep - RowSize.y * .5f), RowSize);
                rect.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? -.6f : .6f);
                row.SetActive(false);
                rows[i] = row.GetComponent<ResultsRow>();
            }

            var pill = PaperUI.Pill("Hint", root.transform, PaperUI.Paper, 64f);
            Center(pill.rectTransform, Middle, new Vector2(0f, -478f), new Vector2(1300f, 64f));
            var hint = Label("Label", pill.transform, font, 34f, 16f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(hint.rectTransform, 8f);

            var sheet = root.AddComponent<ResultsSheet>();
            Assign(sheet, "title", title, "round", round, "hint", hint);
            var so = new SerializedObject(sheet);
            var list = so.FindProperty("rows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false); // ResultsSheet.SetVisible pops the rows in only when it goes from hidden to shown
            return sheet;
        }

        // Neutral row (no offset, no tilt); each scene instance gets its own place and tilt.
        private static GameObject BuildRow(Transform parent, TMP_FontAsset font)
        {
            var panel = PaperUI.Panel("PF_ResultsRow", parent, Color.white);
            Center(panel.rectTransform, Top, Vector2.zero, RowSize);
            var t = panel.transform;

            var medal = PaperUI.Disc("Medal", t, Gold);
            Center(medal.rectTransform, Left, new Vector2(72f, 0f), new Vector2(108f, 108f));
            var place = Label("Place", t, font, 64f, 30f, PaperUI.Ink, TextAlignmentOptions.Center);
            Center(place.rectTransform, Left, new Vector2(72f, 0f), new Vector2(104f, 104f));

            var disc = PaperUI.Disc("Disc", t, Color.white);
            Center(disc.rectTransform, Left, new Vector2(196f, 0f), new Vector2(92f, 92f));
            var slotTag = Label("Tag", disc.transform, font, 38f, 18f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(slotTag.rectTransform, 6f);

            // Typed player names can hold any character: the default font, not Bangers.
            var playerName = Label("Name", t, null, 44f, 24f, PaperUI.Ink, TextAlignmentOptions.Left);
            playerName.overflowMode = TextOverflowModes.Ellipsis;
            PaperUI.Place(playerName.rectTransform, Left, new Vector2(262f, 0f), new Vector2(480f, 90f));

            var stamp = PaperUI.Pill("Round Points", t, PaperUI.TimerTeal, 64f);
            Center(stamp.rectTransform, Left, new Vector2(840f, 0f), new Vector2(150f, 64f));
            stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
            var gained = Label("Label", stamp.transform, font, 44f, 22f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(gained.rectTransform, 4f);

            var total = Label("Total", t, font, 96f, 40f, PaperUI.Ink, TextAlignmentOptions.Right);
            PaperUI.Place(total.rectTransform, Right, new Vector2(-30f, 0f), new Vector2(220f, 110f));

            var row = panel.gameObject.AddComponent<ResultsRow>();
            Assign(row, "panel", panel, "medal", medal.gameObject, "place", place, "disc", disc, "slotTag", slotTag,
                "playerName", playerName, "stamp", stamp.gameObject, "gained", gained, "total", total);
            return panel.gameObject;
        }

        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, float min, Color color,
            TextAlignmentOptions alignment)
        {
            var label = PaperUI.Text(name, parent, font, size, color, alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = size;
            return label;
        }

        // Like PaperUI.Place but turning around its centre (tilt and pop).
        private static void Center(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            PaperUI.Place(rect, anchor, position, size);
            rect.pivot = Middle;
        }

        // Pairs of serialized field name and value.
        private static void Assign(Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                var property = so.FindProperty((string)pairs[i]);
                if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + pairs[i]);
                property.objectReferenceValue = (Object)pairs[i + 1];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CheckSprite(string name, Sprite fromKit)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (asset == null || asset != fromKit)
                throw new InvalidOperationException("PaperUI sprite " + name + " is not the asset in " + SpriteFolder);
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }
    }
}
#endif
