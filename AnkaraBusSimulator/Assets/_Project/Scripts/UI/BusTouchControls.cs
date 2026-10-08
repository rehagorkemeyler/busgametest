using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Mobil sürüş arayüzünü kodla kurar ve BusInput'a bağlar:
    /// solda sanal direksiyon, sağda fren ve gaz pedalı, D/N/R, el freni, kapı ve kamera butonları,
    /// üstte hız/vites göstergesi.
    /// </summary>
    public class BusTouchControls : MonoBehaviour
    {
        [SerializeField] private BusInput input;
        [SerializeField] private BusCameraRig cameraRig;

        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.55f);
        private static readonly Color Idle = new Color(0.85f, 0.87f, 0.9f, 0.9f);
        private static readonly Color Active = new Color(0.98f, 0.76f, 0.18f, 1f);
        private static readonly Color Warning = new Color(0.9f, 0.22f, 0.2f, 1f);
        private static readonly Color Ok = new Color(0.3f, 0.8f, 0.45f, 1f);

        private TouchSteeringWheel wheel;
        private TouchPedal throttle;
        private TouchPedal brake;
        private Image driveButton, neutralButton, reverseButton, handbrakeButton, doorButton;
        private Text gauge;
        private Text status;
        private BusDoorController doors;
        private Font font;
        private GameObject settingsPanel;
        private readonly Image[] qualityButtons = new Image[GrafikAyarlari.SeviyeAdlari.Length];

        public void Bind(BusInput busInput, BusCameraRig rig)
        {
            input = busInput;
            cameraRig = rig;
            doors = input != null ? input.GetComponentInChildren<BusDoorController>() : null;
        }

        private void Awake()
        {
            if (input == null)
                input = FindAnyObjectByType<BusInput>();
            if (cameraRig == null)
                cameraRig = FindAnyObjectByType<BusCameraRig>();
            Bind(input, cameraRig);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            Build();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void Build()
        {
            var canvasGo = new GameObject("SurusArayuzu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // Direksiyon
            var wheelRect = Element(root, "Direksiyon", new Vector2(0f, 0f), new Vector2(330f, 300f), new Vector2(460f, 460f));
            var wheelImage = wheelRect.gameObject.AddComponent<Image>();
            wheelImage.sprite = UiShapes.SteeringWheel;
            wheelImage.color = Idle;
            wheel = wheelRect.gameObject.AddComponent<TouchSteeringWheel>();

            // Pedallar
            brake = Pedal(root, "Fren", new Vector2(-430f, 210f), new Vector2(190f, 300f), Warning);
            throttle = Pedal(root, "Gaz", new Vector2(-190f, 240f), new Vector2(190f, 360f), Ok);

            // Vites seçici
            driveButton = Button(root, "D", new Vector2(1f, 1f), new Vector2(-330f, -90f), new Vector2(110f, 110f), () => input.SelectDrive());
            neutralButton = Button(root, "N", new Vector2(1f, 1f), new Vector2(-210f, -90f), new Vector2(110f, 110f), () => input.SelectNeutral());
            reverseButton = Button(root, "R", new Vector2(1f, 1f), new Vector2(-90f, -90f), new Vector2(110f, 110f), () => input.SelectReverse());

            handbrakeButton = Button(root, "EL FRENİ", new Vector2(1f, 0.5f), new Vector2(-150f, 120f), new Vector2(240f, 110f), () => input.ToggleHandbrake());
            doorButton = Button(root, "KAPILAR", new Vector2(1f, 0.5f), new Vector2(-150f, -10f), new Vector2(240f, 110f), () => input.ToggleDoors());
            Button(root, "KAMERA", new Vector2(0f, 1f), new Vector2(130f, -80f), new Vector2(200f, 90f), () => { if (cameraRig != null) cameraRig.Toggle(); });
            Button(root, "AYARLAR", new Vector2(0f, 1f), new Vector2(130f, -185f), new Vector2(200f, 90f), () => settingsPanel.SetActive(!settingsPanel.activeSelf));

            // Korna: basılı tutuldukça çalar
            var horn = Button(root, "KORNA", new Vector2(0f, 1f), new Vector2(130f, -290f), new Vector2(200f, 90f), () => { });
            var hornTrigger = horn.gameObject.AddComponent<EventTrigger>();
            AddTrigger(hornTrigger, EventTriggerType.PointerDown, () => input.TouchHorn = true);
            AddTrigger(hornTrigger, EventTriggerType.PointerUp, () => input.TouchHorn = false);
            AddTrigger(hornTrigger, EventTriggerType.PointerExit, () => input.TouchHorn = false);

            // Gösterge
            var gaugeRect = Element(root, "Gosterge", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(520f, 110f));
            gaugeRect.gameObject.AddComponent<Image>().sprite = UiShapes.RoundedRect;
            gaugeRect.GetComponent<Image>().type = Image.Type.Sliced;
            gaugeRect.GetComponent<Image>().color = Panel;
            gauge = Label(gaugeRect, "", 54, TextAnchor.MiddleCenter);
            var statusRect = Element(root, "Durum", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 50f));
            status = Label(statusRect, "", 30, TextAnchor.MiddleCenter);

            BuildSettingsPanel(root);
        }

        /// <summary>Görüntü kalitesi seçimi: Düşük / Normal / Yüksek.</summary>
        private void BuildSettingsPanel(RectTransform root)
        {
            // Arkadaki kontrollere dokunulmasın diye ekranı kaplayan yarı saydam zemin
            var blocker = Element(root, "AyarlarPaneli", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            settingsPanel = blocker.gameObject;

            var box = Element(blocker, "Kutu", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 500f));
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.sprite = UiShapes.RoundedRect;
            boxImage.type = Image.Type.Sliced;
            boxImage.color = new Color(0.1f, 0.11f, 0.13f, 0.96f);

            Label(Element(box, "Baslik", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 70f)),
                "GÖRÜNTÜ KALİTESİ", 46, TextAnchor.MiddleCenter);

            for (int i = 0; i < qualityButtons.Length; i++)
            {
                var level = (KaliteSeviyesi)i;
                qualityButtons[i] = Button(box, GrafikAyarlari.SeviyeAdlari[i], new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * 290f, 20f), new Vector2(260f, 140f), () => GrafikAyarlari.Uygula(level));
            }

            var hint = Label(Element(box, "Oneri", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(900f, 50f)),
                $"Bu telefon için önerilen: {GrafikAyarlari.SeviyeAdlari[(int)GrafikAyarlari.Onerilen]}", 30, TextAnchor.MiddleCenter);
            hint.fontStyle = FontStyle.Normal;
            hint.color = Idle;

            Button(box, "KAPAT", new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(260f, 90f), () => settingsPanel.SetActive(false));
            settingsPanel.SetActive(false);
        }

        private TouchPedal Pedal(RectTransform root, string label, Vector2 position, Vector2 size, Color color)
        {
            var rect = Element(root, label, new Vector2(1f, 0f), position, size);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = UiShapes.RoundedRect;
            background.type = Image.Type.Sliced;
            background.color = Panel;

            var fillRect = Element(rect, "Dolgu", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = UiShapes.RoundedRect;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillAmount = 0f;
            fill.color = color;
            fill.raycastTarget = false;

            Label(rect, label.ToUpperInvariant(), 34, TextAnchor.LowerCenter).rectTransform.offsetMin = new Vector2(0f, 16f);
            var pedal = rect.gameObject.AddComponent<TouchPedal>();
            pedal.Fill = fill;
            return pedal;
        }

        private Image Button(RectTransform root, string label, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rect = Element(root, label, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiShapes.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = Panel;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Label(rect, label, size.y > 100f ? 40 : 30, TextAnchor.MiddleCenter);
            return image;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
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

        private Text Label(RectTransform parent, string text, int size, TextAnchor alignment)
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
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void Update()
        {
            if (input == null)
                return;

            input.TouchThrottle = throttle.Value;
            input.TouchBrake = brake.Value;
            input.TouchSteer = wheel.IsHeld || Mathf.Abs(wheel.Value) > 0.001f ? wheel.Value : (float?)null;

            var v = input.Vehicle;
            string gear = v.Selector switch
            {
                BusVehicle.GearSelector.Drive => "D" + v.Gear,
                BusVehicle.GearSelector.Reverse => "R",
                _ => "N",
            };
            gauge.text = $"{Mathf.RoundToInt(v.SpeedKmh)} km/s   {gear}   {Mathf.RoundToInt(v.EngineRpm / 10f) * 10} d/d";

            status.text = v.DoorBrakeActive ? "DURAK FRENİ"
                : v.Handbrake ? "EL FRENİ ÇEKİLİ"
                : v.HillHoldActive ? "YOKUŞ DESTEĞİ"
                : "";
            status.color = v.DoorBrakeActive || v.Handbrake ? Warning : Active;

            driveButton.color = v.Selector == BusVehicle.GearSelector.Drive ? Active : Panel;
            neutralButton.color = v.Selector == BusVehicle.GearSelector.Neutral ? Active : Panel;
            reverseButton.color = v.Selector == BusVehicle.GearSelector.Reverse ? Active : Panel;
            handbrakeButton.color = v.Handbrake ? Warning : Panel;
            doorButton.color = doors != null && doors.AnyOpen ? Ok : Panel;

            if (settingsPanel.activeSelf)
                for (int i = 0; i < qualityButtons.Length; i++)
                    qualityButtons[i].color = (int)GrafikAyarlari.Mevcut == i ? Active : Panel;
        }
    }
}
