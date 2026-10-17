using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnCredibles.Minigames.GarageMice
{
    // Floating "+N" over a sweeper when a swing catches mice, with "COMBO xN" for multi-catches
    // and gold for the golden mouse. Pooled world-space texts built in code, ticked by the controller.
    public sealed class GarageMicePopups : MonoBehaviour
    {
        private const float Seconds = 1.2f;
        private const float Rise = 1.4f;
        private const float Height = 2.4f;
        private const float FontSize = 9f;

        private static readonly Color GainColor = new Color(.35f, 1f, .35f);
        private static readonly Color ComboColor = new Color(1f, .55f, .15f);
        private static readonly Color GoldenColor = new Color(1f, .82f, .15f);

        private sealed class Popup
        {
            public TextMeshPro Text;
            public Vector3 Origin;
            public Color Color;
            public float Age;
        }

        private readonly List<Popup> popups = new List<Popup>();
        private Transform viewCamera;

        public void Setup(Transform cameraTransform) => viewCamera = cameraTransform;

        public void Show(Vector3 position, int points, int combo, bool golden)
        {
            var popup = Take();
            popup.Origin = position + Vector3.up * Height;
            popup.Color = golden ? GoldenColor : combo > 1 ? ComboColor : GainColor;
            popup.Text.text = combo > 1 ? $"+{points}\n<size=55%>COMBO x{combo}</size>" : $"+{points}";
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
                float pop = t < .12f ? Mathf.Lerp(.3f, 1.35f, t / .12f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((t - .12f) * 4f));
                var textTransform = popup.Text.transform;
                textTransform.SetPositionAndRotation(popup.Origin + Vector3.up * (Rise * (1f - (1f - t) * (1f - t))), facing);
                textTransform.localScale = Vector3.one * pop;
                var color = popup.Color;
                color.a = 1f - Mathf.Clamp01((t - .65f) / .35f);
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
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = .25f;
            text.outlineColor = Color.black;
            text.rectTransform.sizeDelta = new Vector2(6f, 4f);
            var created = new Popup { Text = text };
            popups.Add(created);
            return created;
        }
    }
}
