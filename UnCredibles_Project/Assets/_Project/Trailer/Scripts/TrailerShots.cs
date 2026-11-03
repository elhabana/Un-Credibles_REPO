using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // One camera shot of the trailer previs: a Cinemachine camera that travels from one pose to another
    // (or rides along a target) for a fixed time. Shots are played back to back with hard cuts.
    public sealed class TrailerShot
    {
        public string Name;
        public float Duration;
        public Vector3 FromPosition, ToPosition;
        public Vector3 FromEuler, ToEuler;
        public float Fov = 40f;
        public float ToFov = -1f;
        // Optional: the camera rides along this target with an offset (FromPosition/ToPosition become offsets).
        public Transform Follow;
        // Optional: the camera keeps this target framed (Euler poses are then ignored).
        public Transform LookAt;
        public Vector3 LookAtOffset;
        // Called once when the shot starts, to stage the action.
        public System.Action OnStart;
        internal CinemachineCamera Camera;
    }

    // Plays a list of shots on the main camera through a Cinemachine Brain, starting when the scene starts.
    public sealed class TrailerShots : MonoBehaviour
    {
        private readonly List<TrailerShot> shots = new();
        private float startTime;
        private int current = -1;
        public float Elapsed => Time.time - startTime;
        public bool Finished { get; private set; }
        public float TotalDuration { get; private set; }

        public static TrailerShots Create(Camera output)
        {
            var brain = output.GetComponent<CinemachineBrain>();
            if (brain == null) brain = output.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var go = new GameObject("Trailer shots");
            return go.AddComponent<TrailerShots>();
        }

        public TrailerShot Add(TrailerShot shot)
        {
            var go = new GameObject("CM " + shot.Name);
            go.transform.SetParent(transform, false);
            shot.Camera = go.AddComponent<CinemachineCamera>();
            shot.Camera.Lens.FieldOfView = shot.Fov;
            shot.Camera.Lens.NearClipPlane = .05f;
            shot.Camera.Priority = 0;
            if (shot.ToFov < 0f) shot.ToFov = shot.Fov;
            shots.Add(shot);
            TotalDuration += shot.Duration;
            return shot;
        }

        private void Start()
        {
            startTime = Time.time;
        }

        // Jumps the sequence to a given time, to review framing without waiting.
        public void Seek(float seconds)
        {
            startTime = Time.time - seconds;
        }

        private void LateUpdate()
        {
            if (shots.Count == 0) return;
            float t = Elapsed;
            int index = 0;
            float shotStart = 0f;
            while (index < shots.Count - 1 && t >= shotStart + shots[index].Duration)
            {
                shotStart += shots[index].Duration;
                index++;
            }
            Finished = t >= TotalDuration;
            var shot = shots[index];
            if (index != current)
            {
                if (current >= 0) shots[current].Camera.Priority = 0;
                current = index;
                shot.Camera.Priority = 100;
                shot.OnStart?.Invoke();
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - shotStart) / shot.Duration));
            Pose(shot, k);
        }

        private static void Pose(TrailerShot shot, float k)
        {
            var position = Vector3.Lerp(shot.FromPosition, shot.ToPosition, k);
            if (shot.Follow != null) position += shot.Follow.position;
            Quaternion rotation;
            if (shot.LookAt != null)
                rotation = Quaternion.LookRotation(shot.LookAt.position + shot.LookAtOffset - position);
            else
                rotation = Quaternion.Slerp(Quaternion.Euler(shot.FromEuler), Quaternion.Euler(shot.ToEuler), k);
            shot.Camera.transform.SetPositionAndRotation(position, rotation);
            shot.Camera.Lens.FieldOfView = Mathf.Lerp(shot.Fov, shot.ToFov, k);
        }
    }
}
