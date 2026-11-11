using UnCredibles.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Comic panel behind the home menu: a sheet in this graphic's colour with a halftone dot screen, glued to
    // the left of its rect, whose right edge is a diagonal inked with a thick line and a hard shadow. The dots
    // come from a small repeating texture; the line and the shadow sample a blank texel of it. Static mesh.
    public sealed class HalftonePanel : MaskableGraphic
    {
        private const int DotTexture = 32;
        private const float DotRadius = 9f;    // texels; centre + corner dots make a 45 degree grid when tiled
        private const float DotShade = .82f;   // a dot is the fill colour times this
        private const float Feather = 1.5f;    // soft edge so the diagonal does not stair-step
        private static readonly Vector2 BlankUv = new Vector2(8.5f / DotTexture, 8.5f / DotTexture);
        private static readonly Color ShadowColor = new Color(.114f, .114f, .106f, .45f);
        private static Texture2D dots;

        [SerializeField] private float topEdge = 290f;    // x of the diagonal at the top, from the rect's left
        [SerializeField] private float bottomEdge = 440f; // x of the diagonal at the bottom
        [SerializeField] private float dotSpacing = 20f;  // canvas units per texture tile
        [SerializeField] private float inkWidth = 6f;
        [SerializeField] private float shadowWidth = 12f;
        [SerializeField] private float bleed = 200f;      // drawn past the left edge: the intro slide overshoots

        // Unity null check: rebuilt if something destroyed it.
        public override Texture mainTexture => dots != null ? dots : dots = BuildDots();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float left = r.xMin - bleed;
            float top = r.xMin + topEdge - inkWidth * .5f;       // inner side of the ink line
            float bottom = r.xMin + bottomEdge - inkWidth * .5f;
            Color ink = PaperUI.Ink;
            var clearInk = new Color(ink.r, ink.g, ink.b, 0f);
            var clearShadow = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0f);

            Quad(vh, r, left, left, top, bottom, color, color, true);
            Quad(vh, r, top - Feather, bottom - Feather, top, bottom, clearInk, ink, false);
            Quad(vh, r, top, bottom, top + inkWidth, bottom + inkWidth, ink, ink, false);
            top += inkWidth;
            bottom += inkWidth;
            Quad(vh, r, top, bottom, top + shadowWidth, bottom + shadowWidth, ShadowColor, ShadowColor, false);
            top += shadowWidth;
            bottom += shadowWidth;
            Quad(vh, r, top, bottom, top + Feather, bottom + Feather, ShadowColor, clearShadow, false);
        }

        // Full-height band between two slanted lines (x at the top, x at the bottom of each).
        private void Quad(VertexHelper vh, Rect r, float leftTop, float leftBottom, float rightTop, float rightBottom,
            Color leftColor, Color rightColor, bool tiled)
        {
            int start = vh.currentVertCount;
            Vertex(vh, leftBottom, r.yMin, leftColor, tiled);
            Vertex(vh, leftTop, r.yMax, leftColor, tiled);
            Vertex(vh, rightTop, r.yMax, rightColor, tiled);
            Vertex(vh, rightBottom, r.yMin, rightColor, tiled);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private void Vertex(VertexHelper vh, float x, float y, Color tint, bool tiled) =>
            vh.AddVert(new Vector3(x, y), tint, tiled ? new Vector2(x, y) / dotSpacing : BlankUv);

        // White tile with a grey dot in the centre and a quarter dot in each corner, antialiased over one texel.
        private static Texture2D BuildDots()
        {
            var texture = new Texture2D(DotTexture, DotTexture, TextureFormat.RGBA32, false)
            {
                name = "Halftone dots",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[DotTexture * DotTexture];
            float half = DotTexture * .5f;
            for (int y = 0; y < DotTexture; y++)
            for (int x = 0; x < DotTexture; x++)
            {
                float px = x + .5f, py = y + .5f;
                float centre = new Vector2(px - half, py - half).magnitude;
                float corner = new Vector2(Mathf.Min(px, DotTexture - px), Mathf.Min(py, DotTexture - py)).magnitude;
                float v = Mathf.Lerp(DotShade, 1f, Mathf.Clamp01(Mathf.Min(centre, corner) - DotRadius + .5f));
                pixels[y * DotTexture + x] = new Color(v, v, v, 1f);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
