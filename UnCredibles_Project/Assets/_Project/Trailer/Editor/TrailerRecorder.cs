using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace UnCredibles.Trailer.Editor
{
    // Records the Game view to <Downloads>/UN-CREDIBLES_Previs_Tomas/<take>.mp4 at 1920x1080, constant 24 fps, while in Play Mode.
    // Call Record() once Play Mode has started; it stops by itself after the given game-time seconds.
    public static class TrailerRecorder
    {
        public const int Width = 1920, Height = 1080;
        public const float FrameRate = 24f;
        private static RecorderController controller;
        private static float stopAt;
        public static string LastFile { get; private set; }
        public static bool IsRecording => controller != null && controller.IsRecording();

        // Also records every extra angle of the scene's TrailerGameplay, as <take>_<angle>.mp4.
        public static string Record(string take, float seconds)
        {
            if (!EditorApplication.isPlaying) return "Not in Play Mode";
            if (IsRecording) return "Already recording";
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            string folder = TrailerCapture.TakesFolder;
            Directory.CreateDirectory(folder);
            LastFile = Path.Combine(folder, take) + ".mp4";
            settings.AddRecorderSettings(Movie(take, folder, new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height }));
            var gameplay = Object.FindFirstObjectByType<TrailerGameplay>();
            if (gameplay != null)
                for (int i = 0; i < gameplay.Targets.Count; i++)
                    settings.AddRecorderSettings(Movie(take + "_" + gameplay.angles[i].name, folder,
                        new RenderTextureInputSettings { RenderTexture = gameplay.Targets[i], OutputWidth = Width, OutputHeight = Height }));
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = FrameRate;
            settings.CapFrameRate = true;
            controller = new RecorderController(settings);
            controller.PrepareRecording();
            if (!controller.StartRecording()) return "Recorder failed to start";
            stopAt = Time.time + seconds;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return "Recording " + LastFile;
        }

        private static MovieRecorderSettings Movie(string take, string folder, ImageInputSettings input)
        {
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = take;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.ImageInputSettings = input;
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = Path.Combine(folder, take);
            return movie;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            controller = null;
        }

        private static void Tick()
        {
            if (controller == null || !EditorApplication.isPlaying || Time.time >= stopAt) Stop();
        }
    }
}
