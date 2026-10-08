using UnityEngine;

namespace AnkaraBus.Gameplay
{
    /// <summary>
    /// Bir hat sahnesinin akşam ya da gece için bake edilmiş ışığı: lightmap'ler ve ışık probları.
    /// Editor/HavaZamanKurucu üretir: Resources/IsikSetleri/{sahne}_{Aksam|Gece}.asset.
    /// Gündüz sahnenin kendi bake'idir. Lightmap yerleşimi aynı sahneden geldiği için renderer'ların
    /// lightmap indeksleri değişmeden geçerli kalır.
    /// </summary>
    public class IsikSeti : ScriptableObject
    {
        public Texture2D[] renk;
        public Texture2D[] yon;
        [Tooltip("Işık probları: prob başına 27 sayı (SphericalHarmonicsL2, renk × katsayı).")]
        public float[] problar;

        public static string Yol(string sahne, OyunSecimi.Zaman zaman) => $"IsikSetleri/{sahne}_{zaman}";

        public void Uygula()
        {
            var maps = new LightmapData[renk.Length];
            for (int i = 0; i < renk.Length; i++)
                maps[i] = new LightmapData { lightmapColor = renk[i], lightmapDir = yon != null && i < yon.Length ? yon[i] : null };
            LightmapSettings.lightmaps = maps;

            var probes = LightmapSettings.lightProbes;
            if (probes == null || problar == null || problar.Length != probes.count * 27)
                return;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2[probes.count];
            for (int p = 0; p < sh.Length; p++)
                for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++)
                        sh[p][c, k] = problar[p * 27 + c * 9 + k];
            // sahnenin prob nesnesine dokunma (Editor'de varlığa yazılmasın): kopyası
            var kopya = Object.Instantiate(probes);
            kopya.bakedProbes = sh;
            LightmapSettings.lightProbes = kopya;
        }

        public static float[] ProblariOku()
        {
            var probes = LightmapSettings.lightProbes;
            if (probes == null)
                return null;
            var sh = probes.bakedProbes;
            var sonuc = new float[sh.Length * 27];
            for (int p = 0; p < sh.Length; p++)
                for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++)
                        sonuc[p * 27 + c * 9 + k] = sh[p][c, k];
            return sonuc;
        }
    }
}
