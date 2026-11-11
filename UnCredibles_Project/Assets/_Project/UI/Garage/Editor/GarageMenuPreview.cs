#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.UI.Garage.Editor
{
    // Optional edit-mode preview of the authored Home or Lobby panel without changing scene activation.
    // A hidden copy displays the selected panel and the original renderers are culled until the preview closes.
    // Rebuilt shortly after each edit; removed before Play, assembly reloads and closing the scene.
    [InitializeOnLoad]
    public static class GarageMenuPreview
    {
        private enum Mode { Off, Home, Lobby }
        private const string ModeKey = "UnCredibles.GarageMenuPreview";
        private const string MenuPath = "UnCredibles/Garage/Menu preview/";
        private const double RebuildDelay = .25; // after the last edit: not on every step of a drag
        private static readonly List<CanvasRenderer> culled = new List<CanvasRenderer>();
        private static GameObject preview;
        private static double rebuildAt = -1;

        static GarageMenuPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorSceneManager.sceneOpened += (_, _) => Schedule();
            EditorSceneManager.sceneClosing += (_, _) => Clear();
            Undo.postprocessModifications += Modified;
            Undo.undoRedoPerformed += Schedule;
            Schedule();
        }

        private static Mode Current => (Mode)EditorPrefs.GetInt(ModeKey, (int)Mode.Off);

        [MenuItem(MenuPath + "Home", priority = 100)] private static void ShowHome() => SetMode(Mode.Home);
        [MenuItem(MenuPath + "Lobby", priority = 101)] private static void ShowLobby() => SetMode(Mode.Lobby);
        [MenuItem(MenuPath + "Off", priority = 102)] private static void ShowOff() => SetMode(Mode.Off);
        [MenuItem(MenuPath + "Home", true)] private static bool CanShowHome() => Check(Mode.Home);
        [MenuItem(MenuPath + "Lobby", true)] private static bool CanShowLobby() => Check(Mode.Lobby);
        [MenuItem(MenuPath + "Off", true)] private static bool CanShowOff() => Check(Mode.Off);

        private static bool Check(Mode mode)
        {
            Menu.SetChecked(MenuPath + mode, Current == mode);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void SetMode(Mode mode)
        {
            EditorPrefs.SetInt(ModeKey, (int)mode);
            Rebuild();
        }

        private static UndoPropertyModification[] Modified(UndoPropertyModification[] modifications)
        {
            Schedule();
            return modifications;
        }

        private static void Schedule() => rebuildAt = EditorApplication.timeSinceStartup + RebuildDelay;

        private static void Update()
        {
            if (rebuildAt < 0 || EditorApplication.timeSinceStartup < rebuildAt) return;
            rebuildAt = -1;
            Rebuild();
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                rebuildAt = -1;
                Clear();
            }
            else if (change == PlayModeStateChange.EnteredEditMode) Schedule();
        }

        private static void Rebuild()
        {
            Clear();
            if (Current == Mode.Off || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var front = Object.FindFirstObjectByType<GarageFrontend>();
            if (front == null || front.menu == null || front.menuRoot == null || front.menuRoot.transform.parent == null) return;
            var canvas = front.menuRoot.transform.parent;
            var scene = canvas.gameObject.scene;
            bool wasDirty = scene.isDirty;
            try
            {
                preview = Object.Instantiate(canvas.gameObject, canvas.parent);
                if (canvas.parent == null) SceneManager.MoveGameObjectToScene(preview, canvas.gameObject.scene);
                preview.name = canvas.name + " (preview)";
                Hide(preview);
                var copy = preview.transform;
                var menuRoot = Counterpart(canvas, copy, front.menuRoot.transform);
                var lobbyRoot = front.lobbyRoot != null ? Counterpart(canvas, copy, front.lobbyRoot.transform) : null;
                bool home = Current == Mode.Home || lobbyRoot == null;
                menuRoot.gameObject.SetActive(home);
                if (lobbyRoot != null) lobbyRoot.gameObject.SetActive(!home);
                // Last, so the copy above was not made from culled renderers.
                foreach (var renderer in canvas.GetComponentsInChildren<CanvasRenderer>(true))
                    if (!renderer.cull)
                    {
                        renderer.cull = true;
                        culled.Add(renderer);
                    }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Menu preview] Could not build it; showing the scene as authored.\n" + exception);
                Clear();
            }
            finally
            {
                if (!wasDirty) KeepClean(scene);
            }
        }

        private static void Clear()
        {
            if (preview == null && culled.Count == 0) return;
            var scene = preview != null ? preview.scene : culled[0] != null ? culled[0].gameObject.scene : default;
            bool wasDirty = scene.IsValid() && scene.isDirty;
            foreach (var renderer in culled)
                if (renderer != null) renderer.cull = false;
            culled.Clear();
            if (preview != null) Object.DestroyImmediate(preview);
            preview = null;
            if (scene.IsValid() && !wasDirty) KeepClean(scene);
        }

        // Only the preview's own objects changed (all DontSave, nothing serialized), so a clean scene stays clean.
        // shortcut: Unity has no public call to drop a dirty flag; if this internal one disappears the asterisk may
        // show after a rebuild, but saving still writes nothing of the preview.
        private static void KeepClean(Scene scene)
        {
            if (scene.IsValid() && scene.isDirty) ClearDirtiness?.Invoke(null, new object[] { scene });
        }

        private static readonly System.Reflection.MethodInfo ClearDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static void Hide(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Transform Counterpart(Transform root, Transform copy, Transform original) =>
            original == root ? copy : copy.Find(AnimationUtility.CalculateTransformPath(original, root));
    }
}
#endif
