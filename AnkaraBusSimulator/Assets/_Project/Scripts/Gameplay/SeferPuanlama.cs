using System;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Gameplay
{
    /// <summary>
    /// Sefer puanı ve kasa. Otobüsün üzerinde durur, diğer sistemleri yalnızca okur:
    /// binen her yolcu bilet (Kentkart) parası ve puan getirir; durağa düzgün yanaşmak, zamanında bitirmek
    /// puan kazandırır; sert fren/kalkış/viraj (yolcu çoksa daha çok), hız sınırını aşmak, kırmızıda geçmek,
    /// çarpmak ve durak atlamak puan kaybettirir. Hat bitince SeferBitti ile özet verilir.
    /// Ekran: UI/PuanGostergesi. Ayrıntı: docs/PUANLAMA.md
    /// </summary>
    [RequireComponent(typeof(BusVehicle))]
    public class SeferPuanlama : MonoBehaviour
    {
        [Serializable]
        public class Ozet
        {
            public string hat;
            public int puan;
            public float kasa;
            public int yolcu;
            public float sure;
            public float hedefSure;
            public int durak;
            public int atlananDurak;
            public int kirmiziIsik;
            public int sertSurus;
            public int hizIhlali;
            public int carpisma;
            public float konfor;
            public int yildiz;
            public int enIyiPuan;
            public bool rekor;
        }

        [Header("Bilet (Kentkart)")]
        [Tooltip("Tam bilet ücreti (TL). Oyun değeri, gerçek tarifeye göre değiştirilebilir.")]
        [SerializeField] private float tamBilet = 20f;
        [SerializeField] private float indirimliBilet = 10f;
        [Range(0f, 1f)] [SerializeField] private float indirimliOrani = 0.3f;
        [SerializeField] private int biletPuani = 5;

        [Header("Duraklar")]
        [SerializeField] private int durakPuani = 20;
        [Tooltip("Durak merkezine tam yanaşınca eklenen en yüksek puan.")]
        [SerializeField] private int hassasiyetPuani = 30;
        [SerializeField] private int atlamaCezasi = 100;

        [Header("Sürüş konforu (m/sn²)")]
        [SerializeField] private float sertFren = 3f;
        [Tooltip("Bu hızın altında sert fren sayılmaz: duruşun son anında süspansiyon geri yaylanınca ölçülen kısa sıçrama.")]
        [SerializeField] private float sertFrenMinHizKmh = 10f;
        [SerializeField] private float sertKalkis = 1.8f;
        [SerializeField] private float sertViraj = 2.5f;
        [SerializeField] private int konforCezasi = 10;

        [Header("Kurallar")]
        [SerializeField] private float hizSiniriKmh = 50f;
        [SerializeField] private float hizToleransKmh = 5f;
        [Tooltip("Her 3 sn hız sınırı aşımı için ceza.")]
        [SerializeField] private int hizCezasi = 6;
        [SerializeField] private int kirmiziCezasi = 50;
        [Tooltip("Çarpışma cezası: taban + hız (m/sn) × çarpan, en çok 100.")]
        [SerializeField] private int carpismaCezasi = 15;

        [Header("Zaman")]
        [Tooltip("Hedef süre: duraklar arası düz mesafe bu ortalama hızla + durak başına bekleme.")]
        [SerializeField] private float hedefOrtalamaKmh = 20f;
        [SerializeField] private float durakBasinaSaniye = 40f;
        [SerializeField] private int zamanPuani = 100;

        private BusVehicle vehicle;
        private Rigidbody body;
        private RouteTracker tracker;
        private BusPassengers passengers;
        private TrafficLane[] signalLanes = Array.Empty<TrafficLane>();
        private float[] lastSide = Array.Empty<float>();

        private Vector3 lastVelocity;
        private Vector3 smoothAccel;
        private float ignoreAccelUntil;
        private float lastRedPenalty = -10f;
        private float nextBrake, nextLaunch, nextCorner;
        private float speedingTime;
        private float lastCollision = -10f;
        private int lastOnboard;
        private float startTime = -1f;
        private Ozet ozet = new Ozet();
        private bool finished;

        public int Puan => ozet.puan;
        public float Kasa => ozet.kasa;
        public int Yolcu => ozet.yolcu;
        public float Konfor => ozet.konfor;
        public bool Bitti => finished;
        /// <summary>Kırmızıda geçiş vb. anında ceza mesajı için: (açıklama, puan).</summary>
        public event Action<string, int> Puanlandi;
        public event Action<Ozet> SeferBitti;

        private void Awake()
        {
            vehicle = GetComponent<BusVehicle>();
            body = GetComponent<Rigidbody>();
            tracker = GetComponent<RouteTracker>();
            ozet.konfor = 100f;
        }

        private void OnEnable()
        {
            if (tracker == null)
                return;
            tracker.StopServed += OnStopServed;
            tracker.StopSkipped += OnStopSkipped;
            tracker.RouteCompleted += OnRouteCompleted;
        }

        private void OnDisable()
        {
            if (tracker == null)
                return;
            tracker.StopServed -= OnStopServed;
            tracker.StopSkipped -= OnStopSkipped;
            tracker.RouteCompleted -= OnRouteCompleted;
            if (passengers != null)
                passengers.OnboardChanged -= OnOnboardChanged;
        }

        private void Start()
        {
            var lanes = FindObjectsByType<TrafficLane>();
            signalLanes = Array.FindAll(lanes, l => l.HasSignal);
            lastSide = new float[signalLanes.Length];
            for (int i = 0; i < signalLanes.Length; i++)
                lastSide[i] = float.NaN;
            ozet.hat = tracker != null && tracker.Route != null ? tracker.Route.LineNumber : "";
            ozet.hedefSure = TargetTime();
            ignoreAccelUntil = Time.time + 1.5f;
            lastVelocity = body.linearVelocity;
        }

        private void Update()
        {
            if (finished)
                return;
            // yolcu sistemi otobüse çalışma anında eklenir
            if (passengers == null && (passengers = GetComponent<BusPassengers>()) != null)
            {
                lastOnboard = passengers.Onboard;
                passengers.OnboardChanged += OnOnboardChanged;
            }
            // süre ilk hareketle başlar
            if (startTime < 0f && vehicle.SpeedKmh > 2f)
                startTime = Time.time;

            CheckSpeed();
            CheckSignals();
        }

        private void FixedUpdate()
        {
            if (finished)
                return;
            Vector3 v = body.linearVelocity;
            Vector3 a = (v - lastVelocity) / Time.fixedDeltaTime;
            lastVelocity = v;
            // tek karelik sıçramalar (tümsek, tekerlek teması) sayılmasın
            smoothAccel = Vector3.Lerp(smoothAccel, a, 1f - Mathf.Exp(-Time.fixedDeltaTime / 0.35f));
            if (Time.time < ignoreAccelUntil)
                return;

            float along = Vector3.Dot(smoothAccel, transform.forward);
            float side = Mathf.Abs(Vector3.Dot(smoothAccel, transform.right));
            bool movingForward = Vector3.Dot(v, transform.forward) > 0.5f;
            if (movingForward && along < -sertFren && vehicle.SpeedKmh > sertFrenMinHizKmh && Time.time >= nextBrake)
            {
                Comfort("Sert fren", 1f);
                nextBrake = Time.time + 2.5f;
            }
            else if (along > sertKalkis && Time.time >= nextLaunch)
            {
                Comfort("Sert kalkış", 0.5f);
                nextLaunch = Time.time + 2.5f;
            }
            if (side > sertViraj && Time.time >= nextCorner)
            {
                Comfort("Sert viraj", 0.8f);
                nextCorner = Time.time + 2.5f;
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (finished || Time.time - lastCollision < 1f)
                return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.5f)
                return;
            // zemine ve kaldırıma sürtünme çarpışma sayılmaz
            bool wall = false;
            for (int i = 0; i < c.contactCount; i++)
                if (Mathf.Abs(c.GetContact(i).normal.y) < 0.6f)
                    wall = true;
            if (!wall)
                return;
            lastCollision = Time.time;
            ignoreAccelUntil = Time.time + 1f;
            ozet.carpisma++;
            Add("Çarpışma", -Mathf.Min(100, carpismaCezasi + Mathf.RoundToInt(speed * 5f)));
        }

        private void Comfort(string reason, float weight)
        {
            // ayakta yolcu çoksa sarsıntı daha çok şikâyet getirir
            int load = passengers != null ? passengers.Onboard : 0;
            float factor = 1f + load / 30f;
            ozet.sertSurus++;
            ozet.konfor = Mathf.Max(0f, ozet.konfor - 4f * weight * factor);
            Add(reason, -Mathf.RoundToInt(konforCezasi * weight * factor));
        }

        private void CheckSpeed()
        {
            if (vehicle.SpeedKmh > hizSiniriKmh + hizToleransKmh)
            {
                speedingTime += Time.deltaTime;
                if (speedingTime >= 3f)
                {
                    speedingTime -= 3f;
                    ozet.hizIhlali++;
                    Add($"Hız sınırı ({hizSiniriKmh:0} km/s)", -hizCezasi);
                }
            }
            else
                speedingTime = Mathf.Min(speedingTime, 2.5f);
        }

        /// <summary>Otobüsün önü kırmızı ışıklı bir şeridin durma çizgisini aynı yönde geçti mi?</summary>
        private void CheckSignals()
        {
            Vector3 front = transform.position + transform.forward * 5.9f;
            for (int i = 0; i < signalLanes.Length; i++)
            {
                var lane = signalLanes[i];
                lane.Sample(lane.StopLine, out var linePos, out var lineFwd);
                Vector3 offset = front - linePos;
                offset.y = 0f;
                float ahead = Vector3.Dot(offset, lineFwd);
                float lateral = Vector3.Dot(offset, Vector3.Cross(Vector3.up, lineFwd));
                bool relevant = Mathf.Abs(lateral) < 7f && Mathf.Abs(ahead) < 15f
                                && Vector3.Dot(transform.forward, lineFwd) > 0.7f;
                float side = relevant ? Mathf.Sign(ahead) : float.NaN;
                // Yan yana şeritlerin durma çizgileri aynı geçişte birden çok kez sayılmasın
                if (lastSide[i] < 0f && side > 0f && lane.IsRed && Time.time - lastRedPenalty > 3f)
                {
                    lastRedPenalty = Time.time;
                    ozet.kirmiziIsik++;
                    Add("Kırmızı ışık", -kirmiziCezasi);
                }
                lastSide[i] = side;
            }
        }

        private void OnOnboardChanged(int count)
        {
            int boarded = count - lastOnboard;
            lastOnboard = count;
            if (finished)
                return;
            for (int i = 0; i < boarded; i++)
            {
                bool indirimli = UnityEngine.Random.value < indirimliOrani;
                ozet.kasa += indirimli ? indirimliBilet : tamBilet;
                ozet.yolcu++;
                Add(indirimli ? "İndirimli bilet" : "Tam bilet", biletPuani);
            }
        }

        private void OnStopServed(BusStop stop)
        {
            ozet.durak++;
            float accuracy = 1f - Mathf.Clamp01(stop.HorizontalDistance(transform.position) / Mathf.Max(stop.Radius, 0.1f));
            Add($"{stop.StopName} durağı", durakPuani + Mathf.RoundToInt(hassasiyetPuani * accuracy));
        }

        private void OnStopSkipped(BusStop stop)
        {
            ozet.atlananDurak++;
            Add($"{stop.StopName} atlandı", -atlamaCezasi);
        }

        private void OnRouteCompleted()
        {
            if (finished)
                return;
            ozet.sure = startTime < 0f ? 0f : Time.time - startTime;
            float late = Mathf.Max(0f, ozet.sure - ozet.hedefSure);
            int bonus = Mathf.Max(0, zamanPuani - Mathf.RoundToInt(late / 3f));
            if (bonus > 0)
                Add(late <= 0f ? "Zamanında" : "Gecikmeli bitiş", bonus);
            ozet.puan += Mathf.RoundToInt(ozet.konfor); // konfor puanı (en çok 100)
            ozet.yildiz = Stars();

            string key = OyunSecimi.EnIyiPuanAnahtari(ozet.hat);
            ozet.enIyiPuan = PlayerPrefs.GetInt(key, int.MinValue);
            ozet.rekor = ozet.puan > ozet.enIyiPuan;
            if (ozet.rekor)
            {
                PlayerPrefs.SetInt(key, ozet.puan);
                PlayerPrefs.Save();
                ozet.enIyiPuan = ozet.puan;
            }
            finished = true;
            SeferBitti?.Invoke(ozet);
        }

        private int Stars()
        {
            int stars = 5;
            stars -= ozet.atlananDurak * 2;
            stars -= ozet.kirmiziIsik;
            stars -= ozet.carpisma;
            if (ozet.konfor < 80f) stars--;
            if (ozet.konfor < 50f) stars--;
            if (ozet.sure > ozet.hedefSure * 1.3f) stars--;
            return Mathf.Clamp(stars, 1, 5);
        }

        private float TargetTime()
        {
            if (tracker == null || tracker.Route == null || tracker.Route.StopCount == 0)
                return 0f;
            float meters = 0f;
            Vector3 p = transform.position;
            for (int i = 0; i < tracker.Route.StopCount; i++)
            {
                var stop = tracker.Route.GetStop(i);
                if (stop == null)
                    continue;
                meters += Vector3.Distance(p, stop.transform.position);
                p = stop.transform.position;
            }
            return meters / (hedefOrtalamaKmh / 3.6f) + durakBasinaSaniye * tracker.Route.StopCount;
        }

        private void Add(string reason, int points)
        {
            ozet.puan += points;
            Puanlandi?.Invoke(reason, points);
        }
    }
}
