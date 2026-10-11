using AnkaraBus.Route;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüsün LED hat tabelası (nokta matris, LedYazi). Ön tabela: "1 ATAKULE" ile "1 KIZILAY AVM - ATAKULE" dönüşümlü
    /// (sığmayan kayar), hat bitince "SERVİS DIŞI", rotasız (menü vitrini) "ANKARA". Arka tabela: hat numarası.
    /// Yerleşim otobüs tanımına göre (Yerlesimler): BMC ve Conecto'da tabela yüzeyine konan ince bir levha (Sprite);
    /// Millennium'da modelin kendi tabela malzemesinin ("vmatrix", kaynakta "RESERVADO") dokusu değiştirilir.
    /// BusVehicle.Awake ekler; prefab kurulumu gerekmez.
    /// </summary>
    public class HatTabelasi : MonoBehaviour
    {
        private const int OnGenislik = 128;   // nokta
        private const int ArkaGenislik = 52;   // en/boy ≈ 3,3 (Conecto arka tabela kutusu 0,56 × 0,17 m)
        private const float SayfaSuresi = 4f;
        private const float KaymaHizi = 24f;  // nokta / sn

        private struct Levha
        {
            public Vector3 konum, normal;   // otobüs yerel (Unity), normal dışarı
            public float en;                 // m (boy dokunun en/boy oranından)
            public bool arka;                // hat numarası tabelası
            public bool arkaGovdede;         // körüklüde arka gövdeye bağlı
        }

        private struct Yerlesim
        {
            public Levha[] levhalar;
            public string malzeme;           // dokusu değiştirilecek malzeme adı parçası (Millennium)
        }

        private static Yerlesim Bul(string tanim)
        {
            switch (tanim)
            {
                case "BMC_Procity_12LF":
                    // ön camın üst bandının 4 cm arkası (cam z 5,68); arka: arka yüzün üst ortası (z −5,85)
                    return new Yerlesim
                    {
                        levhalar = new[]
                        {
                            new Levha { konum = new Vector3(0f, 2.62f, 5.64f), normal = Vector3.forward, en = 1.6f },
                            new Levha { konum = new Vector3(0f, 2.72f, -5.875f), normal = Vector3.back, en = 0.6f, arka = true },
                        },
                    };
                case "Caio_Millennium_II":
                    return new Yerlesim { levhalar = new Levha[0], malzeme = "vmatrix" };
                case "MB_Conecto_G":
                    // ön: dokudaki tabela kutusu (x ±0,98, y 2,50–2,74), yüzey 15° geriye yatık; arka: sağ üst küçük tabela
                    return new Yerlesim
                    {
                        levhalar = new[]
                        {
                            new Levha { konum = new Vector3(0f, 2.623f, 8.730f), normal = new Vector3(0f, 0.27f, 0.96f), en = 1.96f },
                            new Levha { konum = new Vector3(0.68f, 2.465f, -9.235f), normal = Vector3.back, en = 0.56f, arka = true, arkaGovdede = true },
                        },
                    };
                default:
                    return new Yerlesim { levhalar = new Levha[0] };
            }
        }

        private RouteTracker tracker;
        private BusRoute sonRota;
        private bool sonBitti;
        private Texture2D onDoku, arkaDoku;
        private Color32[] onPiksel, arkaPiksel;
        private string[] sayfalar = { "ANKARA" };
        private int sayfa;
        private float sayfaBasi;
        private bool[,] noktalar;
        private int sonKayma = -1;

        private string malzemeAdi;

        /// <summary>Türkçe büyük harf (i → İ, ı → I); CultureInfo("tr-TR") her platformda olmayabilir.</summary>
        private static string Buyuk(string s) => s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        private void Start()
        {
            tracker = GetComponent<RouteTracker>();
            onDoku = YeniDoku(OnGenislik, out onPiksel);
            arkaDoku = YeniDoku(ArkaGenislik, out arkaPiksel);

            var vehicle = GetComponent<BusVehicle>();
            var yer = Bul(vehicle != null && vehicle.Definition != null ? vehicle.Definition.name : name.Replace("(Clone)", ""));
            var koruklu = GetComponent<KorukluOtobus>();
            foreach (var l in yer.levhalar)
            {
                var ebeveyn = l.arkaGovdede && koruklu != null && koruklu.ArkaGovde != null ? koruklu.ArkaGovde.transform : transform;
                LevhaKur(l, ebeveyn);
            }
            malzemeAdi = yer.malzeme;
            MalzemeyiYenile();
            Guncelle(true);
        }

        private static Texture2D YeniDoku(int genislik, out Color32[] piksel)
        {
            var t = new Texture2D(genislik * LedYazi.NoktaPiksel, LedYazi.Yukseklik * LedYazi.NoktaPiksel, TextureFormat.RGBA32, false)
            {
                name = "HatTabelasi", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            piksel = new Color32[t.width * t.height];
            return t;
        }

        private void LevhaKur(Levha l, Transform ebeveyn)
        {
            var doku = l.arka ? arkaDoku : onDoku;
            var go = new GameObject(l.arka ? "HatTabelasi_Arka" : "HatTabelasi_On");
            // otobüs düz dururken (Start) dünya konumu; arka gövdeye bağlıysa ona göre yerel
            Vector3 normal = transform.TransformDirection(l.normal.normalized);
            go.transform.SetPositionAndRotation(transform.TransformPoint(l.konum), Quaternion.LookRotation(-normal, transform.up));
            go.transform.SetParent(ebeveyn, true);
            var sr = go.AddComponent<SpriteRenderer>();
            // Sprite, ileri yönü boyunca bakan göze doğru okunur; ileri = −normal (içeri), göz dışarıda
            sr.sprite = Sprite.Create(doku, new Rect(0, 0, doku.width, doku.height), new Vector2(0.5f, 0.5f), doku.width / l.en);
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }

        /// <summary>Tabela dokusunu modelin tabela malzemesine (Millennium) yeniden verir. Kalite modeli değişince
        /// (BusKaliteModeli malzeme bloklarını siler) çağrılır.</summary>
        public void MalzemeyiYenile()
        {
            if (string.IsNullOrEmpty(malzemeAdi) || onDoku == null)
                return;
            var blok = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(malzemeAdi))
                        continue;
                    r.GetPropertyBlock(blok, i);
                    blok.SetTexture(mats[i].HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", onDoku);
                    r.SetPropertyBlock(blok, i);
                }
            }
        }

        private void Update() => Guncelle(false);

        private void Guncelle(bool zorla)
        {
            var rota = tracker != null ? tracker.Route : null;
            bool bitti = rota != null && tracker.Completed;
            if (zorla || rota != sonRota || bitti != sonBitti)
            {
                sonRota = rota;
                sonBitti = bitti;
                if (rota == null)
                    sayfalar = new[] { "ANKARA" };
                else if (bitti)
                    sayfalar = new[] { "SERVİS DIŞI" };
                else
                {
                    string son = rota.StopCount > 0 ? rota.GetStop(rota.StopCount - 1).StopName : rota.LineName;
                    sayfalar = new[]
                    {
                        $"{rota.LineNumber} {Buyuk(son)}",
                        $"{rota.LineNumber} {Buyuk(rota.LineName)}",
                    };
                }
                sayfa = 0;
                SayfaAc();
                ArkayiCiz(rota != null && !bitti ? rota.LineNumber : "");
            }

            // sığan sayfa SayfaSuresi kadar durur; sığmayan baştan sona kayar, sonra sıradaki
            int n = noktalar.GetLength(0);
            float gecen = Time.time - sayfaBasi;
            if (n <= OnGenislik)
            {
                if (sayfalar.Length > 1 && gecen > SayfaSuresi)
                    SonrakiSayfa();
                return;
            }
            int kayma = Mathf.FloorToInt((gecen - 1f) * KaymaHizi);   // ilk 1 sn başı durur
            int sonKaymaSiniri = n - OnGenislik;
            if (kayma > sonKaymaSiniri + KaymaHizi)                      // sonu 1 sn durur
            {
                SonrakiSayfa();
                return;
            }
            kayma = Mathf.Clamp(kayma, 0, sonKaymaSiniri);
            if (kayma != sonKayma)
            {
                sonKayma = kayma;
                LedYazi.Ciz(onPiksel, OnGenislik, noktalar, kayma, false);
                Yukle(onDoku, onPiksel);
            }
        }

        private void SonrakiSayfa()
        {
            sayfa = (sayfa + 1) % sayfalar.Length;
            SayfaAc();
        }

        private void SayfaAc()
        {
            noktalar = LedYazi.Noktalar(sayfalar[sayfa]);
            sayfaBasi = Time.time;
            sonKayma = -1;
            bool sigar = noktalar.GetLength(0) <= OnGenislik;
            LedYazi.Ciz(onPiksel, OnGenislik, noktalar, 0, sigar);
            Yukle(onDoku, onPiksel);
        }

        private void ArkayiCiz(string numara)
        {
            LedYazi.Ciz(arkaPiksel, ArkaGenislik, LedYazi.Noktalar(numara), 0, true);
            Yukle(arkaDoku, arkaPiksel);
        }

        private static void Yukle(Texture2D t, Color32[] p)
        {
            t.SetPixels32(p);
            t.Apply(false);
        }

        private void OnDestroy()
        {
            if (onDoku != null) Destroy(onDoku);
            if (arkaDoku != null) Destroy(arkaDoku);
        }
    }
}
