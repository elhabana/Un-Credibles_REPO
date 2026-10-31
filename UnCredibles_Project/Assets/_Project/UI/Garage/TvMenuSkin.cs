using TMPro;
using UnityEngine;

namespace UnCredibles.UI.Garage
{
    // Pixel art of the settings menu on the garage TV. The sprites keep their own colours and are
    // imported with point filtering; TvSettingsOsd lays them out at a fixed size per art pixel.
    [CreateAssetMenu(fileName = "SO_TvMenuSkin", menuName = "UnCredibles/Garage/TV Menu Skin")]
    public sealed class TvMenuSkin : ScriptableObject
    {
        [Tooltip("Pixel font (TTF with font data included). Its TMP asset is made at runtime.")]
        public Font pixelFont;

        [Header("Shapes (9-sliced)")]
        public Sprite panel;
        public Sprite tile;
        public Sprite select;
        public Sprite track;
        public Sprite fill;
        public Sprite well;
        public Sprite knob;

        [Header("Icons")]
        public Sprite general;
        public Sprite music;
        public Sprite effects;
        public Sprite language;
        public Sprite back;
        public Sprite previous;
        public Sprite next;

        [Header("Scenery")]
        public Sprite cloud;
        public Sprite hill;
        public Sprite bush;
        [Tooltip("Tiled along the bottom of the screen.")]
        public Sprite ground;
        public Sprite mouse;

        private TMP_FontAsset fontAsset;

        public TMP_FontAsset Font
        {
            get
            {
                if (fontAsset == null && pixelFont != null) fontAsset = TMP_FontAsset.CreateFontAsset(pixelFont);
                return fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            }
        }
    }
}
