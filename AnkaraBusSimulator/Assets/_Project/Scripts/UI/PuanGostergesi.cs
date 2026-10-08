using System.Collections.Generic;
using AnkaraBus.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Puan arayüzünü kodla kurar: üstte puan/kasa/konfor şeridi, altında kısa süreli puan mesajları
    /// ("+5 Tam bilet", "-10 Sert fren"), hat bitince ortada sefer özeti (yıldız, ayrıntılar, rekor).
    /// Otobüs prefabında SeferPuanlama'nın yanında durur (OtobusKurucu ekler).
    /// </summary>
    [RequireComponent(typeof(SeferPuanlama))]
    public class PuanGostergesi : MonoBehaviour
    {
        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.55f);
        private static readonly Color PanelDark = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        private static readonly Color Plus = new Color(0.45f, 0.9f, 0.55f, 1f);
        private static readonly Color Minus = new Color(1f, 0.42f, 0.38f, 1f);
        private static readonly Color Gold = new Color(0.98f, 0.76f, 0.18f, 1f);

        private const int MaxMessages = 4;
        private const float MessageSeconds = 2.5f;

        private SeferPuanlama puanlama;
        private Font font;
        private GameObject canvasGo;
        private Text bar;
        private RectTransform messageRoot;
        private readonly List<Message> messages = new List<Message>();

        private class Message
        {
            public Text text;
            public string reason;
            public int total;
            public float until;
        }

        private void Awake()
        {
            puanlama = GetComponent<SeferPuanlama>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            puanlama.Puanlandi += OnScored;
            puanlama.SeferBitti += OnFinished;
        }

        private void OnDisable()
        {
            puanlama.Puanlandi -= OnScored;
            puanlama.SeferBitti -= OnFinished;
        }

        private void Start()
        {
            canvasGo = new GameObject("PuanArayuzu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // dokunmatik göstergenin (üst orta) hemen altı
            var barRect = Box(root, "PuanSeridi", new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(620f, 56f), Panel);
            bar = Label(barRect, "", 28, TextAnchor.MiddleCenter, Color.white);

            // BusTouchControls'un durum yazısının (y -215) altında
            messageRoot = Element(root, "Mesajlar", new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(620f, 0f));
        }

        private void OnDestroy()
        {
            if (canvasGo != null)
                Destroy(canvasGo);
        }

        private void Update()
        {
            if (bar == null)
                return;
            bar.text = $"PUAN {puanlama.Puan}    KASA {puanlama.Kasa:0} TL    KONFOR %{Mathf.RoundToInt(puanlama.Konfor)}";

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var m = messages[i];
                float left = m.until - Time.time;
                if (left <= 0f)
                {
                    Destroy(m.text.transform.parent.gameObject);
                    messages.RemoveAt(i);
                    continue;
                }
                var c = m.text.color;
                c.a = Mathf.Clamp01(left / 0.5f);
                m.text.color = c;
            }
            for (int i = 0; i < messages.Count; i++)
                ((RectTransform)messages[i].text.transform.parent).anchoredPosition = new Vector2(0f, -i * 46f);
        }

        private void OnScored(string reason, int points)
        {
            if (messageRoot == null || points == 0)
                return;
            // aynı mesaj üst üste gelirse (ör. arka arkaya binen yolcular) birleştir
            Message m;
            if (messages.Count > 0 && messages[0].reason == reason)
            {
                m = messages[0];
                m.total += points;
            }
            else
            {
                var rect = Element(messageRoot, "Mesaj", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(620f, 42f));
                rect.pivot = new Vector2(0.5f, 1f);
                m = new Message { reason = reason, total = points, text = Label(rect, "", 30, TextAnchor.MiddleCenter, Color.white) };
                m.text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                messages.Insert(0, m);
                while (messages.Count > MaxMessages)
                {
                    Destroy(messages[messages.Count - 1].text.transform.parent.gameObject);
                    messages.RemoveAt(messages.Count - 1);
                }
            }
            m.text.text = $"{(m.total > 0 ? "+" : "")}{m.total} {reason}";
            m.text.color = m.total >= 0 ? Plus : Minus;
            m.until = Time.time + MessageSeconds;
        }

        private void OnFinished(SeferPuanlama.Ozet o)
        {
            if (canvasGo == null)
                return;
            EnsureEventSystem();
            var root = (RectTransform)canvasGo.transform;
            var panel = Box(root, "SeferOzeti", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 720f), PanelDark);

            var title = Element(panel, "Baslik", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 70f));
            Label(title, string.IsNullOrEmpty(o.hat) ? "SEFER TAMAMLANDI" : $"HAT {o.hat} TAMAMLANDI", 44, TextAnchor.MiddleCenter, Color.white);

            for (int i = 0; i < 5; i++)
            {
                var star = Element(panel, "Yildiz", new Vector2(0.5f, 1f), new Vector2((i - 2) * 76f, -140f), new Vector2(64f, 64f));
                var image = star.gameObject.AddComponent<Image>();
                image.sprite = UiShapes.Star;
                image.color = i < o.yildiz ? Gold : new Color(1f, 1f, 1f, 0.15f);
                image.raycastTarget = false;
            }

            int minutes = Mathf.FloorToInt(o.sure / 60f), seconds = Mathf.FloorToInt(o.sure % 60f);
            int targetMin = Mathf.FloorToInt(o.hedefSure / 60f), targetSec = Mathf.FloorToInt(o.hedefSure % 60f);
            string details =
                $"Puan\t{o.puan}{(o.rekor ? "   YENİ REKOR!" : $"   (en iyi {o.enIyiPuan})")}\n" +
                $"Kasa\t{o.kasa:0} TL ({o.yolcu} yolcu)\n" +
                $"Süre\t{minutes}:{seconds:00} (hedef {targetMin}:{targetSec:00})\n" +
                $"Duraklar\t{o.durak} yanaşıldı, {o.atlananDurak} atlandı\n" +
                $"Konfor\t%{Mathf.RoundToInt(o.konfor)} ({o.sertSurus} sert hareket)\n" +
                $"Kırmızı ışık\t{o.kirmiziIsik}\n" +
                $"Hız ihlali\t{o.hizIhlali}\n" +
                $"Çarpışma\t{o.carpisma}";
            var body = Element(panel, "Ayrinti", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(620f, 380f));
            var text = Label(body, details.Replace("\t", ":  "), 30, TextAnchor.UpperLeft, Color.white);
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = 1.15f;

            bool menu = OyunSecimi.MenuVar;
            float x = menu ? 240f : 150f;
            Button(panel, "TEKRAR", new Vector2(-x, 70f), () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            if (menu)
                Button(panel, "MENÜ", new Vector2(0f, 70f), OyunSecimi.AnaMenuyeDon);
            Button(panel, "KAPAT", new Vector2(x, 70f), () => Destroy(panel.gameObject));
        }

        private void Button(RectTransform panel, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var rect = Box(panel, label, new Vector2(0.5f, 0f), position, new Vector2(220f, 90f), Panel);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            Label(rect, label, 34, TextAnchor.MiddleCenter, Color.white);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
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
