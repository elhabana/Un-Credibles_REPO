using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnCredibles.Trailer
{
    // Films a minigame played by bots for the previs. The game camera stays as the wide angle (recorded
    // from the Game view) and extra angles ride along players, each rendered to its own texture so the
    // recorder captures all of them from the same match and the edit can cut between them.
    [DefaultExecutionOrder(1000)]
    public sealed class TrailerGameplay : MonoBehaviour
    {
        [Serializable]
        public sealed class Angle
        {
            public string name = "Follow";
            [Tooltip("Player index to ride along (sorted by name); -1 for a fixed camera.")]
            public int player = 0;
            [Tooltip("Offset from the player (follow) or world position (fixed).")]
            public Vector3 position = new(0f, 3f, -4f);
            [Tooltip("Fixed cameras only.")]
            public Vector3 euler;
            public Vector3 lookOffset = new(0f, .8f, 0f);
            public float fov = 40f;
            [Min(0f)] public float smoothing = .25f;
            [Tooltip("Follow: the offset turns with the player's heading (chase camera).")]
            public bool local;
            [Min(.01f), Tooltip("Chase camera: seconds to catch up with the player's turns.")]
            public float turnSmoothing = .6f;
            [Tooltip("Follow: degrees per second the offset orbits around the player.")]
            public float orbitSpeed;
            [Tooltip("Follow: keep the 'euler' orientation and only slide along with the player (calm, no turning).")]
            public bool lockRotation;
        }

        [Tooltip("Component type names on the game camera (or anywhere) to switch off, e.g. camera fitters.")]
        public string[] disableComponents = new string[0];
        [Tooltip("Overlay canvases to keep (name contains), e.g. the pool splash.")]
        public string[] keepCanvases = new string[0];
        [Tooltip("Component type name that marks a player avatar, e.g. CrossyRoadPlayer.")]
        public string playerType;
        public Angle[] angles = new Angle[0];
        public float exposure = 0f;
        public Color skyColour = new(.56f, .76f, .95f);
        [Tooltip("Editor: when above 0, captures every angle from the first frame for this many seconds.")]
        public float captureSeconds;
        public string captureTake = "Take";
        [Tooltip("Bool or int properties of the player component to log when they change (e.g. IsSlipping), to find the moments in the takes.")]
        public string[] watchProperties = new string[0];

        private readonly List<MonoBehaviour> playerComponents = new();
        private readonly Dictionary<string, string> watched = new();
        private readonly System.Text.StringBuilder eventLog = new();
        private float startTime;

        public readonly List<RenderTexture> Targets = new();
        private readonly List<Camera> cameras = new();
        private readonly List<Vector3> velocities = new();
        private readonly List<float> yaws = new();
        private readonly List<float> yawSpeeds = new();
        private readonly List<Transform> players = new();
        private Camera output;
        // Copy of the game camera rendered to a texture: the "Juego" (wide) angle of the capture.
        private Camera gameCopy;
        public RenderTexture GameTarget { get; private set; }

        private void Start()
        {
            output = Camera.main;
            startTime = Time.time;
            foreach (var typeName in disableComponents)
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (behaviour.GetType().Name == typeName) behaviour.enabled = false;
            var look = TrailerLook.Apply(output, exposure);
            look.keepCanvases.AddRange(keepCanvases);

            gameCopy = NewCamera("Juego", out var gameTarget);
            GameTarget = gameTarget;
            foreach (var angle in angles)
            {
                var cam = NewCamera(angle.name, out var target);
                // Closer angles see past the arena: a sky colour instead of the void.
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColour;
                cam.orthographic = false;
                cam.fieldOfView = angle.fov;
                if (angle.player < 0) cam.transform.SetPositionAndRotation(angle.position, Quaternion.Euler(angle.euler));
                Targets.Add(target);
                cameras.Add(cam);
                velocities.Add(Vector3.zero);
                yaws.Add(0f);
                yawSpeeds.Add(0f);
            }
#if UNITY_EDITOR
            Application.runInBackground = true;
            if (captureSeconds > 0f) Debug.Log("[Trailer] " + TrailerCapture.Begin(captureTake, captureSeconds, Sources()));
#endif
        }

        private Camera NewCamera(string angleName, out RenderTexture target)
        {
            target = new RenderTexture(1920, 1080, 24) { name = "Trailer " + angleName, antiAliasing = 1 };
            target.Create();
            var cam = new GameObject("Trailer camera " + angleName).AddComponent<Camera>();
            cam.CopyFrom(output);
            cam.nearClipPlane = .05f;
            cam.targetTexture = target;
            var data = cam.GetUniversalAdditionalCameraData();
            var source = output.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = source.antialiasing;
            data.antialiasingQuality = source.antialiasingQuality;
            return cam;
        }

        // Every angle for the capture, the game camera copy first.
        public List<KeyValuePair<string, RenderTexture>> Sources()
        {
            var sources = new List<KeyValuePair<string, RenderTexture>> { new("Juego", GameTarget) };
            for (int i = 0; i < angles.Length; i++) sources.Add(new(angles[i].name, Targets[i]));
            return sources;
        }

        // Overlay canvases that must be filmed (e.g. the pool splash) are moved onto the game copy camera.
        private void KeepCanvasesOnCapture()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                foreach (var keep in keepCanvases)
                    if (canvas.name.Contains(keep))
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = gameCopy;
                        canvas.planeDistance = gameCopy.nearClipPlane + .1f;
                    }
            }
        }

        private void FindPlayers()
        {
            if (players.Count > 0 || string.IsNullOrEmpty(playerType)) return;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour.GetType().Name == playerType) playerComponents.Add(behaviour);
            // Avatars are named after their slot ("..._Red_0"): order them by that trailing index.
            playerComponents.Sort((a, b) => a.name[^1].CompareTo(b.name[^1]));
            foreach (var component in playerComponents) players.Add(component.transform);
        }

        // Logs "time player property value" whenever a watched bool changes.
        private void WatchEvents()
        {
            if (watchProperties.Length == 0) return;
            float t = Time.time - startTime;
            for (int i = 0; i < playerComponents.Count; i++)
            {
                var component = playerComponents[i];
                if (component == null) continue;
                foreach (var property in watchProperties)
                {
                    var info = component.GetType().GetProperty(property);
                    if (info == null || (info.PropertyType != typeof(bool) && info.PropertyType != typeof(int))) continue;
                    string value = info.GetValue(component).ToString();
                    string key = i + property;
                    if (watched.TryGetValue(key, out string previous) && previous == value) continue;
                    if (watched.ContainsKey(key)) eventLog.AppendLine($"{t:0.00}\t{component.name}\t{property}\t{value}");
                    watched[key] = value;
                }
            }
        }

        private void LateUpdate()
        {
            gameCopy.transform.SetPositionAndRotation(output.transform.position, output.transform.rotation);
            gameCopy.orthographic = output.orthographic;
            gameCopy.orthographicSize = output.orthographicSize;
            gameCopy.fieldOfView = output.fieldOfView;
            if (keepCanvases.Length > 0) KeepCanvasesOnCapture();
            FindPlayers();
            WatchEvents();
            for (int i = 0; i < angles.Length; i++)
            {
                var angle = angles[i];
                if (angle.player < 0 || angle.player >= players.Count || players[angle.player] == null) continue;
                var player = players[angle.player];
                var target = player.position;
                var cam = cameras[i].transform;
                var offsetFromPlayer = angle.position;
                var lookOffset = angle.lookOffset;
                if (angle.local)
                {
                    float yaw = yaws[i], yawSpeed = yawSpeeds[i];
                    yaws[i] = Mathf.SmoothDampAngle(yaw, player.eulerAngles.y, ref yawSpeed, angle.turnSmoothing);
                    yawSpeeds[i] = yawSpeed;
                    var heading = Quaternion.Euler(0f, yaws[i], 0f);
                    offsetFromPlayer = heading * offsetFromPlayer;
                    lookOffset = heading * lookOffset;
                }
                if (angle.orbitSpeed != 0f)
                    offsetFromPlayer = Quaternion.Euler(0f, angle.orbitSpeed * (Time.time - startTime), 0f) * offsetFromPlayer;
                var wanted = target + offsetFromPlayer;
                var velocity = velocities[i];
                cam.position = angle.smoothing > 0f
                    ? Vector3.SmoothDamp(cam.position, wanted, ref velocity, angle.smoothing)
                    : wanted;
                velocities[i] = velocity;
                cam.rotation = angle.lockRotation
                    ? Quaternion.Euler(angle.euler)
                    : Quaternion.LookRotation(target + lookOffset - cam.position);
            }
        }

        private void OnDestroy()
        {
            foreach (var target in Targets)
                if (target != null) target.Release();
#if UNITY_EDITOR
            if (eventLog.Length > 0 && captureSeconds > 0f)
            {
                string file = System.IO.Path.Combine(TrailerCapture.TakesFolder, captureTake + "_events.txt");
                System.IO.File.WriteAllText(file, eventLog.ToString());
            }
#endif
        }
    }
}
