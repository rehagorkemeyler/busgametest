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
        private static Sprite star;
        private static Sprite arrow;

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

        /// <summary>Beş köşeli yıldız (sefer özeti).</summary>
        public static Sprite Star => star ??= Make(128, (x, y, r) =>
        {
            const float outer = 0.95f, inner = 0.4f;
            float sector = Mathf.PI * 2f / 5f;
            float t = Mathf.Repeat(Mathf.Atan2(x, y), sector);
            if (t > sector * 0.5f)
                t = sector - t;
            float d = Mathf.Sqrt(x * x + y * y);
            var p = new Vector2(d * Mathf.Cos(t), d * Mathf.Sin(t));
            var o = new Vector2(outer, 0f);
            var i = new Vector2(inner * Mathf.Cos(sector * 0.5f), inner * Mathf.Sin(sector * 0.5f));
            var u = (i - o).normalized;
            var n = new Vector2(-u.y, u.x);
            if (Vector2.Dot(-o, n) < 0f)
                n = -n; // içe (merkeze) baksın
            return Mathf.Clamp01(Vector2.Dot(p - o, n) * 64f);
        });

        /// <summary>Yukarı bakan ok ucu (haritada otobüs).</summary>
        public static Sprite Arrow => arrow ??= Make(64, (x, y, r) =>
        {
            // ok: uç (0, 0.9), arka köşeler (±0.7, -0.75), arkada içe çentik (0, -0.35)
            float px = 32f;
            float sag = (0.9f - y) * 0.7f / 1.65f - Mathf.Abs(x);   // yan kenarlar
            float govde = Mathf.Min(sag, y + 0.75f);
            float centik = y - (-0.75f + 0.4f * (1f - Mathf.Clamp01(Mathf.Abs(x) / 0.7f)));
            return Mathf.Clamp01(Mathf.Min(govde, centik) * px);
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
