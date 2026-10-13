using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Yayanın dünyada düz yürüyüşü için basit engel kaçınma: iki nokta arasında (bel hizasında) sabit bir çarpışıcı varsa
    /// (durak sundurması, direk, kulübe; araçlar ve otobüs Rigidbody'li olduğu için sayılmaz) yol onun köşelerinden dolaşır.
    /// Durakta inen yolcular sundurmanın içinden geçiyordu (Y20).
    /// </summary>
    public static class YayaYolu
    {
        private const float Bel = 1.0f;       // ışın yüksekliği (zeminden)
        private const float Pay = 0.45f;      // köşeden uzaklık
        private static readonly RaycastHit[] vuruslar = new RaycastHit[8];

        /// <summary>Kapalıyken düz yürür (yalnız test: önce/sonra karşılaştırması).</summary>
        public static bool Etkin = true;

        /// <summary>'nereden'den 'nereye'ye engelsiz ara noktalar (sonuncusu 'nereye'; engelin içine düşüyorsa dışına alınmış hali).</summary>
        public static List<Vector3> Yol(Vector3 nereden, Vector3 nereye)
        {
            var yol = new List<Vector3>(4);
            if (!Etkin)
            {
                yol.Add(nereye);
                return yol;
            }
            // başlangıç bir engelin içindeyse (kapının önü sundurmanın açık önüne düşüyor) önce en yakın yüzünden çık
            if (Icinde(nereden) != null)
            {
                var cik = Disari(nereden);
                yol.Add(cik);
            }
            nereye = Disari(nereye);
            for (int adim = 0; adim < 3; adim++)    // en çok üç engel art arda
            {
                var bas = yol.Count > 0 ? yol[yol.Count - 1] : nereden;
                var engel = Engel(bas, nereye);
                if (engel == null)
                    break;
                var ara = Dolas(bas, nereye, engel);
                if (ara == null)
                    break;
                yol.AddRange(ara);
            }
            yol.Add(nereye);
            return yol;
        }

        /// <summary>Nokta bir engelin (sundurma, bina) içindeyse engelin en yakın kenarının dışına alır.</summary>
        public static Vector3 Disari(Vector3 p)
        {
            if (!Etkin)
                return p;
            for (int deneme = 0; deneme < 3; deneme++)
            {
                var c = Icinde(p);
                if (c == null)
                    return p;
                var b = c.bounds;
                var adaylar = new List<Vector3>();
                foreach (float pay in new[] { Pay, KucukPay })
                {
                    adaylar.Add(new Vector3(b.min.x - pay, p.y, p.z));
                    adaylar.Add(new Vector3(b.max.x + pay, p.y, p.z));
                    adaylar.Add(new Vector3(p.x, p.y, b.min.z - pay));
                    adaylar.Add(new Vector3(p.x, p.y, b.max.z + pay));
                }
                // en yakın yüz; Pay'lı aday doluysa (yanaşmış otobüs) aynı yüzün küçük paylısı
                adaylar.Sort((x, y) => Duz(p, x).CompareTo(Duz(p, y)));
                var yeni = p;
                foreach (var a in adaylar)
                {
                    var z = Zemin(a, p.y);
                    if (!Dolu(z))
                    {
                        yeni = z;
                        break;
                    }
                }
                if (yeni == p)
                    yeni = Zemin(adaylar[0], p.y);
                p = yeni;
            }
            return p;
        }

        /// <summary>Engelin köşelerinden (tek ya da iki komşu köşe) en kısa engelsiz dolaşma; köşe başka bir engelin içindeyse kullanılmaz.</summary>
        private static List<Vector3> Dolas(Vector3 nereden, Vector3 nereye, Collider engel)
        {
            var koseler = new Vector3[4];
            var gecerli = new bool[4];
            var b0 = engel.bounds;
            float y = Mathf.Lerp(nereden.y, nereye.y, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                foreach (float pay in new[] { Pay, KucukPay })
                {
                    float x = (i == 0 || i == 3) ? b0.min.x - pay : b0.max.x + pay;
                    float z = (i == 0 || i == 1) ? b0.min.z - pay : b0.max.z + pay;
                    koseler[i] = Zemin(new Vector3(x, 0f, z), y);
                    gecerli[i] = !Dolu(koseler[i]);
                    if (gecerli[i])
                        break;
                }
            }
            float enKisa = float.MaxValue;
            List<Vector3> enIyi = null;
            for (int i = 0; i < 4; i++)
                if (gecerli[i] && Acik(nereden, koseler[i]) && Acik(koseler[i], nereye))
                {
                    float d = Duz(nereden, koseler[i]) + Duz(koseler[i], nereye);
                    if (d < enKisa) { enKisa = d; enIyi = new List<Vector3> { koseler[i] }; }
                }
            if (enIyi == null)
                for (int i = 0; i < 4; i++)
                    foreach (int j in new[] { (i + 1) % 4, (i + 3) % 4 })
                        if (gecerli[i] && gecerli[j] && Acik(nereden, koseler[i]) && Acik(koseler[i], koseler[j]) && Acik(koseler[j], nereye))
                        {
                            float d = Duz(nereden, koseler[i]) + Duz(koseler[i], koseler[j]) + Duz(koseler[j], nereye);
                            if (d < enKisa) { enKisa = d; enIyi = new List<Vector3> { koseler[i], koseler[j] }; }
                        }
            return enIyi;
        }

        private const float KucukPay = 0.12f;  // engelle yanaşmış otobüs arasındaki dar şerit için
        private const float Yaricap = 0.1f;    // yürüyüş ışınının yarıçapı

        private static readonly Collider[] tampon = new Collider[8];

        /// <summary>Noktada (bel hizasında, küçük yarıçap) herhangi bir çarpışıcı var mı: sabit engel ya da otobüs/araç.</summary>
        private static bool Dolu(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * Bel, 0.05f, tampon, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (tampon[i].bounds.max.y > p.y + Bel + 0.3f && (tampon[i].attachedRigidbody != null || tampon[i].bounds.min.y < p.y + Bel))
                    return true;
            return false;
        }

        /// <summary>Noktanın bel hizasında sabit, dikey bir çarpışıcı (sundurma, bina, direk) varsa o.</summary>
        public static Collider Icinde(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * Bel, 0.15f, tampon, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (tampon[i].attachedRigidbody == null && tampon[i].bounds.max.y > p.y + Bel + 0.3f)
                    return tampon[i];
            return null;
        }

        private static float Duz(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static bool Acik(Vector3 a, Vector3 b) => Engel(a, b) == null;

        /// <summary>Yolu kesen ilk sabit çarpışıcı (bel hizasında; zemin ve Rigidbody'liler sayılmaz).</summary>
        private static Collider Engel(Vector3 a, Vector3 b)
        {
            var bas = a + Vector3.up * Bel;
            var fark = (b + Vector3.up * Bel) - bas;
            float boy = fark.magnitude;
            if (boy < 0.05f)
                return null;
            int n = Physics.SphereCastNonAlloc(bas, Yaricap, fark / boy, vuruslar, boy, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider en = null;
            float enYakin = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = vuruslar[i];
                if (h.collider.attachedRigidbody != null || h.normal.y > 0.7f || h.distance <= 0f)
                    continue;
                if (h.distance < enYakin)
                {
                    enYakin = h.distance;
                    en = h.collider;
                }
            }
            return en;
        }

        private static Vector3 Zemin(Vector3 p, float varsayilanY)
        {
            var bas = new Vector3(p.x, varsayilanY + 1.5f, p.z);
            if (Physics.Raycast(bas, Vector3.down, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.attachedRigidbody == null && hit.point.y < varsayilanY + 0.6f)
                return hit.point;
            return new Vector3(p.x, varsayilanY, p.z);
        }
    }
}
