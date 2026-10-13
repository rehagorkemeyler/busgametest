using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y20 yolcu testi ("yolcu" görevi; Y11 test build'i ve Editor'de Y18Kurulum.TestBatch). Her otobüste (BMC, Millennium, BMC 18,
    /// Millennium körüklü; sahne yeniden yüklenir):
    /// <list type="bullet">
    /// <item>biniş: 1. durakta (bekleyenlere ek yolcu eklenir, görünen sınırın üstüne) kapılar açılır; binenler koltuğa oturuyor mu,
    /// ayakta tutunanlar, takılan (yürümeye devam eden) yolcular, oturanın koltuk noktasına uzaklığı</item>
    /// <item>sürüş: Bulvar → Cinnah bağlantısı (viraj) 20 km/s; oturanların koltuğa göre kayması, körüklüde mafsal açısı</item>
    /// <item>iniş: 2. durakta; inenler kapıya yürüyüp iniyor mu (hangi kapı, bitiren/takılan)</item>
    /// <item>FPS: 2. durakta, içerideki yolcular gizli ve görünür, dış ve yolcu kamerası (12'şer sn)</item>
    /// </list>
    /// Satırlar "[Yolcu]", görüntüler y20_*.png.
    /// </summary>
    public class YolcuTesti : MonoBehaviour
    {
        private static int sira = -1;
        private static readonly int[] Otobusler = { 0, 1, 2, 3 };
        private const BindingFlags Ozel = BindingFlags.NonPublic | BindingFlags.Instance;

        private BusVehicle bus;
        private BusInput input;
        private BusCameraRig rig;
        private Camera cam;
        private BusDoorController kapilar;
        private BusPassengers yolcular;
        private RouteTracker tracker;
        private KorukluOtobus kor;
        private OtobusKoltuklari koltuklar;
        private PassengerManager manager;
        private OtobusOlcusu olcu;
        private string otobus;

        private static void Log(string s) => Debug.Log("[Yolcu] " + s);

        private IEnumerator Start()
        {
            if (sira < 0)
            {
                sira = 0;
                if (OyunSecimi.Otobus != Otobusler[0])
                {
                    OyunSecimi.Otobus = Otobusler[0];
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    yield break;
                }
            }
            yield return new WaitForSeconds(3f);
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            rig = FindAnyObjectByType<BusCameraRig>();
            cam = rig.GetComponentInChildren<Camera>() ?? Camera.main;
            kapilar = bus.GetComponentInChildren<BusDoorController>();
            yolcular = bus.GetComponent<BusPassengers>();
            tracker = bus.GetComponent<RouteTracker>();
            kor = bus.GetComponent<KorukluOtobus>();
            koltuklar = bus.GetComponent<OtobusKoltuklari>();
            manager = PassengerManager.Instance;
            olcu = OtobusOlcusu.Olc(bus);
            otobus = KapiTesti.KisaAd(bus.name);
            bus.Handbrake = true;
            foreach (var s in FindObjectsByType<TrafficSpawner>())
                s.enabled = false;
            foreach (var c in FindObjectsByType<TrafficCar>())
                Destroy(c.gameObject);

            var karakterler = (GameObject[])typeof(PassengerManager).GetField("karakterler", Ozel).GetValue(manager);
            Log($"BASLA otobüs={bus.Definition?.displayName} kalite={GrafikAyarlari.Mevcut} cihaz={SystemInfo.deviceModel} " +
                $"koltuk={(koltuklar != null ? koltuklar.Koltuklar.Count : -1)} ayakta={(koltuklar != null ? koltuklar.Ayakta.Count : -1)} " +
                $"görünen_sınır={MaxGorunur()} Resources/Yolcular={karakterler.Length}");

#if UNITY_EDITOR
            // Editor'de: YAYAYOLU=0 → sundurma kaçınması kapalı (önce/sonra), YOLCU_FPS=0 → FPS adımı atlanır
            YayaYolu.Etkin = System.Environment.GetEnvironmentVariable("YAYAYOLU") != "0";
            bool fps = System.Environment.GetEnvironmentVariable("YOLCU_FPS") != "0";
#else
            bool fps = true;
#endif
            Log($"{otobus} sundurma kaçınması={(YayaYolu.Etkin ? "açık" : "KAPALI")}");
            if (yolcular == null || tracker == null || tracker.Route == null || tracker.Route.StopCount < 3)
                Log("HATA BusPassengers/rota yok");
            else
            {
                yield return Binis(1);
                yield return Surus();
                yield return Inis(2);
                if (fps)
                    yield return Fps();
            }

            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = null;
            Log($"BITTI {otobus}");
            sira++;
            if (sira < Otobusler.Length)
            {
                OyunSecimi.Otobus = Otobusler[sira];
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield break;
            }
            sira = -1;
            OyunSecimi.Otobus = 2;
            Log("BITTI hepsi");
        }

        private static int MaxGorunur() => (int)typeof(BusPassengers).GetProperty("MaxGorunur", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private List<Passenger> Icerde() => (List<Passenger>)typeof(BusPassengers).GetField("icerde", Ozel).GetValue(yolcular);

        // ------------------------------------------------------------ biniş

        private IEnumerator Binis(int hedef)
        {
            typeof(BusPassengers).GetField("onboard", Ozel).SetValue(yolcular, 0);
            var durak = DuragaKoy(hedef);
            yield return Bekle(2.5f);
            Sayim("durak1");
            // bekleyenlere ek: görünen sınırın 4 fazlası olana kadar (koltuklar dolunca ayakta tutunanlar görünsün)
            int ek = 0;
            var liste = BekleyenListesi(durak);
            while (liste != null && liste.Count < MaxGorunur() + 4)
            {
                var p = manager.Get(manager.SidewalkPoint(durak, Random.Range(-5f, 5f)), Quaternion.LookRotation(-bus.transform.right));
                if (p == null)
                    break;
                p.Stand(-bus.transform.right);
                liste.Add(p);
                ek++;
            }
            Log($"{otobus} biniş: durak={durak.StopName} içinde={durak.Contains(bus.transform.position)} bekleyen={manager.WaitingAt(durak).Count} (ek {ek})");
            yield return Cek("durak_bekleyenler", bus.transform.TransformPoint(olcu.YariGenislik + 1.2f, 1.7f, olcu.On + 3f), kapilar.DoorCenter(0) + bus.transform.right * 3f);

            kapilar.ToggleAll();
            float t0 = Time.time;
            bool c1 = false, c2 = false;
            while (Time.time - t0 < 60f)
            {
                yield return null;
                float t = Time.time - t0;
                if (!c1 && t > 6f)
                {
                    c1 = true;
                    yield return Kip("binis_kapi", BusCameraRig.Mode.Door);
                    yield return Cek("binis_on_kapi", kapilar.DoorCenter(0) + bus.transform.right * 3.5f + bus.transform.forward * 2f + Vector3.up * 0.6f, kapilar.DoorCenter(0));
                }
                if (!c2 && t > 13f)
                {
                    c2 = true;
                    yield return Kip("binis_yolcu", BusCameraRig.Mode.Interior);
                    yield return Koridor("binis");
                }
                if (c2 && !yolcular.IsBusy && Yuruyen() == 0)
                    break;
            }
            Log($"{otobus} biniş bitti {Time.time - t0:F1} sn: meşgul={yolcular.IsBusy} sayı={yolcular.Onboard} kalan_bekleyen={manager.WaitingAt(durak).Count}");
            Durum("binis");
            yield return Kip("oturdu_yolcu", BusCameraRig.Mode.Interior);
            yield return Kip("oturdu_kokpit", BusCameraRig.Mode.Cockpit);
            yield return Koridor("oturdu");
            kapilar.ToggleAll();
            yield return Bekle(2f);
        }

        private int Yuruyen()
        {
            int n = 0;
            foreach (var p in Icerde())
                if (p != null && p.IsWalking)
                    n++;
            return n;
        }

        /// <summary>İçerideki görünen yolcuların durumu: oturan (koltuğa uzaklık), tutunan, yürüyen (takılan; otobüs uzayında yeri).</summary>
        private void Durum(string ad)
        {
            int otur = 0, tutun = 0, yuru = 0, diger = 0;
            float enUzak = 0f, enAci = 0f;
            var sb = new System.Text.StringBuilder();
            var arka = kor != null && kor.ArkaGovde != null ? kor.ArkaGovde.transform : null;
            int arkada = 0;
            foreach (var p in Icerde())
            {
                if (p == null) continue;
                var ust = p.transform.parent;
                if (p.Oturuyor && ust != null && ust.name.StartsWith("Koltuk_"))
                {
                    otur++;
                    enUzak = Mathf.Max(enUzak, p.transform.localPosition.magnitude);
                    enAci = Mathf.Max(enAci, Quaternion.Angle(p.transform.localRotation, Quaternion.identity));
                    if (arka != null && ust.parent == arka) arkada++;
                }
                else if (ust != null && ust.name.StartsWith("Ayakta_"))
                    tutun++;
                else if (p.IsWalking)
                {
                    yuru++;
                    var y = bus.transform.InverseTransformPoint(p.transform.position);
                    sb.Append($"{p.name}@({y.x:F1},{y.y:F1},{y.z:F1}) ");
                }
                else
                {
                    diger++;
                    var y = bus.transform.InverseTransformPoint(p.transform.position);
                    sb.Append($"DİĞER {p.name} ebeveyn={(ust != null ? ust.name : "-")}@({y.x:F1},{y.y:F1},{y.z:F1}) ");
                }
            }
            var anim = 0;
            foreach (var p in Icerde())
            {
                var a = p != null ? p.GetComponentInChildren<Animator>() : null;
                if (a != null && p.Oturuyor && a.GetCurrentAnimatorStateInfo(0).IsName("Otur")) anim++;
            }
            Log($"{otobus} {ad} durum: görünen={Icerde().Count} oturan={otur} (Otur animasyonunda {anim}, arka gövdede {arkada}) tutunan={tutun} yürüyen={yuru} diğer={diger} " +
                $"koltuğa_en_uzak={enUzak:F3} m açı={enAci:F1}° sayı={yolcular.Onboard} {sb}");
        }

        // ------------------------------------------------------------ sürüş (viraj)

        private IEnumerator Surus()
        {
            TrafficLane serit = null;
            foreach (var l in FindObjectsByType<TrafficLane>())
                if (l.name == "Serit_Baglanti_Bulvar_Cinnah")
                    serit = l;
            if (serit == null)
            {
                Log($"{otobus} sürüş: şerit yok, atlandı");
                yield break;
            }
            serit.Sample(0f, out var p0, out var f0);
            OtobusIsinla.ZemineTasi(bus, p0 - f0 * 25f + Vector3.up * 0.3f, Quaternion.LookRotation(f0), 0.3f);
            kor?.ArkayiHizala();
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var rb in bus.GetComponentsInChildren<Rigidbody>())
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            // oturanların başlangıç yerel konumları (koltuk noktasına göre)
            var oturan = new List<Passenger>();
            foreach (var p in Icerde())
                if (p != null && p.Oturuyor)
                    oturan.Add(p);
            rig.enabled = true;
            rig.SetMode(BusCameraRig.Mode.Chase);
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            float t0 = Time.time, kayma = 0f, aci = 0f, mafsal = 0f, hizMax = 0f;
            bool cekildi = false;
            int yayaYeni = 0, yayaEski = 0;
            while (Time.time - t0 < 22f)
            {
                yield return null;
                float d = serit.EnYakinKonum(bus.transform.position, out _);
                if (d > serit.Length - 3f) break;
                serit.Sample(d + 12f, out var h, out _);
                var yerel = bus.transform.InverseTransformPoint(h);
                input.AutoSteer = Mathf.Clamp(Mathf.Atan2(yerel.x, yerel.z) * 1.5f, -0.6f, 0.6f);
                float fark = 20f - bus.SpeedKmh;
                input.AutoThrottle = Mathf.Clamp01(0.5f + fark * 0.3f);
                input.AutoBrake = fark < -2f ? Mathf.Clamp01(-fark * 0.15f) : 0f;
                hizMax = Mathf.Max(hizMax, bus.SpeedKmh);
                foreach (var p in oturan)
                {
                    kayma = Mathf.Max(kayma, p.transform.localPosition.magnitude);
                    aci = Mathf.Max(aci, Quaternion.Angle(p.transform.localRotation, Quaternion.identity));
                }
                if (kor != null) mafsal = Mathf.Max(mafsal, Mathf.Abs(kor.MafsalAcisi));
                if (!cekildi && (kor != null ? Mathf.Abs(kor.MafsalAcisi) > 12f : Time.time - t0 > 9f))
                {
                    cekildi = true;
                    yield return Cek("viraj_onden_arkaya", bus.transform.TransformPoint(0f, 1.75f, olcu.On - 1.4f), bus.transform.TransformPoint(0f, 1.0f, -olcu.Arka));
                    yield return Kip("viraj_yolcu", BusCameraRig.Mode.Interior);
                    rig.SetMode(BusCameraRig.Mode.Chase);
                }
            }
            foreach (var p in FindObjectsByType<Passenger>())
                if (p.transform.parent == manager.transform && !Icerde().Contains(p))
                {
                    if (p.GetComponentInChildren<Animator>() != null) yayaYeni++; else yayaEski++;
                }
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
            yield return Bekle(2.5f);
            Log($"{otobus} sürüş: {Time.time - t0:F1} sn, en yüksek {hizMax:F0} km/s, oturan={oturan.Count}, koltuğa göre en büyük kayma={kayma:F4} m açı={aci:F2}°" +
                $"{(kor != null ? $", mafsal en çok {mafsal:F1}°" : "")}; dışarıdaki yayalar yeni={yayaYeni} eski={yayaEski}");
            Durum("surus_sonu");
            bus.Handbrake = true;
        }

        // ------------------------------------------------------------ iniş

        private IEnumerator Inis(int hedef)
        {
            var durak = DuragaKoy(hedef);
            yield return Bekle(2f);
            var once = new List<Passenger>(Icerde());
            int sayi0 = yolcular.Onboard;
            kapilar.ToggleAll();
            float t0 = Time.time;
            bool c1 = false, c2 = false;
            var inenler = new HashSet<Passenger>();
            var kapiSayisi = new int[kapilar.DoorCount];
            var kapiyaVardi = new HashSet<Passenger>();
            var engelde = new Dictionary<Passenger, string>();   // sundurmanın arka/yan duvarından geçenler (açık ön sayılmaz)
            var onceki = new Dictionary<Passenger, Vector3>();
            var yayalar = new List<Passenger>();
            var kutular = SundurmaKutulari();
            float sonTarama = -1f;
            while (Time.time - t0 < 45f)
            {
                yield return null;
                float t = Time.time - t0;
                if (t - sonTarama > 0.5f)
                {
                    sonTarama = t;
                    yayalar.Clear();
                    yayalar.AddRange(FindObjectsByType<Passenger>());
                }
                foreach (var p in yayalar)
                {
                    if (p == null || !p.isActiveAndEnabled || Icerde().Contains(p))
                        continue;
                    var simdi = p.transform.position;
                    if (onceki.TryGetValue(p, out var eski) && !engelde.ContainsKey(p))
                    {
                        string ne = DuvardanGecti(kutular, eski, simdi);
                        if (ne != null) engelde[p] = ne;
                    }
                    onceki[p] = simdi;
                }
                foreach (var p in once)
                    if (!Icerde().Contains(p))
                    {
                        inenler.Add(p);
                        // kapının hemen dışından geçtiyse (yerde, otobüsün yanında) o kapıyı say
                        if (!kapiyaVardi.Contains(p))
                            for (int i = 0; i < kapilar.DoorCount; i++)
                            {
                                var k = kapilar.DoorCenter(i) + bus.transform.right * 0.8f;
                                var fark = p.transform.position - k;
                                fark.y = 0f;
                                if (fark.magnitude < 0.5f)
                                {
                                    kapiSayisi[i]++;
                                    kapiyaVardi.Add(p);
                                }
                            }
                    }
                if (!c1 && t > 3.5f)
                {
                    c1 = true;
                    yield return Kip("inis_yolcu", BusCameraRig.Mode.Interior);
                    yield return Kip("inis_kapi", BusCameraRig.Mode.Door);
                }
                if (!c2 && t > 8f)
                {
                    c2 = true;
                    yield return Cek("inis_yan", bus.transform.TransformPoint(olcu.YariGenislik + 9f, 1.8f, (olcu.On - olcu.Arka) * 0.5f), bus.transform.TransformPoint(0f, 1.2f, (olcu.On - olcu.Arka) * 0.5f));
                    yield return Koridor("inis");
                }
                bool inenYuruyor = false;
                foreach (var p in inenler)
                    if (p.gameObject.activeInHierarchy && p.IsWalking)
                        inenYuruyor = true;
                if (c2 && !yolcular.IsBusy && !inenYuruyor && Yuruyen() == 0)
                    break;
            }
            int bitiren = 0;
            var takilan = new System.Text.StringBuilder();
            foreach (var p in inenler)
                if (!p.gameObject.activeInHierarchy) bitiren++;
                else
                {
                    var y = bus.transform.InverseTransformPoint(p.transform.position);
                    takilan.Append($"{p.name}@({y.x:F1},{y.y:F1},{y.z:F1}) yürüyor={p.IsWalking} ");
                }
            Log($"{otobus} iniş: durak={durak.StopName} {Time.time - t0:F1} sn, sayı {sayi0} → {yolcular.Onboard}, görünen inen={inenler.Count} " +
                $"(havuza dönen {bitiren}), kapı başına (kapı dışından geçen) [{string.Join(", ", kapiSayisi)}] {takilan}");
            var engelAdlari = new List<string>();
            foreach (var e in engelde) engelAdlari.Add(e.Value);
            Log($"{otobus} iniş: sundurmanın arka/yan duvarından geçen yaya={engelde.Count} (sundurma kutusu {kutular.Count}) [{string.Join(", ", engelAdlari)}]");
            Durum("inis");
            kapilar.ToggleAll();
            yield return Bekle(2f);
        }

        /// <summary>Otobüsün yakınındaki sabit kutu çarpışıcıları (durak sundurması) ve açık ön yüzleri (otobüse bakan yüz).</summary>
        private List<(BoxCollider kutu, int eksen, float isaret)> SundurmaKutulari()
        {
            var liste = new List<(BoxCollider, int, float)>();
            foreach (var c in FindObjectsByType<BoxCollider>())
            {
                if (c.attachedRigidbody != null || c.bounds.size.y < 1.3f || (c.bounds.center - bus.transform.position).sqrMagnitude > 20f * 20f)
                    continue;
                var yerel = c.transform.InverseTransformPoint(bus.transform.position) - c.center;
                var yari = c.size * 0.5f;
                bool x = Mathf.Abs(yerel.x) / yari.x > Mathf.Abs(yerel.z) / yari.z;
                liste.Add((c, x ? 0 : 2, Mathf.Sign(x ? yerel.x : yerel.z)));
            }
            return liste;
        }

        /// <summary>a → b adımı bir kutunun açık ön yüzü dışındaki yüzünü (arka, yanlar) bel hizasında kesti mi.</summary>
        private static string DuvardanGecti(List<(BoxCollider kutu, int eksen, float isaret)> kutular, Vector3 a, Vector3 b)
        {
            foreach (var (k, onEksen, onIsaret) in kutular)
            {
                var la = k.transform.InverseTransformPoint(a + Vector3.up) - k.center;
                var lb = k.transform.InverseTransformPoint(b + Vector3.up) - k.center;
                var yari = k.size * 0.5f;
                foreach (int eksen in new[] { 0, 2 })
                    foreach (float isaret in new[] { -1f, 1f })
                    {
                        if (eksen == onEksen && isaret == onIsaret)
                            continue;
                        float duz = isaret * yari[eksen];
                        float da = la[eksen] - duz, db = lb[eksen] - duz;
                        if (da * db >= 0f)
                            continue;
                        var kes = Vector3.Lerp(la, lb, da / (da - db));
                        int diger = eksen == 0 ? 2 : 0;
                        if (Mathf.Abs(kes[diger]) <= yari[diger] && Mathf.Abs(kes.y) <= yari.y)
                            return $"{k.name}/{(eksen == onEksen ? "arka" : "yan")}";
                    }
            }
            return null;
        }

        // ------------------------------------------------------------ FPS

        private IEnumerator Fps()
        {
            var icerde = new List<Passenger>(Icerde());
            int gorunen = icerde.Count;
            using var probe = new PerfProbe();
            foreach (bool goster in new[] { false, true, false, true })
            {
                foreach (var p in icerde)
                    if (p != null) p.gameObject.SetActive(goster);
                foreach (var kip in new[] { BusCameraRig.Mode.Chase, BusCameraRig.Mode.Interior })
                {
                    rig.enabled = true;
                    rig.SetMode(kip);
                    yield return Bekle(2f);
                    probe.Reset();
                    for (float t = 0f; t < 12f; t += Time.unscaledDeltaTime)
                    {
                        yield return null;
                        probe.Sample();
                    }
                    Debug.Log(probe.Report($"y20_{otobus}_{GrafikAyarlari.Mevcut}_{(kip == BusCameraRig.Mode.Chase ? "dis" : "yolcu")}_{(goster ? $"yolcu{gorunen}" : "yolcusuz")}"));
                }
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
        }

        // ------------------------------------------------------------ yardımcılar

        /// <summary>Otobüsü durağa koyar (KapiTesti.Yolcular gibi) ve sıradaki durak yapar.</summary>
        private BusStop DuragaKoy(int hedef)
        {
            typeof(RouteTracker).GetProperty("NextStopIndex").SetValue(tracker, hedef);
            var durak = tracker.Route.GetStop(hedef);
            OtobusIsinla.ZemineTasi(bus, durak.transform.position, Quaternion.Euler(0f, durak.transform.eulerAngles.y, 0f), 0.2f);
            kor?.ArkayiHizala();
            return durak;
        }

        private IList BekleyenListesi(BusStop durak)
        {
            var stops = (IList)typeof(PassengerManager).GetField("stops", Ozel).GetValue(manager);
            foreach (var s in stops)
                if ((BusStop)s.GetType().GetField("stop").GetValue(s) == durak)
                    return (IList)s.GetType().GetField("waiting").GetValue(s);
            return null;
        }

        private void Sayim(string ad)
        {
            int yeni = 0, eski = 0;
            foreach (var p in FindObjectsByType<Passenger>())
                if (p.GetComponentInChildren<Animator>() != null) yeni++; else eski++;
            Log($"{otobus} {ad} sahnedeki yayalar: yeni (Quaternius)={yeni} eski={eski}");
        }

        private IEnumerator Bekle(float sn)
        {
            float bitis = Time.time + sn;
            while (Time.time < bitis)
            {
                yield return new WaitForFixedUpdate();
                if (bus.Handbrake)
                    foreach (var rb in bus.GetComponentsInChildren<Rigidbody>())
                        if (rb.linearVelocity.sqrMagnitude > 0.01f && Time.time < bitis - sn + 0.6f)
                            rb.linearVelocity = Vector3.zero;
            }
        }

        private IEnumerator Kip(string ad, BusCameraRig.Mode kip)
        {
            rig.enabled = true;
            rig.SetTarget(bus);
            rig.SetMode(kip);
            yield return new WaitForSeconds(0.8f);
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, $"y20_{otobus}_{GrafikAyarlari.Mevcut}_{ad}.png");
        }

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak)
        {
            bool rigAcik = rig.enabled;
            rig.enabled = false;
            float yakin = cam.nearClipPlane;
            cam.nearClipPlane = 0.05f;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, $"y20_{otobus}_{GrafikAyarlari.Mevcut}_{ad}.png");
            cam.nearClipPlane = yakin;
            rig.enabled = rigAcik;
        }

        /// <summary>Koridordan: önden arkaya ve arkadan öne (yolcu göz hizasının biraz üstünden).</summary>
        private IEnumerator Koridor(string ad)
        {
            yield return Cek($"{ad}_onden_arkaya", bus.transform.TransformPoint(0f, 2.0f, olcu.On - 1.0f), bus.transform.TransformPoint(0f, 0.9f, -olcu.Arka * 0.6f));
            var arka = kor != null && kor.ArkaGovde != null ? kor.ArkaGovde.transform : bus.transform;
            float arkaZ = arka.InverseTransformPoint(bus.transform.TransformPoint(0f, 0f, -olcu.Arka)).z;
            yield return Cek($"{ad}_arkadan_one", arka.TransformPoint(0f, 2.0f, arkaZ + 0.8f), bus.transform.TransformPoint(0f, 0.9f, olcu.On * 0.4f));
        }
    }
}
