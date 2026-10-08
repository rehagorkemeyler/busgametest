using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using UnityEngine;
using UnityEngine.UI;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Sağ üstte yuvarlak mini harita (otobüsün yönü yukarı) ve yol bilgisi: sıradaki durak ve kalan mesafe,
    /// yaklaşan dönüş, rotadan çıkma uyarısı. Dokununca bütün hattı gösteren büyük harita açılır.
    /// Harita dokusu oyun açılırken bir kez çizilir: trafik şeritleri (yollar), binalar, ağaçlar,
    /// güzergâh (RotaRehberi) ve duraklar. Sahne dosyası gerekmez; otobüs prefabında RotaRehberi'nin yanında durur.
    /// </summary>
    [RequireComponent(typeof(RotaRehberi))]
    public class MiniHarita : MonoBehaviour
    {
        [Tooltip("Mini haritada merkezden kenara görünen mesafe (m).")]
        [SerializeField] private float gorusYaricapi = 160f;
        [SerializeField] private float capPiksel = 240f;

        private static readonly Color32 Zemin = new Color32(38, 46, 41, 255);
        private static readonly Color32 Bina = new Color32(84, 86, 94, 255);
        private static readonly Color32 Agac = new Color32(46, 78, 52, 255);
        private static readonly Color32 YolRengi = new Color32(150, 154, 160, 255);
        private static readonly Color32 Rota = new Color32(250, 194, 46, 255);
        private static readonly Color32 DurakRengi = new Color32(255, 255, 255, 255);
        private static readonly Color32 Kenar = new Color32(20, 22, 26, 255);
        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.75f);
        private static readonly Color Gold = new Color(0.98f, 0.76f, 0.18f, 1f);
        private static readonly Color Uyari = new Color(1f, 0.42f, 0.38f, 1f);

        private RotaRehberi rehber;
        private Texture2D doku;
        private Vector2 dunyaMin;
        private float pikselMetre;
        private Font font;

        private GameObject canvasGo;
        private RectTransform harita, otobusIsareti, durakIsareti, kuzey;
        private Text bilgi;
        private GameObject buyuk;
        private RectTransform buyukHarita, buyukOtobus;

        private void Awake()
        {
            rehber = GetComponent<RotaRehberi>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Start()
        {
            // RotaRehberi.Start'ta güzergâh hazır olur; sıralama garanti olmadığı için burada da kurulabilir
            if (!rehber.Hazir)
                rehber.Kur();
            if (!rehber.Hazir)
            {
                enabled = false;
                return;
            }
            DokuyuCiz();
            ArayuzuKur();
        }

        private void OnDestroy()
        {
            if (canvasGo != null)
                Destroy(canvasGo);
            if (doku != null)
                Destroy(doku);
        }

        private void Update()
        {
            if (harita == null)
                return;
            Vector3 p = transform.position;
            float yaw = transform.eulerAngles.y;
            float olcek = capPiksel * 0.5f / gorusYaricapi; // UI piksel / metre

            harita.sizeDelta = new Vector2(doku.width, doku.height) / pikselMetre * olcek;
            harita.pivot = DokuUV(p);
            harita.anchoredPosition = Vector2.zero;
            harita.localRotation = Quaternion.Euler(0f, 0f, yaw);
            kuzey.anchoredPosition = Dondur(new Vector2(0f, capPiksel * 0.5f + 4f), yaw);

            // sıradaki durak: haritada, uzaksa kenarda
            var stop = rehber.Tracker.NextStop;
            durakIsareti.gameObject.SetActive(stop != null);
            if (stop != null)
            {
                Vector3 d = stop.transform.position - p;
                Vector2 ekran = Dondur(new Vector2(d.x, d.z) * olcek, yaw);
                float sinir = capPiksel * 0.5f - 12f;
                if (ekran.magnitude > sinir)
                    ekran = ekran.normalized * sinir;
                durakIsareti.anchoredPosition = ekran;
            }

            bilgi.text = BilgiYazisi(out bool uyari);
            bilgi.color = uyari ? Uyari : Color.white;

            if (buyuk.activeSelf)
            {
                buyukOtobus.anchoredPosition = (DokuUV(p) - new Vector2(0.5f, 0.5f)) * buyukHarita.rect.size;
                buyukOtobus.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }
        }

        private string BilgiYazisi(out bool uyari)
        {
            uyari = false;
            var tracker = rehber.Tracker;
            if (tracker.Completed)
                return "Hat tamamlandı";
            if (rehber.RotadanCikti)
            {
                uyari = true;
                return "ROTADAN ÇIKTIN\nsarı çizgiye dön";
            }
            string satir1 = $"{tracker.NextStop.StopName}  {Mesafe(rehber.DurakaKalan)}";
            string satir2;
            if (rehber.DurakaKalan >= 0f && rehber.DurakaKalan < 120f)
                satir2 = "durağa yanaş";
            else if (rehber.SiradakiDonus != RotaRehberi.Donus.Yok)
                satir2 = $"{Mesafe(rehber.DonuseKalan)} sonra {(rehber.SiradakiDonus == RotaRehberi.Donus.Sag ? "SAĞA" : "SOLA")}";
            else
                satir2 = "düz devam";
            return satir1 + "\n" + satir2;
        }

        private static string Mesafe(float m) =>
            m < 0f ? "" : m >= 1000f ? $"{m / 1000f:0.0} km" : $"{Mathf.RoundToInt(m / 10f) * 10} m";

        /// <summary>UI'de saat yönünün tersine 'derece' döndürür (harita yaw kadar döndüğü için).</summary>
        private static Vector2 Dondur(Vector2 v, float derece)
        {
            float r = derece * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private Vector2 DokuUV(Vector3 p) => new Vector2(
            (p.x - dunyaMin.x) * pikselMetre / doku.width,
            (p.z - dunyaMin.y) * pikselMetre / doku.height);

        // ---------------------------------------------------------------- doku

        private Color32[] piksel;
        private int w, h;

        private void DokuyuCiz()
        {
            var seritler = FindObjectsByType<TrafficLane>();
            var binalar = new List<(Vector3[] kose, bool agac)>();
            foreach (var grup in Gruplar("Binalar", "SimgeYapilar", "Duraklar"))
                foreach (var mf in grup.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)
                        binalar.Add((TabanKoseleri(mf), false));
            foreach (var grup in Gruplar("Agaclar"))
                foreach (Transform t in grup)
                    binalar.Add((new[] { t.position }, true));

            // sınırlar: güzergâh ve şeritler (binalar dahil edilmez, harita gereksiz büyümesin)
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            void Genislet(Vector3 v)
            {
                min = Vector2.Min(min, new Vector2(v.x, v.z));
                max = Vector2.Max(max, new Vector2(v.x, v.z));
            }
            foreach (var v in rehber.Yol)
                Genislet(v);
            foreach (var serit in seritler)
            {
                serit.Sample(0f, out var a, out _);
                serit.Sample(serit.Length, out var b, out _);
                Genislet(a);
                Genislet(b);
            }
            min -= Vector2.one * 70f;
            max += Vector2.one * 70f;
            dunyaMin = min;
            Vector2 boy = max - min;
            pikselMetre = Mathf.Min(1.2f, 2048f / Mathf.Max(boy.x, boy.y));
            w = Mathf.CeilToInt(boy.x * pikselMetre);
            h = Mathf.CeilToInt(boy.y * pikselMetre);
            piksel = new Color32[w * h];
            for (int i = 0; i < piksel.Length; i++)
                piksel[i] = Zemin;

            foreach (var (kose, agac) in binalar)
                if (agac)
                    Daire(kose[0], 2.5f, Agac);
            foreach (var (kose, agac) in binalar)
                if (!agac)
                    Cokgen(kose, Bina);
            foreach (var serit in seritler)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(serit.Length / 4f) + 1);
                serit.Sample(0f, out var onceki, out _);
                for (int k = 1; k < n; k++)
                {
                    serit.Sample(Mathf.Min(k * 4f, serit.Length), out var p, out _);
                    Cizgi(onceki, p, 3.8f, YolRengi);
                    onceki = p;
                }
            }
            var yol = rehber.Yol;
            for (int i = 1; i < yol.Count; i++)
                Cizgi(yol[i - 1], yol[i], 2.6f, Rota);
            var route = rehber.Tracker.Route;
            for (int i = 0; i < route.StopCount; i++)
            {
                var stop = route.GetStop(i);
                if (stop == null)
                    continue;
                Daire(stop.transform.position, 7f, Kenar);
                Daire(stop.transform.position, 5f, DurakRengi);
            }

            doku = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            doku.SetPixels32(piksel);
            doku.Apply(false, true); // CPU kopyası bırakılır
            piksel = null;
        }

        private static IEnumerable<Transform> Gruplar(params string[] adlar)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var kok in scene.GetRootGameObjects())
                foreach (Transform cocuk in kok.transform)
                    if (System.Array.IndexOf(adlar, cocuk.name) >= 0)
                        yield return cocuk;
        }

        private static Vector3[] TabanKoseleri(MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds;
            var t = mf.transform;
            return new[]
            {
                t.TransformPoint(new Vector3(b.min.x, b.min.y, b.min.z)),
                t.TransformPoint(new Vector3(b.max.x, b.min.y, b.min.z)),
                t.TransformPoint(new Vector3(b.max.x, b.min.y, b.max.z)),
                t.TransformPoint(new Vector3(b.min.x, b.min.y, b.max.z)),
            };
        }

        private Vector2 Px(Vector3 p) => new Vector2((p.x - dunyaMin.x) * pikselMetre, (p.z - dunyaMin.y) * pikselMetre);

        private void Cizgi(Vector3 a, Vector3 b, float genislik, Color32 renk)
        {
            Vector3 d = b - a;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f)
                return;
            Vector3 yan = Vector3.Cross(Vector3.up, d.normalized) * (genislik * 0.5f);
            Vector3 uzat = d.normalized * (genislik * 0.25f); // eklemlerde boşluk kalmasın
            Cokgen(new[] { a - yan - uzat, b - yan + uzat, b + yan + uzat, a + yan - uzat }, renk);
        }

        /// <summary>Dışbükey çokgeni doldurur (köşeler dünya koordinatında, sıra yönü fark etmez).</summary>
        private void Cokgen(Vector3[] kose, Color32 renk)
        {
            int n = kose.Length;
            var p = new Vector2[n];
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                p[i] = Px(kose[i]);
                x0 = Mathf.Min(x0, p[i].x); x1 = Mathf.Max(x1, p[i].x);
                y0 = Mathf.Min(y0, p[i].y); y1 = Mathf.Max(y1, p[i].y);
            }
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(w - 1, Mathf.CeilToInt(x1));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(h - 1, Mathf.CeilToInt(y1));
            for (int y = iy0; y <= iy1; y++)
            for (int x = ix0; x <= ix1; x++)
            {
                var q = new Vector2(x + 0.5f, y + 0.5f);
                bool poz = false, neg = false;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = p[i], b = p[(i + 1) % n];
                    float c = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
                    if (c > 0f) poz = true;
                    else if (c < 0f) neg = true;
                }
                if (!(poz && neg))
                    piksel[y * w + x] = renk;
            }
        }

        private void Daire(Vector3 merkez, float yaricap, Color32 renk)
        {
            Vector2 c = Px(merkez);
            float r = yaricap * pikselMetre;
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), ix1 = Mathf.Min(w - 1, Mathf.CeilToInt(c.x + r));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), iy1 = Mathf.Min(h - 1, Mathf.CeilToInt(c.y + r));
            for (int y = iy0; y <= iy1; y++)
            for (int x = ix0; x <= ix1; x++)
                if ((new Vector2(x + 0.5f, y + 0.5f) - c).sqrMagnitude <= r * r)
                    piksel[y * w + x] = renk;
        }

        // ---------------------------------------------------------------- arayüz

        private void ArayuzuKur()
        {
            canvasGo = new GameObject("MiniHaritaArayuzu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // D/N/R düğmelerinin altında, EL FRENİ'nin üstünde
            var cerceve = Eleman(root, "MiniHarita", new Vector2(1f, 1f), new Vector2(-165f, -285f), Vector2.one * (capPiksel + 12f));
            var cerceveImg = cerceve.gameObject.AddComponent<Image>();
            cerceveImg.sprite = UiShapes.Circle;
            cerceveImg.color = Panel;
            var dugme = cerceve.gameObject.AddComponent<Button>();
            dugme.targetGraphic = cerceveImg;
            dugme.onClick.AddListener(() => buyuk.SetActive(true));

            var maske = Eleman(cerceve, "Maske", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * capPiksel);
            var maskeImg = maske.gameObject.AddComponent<Image>();
            maskeImg.sprite = UiShapes.Circle;
            maskeImg.raycastTarget = false;
            maske.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            harita = Eleman(maske, "Doku", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            var raw = harita.gameObject.AddComponent<RawImage>();
            raw.texture = doku;
            raw.raycastTarget = false;

            durakIsareti = Eleman(maske, "SiradakiDurak", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 20f);
            var durakImg = durakIsareti.gameObject.AddComponent<Image>();
            durakImg.sprite = UiShapes.Circle;
            durakImg.color = Gold;
            durakImg.raycastTarget = false;
            durakIsareti.gameObject.AddComponent<Outline>().effectColor = Color.black;

            otobusIsareti = Eleman(maske, "Otobus", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 30f);
            var otobusImg = otobusIsareti.gameObject.AddComponent<Image>();
            otobusImg.sprite = UiShapes.Arrow;
            otobusImg.color = new Color(0.3f, 0.75f, 1f, 1f);
            otobusImg.raycastTarget = false;
            otobusIsareti.gameObject.AddComponent<Outline>().effectColor = Color.black;

            kuzey = Eleman(cerceve, "Kuzey", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 26f);
            var kuzeyImg = kuzey.gameObject.AddComponent<Image>();
            kuzeyImg.sprite = UiShapes.Circle;
            kuzeyImg.color = new Color(0.75f, 0.2f, 0.2f, 1f);
            kuzeyImg.raycastTarget = false;
            Yazi(kuzey, "K", 18, TextAnchor.MiddleCenter);

            var bant = Eleman(root, "YolBilgisi", new Vector2(1f, 1f), new Vector2(-165f, -285f - capPiksel * 0.5f + 10f), new Vector2(300f, 62f));
            var bantImg = bant.gameObject.AddComponent<Image>();
            bantImg.sprite = UiShapes.RoundedRect;
            bantImg.type = Image.Type.Sliced;
            bantImg.color = Panel;
            bantImg.raycastTarget = false;
            bilgi = Yazi(bant, "", 22, TextAnchor.MiddleCenter);
            bilgi.lineSpacing = 0.95f;

            BuyukHaritayiKur(root);
        }

        /// <summary>Bütün hat, kuzey yukarı (uzun hatlar yatay çevrilir); dokununca kapanır.</summary>
        private void BuyukHaritayiKur(RectTransform root)
        {
            var perde = Eleman(root, "BuyukHarita", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            perde.anchorMin = Vector2.zero;
            perde.anchorMax = Vector2.one;
            var perdeImg = perde.gameObject.AddComponent<Image>();
            perdeImg.color = new Color(0f, 0f, 0f, 0.7f);
            var kapat = perde.gameObject.AddComponent<Button>();
            kapat.targetGraphic = perdeImg;
            kapat.onClick.AddListener(() => buyuk.SetActive(false));
            buyuk = perde.gameObject;

            bool yatay = doku.height > doku.width;
            float uzun = Mathf.Max(doku.width, doku.height), kisa = Mathf.Min(doku.width, doku.height);
            float olcek = Mathf.Min(1700f / uzun, 860f / kisa);
            buyukHarita = Eleman(perde, "Harita", new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(doku.width, doku.height) * olcek);
            buyukHarita.localRotation = Quaternion.Euler(0f, 0f, yatay ? 90f : 0f);
            var raw = buyukHarita.gameObject.AddComponent<RawImage>();
            raw.texture = doku;
            raw.raycastTarget = false;

            var route = rehber.Tracker.Route;
            for (int i = 0; i < route.StopCount; i++)
            {
                var stop = route.GetStop(i);
                if (stop == null)
                    continue;
                var etiket = Eleman(buyukHarita, "Durak_" + i, new Vector2(0.5f, 0.5f),
                    (DokuUV(stop.transform.position) - new Vector2(0.5f, 0.5f)) * buyukHarita.sizeDelta, new Vector2(260f, 70f));
                etiket.localRotation = Quaternion.Inverse(buyukHarita.localRotation); // yazı düz kalsın
                etiket.pivot = new Vector2(0.5f, -0.15f);
                var yazi = Yazi(etiket, $"{i + 1}. {stop.StopName}", 26, TextAnchor.LowerCenter);
                yazi.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }

            buyukOtobus = Eleman(buyukHarita, "Otobus", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 36f);
            var img = buyukOtobus.gameObject.AddComponent<Image>();
            img.sprite = UiShapes.Arrow;
            img.color = new Color(0.3f, 0.75f, 1f, 1f);
            img.raycastTarget = false;
            buyukOtobus.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var baslik = Eleman(perde, "Baslik", new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1200f, 60f));
            Yazi(baslik, $"HAT {route.LineNumber}  {route.LineName}   (kapatmak için dokun)", 34, TextAnchor.MiddleCenter);
            buyuk.SetActive(false);
        }

        private static RectTransform Eleman(RectTransform parent, string ad, Vector2 anchor, Vector2 konum, Vector2 boyut)
        {
            var rect = new GameObject(ad, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = konum;
            rect.sizeDelta = boyut;
            return rect;
        }

        private Text Yazi(RectTransform parent, string metin, int boyut, TextAnchor hiza)
        {
            var rect = Eleman(parent, "Yazi", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            var t = rect.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = metin;
            t.fontSize = boyut;
            t.fontStyle = FontStyle.Bold;
            t.alignment = hiza;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
