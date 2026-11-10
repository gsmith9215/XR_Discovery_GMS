using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    /// <summary>Shared nine-slice shapes for the runtime world-space menu.</summary>
    internal static class RoundedUiSprite
    {
        private static Sprite button;
        private static Sprite window;

        public static void Apply(UnityEngine.UI.Image image, bool largeCorners = false)
        {
            if (largeCorners)
                image.sprite = window != null ? window : window = Create(96, 32, 38);
            else
                image.sprite = button != null ? button : button = Create(64, 16, 20);
            image.type = UnityEngine.UI.Image.Type.Sliced;
        }

        private static Sprite Create(int size, float radius, int border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Rounded UI shape",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * .5f;
            float straight = size * .5f - radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - center) - straight, 0);
                    float dy = Mathf.Max(Mathf.Abs(y - center) - straight, 0);
                    byte alpha = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
