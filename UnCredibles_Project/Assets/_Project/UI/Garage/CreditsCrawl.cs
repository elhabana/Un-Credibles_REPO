using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Credits that scroll away into the distance on a tilted plane, space-opera style. The canvas is
    // screen-space overlay (no perspective), so the text mesh is projected by hand every frame:
    // the bottom line is full size and the text shrinks and fades towards a horizon near the top.
    public sealed class CreditsCrawl : MonoBehaviour
    {
        private const string Credits =
            "<size=150%>UN-CREDIBLES</size>\n\n\n" +
            "Cristian Rivero Llacer (Habana)\n\n" +
            "Fausto\n\n" +
            "Lucia Llanes\n\n" +
            "Mario Marquina\n\n\n\n" +
            "<size=55%>SFX: juniorsoundays - Pixabay</size>";
        private static readonly Color Yellow = new Color(1f, .84f, .2f);
        private const float FontSize = 72f;
        private const float Width = 1100f;
        // Distance of the eye from the plane, in canvas units: lower tilts it more.
        private const float Depth = 700f;
        // Horizon height as a share of the screen.
        private const float Horizon = .9f;
        private const float Speed = 70f;

        private TextMeshProUGUI text;
        private RectTransform area;
        private float scroll;

        // Replaces the placeholder content of the credits panel; its VOLVER button stays on top.
        public static void Build(RectTransform panel, TMP_FontAsset font)
        {
            foreach (Transform child in panel)
                if (child.GetComponentInChildren<Button>(true) == null) child.gameObject.SetActive(false);

            var backdrop = new GameObject("Credits backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backdrop.transform.SetParent(panel, false);
            backdrop.transform.SetAsFirstSibling();
            backdrop.color = new Color(0f, 0f, 0f, .88f);
            backdrop.raycastTarget = false;
            Stretch(backdrop.rectTransform);

            var crawl = new GameObject("Credits crawl", typeof(RectTransform)).AddComponent<CreditsCrawl>();
            crawl.area = (RectTransform)crawl.transform;
            crawl.area.SetParent(panel, false);
            crawl.area.SetSiblingIndex(1);
            Stretch(crawl.area);

            var text = new GameObject("Credits text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(crawl.area, false);
            if (font != null) text.font = font;
            text.text = Credits;
            text.fontSize = FontSize;
            text.color = Yellow;
            text.alignment = TextAlignmentOptions.Top;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            // The top of the text starts on the bottom edge of the screen; its local origin is there too.
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Width, text.GetPreferredValues(Credits, Width, 0f).y);
            crawl.text = text;
        }

        private void OnEnable() => scroll = 0f;

        private void LateUpdate()
        {
            if (text == null) return;
            scroll += Speed * Time.unscaledDeltaTime;
            // Start again once the last line has gone far away.
            if (scroll > text.rectTransform.sizeDelta.y + Depth * 8f) scroll = 0f;

            text.ForceMeshUpdate();
            float horizon = area.rect.height * Horizon;
            var info = text.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var vertices = info.meshInfo[m].vertices;
                var colors = info.meshInfo[m].colors32;
                for (int i = 0; i < info.meshInfo[m].vertexCount; i++)
                {
                    // Distance along the plane from the bottom edge; below the edge the line is still to come.
                    float along = Mathf.Max(vertices[i].y + scroll, -Depth * .8f);
                    float perspective = Depth / (Depth + along);
                    vertices[i] = new Vector3(vertices[i].x * perspective, horizon * (1f - perspective), 0f);
                    colors[i].a = (byte)(colors[i].a * Mathf.SmoothStep(0f, 1f, (perspective - .08f) / .3f));
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
