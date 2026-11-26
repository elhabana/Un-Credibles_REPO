using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnCredibles.Minigames.EditorTools
{
    // Records the short gameplay loop of the rules card. It renders Camera.main (no HUD) for a few seconds,
    // saves VID_MG_<Name>_Preview.mp4 in the minigame's Art folder and assigns it as the MinigameData preview.
    // Recording again replaces the clip.
    //  - "Record 8 s (Play Mode)": the minigame scene already playing, from now.
    //  - "Record all minigames": opens every SCN_MG_ scene of the build with four bots, plays it, records
    //    a stretch of the game and moves on to the next one. Also started by creating Temp/record-rules-videos.request.
    [InitializeOnLoad]
    public static class RulesVideoRecorder
    {
        private const string MenuPath = "UnCredibles/Rules Video/";
        private const int Width = 1280;
        private const int Height = 720;
        private const int FrameRate = 30;
        private const float Seconds = 8f;
        private const float GameSecondsBeforeRecording = 12f; // trails, carried grandmas... something to see
        private const string RequestFile = "Temp/record-rules-videos.request";
        private const string BakeRequestFile = "Temp/bake-minigame-ui.request";
        private const string Vp8RequestFile = "Temp/vp8-rules-videos.request";
        private const string LogFile = "Temp/record-rules-videos.log";
        private const string QueueKey = "RulesVideo.Queue";
        private const string ReturnSceneKey = "RulesVideo.ReturnScene";

        private const string NextKey = "RulesVideo.Next";
        private const string SavedKey = "RulesVideo.Saved";

        static RulesVideoRecorder()
        {
            EditorApplication.playModeStateChanged += HandlePlayMode;
            // A pending "next scene" survives domain reloads (a delayCall would not).
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(NextKey, false) || EditorApplication.isPlayingOrWillChangePlaymode
                    || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                SessionState.SetBool(NextKey, false);
                PlayNext();
            };
            EditorApplication.delayCall += () =>
            {
                // Optional first step: rebuild the minigame UI prefabs (the rules card) before filming.
                if (File.Exists(BakeRequestFile))
                {
                    File.Delete(BakeRequestFile);
                    UnCredibles.Minigames.Editor.MinigameUIBake.Bake();
                }
                if (File.Exists(Vp8RequestFile))
                {
                    File.Delete(Vp8RequestFile);
                    UseVp8ForAll();
                }
                if (!File.Exists(RequestFile)) return;
                File.Delete(RequestFile);
                RecordAll();
            };
        }

        [MenuItem(MenuPath + "Record 8 s (Play Mode)")]
        private static void RecordNow()
        {
            if (!StartRecording(null))
                EditorUtility.DisplayDialog("Rules video", "Needs a minigame scene in Play Mode with a MinigameData and a Main Camera.", "OK");
        }

        [MenuItem(MenuPath + "Record 8 s (Play Mode)", true)]
        private static bool CanRecordNow() => EditorApplication.isPlaying && Object.FindFirstObjectByType<Runner>() == null;

        [MenuItem(MenuPath + "Record all minigames")]
        private static void RecordAll()
        {
            File.WriteAllText(LogFile, "");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Log("Leave Play Mode first.");
                return;
            }
            // Opening other scenes would throw away unsaved work.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (!EditorSceneManager.GetSceneAt(i).isDirty) continue;
                Log("Save the open scenes first: one has unsaved changes.");
                return;
            }

            var queue = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (Path.GetFileName(scene.path).StartsWith("SCN_MG_")) queue.Add(scene.path);
            SessionState.SetString(ReturnSceneKey, EditorSceneManager.GetActiveScene().path);
            SessionState.SetString(QueueKey, string.Join("|", queue));
            Log($"Recording {queue.Count} minigame(s).");
            PlayNext();
        }

        private static string[] Queue
        {
            get
            {
                string value = SessionState.GetString(QueueKey, "");
                return value.Length == 0 ? Array.Empty<string>() : value.Split('|');
            }
        }

        private static void PlayNext()
        {
            var queue = Queue;
            if (queue.Length == 0)
            {
                string back = SessionState.GetString(ReturnSceneKey, "");
                SessionState.EraseString(ReturnSceneKey);
                if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back);
                Log("DONE");
                return;
            }

            var opened = EditorSceneManager.OpenScene(queue[0]);
            // Four bots for a hands-off game. Only in memory: the scene is reopened from disk afterwards.
            foreach (var root in opened.GetRootGameObjects())
            {
                foreach (var bootstrap in root.GetComponentsInChildren<DebugBootstrap>(true))
                {
                    var serialized = new SerializedObject(bootstrap);
                    var players = serialized.FindProperty("players");
                    players.arraySize = 4;
                    for (int i = 0; i < players.arraySize; i++)
                        players.GetArrayElementAtIndex(i).FindPropertyRelative("inputSource").intValue = (int)UnCredibles.Players.InputSourceType.AI;
                    serialized.FindProperty("skipCountdown").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Log($"Playing {queue[0]}");
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayMode(PlayModeStateChange change)
        {
            if (Queue.Length == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(SavedKey, false);
                EditorApplication.update += WaitThenRecord;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= WaitThenRecord;
                if (!SessionState.GetBool(SavedKey, false)) Log("  play mode ended before the clip was saved");
                var queue = Queue;
                // Reopen the scene from disk so the bot setup is not left as unsaved changes.
                EditorSceneManager.OpenScene(queue[0]);
                SessionState.SetString(QueueKey, string.Join("|", queue, 1, queue.Length - 1));
                SessionState.SetBool(NextKey, true);
            }
        }

        private static float playingSince = -1f;

        private static void WaitThenRecord()
        {
            var controller = Object.FindFirstObjectByType<MinigameController>();
            if (controller == null || !controller.IsPlaying)
            {
                playingSince = -1f;
                return;
            }
            if (playingSince < 0f) playingSince = Time.time;
            // Mechanic clips start filming at once (their events can come early); the preview waits for some action.
            bool paged = controller.Data != null && controller.Data.Mechanics.Count > 0;
            if (!paged && Time.time - playingSince < GameSecondsBeforeRecording) return;

            EditorApplication.update -= WaitThenRecord;
            playingSince = -1f;
            Action<bool> done = saved =>
            {
                SessionState.SetBool(SavedKey, saved);
                Log(saved ? "  saved" : "  NOT saved");
                EditorApplication.ExitPlaymode();
            };
            if (!(paged ? StartMechanics(done) : StartRecording(done)))
            {
                Log("  no minigame / camera found");
                EditorApplication.ExitPlaymode();
            }
        }

        private static bool StartRecording(Action<bool> done)
        {
            var controller = Object.FindFirstObjectByType<MinigameController>();
            var camera = Camera.main;
            if (controller == null || controller.Data == null || camera == null) return false;
            // A plain scene object: leaving Play Mode destroys it, and OnDestroy drops the half clip.
            new GameObject("RulesVideoRecorder").AddComponent<Runner>().Begin(controller.Data, camera, done);
            return true;
        }

        private static bool StartMechanics(Action<bool> done)
        {
            var controller = Object.FindFirstObjectByType<MinigameController>();
            var camera = Camera.main;
            if (controller == null || controller.Data == null || camera == null) return false;
            new GameObject("RulesVideoRecorder").AddComponent<MechanicRunner>().Begin(controller, camera, done);
            return true;
        }

        // Renders the camera without the name tags over the players ("BOT", "P1").
        private static void RenderClean(Camera view, RenderTexture target, PlayerPresentation[] players)
        {
            foreach (var player in players)
                if (player != null && player.Label != null) player.Label.renderer.enabled = false;
            var previousTarget = view.targetTexture;
            view.targetTexture = target;
            view.Render();
            view.targetTexture = previousTarget;
            foreach (var player in players)
                if (player != null && player.Label != null) player.Label.renderer.enabled = true;
        }

        private static PlayerPresentation[] FindPlayers() =>
            Object.FindObjectsByType<PlayerPresentation>(FindObjectsSortMode.None);

        // Copies the encoded temp file into the minigame's Art folder and imports it.
        private static UnityEngine.Video.VideoClip SaveClip(string tempPath, MinigameData data, string suffix)
        {
            string assetPath = PathFor(data, suffix);
            File.Copy(tempPath, assetPath, true);
            File.Delete(tempPath);
            AssetDatabase.ImportAsset(assetPath);
            UseVp8(assetPath);
            Debug.Log($"[RulesVideo] Saved {assetPath}.");
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(assetPath);
        }

        // Imported as VP8: Unity decodes it itself. The raw H.264 went through the system decoder, which froze
        // the card's clips for up to almost a second at a time (several clips prepared at once).
        private static void UseVp8(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is VideoClipImporter importer)) return;
            var settings = importer.defaultTargetSettings;
            if (settings.enableTranscoding && settings.codec == VideoCodec.VP8) return;
            settings.enableTranscoding = true;
            settings.codec = VideoCodec.VP8;
            settings.bitrateMode = VideoBitrateMode.High;
            importer.defaultTargetSettings = settings;
            importer.SaveAndReimport();
        }

        [MenuItem(MenuPath + "Import rules videos as VP8")]
        private static void UseVp8ForAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("VID_MG_ t:VideoClip"))
                UseVp8(AssetDatabase.GUIDToAssetPath(guid));
            Debug.Log("[RulesVideo] VP8 DONE");
        }

        private static void Log(string line)
        {
            Debug.Log("[RulesVideo] " + line);
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }

        // VID_MG_<Name>_Preview.mp4 or VID_MG_<Name>_<Mechanic>.mp4, in the minigame's Art folder
        // (SO_MG_<Name>_Data lives in its sibling ScriptableObjects/).
        private static string PathFor(MinigameData data, string suffix)
        {
            string folder = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(data)));
            string technicalName = data.SceneName.Replace("SCN_MG_", "");
            string name = char.ToUpperInvariant(suffix[0]) + suffix.Substring(1);
            return $"{folder}/Art/VID_MG_{technicalName}_{name}.mp4".Replace('\\', '/');
        }

        // Films one clip per mechanic of the minigame. The last PreSeconds of the game are always kept on the GPU;
        // when the minigame reports a mechanic still missing (MinigameController.MechanicShown), the clip is those
        // seconds plus PostSeconds more, cropped around that player (zoomed in, following them smoothly).
        private sealed class MechanicRunner : MonoBehaviour
        {
            // The crop is exactly ClipWidth x ClipHeight pixels of the render (no rescaling, so it stays sharp),
            // and both sizes are multiples of 16: H.264 decoders pad other sizes and the edges can come out odd.
            private const int RenderWidth = 1536;
            private const int RenderHeight = 864;
            private const int ClipWidth = 1024;   // RenderWidth / Zoom
            private const int ClipHeight = 576;
            private const float Zoom = 1.5f;
            private const float PreSeconds = 2.5f;
            private const float PostSeconds = 2f;
            private const float Follow = .06f;    // how fast the crop catches up with the player, per frame

            private MinigameController controller;
            private MinigameData data;
            private Camera view;
            private Action<bool> done;
            private PlayerPresentation[] players;
            private RenderTexture render;
            private RenderTexture[] ring;
            private Vector2[,] focus;             // viewport position of every slot, per ring frame
            private RenderTexture crop;
            private Texture2D readback;
            private readonly List<string> missing = new List<string>();
            private int frames;
            private int savedCount;

            private string activeKey;
            private int activeSlot;
            private MediaEncoder encoder;
            private string tempPath;
            private int postLeft;
            private Vector2 center;

            public void Begin(MinigameController minigame, Camera camera, Action<bool> onDone)
            {
                controller = minigame;
                data = minigame.Data;
                view = camera;
                done = onDone;
                players = FindPlayers();
                foreach (var mechanic in data.Mechanics) missing.Add(mechanic.key);

                int ringSize = Mathf.RoundToInt(PreSeconds * FrameRate);
                render = new RenderTexture(RenderWidth, RenderHeight, 24);
                ring = new RenderTexture[ringSize];
                for (int i = 0; i < ringSize; i++) ring[i] = new RenderTexture(RenderWidth, RenderHeight, 0);
                focus = new Vector2[ringSize, UnCredibles.Players.PlayerRegistry.MaxPlayers];
                crop = new RenderTexture(ClipWidth, ClipHeight, 0);
                readback = new Texture2D(ClipWidth, ClipHeight, TextureFormat.RGBA32, false);
                Time.captureFramerate = FrameRate;
                MinigameController.MechanicShown += HandleMechanic;
                Debug.Log($"[RulesVideo] Filming the mechanics of {data.DisplayName}: {string.Join(", ", missing)}");
            }

            private void HandleMechanic(MinigameController source, string key, UnCredibles.Players.PlayerSlot slot)
            {
                // One clip at a time, only missing ones, and only with a full ring of frames before it.
                if (source != controller || encoder != null || slot == null || !missing.Contains(key) || frames < ring.Length) return;
                activeKey = key;
                activeSlot = slot.SlotIndex;
                tempPath = Path.Combine(Path.GetTempPath(), $"RulesVideo_{Guid.NewGuid():N}.mp4");
                encoder = new MediaEncoder(tempPath, new VideoTrackAttributes
                {
                    frameRate = new MediaRational(FrameRate),
                    width = ClipWidth,
                    height = ClipHeight,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High,
                });
                // The seconds before the event, oldest first.
                int oldest = frames % ring.Length;
                center = focus[oldest, activeSlot];
                for (int i = 0; i < ring.Length; i++) Encode((oldest + i) % ring.Length);
                postLeft = Mathf.RoundToInt(PostSeconds * FrameRate);
                Log($"  {key}: player {activeSlot + 1}");
            }

            private void LateUpdate()
            {
                if (ring == null) return;
                if (view == null) view = Camera.main;
                if (view == null || controller == null || controller.State >= MinigameState.Results)
                {
                    Finish();
                    return;
                }

                int index = frames % ring.Length;
                RenderClean(view, render, players);
                Graphics.Blit(render, ring[index]);
                foreach (var player in players)
                {
                    if (player == null || player.Slot == null) continue;
                    var point = view.WorldToViewportPoint(player.transform.position + Vector3.up);
                    focus[index, player.Slot.SlotIndex] = new Vector2(point.x, point.y);
                }
                frames++;

                if (encoder == null) return;
                Encode(index);
                if (--postLeft > 0) return;
                CloseClip();
                if (missing.Count == 0) Finish();
            }

            // Crops the frame around the followed player (kept inside the picture) into the clip.
            private void Encode(int index)
            {
                center = Vector2.Lerp(center, focus[index, activeSlot], Follow);
                float size = 1f / Zoom;
                // Snapped to whole pixels of the render: no blur from sampling between them.
                float x = Mathf.Round(Mathf.Clamp(center.x - size * .5f, 0f, 1f - size) * RenderWidth) / RenderWidth;
                float y = Mathf.Round(Mathf.Clamp(center.y - size * .5f, 0f, 1f - size) * RenderHeight) / RenderHeight;
                Graphics.Blit(ring[index], crop, new Vector2(size, size), new Vector2(x, y));
                var previous = RenderTexture.active;
                RenderTexture.active = crop;
                readback.ReadPixels(new Rect(0, 0, ClipWidth, ClipHeight), 0, 0, false);
                readback.Apply(false);
                RenderTexture.active = previous;
                encoder.AddFrame(readback);
            }

            private void CloseClip()
            {
                encoder.Dispose();
                encoder = null;
                var clip = SaveClip(tempPath, data, activeKey);
                var serialized = new SerializedObject(data);
                var mechanics = serialized.FindProperty("mechanics");
                for (int i = 0; i < mechanics.arraySize; i++)
                {
                    var element = mechanics.GetArrayElementAtIndex(i);
                    if (element.FindPropertyRelative("key").stringValue == activeKey)
                        element.FindPropertyRelative("clip").objectReferenceValue = clip;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                File.WriteAllBytes($"Temp/VID_{data.SceneName}_{activeKey}.png", readback.EncodeToPNG()); // last frame, to check it
                missing.Remove(activeKey);
                savedCount++;
                activeKey = null;
            }

            private void Finish()
            {
                if (ring == null) return;
                MinigameController.MechanicShown -= HandleMechanic;
                Time.captureFramerate = 0;
                if (encoder != null)
                {
                    // Cut short (the game ended): drop the half clip.
                    encoder.Dispose();
                    encoder = null;
                    File.Delete(tempPath);
                }
                if (missing.Count > 0) Log("  never happened: " + string.Join(", ", missing));
                render.Release();
                foreach (var texture in ring) texture.Release();
                crop.Release();
                Destroy(readback);
                ring = null;
                var callback = done;
                done = null;
                callback?.Invoke(savedCount > 0);
                if (this != null) Destroy(gameObject);
            }

            // Leaving Play Mode early: keep what was saved, drop the rest.
            private void OnDestroy() => Finish();
        }

        private sealed class Runner : MonoBehaviour
        {
            private MinigameData data;
            private Camera view;
            private Action<bool> done;
            private PlayerPresentation[] players;
            private RenderTexture target;
            private Texture2D frame;
            private MediaEncoder encoder;
            private string tempPath;
            private int framesLeft;
            private int framesTotal;

            public void Begin(MinigameData minigame, Camera camera, Action<bool> onDone)
            {
                data = minigame;
                view = camera;
                done = onDone;
                players = FindPlayers();
                target = new RenderTexture(Width, Height, 24);
                frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tempPath = Path.Combine(Path.GetTempPath(), $"RulesVideo_{Guid.NewGuid():N}.mp4");
                var video = new VideoTrackAttributes
                {
                    frameRate = new MediaRational(FrameRate),
                    width = Width,
                    height = Height,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.Medium,
                };
                encoder = new MediaEncoder(tempPath, video);
                framesTotal = framesLeft = Mathf.RoundToInt(Seconds * FrameRate);
                // Game time advances exactly one video frame per rendered frame: smooth even if the editor is slow.
                Time.captureFramerate = FrameRate;
                Debug.Log($"[RulesVideo] Recording {Seconds} s of {data.DisplayName}...");
            }

            private void LateUpdate()
            {
                if (encoder == null) return;
                if (view == null) view = Camera.main;
                if (view == null)
                {
                    Finish(false);
                    Destroy(gameObject);
                    return;
                }

                RenderClean(view, target, players);

                var previousActive = RenderTexture.active;
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                frame.Apply(false);
                RenderTexture.active = previousActive;
                encoder.AddFrame(frame);
                // A still of the middle of the clip, to check it without a video player (Temp/, not imported).
                if (framesLeft == framesTotal / 2)
                    File.WriteAllBytes($"Temp/VID_{data.SceneName}.png", frame.EncodeToPNG());

                if (--framesLeft > 0) return;
                Finish(true);
                Destroy(gameObject);
            }

            // Leaving Play Mode early stops the recording without saving a half clip.
            private void OnDestroy()
            {
                if (encoder != null) Finish(false);
            }

            private void Finish(bool save)
            {
                Time.captureFramerate = 0;
                encoder.Dispose();
                encoder = null;
                target.Release();
                Destroy(target);
                Destroy(frame);

                if (save)
                {
                    var clip = SaveClip(tempPath, data, "Preview");
                    var serialized = new SerializedObject(data);
                    serialized.FindProperty("preview").objectReferenceValue = clip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
                else File.Delete(tempPath);
                done?.Invoke(save);
            }
        }
    }
}
