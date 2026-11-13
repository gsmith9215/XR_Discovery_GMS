using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    /// <summary>Shared nine-slice shapes for the runtime world-space menu.</summary>
    internal static class RoundedUiSprite
    {
        private static Sprite button;
        private static Sprite window;
        private static Sprite capsule;

        public static void Apply(UnityEngine.UI.Image image, bool largeCorners = false)
        {
            if (largeCorners)
                image.sprite = window != null ? window : window = Create(96, 32, 38);
            else
                image.sprite = button != null ? button : button = Create(64, 16, 20);
            image.type = UnityEngine.UI.Image.Type.Sliced;
        }

        public static void ApplyCapsule(UnityEngine.UI.Image image)
        {
            image.sprite = capsule != null ? capsule : capsule = CreateCapsule();
            image.type = UnityEngine.UI.Image.Type.Sliced;
        }

        private static Sprite CreateCapsule()
        {
            const int width = 64;
            const int height = 80;
            const float horizontalRadius = width / 2f;
            const float capHeight = 24f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Capsule UI shape",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            float centerX = (width - 1) * .5f;
            float centerY = (height - 1) * .5f;
            float straight = height * .5f - capHeight;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - centerX;
                    float dy = Mathf.Max(Mathf.Abs(y - centerY) - straight, 0);
                    float curve = Mathf.Sqrt(dx * dx / (horizontalRadius * horizontalRadius) +
                        dy * dy / (capHeight * capHeight));
                    byte alpha = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01((1f - curve) * horizontalRadius + .5f));
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(horizontalRadius, capHeight, horizontalRadius, capHeight));
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
