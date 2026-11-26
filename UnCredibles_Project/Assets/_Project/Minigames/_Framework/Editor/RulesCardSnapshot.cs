using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnCredibles.Minigames.EditorTools
{
    // Screenshots of the rules card to review its layout without playing a whole match:
    // plays each paged minigame scene with the card on (P1 keyboard, never pressing, so it stays up)
    // and saves Temp/RulesCard_<Scene>_1.png (first page) and _2.png (after the pages turned once).
    // Started from the menu or by creating Temp/capture-rules-card.request.
    [InitializeOnLoad]
    public static class RulesCardSnapshot
    {
        private const string RequestFile = "Temp/capture-rules-card.request";
        private const string QueueKey = "RulesCardSnapshot.Queue";
        private const string NextKey = "RulesCardSnapshot.Next";
        private static readonly float[] ShotTimes = { 2.5f, 9.5f, 16f }; // real seconds after entering Play Mode

        private static float playStart = -1f;
        private static int shot;

        static RulesCardSnapshot()
        {
            EditorApplication.playModeStateChanged += HandlePlayMode;
            EditorApplication.update += () =>
            {
                if (File.Exists(RequestFile) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                {
                    File.Delete(RequestFile);
                    CaptureAll();
                }
                if (!SessionState.GetBool(NextKey, false) || EditorApplication.isPlayingOrWillChangePlaymode
                    || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                SessionState.SetBool(NextKey, false);
                PlayNext();
            };
        }

        [MenuItem("UnCredibles/Rules Video/Screenshot rules cards")]
        private static void CaptureAll()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[RulesCard] Save the open scenes first.");
                    return;
                }
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(MinigameData)))
            {
                var data = AssetDatabase.LoadAssetAtPath<MinigameData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.Mechanics.Count == 0) continue;
                foreach (var scene in EditorBuildSettings.scenes)
                    if (Path.GetFileNameWithoutExtension(scene.path) == data.SceneName) scenes.Add(scene.path);
            }
            SessionState.SetString(QueueKey, string.Join("|", scenes));
            PlayNext();
        }

        private static string[] Queue
        {
            get
            {
                string value = SessionState.GetString(QueueKey, "");
                return value.Length == 0 ? System.Array.Empty<string>() : value.Split('|');
            }
        }

        private static void PlayNext()
        {
            var queue = Queue;
            if (queue.Length == 0)
            {
                Debug.Log("[RulesCard] DONE");
                return;
            }
            var opened = EditorSceneManager.OpenScene(queue[0]);
            foreach (var root in opened.GetRootGameObjects())
            {
                foreach (var bootstrap in root.GetComponentsInChildren<DebugBootstrap>(true))
                {
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("showRulesCard").boolValue = true;
                    var players = serialized.FindProperty("players");
                    players.arraySize = 4;
                    for (int i = 0; i < 4; i++)
                        players.GetArrayElementAtIndex(i).FindPropertyRelative("inputSource").intValue =
                            (int)(i == 0 ? UnCredibles.Players.InputSourceType.Keyboard : UnCredibles.Players.InputSourceType.AI);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayMode(PlayModeStateChange change)
        {
            if (Queue.Length == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                playStart = Time.realtimeSinceStartup;
                shot = 0;
                EditorApplication.update += Shoot;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Shoot;
                var queue = Queue;
                EditorSceneManager.OpenScene(queue[0]); // drop the in-memory card setup
                SessionState.SetString(QueueKey, string.Join("|", queue, 1, queue.Length - 1));
                SessionState.SetBool(NextKey, true);
            }
        }

        // Every editor update: which frame each playing card video shows, to spot stalls (Temp/rules-card-playback.log).
        private static void Probe()
        {
            float t = Time.realtimeSinceStartup - playStart;
            foreach (var player in Object.FindObjectsByType<UnityEngine.Video.VideoPlayer>(FindObjectsSortMode.None))
            {
                if (!player.isActiveAndEnabled || player.clip == null) continue;
                File.AppendAllText(PlaybackLog,
                    $"{t:F3}\t{player.clip.name}\tprepared={player.isPrepared}\tplaying={player.isPlaying}\tframe={player.frame}\ttime={player.time:F3}\tdt={Time.unscaledDeltaTime:F3}\n");
            }
        }

        private const string PlaybackLog = "Temp/rules-card-playback.log";

        private static void Shoot()
        {
            Probe();
            if (shot >= ShotTimes.Length)
            {
                EditorApplication.update -= Shoot;
                EditorApplication.ExitPlaymode();
                return;
            }
            if (Time.realtimeSinceStartup - playStart < ShotTimes[shot]) return;
            string scene = Path.GetFileNameWithoutExtension(Queue[0]);
            string path = $"Temp/RulesCard_{scene}_{shot + 1}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[RulesCard] " + path);
            shot++;
        }
    }
}
