using UnityEngine;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Arayüz için kodla üretilen basit şekiller (daire, direksiyon simidi). Görsel asset gerektirmez.
    /// </summary>
    public static class UiShapes
    {
        private static Sprite circle;
        private static Sprite roundedRect;
        private static Sprite steeringWheel;

        public static Sprite Circle => circle ??= Make(128, (x, y, r) => Mathf.Clamp01(r * 64f - Mathf.Sqrt(x * x + y * y) * 64f));

        public static Sprite RoundedRect
        {
            get
            {
                if (roundedRect != null)
                    return roundedRect;
                const int size = 64;
                const float radius = 0.35f;
                roundedRect = Make(size, (x, y, r) =>
                {
                    float dx = Mathf.Max(Mathf.Abs(x) - (1f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y) - (1f - radius), 0f);
                    return Mathf.Clamp01((radius - Mathf.Sqrt(dx * dx + dy * dy)) * size * 0.5f);
                }, new Vector4(24, 24, 24, 24));
                return roundedRect;
            }
        }

        /// <summary>Jant, göbek ve üç kollu direksiyon simidi.</summary>
        public static Sprite SteeringWheel => steeringWheel ??= Make(256, (x, y, r) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float px = 128f;
            float rim = Mathf.Clamp01((0.1f - Mathf.Abs(d - 0.86f)) * px);
            float hub = Mathf.Clamp01((0.22f - d) * px);
            float spokes = 0f;
            if (d < 0.86f)
            {
                // Sola, sağa ve aşağı kol
                spokes = Mathf.Max(spokes, Mathf.Clamp01((0.06f - Mathf.Abs(y)) * px));
                if (y < 0f)
                    spokes = Mathf.Max(spokes, Mathf.Clamp01((0.06f - Mathf.Abs(x)) * px));
            }
            return Mathf.Max(rim, Mathf.Max(hub, spokes));
        });

        private static Sprite Make(int size, System.Func<float, float, float, float> alpha, Vector4 border = default)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f;
                float y = (j + 0.5f) / size * 2f - 1f;
                byte a = (byte)(alpha(x, y, 1f) * 255f);
                pixels[j * size + i] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
