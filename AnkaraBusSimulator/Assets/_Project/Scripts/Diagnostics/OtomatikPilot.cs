using System.Collections;
using System.Collections.Generic;
using System.Text;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Hattı baştan sona kendisi süren test pilotu. Güzergâh, sıralı şerit adlarından ve şeritlerin
    /// çıkışlarından (TrafficLane.Exits) kurulur; sahnede olmayan şeritler atlanır (Hat 1: bulvar → kavşak
    /// bağlantısı → Cinnah, Hat 2: yalnızca bulvar). Öndeki araca göre yavaşlar, kırmızı ışıkta durur,
    /// her durakta kapıları açıp yolcu iniş-binişini ve RouteTracker'ın durağı tamamlamasını bekler.
    /// Yolcu, trafik ışığı ve dönüş sayıları "[Surus]" satırları olarak loga yazılır.
    /// Yalnızca test build'lerinde ve SurusTestiEditor'de sahneye eklenir.
    /// </summary>
    public class OtomatikPilot : MonoBehaviour
    {
        [SerializeField] private string[] seritler = { "Serit_Bulvar_Gidis_3", "Serit_Baglanti_Bulvar_Cinnah", "Serit_Cinnah_Gidis_2" };
        [Tooltip("Şerit hız sınırının bu oranıyla gidilir.")]
        [SerializeField] private float hizOrani = 0.9f;
        [SerializeField] private float rahatYavaslama = 1.3f;
        [Tooltip("Bir yerde bu kadar saniye kalırsa (durak ve kırmızı ışık hariç) takıldı sayılır.")]
        [SerializeField] private float takilmaSuresi = 45f;
        [Tooltip("Hat bitince yüklenecek sahne (boşsa durur).")]
        [SerializeField] private string sonrakiSahne;

        [Header("Senaryo (testler)")]
        [Tooltip("Gaz pedalının en fazla basılacağı oran (1 = tam gaz).")]
        [SerializeField] private float maxGaz = 1f;
        [Tooltip("Bu sıradaki durakta durmadan geç (-1: hiçbiri).")]
        [SerializeField] private int atlanacakDurak = -1;
        [Tooltip("Kırmızı ışıkta bilerek geç (yeşilse çizgide kırmızıyı bekler).")]
        [SerializeField] private bool kirmizidaGec;
        [SerializeField] private float baslamaGecikmesi = 3f;

        // otobüsün ön ucu ve dingil mesafesi (Start'ta ölçülür; BMC: 6 / 5,6 m)
        private float OnUzunluk = 6f;
        private float dingil = 5.6f;

        private readonly List<Vector3> yol = new List<Vector3>();
        private readonly List<float> yolMesafe = new List<float>();
        private readonly List<float> yolHiz = new List<float>();
        private readonly List<(float mesafe, TrafficLane serit)> durmaCizgileri = new List<(float, TrafficLane)>();

        private BusVehicle bus;
        private BusInput input;
        private BusDoorController doors;
        private RouteTracker tracker;
        private BusPassengers passengers;
        private Collider[] ownColliders;
        private float ilerleme;
        private int mevcutIndex;
        private string sonEngel;
        private float engelBaslangic = -1f;
        private float baslangic;
        private bool kirmizidaBekliyor;

        // Trafik gözlemi
        private TrafficCar[] trafik;
        private TrafficSignal[] isiklar;
        private readonly Dictionary<TrafficCar, (TrafficLane serit, float mesafe, bool kirmizida)> aracDurumu =
            new Dictionary<TrafficCar, (TrafficLane, float, bool)>();
        private readonly SortedDictionary<string, int> donusler = new SortedDictionary<string, int>();
        private readonly Dictionary<TrafficSignal, TrafficSignal.SignalState[]> isikDurumu =
            new Dictionary<TrafficSignal, TrafficSignal.SignalState[]>();
        private int isikDegisimi;
        private int kirmizidaDuran;
        private int aracIcinYavaslama;
        private bool aracOnde;

        // Yolcu gözlemi
        private int binen, inen;

        public void Senaryo(float gaz, int atla, bool kirmizi, float gecikme)
        {
            maxGaz = gaz;
            atlanacakDurak = atla;
            kirmizidaGec = kirmizi;
            baslamaGecikmesi = gecikme;
        }

        public void Configure(string[] seritSirasi, string sonraki)
        {
            if (seritSirasi != null && seritSirasi.Length > 0)
                seritler = seritSirasi;
            sonrakiSahne = sonraki;
        }

        private IEnumerator Start()
        {
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            doors = bus.GetComponentInChildren<BusDoorController>();
            tracker = bus.GetComponent<RouteTracker>();
            ownColliders = bus.GetComponentsInChildren<Collider>();
            var olcu = OtobusOlcusu.Olc(bus);
            OnUzunluk = olcu.On + 0.1f;
            var koruklu = bus.GetComponent<KorukluOtobus>();
            if (koruklu != null && koruklu.ArkaGovde != null)
                ownColliders = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(ownColliders,
                    koruklu.ArkaGovde.GetComponentsInChildren<Collider>()));
            dingil = DingilMesafesi(bus);
            if (!YoluKur())
                yield break;

            tracker.StopServed += stop => Debug.Log($"[Surus] DURAK {stop.StopName} tamamlandı, t={Time.time - baslangic:F0} sn");
            tracker.RouteCompleted += () => Debug.Log($"[Surus] HAT TAMAMLANDI, süre {Time.time - baslangic:F0} sn");

            // BusPassengers'ı PassengerManager ekler; birkaç kare bekle
            yield return new WaitForSeconds(baslamaGecikmesi);
            passengers = bus.GetComponent<BusPassengers>();
            int oncekiYolcu = passengers != null ? passengers.Onboard : 0;
            if (passengers != null)
                passengers.OnboardChanged += n =>
                {
                    if (n > oncekiYolcu) binen += n - oncekiYolcu; else inen += oncekiYolcu - n;
                    oncekiYolcu = n;
                };
            var puanlama = bus.GetComponent<AnkaraBus.Gameplay.SeferPuanlama>();
            if (puanlama != null)
                puanlama.SeferBitti += o => Debug.Log(
                    $"[Surus] PUAN puan={o.puan} yıldız={o.yildiz} kasa={o.kasa:F2} yolcu={o.yolcu} konfor={o.konfor:F0} " +
                    $"süre={o.sure:F0}/{o.hedefSure:F0} sn atlanan_durak={o.atlananDurak} kırmızı_ışık={o.kirmiziIsik} " +
                    $"sert_sürüş={o.sertSurus} hız_ihlali={o.hizIhlali} çarpışma={o.carpisma}");
            trafik = FindObjectsByType<TrafficCar>();
            isiklar = FindObjectsByType<TrafficSignal>();

            baslangic = Time.time;
            Debug.Log($"[Surus] BASLA sahne={SceneManager.GetActiveScene().name} hat={tracker.Route.LineNumber} " +
                      $"yol={yolMesafe[yolMesafe.Count - 1]:F0} m, durak={tracker.Route.StopCount}, trafik={trafik.Length} araç, " +
                      $"ışık={isiklar.Length}, yolcu_sistemi={(passengers != null ? "var" : "YOK")}");
            var kor = bus.GetComponent<KorukluOtobus>();
            Debug.Log($"[Surus] başlangıç konum={bus.transform.position} temas={Temaslar()}" +
                      (kor != null && kor.ArkaGovde != null ? $" arka_gövde={bus.transform.InverseTransformPoint(kor.ArkaGovde.position)} mafsal={kor.MafsalAcisi:F1}°" : ""));

            float sonIlerleme = 0f, sonIlerlemeZamani = Time.time;
            float logZamani = 0f;
            while (!tracker.Completed)
            {
                TrafigiGozle();
                var stop = tracker.NextStop;
                bool atla = tracker.NextStopIndex == atlanacakDurak;
                float stopMesafe = atla ? float.MaxValue : MesafeYolBoyunca(stop.transform.position);

                if (!atla && stop.Contains(bus.transform.position) && stopMesafe - ilerleme < 1.5f)
                {
                    yield return DuraktaBekle(stop);
                    sonIlerlemeZamani = Time.time;
                    continue;
                }

                Sur(stopMesafe);

                if (ilerleme > sonIlerleme + 5f || kirmizidaBekliyor)
                {
                    sonIlerleme = ilerleme;
                    sonIlerlemeZamani = Time.time;
                }
                else if (Time.time - sonIlerlemeZamani > takilmaSuresi)
                {
                    Debug.LogError($"[Surus] TAKILDI konum={bus.transform.position} ilerleme={ilerleme:F0} m hız={bus.SpeedKmh:F0} " +
                                   $"öndeki={sonEngel ?? "yok"} temas={Temaslar()} gaz={input.AutoThrottle:F2} fren={input.AutoBrake:F2} " +
                                   $"direksiyon={input.AutoSteer:F2} el_freni={bus.Handbrake} kapı_freni={bus.DoorBrakeActive} " +
                                   $"kapı_açık={bus.GetComponent<BusDoorController>()?.AnyOpen} yolcu_meşgul={bus.GetComponent<AnkaraBus.Passengers.BusPassengers>()?.IsBusy} " +
                                   $"vites={bus.Gear} rpm={bus.EngineRpm:F0} fren_basıncı={bus.BrakePressure:F2} tekerler={TekerDurumu()}");
                    sonIlerlemeZamani = Time.time;
                }

                if (Time.time > logZamani)
                {
                    logZamani = Time.time + 15f;
                    Debug.Log($"[Surus] t={Time.time - baslangic:F0} ilerleme={ilerleme:F0} m hız={bus.SpeedKmh:F0} vites={bus.Gear} " +
                              $"sıradaki={stop.StopName} yakın_araç={YakinHareketliArac()} araç_için_yavaşlama={aracIcinYavaslama} " +
                              $"ışık={IsikOzeti()}");
                }
                yield return null;
            }

            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
            Debug.Log($"[Surus] TRAFIK ışık_değişimi={isikDegisimi} kırmızıda_duran_araç={kirmizidaDuran} " +
                      $"dönüşler={(donusler.Count == 0 ? "yok" : string.Join(", ", Donusler()))}");
            Debug.Log($"[Surus] YOLCU toplam binen={binen} inen={inen} araçta={(passengers != null ? passengers.Onboard : 0)}");
            Debug.Log($"[Surus] BITTI süre={Time.time - baslangic:F0} sn");

            if (!string.IsNullOrEmpty(sonrakiSahne))
            {
                yield return new WaitForSeconds(3f);
                Debug.Log($"[Surus] SONRAKI {sonrakiSahne}");
                SceneManager.LoadScene(sonrakiSahne);
            }
        }

        private IEnumerable<string> Donusler()
        {
            foreach (var pair in donusler)
                yield return $"{pair.Key}={pair.Value}";
        }

        private bool YoluKur()
        {
            var bulunan = new Dictionary<string, TrafficLane>();
            foreach (var lane in FindObjectsByType<TrafficLane>())
                bulunan[lane.name] = lane;
            var sira = new List<TrafficLane>();
            foreach (var ad in seritler)
                if (bulunan.TryGetValue(ad, out var lane))
                    sira.Add(lane);
            if (sira.Count == 0)
            {
                Debug.LogError("[Surus] Şeritler bulunamadı: " + string.Join(", ", seritler));
                return false;
            }

            float basla = 0f;
            for (int i = 0; i < sira.Count; i++)
            {
                var lane = sira[i];
                float bitis = lane.Length;
                float sonrakiBasla = 0f;
                if (i + 1 < sira.Count)
                    foreach (var exit in lane.Exits)
                        if (exit.target == sira[i + 1])
                        {
                            bitis = exit.at;
                            sonrakiBasla = exit.targetAt;
                        }

                if (lane.StopLine >= basla && lane.StopLine < bitis)
                    durmaCizgileri.Add((OncekiMesafe() + (lane.StopLine - basla), lane));
                for (float d = basla; d < bitis; d += 2f)
                {
                    lane.Sample(d, out var p, out _);
                    Ekle(p, lane.SpeedLimitKmh * hizOrani);
                }
                basla = sonrakiBasla;
            }

            // Son durak şeridin bitişinin biraz ilerisinde olabilir (ör. dönüş halkası girişi)
            Vector3 son = yol[yol.Count - 1];
            Vector3 yon = Duz(son - yol[yol.Count - 2]).normalized;
            for (int i = 1; i <= 6; i++)
                Ekle(son + yon * (2f * i), 15f);

            // Virajlarda hız: önümüzdeki 25 m'deki yön değişimine göre
            for (int i = 1; i < yol.Count; i++)
            {
                int j = Mathf.Min(yol.Count - 1, i + 12);
                if (j <= i + 1) continue;
                float aci = Vector3.Angle(Duz(yol[i] - yol[i - 1]), Duz(yol[j] - yol[j - 1]));
                if (aci > 12f)
                    yolHiz[i] = Mathf.Min(yolHiz[i], Mathf.Lerp(30f, 15f, Mathf.InverseLerp(12f, 60f, aci)));
            }
            return true;
        }

        private float OncekiMesafe() => yolMesafe.Count == 0 ? 0f : yolMesafe[yolMesafe.Count - 1];

        private void Ekle(Vector3 p, float hizKmh)
        {
            float mesafe = yol.Count == 0 ? 0f : yolMesafe[yolMesafe.Count - 1] + Vector3.Distance(yol[yol.Count - 1], p);
            yol.Add(p);
            yolMesafe.Add(mesafe);
            yolHiz.Add(hizKmh);
        }

        private static Vector3 Duz(Vector3 v) { v.y = 0f; return v; }

        private float MesafeYolBoyunca(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < yol.Count; i++)
            {
                float d = Duz(yol[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return yolMesafe[best];
        }

        private void Sur(float stopMesafe)
        {
            int index = EnYakin(bus.transform.position);
            mevcutIndex = index;
            ilerleme = yolMesafe[index];
            float v = Mathf.Max(0f, bus.ForwardSpeed);

            // Pure pursuit
            float bakis = Mathf.Clamp(7f + v * 0.9f, 7f, 22f);
            int hedef = index;
            while (hedef < yol.Count - 1 && yolMesafe[hedef] - ilerleme < bakis)
                hedef++;
            Vector3 local = bus.transform.InverseTransformPoint(yol[hedef]);
            float alpha = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f));
            float wheelbase = dingil;
            float steerDeg = Mathf.Atan2(2f * wheelbase * Mathf.Sin(alpha), bakis) * Mathf.Rad2Deg;
            input.AutoSteer = Mathf.Clamp(steerDeg / Mathf.Max(bus.Spec.maxSteerAngle * 0.95f, 1f), -1f, 1f);

            // Hedef hız: yol hızı, durağa yaklaşma, kırmızı ışık, öndeki araç
            float hedefHiz = yolHiz[Mathf.Min(yol.Count - 1, index + 6)] / 3.6f;
            float durakKalan = stopMesafe - ilerleme;
            if (durakKalan > 0f)
                hedefHiz = Mathf.Min(hedefHiz, Mathf.Sqrt(2f * rahatYavaslama * Mathf.Max(0f, durakKalan - 0.5f)) + 0.6f);

            kirmizidaBekliyor = false;
            foreach (var (mesafe, serit) in durmaCizgileri)
            {
                float kalan = mesafe - ilerleme - OnUzunluk;
                if (kalan < -1f || kalan > 80f)
                    continue;
                bool durabilir = v * v / (2f * 3f) < kalan;
                if (kirmizidaGec)
                {
                    // Test: kırmızıda geç. Işık yeşilse çizgide kırmızıyı bekle, kırmızıysa dur(ma).
                    if (kalan < 40f && !serit.IsRed)
                    {
                        hedefHiz = Mathf.Min(hedefHiz, Mathf.Sqrt(2f * 1.8f * Mathf.Max(0f, kalan - 1f)));
                        kirmizidaBekliyor = kalan < 15f;
                    }
                    continue;
                }
                if (serit.MustStop(durabilir))
                {
                    hedefHiz = Mathf.Min(hedefHiz, Mathf.Sqrt(2f * 1.8f * Mathf.Max(0f, kalan - 1f)));
                    kirmizidaBekliyor = kalan < 15f;
                }
            }

            float onBos = OnundekiMesafe();
            bool engel = false;
            if (onBos < 60f)
            {
                float aracHizi = Mathf.Sqrt(2f * 2.5f * Mathf.Max(0f, onBos - 7f));
                engel = aracHizi < hedefHiz;
                hedefHiz = Mathf.Min(hedefHiz, aracHizi);
            }
            if (engel && !aracOnde)
                aracIcinYavaslama++;
            aracOnde = engel;
            // Öndeki araç (ör. kırmızıda bekleyen) yüzünden durmak bir süre takılma sayılmaz
            if (engel && onBos < 15f && bus.IsStopped)
            {
                if (engelBaslangic < 0f) engelBaslangic = Time.time;
                kirmizidaBekliyor |= Time.time - engelBaslangic < 60f;
            }
            else
            {
                engelBaslangic = -1f;
            }

            float fark = hedefHiz - v;
            input.AutoThrottle = fark > 0.3f ? Mathf.Min(maxGaz, Mathf.Clamp01(fark * 0.35f + 0.15f)) : 0f;
            input.AutoBrake = fark < -0.5f ? Mathf.Clamp01(-fark * 0.25f) : (hedefHiz < 0.2f ? (bus.IsStopped ? 0.6f : 0.3f) : 0f);
        }

        private int EnYakin(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < yol.Count; i++)
            {
                if (yolMesafe[i] < ilerleme - 20f) continue;
                if (yolMesafe[i] > ilerleme + 60f) break;
                float d = Duz(yol[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private float OnundekiMesafe()
        {
            var t = bus.transform;
            Vector3 origin = t.position + t.up * 1.5f + t.forward * (OnUzunluk + 0.5f);
            var hits = Physics.SphereCastAll(origin, 1.1f, t.forward, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            sonEngel = null;
            foreach (var hit in hits)
            {
                if (System.Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.rigidbody == null) continue; // yol ve binalar yok sayılır, araçlar Rigidbody'li
                // Yalnızca kendi güzergâhımızdaki araçlar: virajda karşı şeritte bekleyenleri yok say
                if (YolaUzaklik(hit.point, mevcutIndex, 40) > 2.2f) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    sonEngel = hit.collider.attachedRigidbody.name;
                }
            }
            return best;
        }

        /// <summary>Ön aks ile aynı gövdedeki arka aks arası (körüklüde ön gövdenin arka aksı).</summary>
        private static float DingilMesafesi(BusVehicle v)
        {
            float onZ = 0f, arkaZ = 0f;
            int on = 0, arka = 0;
            var body = v.GetComponent<Rigidbody>();
            foreach (var c in v.GetComponentsInChildren<WheelCollider>())
            {
                if (c.attachedRigidbody != body)
                    continue;
                float z = v.transform.InverseTransformPoint(c.transform.position).z;
                if (c.name.Contains("On")) { onZ += z; on++; }
                else { arkaZ += z; arka++; }
            }
            return on > 0 && arka > 0 ? Mathf.Abs(onZ / on - arkaZ / arka) : 5.6f;
        }

        /// <summary>Noktanın, önümüzdeki güzergâh parçasına yatay uzaklığı.</summary>
        private float YolaUzaklik(Vector3 p, int from, int count)
        {
            float best = float.MaxValue;
            int to = Mathf.Min(yol.Count - 1, from + count);
            for (int i = Mathf.Max(0, from - 2); i < to; i++)
            {
                Vector3 a = Duz(yol[i]), b = Duz(yol[i + 1]), q = Duz(p);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                best = Mathf.Min(best, Vector3.Distance(q, a + ab * t));
            }
            return best;
        }

        private IEnumerator DuraktaBekle(BusStop stop)
        {
            // Normal sürücü gibi: duruşa kadar hafif fren, durunca tam frenle tut
            input.AutoThrottle = 0f;
            input.AutoBrake = 0.3f;
            while (!bus.IsStopped)
                yield return null;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(0.5f);

            int bekleyen = PassengerManager.Instance != null ? PassengerManager.Instance.WaitingAt(stop).Count : -1;
            int binenOnce = binen, inenOnce = inen;
            int beklenen = tracker.NextStopIndex + 1;
            doors.ToggleAll();
            float t0 = Time.time;
            while (tracker.NextStopIndex < beklenen && Time.time - t0 < 90f)
            {
                TrafigiGozle();
                yield return null;
            }
            if (tracker.NextStopIndex < beklenen)
                Debug.LogError($"[Surus] {stop.StopName} durağı 90 sn'de tamamlanmadı (kapılar açık={doors.AnyOpen}, " +
                               $"iniş-biniş sürüyor={(passengers != null && passengers.IsBusy)})");
            Debug.Log($"[Surus] YOLCU {stop.StopName}: bekleyen={bekleyen} binen={binen - binenOnce} inen={inen - inenOnce} " +
                      $"araçta={(passengers != null ? passengers.Onboard : 0)} süre={Time.time - t0:F0} sn");
            if (doors.AnyOpen)
                doors.ToggleAll();
            yield return new WaitForSeconds(1.6f);
            input.AutoBrake = 0f;
        }

        // --- Trafik gözlemi ---

        private void TrafigiGozle()
        {
            foreach (var signal in isiklar)
            {
                if (signal == null) continue;
                var simdi = new[] { signal.State(0), signal.State(1) };
                if (isikDurumu.TryGetValue(signal, out var once) && (once[0] != simdi[0] || once[1] != simdi[1]))
                    isikDegisimi++;
                isikDurumu[signal] = simdi;
            }

            foreach (var car in trafik)
            {
                if (car == null || car.Lane == null) continue;
                var lane = car.Lane;
                bool varOnce = aracDurumu.TryGetValue(car, out var once);

                // Dönüş: önceki şeridin çıkışı yeni şeride bağlanıyorsa (yeniden konumlandırma değil)
                if (varOnce && once.serit != lane)
                    foreach (var exit in once.serit.Exits)
                        if (exit.target == lane)
                        {
                            string ad = $"{Kisa(once.serit.name)}→{Kisa(lane.name)}";
                            donusler[ad] = donusler.TryGetValue(ad, out int n) ? n + 1 : 1;
                        }

                // Kırmızıda duran: durma çizgisinin hemen önünde, ışık kırmızı ve araç kıpırdamıyor
                bool cizgide = lane.StopLine >= 0f && lane.MustStop(true) &&
                               lane.StopLine - car.Distance is > -0.5f and < 12f;
                bool duruyor = varOnce && once.serit == lane && Mathf.Abs(car.Distance - once.mesafe) < 0.02f;
                bool kirmizidaDuruyor = cizgide && duruyor;
                if (kirmizidaDuruyor && !(varOnce && once.kirmizida))
                    kirmizidaDuran++;
                aracDurumu[car] = (lane, car.Distance, kirmizidaDuruyor);
            }
        }

        private static string Kisa(string serit) => serit.StartsWith("Serit_") ? serit.Substring(6) : serit;

        private string IsikOzeti()
        {
            if (isiklar == null || isiklar.Length == 0) return "yok";
            var sb = new StringBuilder();
            foreach (var s in isiklar)
                sb.Append(s.State(0).ToString()[0]).Append('/').Append(s.State(1).ToString()[0]).Append(' ');
            return sb.Append($"değişim={isikDegisimi} kırmızıda_duran={kirmizidaDuran} dönüş={ToplamDonus()}").ToString();
        }

        private int ToplamDonus()
        {
            int n = 0;
            foreach (var v in donusler.Values) n += v;
            return n;
        }

        private int YakinHareketliArac()
        {
            int n = 0;
            foreach (var car in trafik)
                if (car != null && car.gameObject.activeInHierarchy && car.Lane != null &&
                    (car.transform.position - bus.transform.position).sqrMagnitude < 80f * 80f)
                    n++;
            return n;
        }

        /// <summary>Takılınca teşhis: otobüsün gövde kutusuna değen (otobüse ait olmayan) çarpıştırıcılar.</summary>
        private string Temaslar()
        {
            var adlar = new System.Collections.Generic.List<string>();
            // körüklüde arka gövde sahne kökünde: onun kutusu da otobüsün
            var koruklu = bus.GetComponent<KorukluOtobus>();
            var arka = koruklu != null && koruklu.ArkaGovde != null ? koruklu.ArkaGovde.transform : null;
            bool Otobusun(Transform t) => t.IsChildOf(bus.transform) || (arka != null && t.IsChildOf(arka));
            var kutular = new System.Collections.Generic.List<BoxCollider>(bus.GetComponentsInChildren<BoxCollider>());
            if (arka != null)
                kutular.AddRange(arka.GetComponentsInChildren<BoxCollider>());
            foreach (var c in kutular)
            {
                // kutunun kendi yönünde, gerçek boyutunda (OBB) bak; dünya AABB'si dönük kutuda çok büyük olur
                var merkez = c.transform.TransformPoint(c.center);
                var yari = Vector3.Scale(c.size * 0.5f, c.transform.lossyScale) + Vector3.one * 0.05f;
                foreach (var h in Physics.OverlapBox(merkez, yari, c.transform.rotation))
                    if (!Otobusun(h.transform) && !(h is WheelCollider) && adlar.Count < 8)
                    {
                        var p = bus.transform.InverseTransformPoint(h.ClosestPoint(merkez));
                        adlar.Add($"{c.name}×{h.name}({h.GetType().Name}) otobüste ({p.x:F1},{p.y:F1},{p.z:F1})");
                    }
            }
            return adlar.Count > 0 ? string.Join(",", adlar) : "yok";
        }

        /// <summary>Takılınca teşhis: her tekerin yerde olup olmadığı, yükü ve boyuna kayması (körüklüde arka gövdeninkiler de).</summary>
        private string TekerDurumu()
        {
            var tekerler = new System.Collections.Generic.List<WheelCollider>(bus.GetComponentsInChildren<WheelCollider>());
            var kor = bus.GetComponent<KorukluOtobus>();
            if (kor != null && kor.ArkaGovde != null)
                tekerler.AddRange(kor.ArkaGovde.GetComponentsInChildren<WheelCollider>());
            var parcalar = new System.Collections.Generic.List<string>();
            foreach (var w in tekerler)
                parcalar.Add(w.GetGroundHit(out var h)
                    ? $"{w.name}:yerde {h.force / 1000f:F0}kN kayma {h.forwardSlip:F2} rpm {w.rpm:F0} m={w.sprungMass:F0} motor={w.motorTorque:F0} fren={w.brakeTorque:F0}"
                    : $"{w.name}:HAVADA rpm {w.rpm:F0}");
            if (kor != null && kor.ArkaGovde != null)
                parcalar.Add($"arka_gövde y={kor.ArkaGovde.position.y - bus.transform.position.y:F2} kütle={kor.ArkaGovde.mass:F0} mafsal={kor.MafsalAcisi:F1}° " +
                             $"hız_ön={bus.GetComponent<Rigidbody>().linearVelocity.magnitude:F2} hız_arka={kor.ArkaGovde.linearVelocity.magnitude:F2} " +
                             $"kinematik={bus.GetComponent<Rigidbody>().isKinematic}/{kor.ArkaGovde.isKinematic} kısıt={bus.GetComponent<Rigidbody>().constraints}/{kor.ArkaGovde.constraints} " +
                             $"uyku={bus.GetComponent<Rigidbody>().IsSleeping()}/{kor.ArkaGovde.IsSleeping()}");
            return string.Join(" | ", parcalar);
        }
    }
}
