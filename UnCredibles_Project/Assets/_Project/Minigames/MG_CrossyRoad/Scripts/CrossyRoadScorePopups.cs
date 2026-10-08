using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Big floating numbers over a player every time their score changes: green "+50" for a
    // saved grandma, gold "+100" in the final rush, red "-15" / "-25" when run over.
    // Pooled world-space texts built in code, always facing the camera. Ticked by the controller.
    public sealed class CrossyRoadScorePopups : MonoBehaviour
    {
        private const float Seconds = 1.3f;
        private const float Rise = 1.6f;
        private const float Height = 2.2f;

        [SerializeField, Tooltip("Camera the texts turn to face.")] private Transform viewCamera;
        [SerializeField, Min(1f)] private float fontSize = 10f;
        [SerializeField] private Color gainColor = new Color(0.35f, 1f, 0.35f);
        [SerializeField] private Color bonusColor = new Color(1f, 0.82f, 0.15f);
        [SerializeField] private Color lossColor = new Color(1f, 0.25f, 0.2f);

        private sealed class Popup
        {
            public TextMeshPro Text;
            public Vector3 Origin;
            public Color Color;
            public float Age;
        }

        private readonly List<Popup> popups = new List<Popup>();

        public void Show(Vector3 position, int points, bool bonus)
        {
            var popup = Take();
            popup.Origin = position + Vector3.up * Height;
            popup.Color = points < 0 ? lossColor : bonus ? bonusColor : gainColor;
            popup.Text.text = points > 0
                ? (bonus ? $"+{points}\n<size=55%>x2</size>" : $"+{points}")
                : points.ToString();
        }

        public void Tick(float deltaTime)
        {
            var facing = viewCamera != null ? viewCamera.rotation : Quaternion.identity;
            foreach (var popup in popups)
            {
                if (!popup.Text.gameObject.activeSelf) continue;
                popup.Age += deltaTime;
                float t = popup.Age / Seconds;
                if (t >= 1f)
                {
                    popup.Text.gameObject.SetActive(false);
                    continue;
                }

                // Pops in, floats up and fades out at the end.
                float pop = t < 0.12f ? Mathf.Lerp(0.3f, 1.35f, t / 0.12f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((t - 0.12f) * 4f));
                var transformText = popup.Text.transform;
                transformText.SetPositionAndRotation(popup.Origin + Vector3.up * (Rise * (1f - (1f - t) * (1f - t))), facing);
                transformText.localScale = Vector3.one * pop;
                var color = popup.Color;
                color.a = 1f - Mathf.Clamp01((t - 0.65f) / 0.35f);
                popup.Text.color = color;
            }
        }

        private Popup Take()
        {
            foreach (var popup in popups)
            {
                if (popup.Text.gameObject.activeSelf) continue;
                popup.Age = 0f;
                popup.Text.gameObject.SetActive(true);
                return popup;
            }

            var go = new GameObject("ScorePopup");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.rectTransform.sizeDelta = new Vector2(6f, 4f);
            var created = new Popup { Text = text };
            popups.Add(created);
            return created;
        }
    }
}
