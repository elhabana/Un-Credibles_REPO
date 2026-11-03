#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnCredibles.Trailer
{
    // Fast previs capture: game time is locked to 24 fps (Time.captureFramerate), each source render texture
    // is read back from the GPU every frame and piped raw into its own FFmpeg process (H.264 MP4).
    // Much faster than the Recorder package with several outputs. Editor only.
    public sealed class TrailerCapture : MonoBehaviour
    {
        public const int FrameRate = 24;
        public static string FfmpegPath = @"C:\Users\haban\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin\ffmpeg.exe";
        // Takes go outside the project, to the user's Downloads.
        public static string TakesFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads", "UN-CREDIBLES_Previs_Tomas");

        private sealed class Output
        {
            public RenderTexture Source;
            public Process Process;
            public Stream Input;
            public string File;
        }

        private readonly List<Output> outputs = new();
        private int framesLeft;
        private int pending;
        public static TrailerCapture Current { get; private set; }
        public static bool IsCapturing => Current != null;
        public int FramesLeft => framesLeft;

        // Starts capturing the given render textures (name -> texture) for the given seconds of game time.
        public static string Begin(string take, float seconds, IList<KeyValuePair<string, RenderTexture>> sources)
        {
            if (Current != null) return "Already capturing";
            string folder = TakesFolder;
            Directory.CreateDirectory(folder);
            var capture = new GameObject("Trailer capture").AddComponent<TrailerCapture>();
            capture.framesLeft = Mathf.RoundToInt(seconds * FrameRate);
            var files = new List<string>();
            foreach (var source in sources)
            {
                string file = Path.Combine(folder, take + "_" + source.Key + ".mp4");
                var texture = source.Value;
                var info = new ProcessStartInfo(FfmpegPath,
                    $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {texture.width}x{texture.height} -r {FrameRate} -i - " +
                    $"-vf vflip -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p -movflags +faststart \"{file}\"")
                {
                    UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true
                };
                var process = Process.Start(info);
                capture.outputs.Add(new Output { Source = texture, Process = process, Input = process.StandardInput.BaseStream, File = file });
                files.Add(file);
            }
            Time.captureFramerate = FrameRate;
            Current = capture;
            capture.StartCoroutine(capture.Run());
            return "Capturing " + string.Join(", ", files);
        }

        private IEnumerator Run()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (framesLeft > 0)
            {
                yield return endOfFrame;
                foreach (var output in outputs)
                {
                    var target = output;
                    pending++;
                    AsyncGPUReadback.Request(target.Source, 0, TextureFormat.RGBA32, request =>
                    {
                        pending--;
                        if (request.hasError || target.Input == null) return;
                        var data = request.GetData<byte>();
                        target.Input.Write(data.ToArray(), 0, data.Length);
                    });
                }
                framesLeft--;
            }
            while (pending > 0)
            {
                AsyncGPUReadback.WaitAllRequests();
                yield return null;
            }
            Finish();
        }

        private void Finish()
        {
            Time.captureFramerate = 0;
            foreach (var output in outputs)
            {
                output.Input?.Close();
                output.Input = null;
                output.Process?.WaitForExit(60000);
            }
            outputs.Clear();
            Current = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Current != this) return;
            Time.captureFramerate = 0;
            foreach (var output in outputs)
            {
                output.Input?.Close();
                output.Input = null;
            }
            Current = null;
        }
    }
}
#endif
