using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Nokta matris LED yazı (otobüs hat tabelası). 5x7 harfler, Türkçe harflerin noktası/çengeli üst ve alt satırda;
    /// her harf 9 satır (üst işaret, 7 satır gövde, alt çengel), gövde satırları iki kat yükseklikte çizilir: 16 nokta yükseklik.
    /// Bir nokta 4x4 piksel (yanan: kehribar, sönük: koyu).
    /// </summary>
    public static class LedYazi
    {
        public const int Yukseklik = 16;
        public const int NoktaPiksel = 4;
        // satır yükseklikleri (nokta): üst işaret 1, gövde 7 x 2, alt çengel 1
        private static readonly int[] SatirBoyu = { 1, 2, 2, 2, 2, 2, 2, 2, 1 };

        public static readonly Color32 Yanan = new Color32(255, 170, 20, 255);
        public static readonly Color32 Sonuk = new Color32(34, 20, 6, 255);
        public static readonly Color32 Bosluk = new Color32(8, 6, 4, 255);

        private static Dictionary<char, string[]> harfler;

        /// <summary>Metni nokta dizisine çizer (genişlik × 16, true = yanan). Harf arası 2 nokta (gövde iki kat genişlikte).</summary>
        public static bool[,] Noktalar(string metin)
        {
            Hazirla();
            var sutunlar = new List<bool[]>();
            foreach (char ham in metin)
            {
                char c = char.ToUpperInvariant(ham == 'i' ? 'İ' : ham == 'ı' ? 'I' : ham);
                if (!harfler.TryGetValue(c, out var satirlar))
                    satirlar = harfler['?'];
                int en = satirlar[1].Length;
                for (int x = 0; x < en; x++)
                {
                    // her sütun iki nokta genişliğinde
                    var sutun = new bool[Yukseklik];
                    int y = 0;
                    for (int r = 0; r < 9; r++)
                    {
                        bool yan = x < satirlar[r].Length && satirlar[r][x] == '#';
                        for (int k = 0; k < SatirBoyu[r]; k++)
                            sutun[y++] = yan;
                    }
                    sutunlar.Add(sutun);
                    sutunlar.Add(sutun);
                }
                sutunlar.Add(new bool[Yukseklik]);
                sutunlar.Add(new bool[Yukseklik]);
            }
            if (sutunlar.Count >= 2)
                sutunlar.RemoveRange(sutunlar.Count - 2, 2);
            var sonuc = new bool[sutunlar.Count, Yukseklik];
            for (int x = 0; x < sutunlar.Count; x++)
                for (int y = 0; y < Yukseklik; y++)
                    sonuc[x, y] = sutunlar[x][y];
            return sonuc;
        }

        /// <summary>Nokta dizisinin [bas, bas + genislik) penceresini piksellere yazar (üst satır y = 0).
        /// ortala: dizi pencereden darsa ortaya koyar.</summary>
        public static void Ciz(Color32[] piksel, int genislik, bool[,] noktalar, int bas, bool ortala)
        {
            int n = noktalar.GetLength(0);
            int kayma = ortala && n < genislik ? (genislik - n) / 2 : 0;
            int pw = genislik * NoktaPiksel;
            for (int x = 0; x < genislik; x++)
            {
                int sx = x - kayma + (ortala ? 0 : bas);
                for (int y = 0; y < Yukseklik; y++)
                {
                    bool yan = sx >= 0 && sx < n && noktalar[sx, y];
                    var renk = yan ? Yanan : Sonuk;
                    // doku satırları aşağıdan yukarı: üst satır en sonda
                    int py0 = (Yukseklik - 1 - y) * NoktaPiksel;
                    for (int dy = 0; dy < NoktaPiksel; dy++)
                        for (int dx = 0; dx < NoktaPiksel; dx++)
                            piksel[(py0 + dy) * pw + x * NoktaPiksel + dx] = dx == NoktaPiksel - 1 || dy == 0 ? Bosluk : renk;
                }
            }
        }

        private static void Hazirla()
        {
            if (harfler != null)
                return;
            harfler = new Dictionary<char, string[]>();
            // gövde (7 satır); üst işaret ve alt çengel ayrı
            void H(char c, string govde, string ust = "", string alt = "")
            {
                var g = govde.Split('|');
                int en = 0;
                foreach (var s in g) en = Mathf.Max(en, s.Length);
                var satir = new string[9];
                satir[0] = ust.PadRight(en);
                for (int i = 0; i < 7; i++) satir[i + 1] = g[i].PadRight(en);
                satir[8] = alt.PadRight(en);
                harfler[c] = satir;
            }
            H('A', ".###.|#...#|#...#|#####|#...#|#...#|#...#");
            H('B', "####.|#...#|#...#|####.|#...#|#...#|####.");
            H('C', ".###.|#...#|#....|#....|#....|#...#|.###.");
            H('Ç', ".###.|#...#|#....|#....|#....|#...#|.###.", alt: "..#..");
            H('D', "####.|#...#|#...#|#...#|#...#|#...#|####.");
            H('E', "#####|#....|#....|####.|#....|#....|#####");
            H('F', "#####|#....|#....|####.|#....|#....|#....");
            H('G', ".###.|#...#|#....|#.###|#...#|#...#|.####");
            H('Ğ', ".###.|#...#|#....|#.###|#...#|#...#|.####", ust: ".###.");
            H('H', "#...#|#...#|#...#|#####|#...#|#...#|#...#");
            H('I', "###|.#.|.#.|.#.|.#.|.#.|###");
            H('İ', "###|.#.|.#.|.#.|.#.|.#.|###", ust: ".#.");
            H('J', "..###|...#.|...#.|...#.|...#.|#..#.|.##..");
            H('K', "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#");
            H('L', "#....|#....|#....|#....|#....|#....|#####");
            H('M', "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#");
            H('N', "#...#|#...#|##..#|#.#.#|#..##|#...#|#...#");
            H('O', ".###.|#...#|#...#|#...#|#...#|#...#|.###.");
            H('Ö', ".###.|#...#|#...#|#...#|#...#|#...#|.###.", ust: ".#.#.");
            H('P', "####.|#...#|#...#|####.|#....|#....|#....");
            H('Q', ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#");
            H('R', "####.|#...#|#...#|####.|#.#..|#..#.|#...#");
            H('S', ".####|#....|#....|.###.|....#|....#|####.");
            H('Ş', ".####|#....|#....|.###.|....#|....#|####.", alt: "..#..");
            H('T', "#####|..#..|..#..|..#..|..#..|..#..|..#..");
            H('U', "#...#|#...#|#...#|#...#|#...#|#...#|.###.");
            H('Ü', "#...#|#...#|#...#|#...#|#...#|#...#|.###.", ust: ".#.#.");
            H('V', "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..");
            H('W', "#...#|#...#|#...#|#.#.#|#.#.#|##.##|#...#");
            H('X', "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#");
            H('Y', "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..");
            H('Z', "#####|....#|...#.|..#..|.#...|#....|#####");
            H('0', ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.");
            H('1', ".#.|##.|.#.|.#.|.#.|.#.|###");
            H('2', ".###.|#...#|....#|...#.|..#..|.#...|#####");
            H('3', "####.|....#|....#|.###.|....#|....#|####.");
            H('4', "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.");
            H('5', "#####|#....|####.|....#|....#|#...#|.###.");
            H('6', ".###.|#....|#....|####.|#...#|#...#|.###.");
            H('7', "#####|....#|...#.|..#..|.#...|.#...|.#...");
            H('8', ".###.|#...#|#...#|.###.|#...#|#...#|.###.");
            H('9', ".###.|#...#|#...#|.####|....#|....#|.###.");
            H(' ', "..|..|..|..|..|..|..");
            H('-', "...|...|...|###|...|...|...");
            H('.', ".|.|.|.|.|.|#");
            H(':', ".|.|#|.|#|.|.");
            H('/', "....#|...#.|...#.|..#..|.#...|.#...|#....");
            H('?', ".###.|#...#|....#|...#.|..#..|.....|..#..");
        }
    }
}
