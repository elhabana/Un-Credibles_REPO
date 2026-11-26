using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnCredibles.Minigames.CrossyRoad.EditorTools
{
    // Where does Crossy Road lose frames? Plays a bot game in stretches and measures GPU and main-thread time:
    //   comic filter on, off, on, off (alternating, so a cost growing with the game shows up as such).
    // Writes Temp/cr-perf.txt. Menu or Temp/cr-perf.request. Keep the editor focused while it runs.
    [InitializeOnLoad]
    public static class CrossyRoadPerfProbe
    {
        private const string ScenePath = "Assets/_Project/Minigames/MG_CrossyRoad/Scenes/SCN_MG_CrossyRoad.unity";
        private const string RequestFile = "Temp/cr-perf.request";
        private const string ReportFile = "Temp/cr-perf.txt";
        private const string ActiveKey = "CrossyRoadPerf.Active";
        private const float Warmup = 5f;
        private const float Stretch = 8f;
        private static readonly string[] Names = { "A normal", "B no comic filter", "C normal again", "D no comic filter again" };

        private static float start;
        private static int phase;
        private static readonly List<float>[] gpu = Enumerable.Range(0, 4).Select(_ => new List<float>()).ToArray();
        private static readonly List<float>[] cpu = Enumerable.Range(0, 4).Select(_ => new List<float>()).ToArray();
        private static readonly List<float>[] frame = Enumerable.Range(0, 4).Select(_ => new List<float>()).ToArray();
        private static readonly FrameTiming[] timings = new FrameTiming[1];
        private static ProfilerRecorder mainThread;
        private static ScriptableRendererFeature ssao;
        private static bool ssaoWasActive;
        private static float nextRequestCheck;

        static CrossyRoadPerfProbe()
        {
            EditorApplication.playModeStateChanged += HandlePlayMode;
            EditorApplication.update += () =>
            {
                if (Time.realtimeSinceStartup < nextRequestCheck) return; // a file check per second, not per frame
                nextRequestCheck = Time.realtimeSinceStartup + 1f;
                if (!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                File.Delete(RequestFile);
                Run();
            };
        }

        [MenuItem("UnCredibles/Crossy Road/Measure performance")]
        private static void Run()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Perf] Save the open scenes first.");
                    return;
                }
            var scene = EditorSceneManager.OpenScene(ScenePath);
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
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                start = Time.realtimeSinceStartup;
                phase = -1;
                foreach (var list in gpu.Concat(cpu).Concat(frame)) list.Clear();
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                ssao = FindSsao();
                ssaoWasActive = ssao != null && ssao.isActive;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(ActiveKey, false);
                mainThread.Dispose();
                if (ssao != null) ssao.SetActive(ssaoWasActive); // the renderer asset is shared: always put it back
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[Perf] DONE");
            }
        }

        private static void Tick()
        {
            float t = Time.realtimeSinceStartup - start - Warmup;
            FrameTimingManager.CaptureFrameTimings();
            if (t < 0f) return;
            int now = Mathf.Min(Names.Length, (int)(t / Stretch));
            if (now != phase)
            {
                phase = now;
                if (phase == Names.Length)
                {
                    EditorApplication.update -= Tick;
                    Report();
                    EditorApplication.ExitPlaymode();
                    return;
                }
                Apply(phase);
                return; // the switch frame itself is not representative
            }
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0) gpu[phase].Add((float)timings[0].gpuFrameTime);
            cpu[phase].Add(mainThread.LastValue / 1e6f);
            frame[phase].Add(Time.unscaledDeltaTime * 1000f);
        }

        private static void Apply(int stretch)
        {
            // Alternating, so a cost that only grows with the game's progress cannot pass for the filter's.
            foreach (var filter in Object.FindObjectsByType<UnCredibles.Shared.ComicFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                filter.enabled = stretch % 2 == 0;
        }

        private static ScriptableRendererFeature FindSsao()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return null;
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (!(field?.GetValue(asset) is ScriptableRendererData[] list)) return null;
            foreach (var data in list)
                if (data != null)
                    foreach (var feature in data.rendererFeatures)
                        if (feature != null && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion")) return feature;
            return null;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        private static void Report()
        {
            var report = new StringBuilder($"SSAO feature found: {ssao != null}\n");
            for (int i = 0; i < Names.Length; i++)
                report.AppendLine($"{Names[i],-24} frames {frame[i].Count,4}  frame {Median(frame[i]),6:F1} ms  " +
                                  $"GPU {Median(gpu[i]),6:F1} ms (samples {gpu[i].Count})  main thread {Median(cpu[i]),6:F1} ms");
            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[Perf] " + report.ToString().Replace("\n", " | "));
        }
    }
}
