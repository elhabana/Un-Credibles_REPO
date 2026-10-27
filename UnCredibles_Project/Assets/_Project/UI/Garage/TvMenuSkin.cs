using TMPro;
using UnityEngine;

namespace UnCredibles.UI.Garage
{
    // Art of the settings menu on the garage TV. White sprites are tinted by TvSettingsOsd;
    // the slider fill and knob keep their own colours.
    [CreateAssetMenu(fileName = "SO_TvMenuSkin", menuName = "UnCredibles/Garage/TV Menu Skin")]
    public sealed class TvMenuSkin : ScriptableObject
    {
        public TMP_FontAsset titleFont;

        [Header("Shapes (9-sliced)")]
        public Sprite round;
        public Sprite roundOutline;
        public Sprite tile;
        public Sprite fill;
        public Sprite knob;
        public Sprite brush;

        [Header("Icons")]
        public Sprite general;
        public Sprite music;
        public Sprite effects;
        public Sprite language;
        public Sprite back;
        public Sprite previous;
        public Sprite next;

        [Header("Doodles")]
        public Sprite crown;
        public Sprite star;
    }
}
