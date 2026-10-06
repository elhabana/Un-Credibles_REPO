using UnityEngine;

namespace UnCredibles.BatPad
{
    // One pixel per module plus the white quiet zone phones need to detect the code.
    // Point filtering keeps the modules sharp when a RawImage scales it up.
    public static class QrTexture
    {
        private const int QuietZone = 4;

        public static Texture2D Create(string text)
        {
            bool[,] modules = QrCode.Encode(text);
            int count = modules.GetLength(0);
            int size = count + QuietZone * 2;

            var pixels = new Color32[size * size];
            var light = new Color32(255, 255, 255, 255);
            var dark = new Color32(0, 0, 0, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = light;

            for (int y = 0; y < count; y++)
            {
                // Texture rows start at the bottom, QR rows at the top.
                int row = size - 1 - (y + QuietZone);
                for (int x = 0; x < count; x++)
                    if (modules[y, x]) pixels[row * size + x + QuietZone] = dark;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BatPad QR",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
