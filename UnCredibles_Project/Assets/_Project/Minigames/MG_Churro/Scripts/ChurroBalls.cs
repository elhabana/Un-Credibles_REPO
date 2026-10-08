using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Beach balls thrown from outside the pool at one player, at head height: duck to let them
    // fly over. A red "!" over the target warns from the moment of the throw. The controller only
    // throws when the timing is fair (see ChurroController); this class flies them and reports hits.
    public sealed class ChurroBalls : MonoBehaviour
    {
        private const float PastDistance = 2.5f; // keeps flying this far after the target, then pops
        private const float PopSeconds = 0.15f;

        [SerializeField] private Transform ballPrefab;
        [SerializeField, Tooltip("Camera the warning marks turn to face.")] private Transform viewCamera;
        [SerializeField] private Color warningColor = new Color(1f, 0.2f, 0.15f);

        private sealed class Ball
        {
            public Transform Transform;
            public TextMeshPro Warning;
            public ChurroPlayer Target;
            public Vector3 From;
            public Vector3 Through;   // where it passes the target
            public float Flight;      // seconds from the throw to the target
            public float Age;
            public bool Resolved;
            public Vector3 SpinAxis;
            public bool Active;
        }

        private readonly List<Ball> balls = new List<Ball>();
        private ChurroSettings settings;
        private Vector3 center;

        public void Initialize(ChurroSettings churroSettings, Vector3 poolCenter)
        {
            settings = churroSettings;
            center = poolCenter;
        }

        // Thrown from beyond the target's float, flying towards the centre over its head.
        public void Throw(ChurroPlayer target, float flightSeconds)
        {
            var ball = Take();
            var outward = target.transform.position - center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector3.forward;

            ball.Target = target;
            ball.Through = target.FloatPosition + Vector3.up * settings.BallHeight;
            ball.From = ball.Through + outward * settings.BallThrowDistance + Vector3.up * 1.2f;
            ball.Flight = Mathf.Max(0.2f, flightSeconds);
            ball.Age = 0f;
            ball.Resolved = false;
            ball.SpinAxis = Vector3.Cross(Vector3.up, -outward);
            ball.Transform.position = ball.From;
            ball.Transform.localScale = Vector3.one * (settings.BallRadius * 2f);
            ball.Transform.gameObject.SetActive(true);
            ball.Warning.gameObject.SetActive(true);
        }

        // Seconds until the next ball reaches this player (for the AI), or MaxValue.
        public float TimeUntilHit(ChurroPlayer player)
        {
            float best = float.MaxValue;
            foreach (var ball in balls)
                if (ball.Active && !ball.Resolved && ball.Target == player)
                    best = Mathf.Min(best, ball.Flight - ball.Age);
            return best;
        }

        public bool IsTargeted(ChurroPlayer player) => TimeUntilHit(player) < float.MaxValue;

        // Moves every ball. Players hit this frame (standing when the ball reached them) are added
        // to `hits`; pass null on clients, where the host decides who falls.
        public void Tick(float deltaTime, List<ChurroPlayer> hits)
        {
            var facing = viewCamera != null ? viewCamera.rotation : Quaternion.identity;
            foreach (var ball in balls)
            {
                if (!ball.Active) continue;
                ball.Age += deltaTime;
                float t = ball.Age / ball.Flight;

                if (!ball.Resolved && t >= 1f)
                {
                    ball.Resolved = true;
                    ball.Warning.gameObject.SetActive(false);
                    if (hits != null && ball.Target.IsIn && !ball.Target.IsDucking) hits.Add(ball.Target);
                }

                // Gentle lob down to head height, then straight on towards the centre.
                var direction = (ball.Through - ball.From);
                direction.y = 0f;
                var position = Vector3.Lerp(ball.From, ball.Through, Mathf.Min(t, 1f));
                position.y += Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 0.6f;
                if (t > 1f) position = ball.Through + direction.normalized * ((t - 1f) * direction.magnitude);
                ball.Transform.position = position;
                ball.Transform.Rotate(ball.SpinAxis, 540f * deltaTime, Space.World);

                float pastSeconds = PastDistance / Mathf.Max(0.1f, direction.magnitude / ball.Flight);
                float end = 1f + pastSeconds / ball.Flight;
                if (t >= end)
                {
                    float pop = 1f - Mathf.Clamp01((t - end) * ball.Flight / PopSeconds);
                    ball.Transform.localScale = Vector3.one * (settings.BallRadius * 2f * pop);
                    if (pop <= 0f) Release(ball);
                }

                if (!ball.Resolved)
                {
                    float pulse = 1f + Mathf.Abs(Mathf.Sin(ball.Age * 14f)) * 0.3f;
                    ball.Warning.transform.SetPositionAndRotation(ball.Target.FloatPosition + Vector3.up * 2.2f, facing);
                    ball.Warning.transform.localScale = Vector3.one * pulse;
                }
            }
        }

        public void Clear()
        {
            foreach (var ball in balls)
                if (ball.Active) Release(ball);
        }

        private void Release(Ball ball)
        {
            ball.Active = false;
            ball.Transform.gameObject.SetActive(false);
            ball.Warning.gameObject.SetActive(false);
        }

        private Ball Take()
        {
            foreach (var ball in balls)
            {
                if (ball.Active) continue;
                ball.Active = true;
                return ball;
            }

            var created = new Ball { Transform = Instantiate(ballPrefab, transform), Warning = CreateWarning(), Active = true };
            balls.Add(created);
            return created;
        }

        private TextMeshPro CreateWarning()
        {
            var go = new GameObject("BallWarning");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.text = "!";
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = warningColor;
            text.outlineWidth = 0.3f;
            text.outlineColor = Color.black;
            text.rectTransform.sizeDelta = new Vector2(2f, 3f);
            go.SetActive(false);
            return text;
        }
    }
}
