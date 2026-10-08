using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Ana menü arayüzünü kodla kurar (BusTouchControls ile aynı görünüm): solda hat kartları (en iyi puanla),
    /// kaplama ve görüntü kalitesi seçimi, sağ altta SEFERE BAŞLA. Sağ yarıda MenuVitrini otobüsü döndürür;
    /// kaplama seçilince vitrindeki otobüs de değişir. Seçimler OyunSecimi'nde (PlayerPrefs) saklanır.
    /// Sahne: Ankara Bus > Ana Menü Sahnesini Kur.
    /// </summary>
    public class AnaMenu : MonoBehaviour
    {
        [Tooltip("Vitrindeki otobüsün kaplaması (seçim önizlemesi).")]
        [SerializeField] private BusLivery vitrinKaplamasi;

        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.72f);
        private static readonly Color Card = new Color(0.12f, 0.13f, 0.16f, 0.88f);
        private static readonly Color Idle = new Color(0.85f, 0.87f, 0.9f, 0.9f);
        private static readonly Color Active = new Color(0.98f, 0.76f, 0.18f, 1f);
        private static readonly Color Ok = new Color(0.25f, 0.72f, 0.4f, 1f);
        private static readonly Color Dim = new Color(0.65f, 0.68f, 0.72f, 1f);

        private Font font;
        private readonly List<Image> hatKartlari = new List<Image>();
        private readonly List<Image> kaplamaDugmeleri = new List<Image>();
        private readonly Image[] kaliteDugmeleri = new Image[GrafikAyarlari.SeviyeAdlari.Length];
        private GameObject yukleniyor;
        private Text yukleniyorYazi;
        private bool basladi;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
            Yenile();
            KaplamayiOnizle();
        }

        private void Update()
        {
            // Android geri tuşu
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !basladi)
                Application.Quit();
        }

        private void Build()
        {
            var canvasGo = new GameObject("MenuArayuzu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // Menünün arkası: arka plandaki renkli apartmanların üstünde yazılar okunsun
            var arka = new GameObject("MenuArkasi", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            arka.SetParent(root, false);
            arka.anchorMin = new Vector2(0f, 0f);
            arka.anchorMax = new Vector2(0f, 1f);
            arka.pivot = new Vector2(0f, 0.5f);
            arka.sizeDelta = new Vector2(940f, 0f);
            var arkaImage = arka.GetComponent<Image>();
            arkaImage.color = new Color(0.04f, 0.05f, 0.07f, 0.55f);
            arkaImage.raycastTarget = false;

            // Başlık
            var baslik = Label(Element(root, "Baslik", new Vector2(0f, 1f), new Vector2(470f, -80f), new Vector2(860f, 90f)),
                "ANKARA OTOBÜS", 76, TextAnchor.MiddleLeft, Color.white);
            baslik.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            var alt = Label(Element(root, "AltBaslik", new Vector2(0f, 1f), new Vector2(470f, -140f), new Vector2(860f, 40f)),
                "EGO şoförü ol: yolcuyu al, durağa yanaş, kırmızıda dur.", 28, TextAnchor.MiddleLeft, Idle);
            alt.fontStyle = FontStyle.Normal;

            // Hat kartları
            Bolum(root, "HAT", -200f);
            for (int i = 0; i < OyunSecimi.Hatlar.Length; i++)
            {
                int index = i;
                var hat = OyunSecimi.Hatlar[i];
                bool mevcut = Application.CanStreamedLevelBeLoaded(hat.sahne);
                var kart = Box(root, "Hat_" + hat.numara, new Vector2(0f, 1f), new Vector2(470f, -305f - i * 165f), new Vector2(860f, 150f), Card);
                var image = kart.GetComponent<Image>();
                hatKartlari.Add(image);
                var button = kart.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.interactable = mevcut;
                button.onClick.AddListener(() => { OyunSecimi.SeciliHat = index; Yenile(); });

                var numara = Box(kart, "Numara", new Vector2(0f, 0.5f), new Vector2(75f, 0f), new Vector2(110f, 110f), Active);
                Label(numara, hat.numara, 64, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.1f, 1f));
                Label(Element(kart, "Ad", new Vector2(0f, 1f), new Vector2(480f, -42f), new Vector2(680f, 50f)),
                    hat.ad, 38, TextAnchor.MiddleLeft, Color.white);
                var duraklar = Label(Element(kart, "Duraklar", new Vector2(0f, 1f), new Vector2(480f, -85f), new Vector2(680f, 36f)),
                    hat.duraklar, 24, TextAnchor.MiddleLeft, Dim);
                duraklar.fontStyle = FontStyle.Normal;
                int? enIyi = OyunSecimi.EnIyiPuan(hat.numara);
                var puan = Label(Element(kart, "EnIyi", new Vector2(0f, 1f), new Vector2(480f, -120f), new Vector2(680f, 34f)),
                    !mevcut ? "Sahne build'de yok" : enIyi.HasValue ? $"En iyi puan: {enIyi.Value}" : "Henüz oynanmadı",
                    24, TextAnchor.MiddleLeft, enIyi.HasValue ? Active : Dim);
                puan.fontStyle = FontStyle.Normal;
            }

            // Kaplama
            float kaplamaY = -305f - OyunSecimi.Hatlar.Length * 165f - 30f;
            Bolum(root, "KAPLAMA", kaplamaY);
            int adet = vitrinKaplamasi != null && vitrinKaplamasi.Count > 0 ? vitrinKaplamasi.Count : 3;
            string[] varsayilan = { "EGO kırmızı", "EGO mavi", "Özel Halk" };
            for (int i = -1; i < adet; i++)
            {
                int index = i;
                string ad = i < 0 ? "Rastgele"
                    : vitrinKaplamasi != null && vitrinKaplamasi.NameAt(i) != null ? vitrinKaplamasi.NameAt(i)
                    : i < varsayilan.Length ? varsayilan[i] : $"Kaplama {i + 1}";
                float genislik = 860f / (adet + 1) - 10f;
                var dugme = Dugme(root, ad, new Vector2(0f, 1f), new Vector2(40f + genislik * 0.5f + (i + 1) * (genislik + 10f), kaplamaY - 75f),
                    new Vector2(genislik, 80f), 26, () => { OyunSecimi.Kaplama = index; Yenile(); KaplamayiOnizle(); });
                kaplamaDugmeleri.Add(dugme);
            }

            // Görüntü kalitesi
            float kaliteY = kaplamaY - 155f;
            Bolum(root, "GÖRÜNTÜ", kaliteY);
            for (int i = 0; i < kaliteDugmeleri.Length; i++)
            {
                var seviye = (KaliteSeviyesi)i;
                kaliteDugmeleri[i] = Dugme(root, GrafikAyarlari.SeviyeAdlari[i], new Vector2(0f, 1f),
                    new Vector2(40f + 140f + i * 290f, kaliteY - 75f), new Vector2(280f, 80f), 28,
                    () => { GrafikAyarlari.Uygula(seviye); Yenile(); });
            }
            var oneri = Label(Element(root, "Oneri", new Vector2(0f, 1f), new Vector2(470f, kaliteY - 140f), new Vector2(860f, 34f)),
                $"Bu telefon için önerilen: {GrafikAyarlari.SeviyeAdlari[(int)GrafikAyarlari.Onerilen]}", 24, TextAnchor.MiddleLeft, Dim);
            oneri.fontStyle = FontStyle.Normal;

            // Başla
            var basla = Dugme(root, "SEFERE BAŞLA", new Vector2(1f, 0f), new Vector2(-260f, 110f), new Vector2(440f, 130f), 46, Baslat);
            basla.color = Ok;
            var cikis = Dugme(root, "ÇIKIŞ", new Vector2(1f, 0f), new Vector2(-580f, 110f), new Vector2(160f, 130f), 30, Application.Quit);
            cikis.color = Panel;
            Label(Element(root, "Ipucu", new Vector2(1f, 1f), new Vector2(-330f, -60f), new Vector2(600f, 40f)),
                "Otobüsü parmağınla çevirebilirsin", 24, TextAnchor.MiddleRight, Dim).fontStyle = FontStyle.Normal;

            // Yükleniyor ekranı
            var perde = Element(root, "Yukleniyor", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            perde.anchorMin = Vector2.zero;
            perde.anchorMax = Vector2.one;
            perde.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.94f);
            yukleniyorYazi = Label(perde, "", 48, TextAnchor.MiddleCenter, Color.white);
            yukleniyor = perde.gameObject;
            yukleniyor.SetActive(false);
        }

        private void Yenile()
        {
            int hat = OyunSecimi.SeciliHat;
            for (int i = 0; i < hatKartlari.Count; i++)
                hatKartlari[i].color = i == hat ? new Color(0.22f, 0.2f, 0.12f, 0.95f) : Card;
            int kaplama = OyunSecimi.Kaplama;
            if (kaplama == -2)
                kaplama = 0; // kayıt yoksa prefabın kaplaması (EGO kırmızı)
            for (int i = 0; i < kaplamaDugmeleri.Count; i++)
                kaplamaDugmeleri[i].color = i - 1 == kaplama ? Active : Panel;
            for (int i = 0; i < kaliteDugmeleri.Length; i++)
                kaliteDugmeleri[i].color = (int)GrafikAyarlari.Mevcut == i ? Active : Panel;
        }

        private void KaplamayiOnizle()
        {
            if (vitrinKaplamasi == null)
                return;
            int kaplama = OyunSecimi.Kaplama;
            if (kaplama == -1)
                vitrinKaplamasi.SelectRandom();
            else
                vitrinKaplamasi.Select(Mathf.Max(kaplama, 0));
        }

        private void Baslat()
        {
            if (basladi)
                return;
            var hat = OyunSecimi.Hatlar[OyunSecimi.SeciliHat];
            if (!Application.CanStreamedLevelBeLoaded(hat.sahne))
            {
                Debug.LogWarning("[AnaMenu] Sahne Build Settings'te yok: " + hat.sahne);
                return;
            }
            basladi = true;
            StartCoroutine(Yukle(hat));
        }

        private IEnumerator Yukle(OyunSecimi.Hat hat)
        {
            yukleniyor.SetActive(true);
            yukleniyorYazi.text = $"HAT {hat.numara}\n{hat.ad}\n\nYükleniyor…";
            yield return null; // perde çizilsin
            var islem = SceneManager.LoadSceneAsync(hat.sahne);
            while (!islem.isDone)
            {
                yukleniyorYazi.text = $"HAT {hat.numara}\n{hat.ad}\n\nYükleniyor… %{Mathf.RoundToInt(Mathf.Clamp01(islem.progress / 0.9f) * 100f)}";
                yield return null;
            }
        }

        private void Bolum(RectTransform root, string ad, float y)
        {
            Label(Element(root, "Bolum_" + ad, new Vector2(0f, 1f), new Vector2(470f, y), new Vector2(860f, 40f)),
                ad, 30, TextAnchor.MiddleLeft, Active);
        }

        private Image Dugme(RectTransform root, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var rect = Box(root, label, anchor, position, size, Panel);
            var image = rect.GetComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Label(rect, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            return image;
        }

        private static RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rect = Element(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiShapes.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;
            return rect;
        }

        private static RectTransform Element(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(RectTransform parent, string text, int size, TextAnchor alignment, Color color)
        {
            var rect = Element(parent, "Yazi", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }
    }
}
