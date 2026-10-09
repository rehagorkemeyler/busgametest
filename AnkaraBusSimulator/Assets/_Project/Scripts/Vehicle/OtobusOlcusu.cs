using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüsün boyları, gövde çarpışma kutularından ("Carpisma_*" BoxCollider'lar) ölçülür; 12 m'lik BMC'ye göre
    /// sabit sayılar yerine kullanılır (puanlamada ön uç, yaya çarpması, kapı kamerası, otomatik pilot).
    /// Körüklü otobüste arka gövdenin kutusu ayrı bir Rigidbody'dedir (KorukluOtobus) ve o da sayılır.
    /// </summary>
    public readonly struct OtobusOlcusu
    {
        /// <summary>Kökten ön uca uzaklık (m, kökün ileri ekseni boyunca).</summary>
        public readonly float On;
        /// <summary>Kökten arka uca uzaklık (pozitif m; körüklüde düz dururkenki).</summary>
        public readonly float Arka;
        public readonly float YariGenislik;
        public readonly float Yukseklik;
        public readonly BoxCollider[] Kutular;

        public float Uzunluk => On + Arka;

        private OtobusOlcusu(float on, float arka, float yariGenislik, float yukseklik, BoxCollider[] kutular)
        {
            On = on;
            Arka = arka;
            YariGenislik = yariGenislik;
            Yukseklik = yukseklik;
            Kutular = kutular;
        }

        /// <summary>BMC Procity 12LF (eski sabit değerler); kutu bulunamazsa.</summary>
        public static readonly OtobusOlcusu Varsayilan = new OtobusOlcusu(5.9f, 6.1f, 1.25f, 3.2f, new BoxCollider[0]);

        public static OtobusOlcusu Olc(Component otobus)
        {
            if (otobus == null)
                return Varsayilan;
            var kok = otobus.transform;
            var kutular = new List<BoxCollider>();
            foreach (var c in kok.GetComponentsInChildren<BoxCollider>(true))
                if (c.name.StartsWith("Carpisma_"))
                    kutular.Add(c);
            var koruklu = kok.GetComponent<KorukluOtobus>();
            if (koruklu != null && koruklu.ArkaGovde != null)
                foreach (var c in koruklu.ArkaGovde.GetComponentsInChildren<BoxCollider>(true))
                    if (c.name.StartsWith("Carpisma_") && !kutular.Contains(c))
                        kutular.Add(c);
            if (kutular.Count == 0)
                return Varsayilan;

            float on = float.MinValue, arka = float.MaxValue, genislik = 0f, yukseklik = 0f;
            foreach (var c in kutular)
                for (int i = 0; i < 8; i++)
                {
                    var kose = c.center + Vector3.Scale(c.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = kok.InverseTransformPoint(c.transform.TransformPoint(kose));
                    on = Mathf.Max(on, p.z);
                    arka = Mathf.Min(arka, p.z);
                    genislik = Mathf.Max(genislik, Mathf.Abs(p.x));
                    yukseklik = Mathf.Max(yukseklik, p.y);
                }
            return new OtobusOlcusu(on, -arka, genislik, yukseklik, kutular.ToArray());
        }

        /// <summary>Dünya noktası otobüsün gövde kutularından birinin içinde mi (kenar payıyla)?</summary>
        public bool Icinde(Vector3 dunya, float pay = 0.15f)
        {
            foreach (var c in Kutular)
            {
                if (c == null)
                    continue;
                var p = c.transform.InverseTransformPoint(dunya) - c.center;
                var yari = c.size * 0.5f + Vector3.one * pay;
                if (Mathf.Abs(p.x) < yari.x && Mathf.Abs(p.y) < yari.y && Mathf.Abs(p.z) < yari.z)
                    return true;
            }
            return false;
        }
    }
}
