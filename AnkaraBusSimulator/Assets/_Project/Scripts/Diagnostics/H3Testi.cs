using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.Harita;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// H3 (Hat 408) yerel testi ("h3" görevi; Y11 test build'i, intent "-e sahne Hat3_KizilayGunesevler"). Intent "adim":
    /// <list type="bullet">
    /// <item>durak: 18 durakta sırayla otobüs durağa konur; durak içinde mi, bekleyen, trafik, kapılar açılır, biniş/iniş, durak sayıldı mı;
    /// kalkışta anons (SonAnons); 5 durakta (Kızılay, Ulus, Dışkapı, Altınpark, Güneşevler) FPS (dış ve kokpit); ekran ve havadan görüntü</item>
    /// <item>ufuk: 6 noktada sürücü gözünden dört yön + yukarıdan (harita kenarı, uzak şehir, sis)</item>
    /// <item>kopru: köprü/alt geçitlerden 20 km/s geçiş; temas, takılma, en düşük hız</item>
    /// <item>yokus: Mehmet Akif Ersoy yokuşu, otobüs kütlesine +5,6 t (80 yolcu), tam gaz; en düşük hız, çıktı mı</item>
    /// <item>perf: yalnız beş duraktaki FPS</item>
    /// <item>trafik: Kızılay ve iki ışıklı kavşakta 40 sn: çevredeki araç, kırmızıda duran / geçen</item>
    /// </list>
    /// Satırlar "[H3]", görüntüler h3_*.png. Varsayılan adım: hepsi.
    /// </summary>
    public class H3Testi : MonoBehaviour
    {
        public static string Adim = "hepsi";

        private BusVehicle bus;
        private BusInput input;
        private BusCameraRig rig;
        private Camera cam;
        private BusDoorController kapilar;
        private BusPassengers yolcular;
        private RouteTracker tracker;
        private RotaRehberi rehber;
        private KorukluOtobus kor;
        private PassengerManager manager;
        private ParcaAkisi akis;
        private AnonsSistemi anons;
        private string etiket;

        private static void Log(string s) => Debug.Log("[H3] " + s);

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(4f);
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            rig = FindAnyObjectByType<BusCameraRig>();
            cam = rig.GetComponentInChildren<Camera>() ?? Camera.main;
            kapilar = bus.GetComponentInChildren<BusDoorController>();
            yolcular = bus.GetComponent<BusPassengers>();
            tracker = bus.GetComponent<RouteTracker>();
            rehber = bus.GetComponent<RotaRehberi>();
            kor = bus.GetComponent<KorukluOtobus>();
            manager = PassengerManager.Instance;
            akis = FindAnyObjectByType<ParcaAkisi>();
            anons = bus.GetComponent<AnonsSistemi>();
            etiket = $"{GrafikAyarlari.Mevcut}_{OyunSecimi.SeciliZaman}{(OyunSecimi.SeciliHava != OyunSecimi.Hava.Acik ? OyunSecimi.SeciliHava.ToString() : "")}";
            var route = tracker.Route;
            int acik = 0;
            if (akis != null) foreach (var p in akis.Parcalar) if (p.acik) acik++;
            Log($"BASLA adım={Adim} otobüs={bus.Definition?.displayName} {etiket} cihaz={SystemInfo.deviceModel} hat={route?.LineNumber} '{route?.LineName}' " +
                $"durak={route?.StopCount} güzergâh={(rehber != null ? rehber.Uzunluk : -1f):F0} m parça={(akis != null ? akis.Parcalar.Count : -1)} açık={acik} " +
                $"yarıçap={ParcaAkisi.Yaricap} şerit={TrafficLane.Hepsi.Count} ışık={FindObjectsByType<TrafficSignal>().Length} araç={TrafficCar.Aktifler.Count}");
            bus.Handbrake = true;

            if (Adim == "hepsi" || Adim == "durak") yield return Duraklar();
            if (Adim == "perf") yield return Perf();
            if (Adim == "hepsi" || Adim == "ufuk") yield return Ufuk();
            if (Adim == "hepsi" || Adim == "trafik") yield return Trafik();
            if (Adim == "hepsi" || Adim == "kopru") yield return Kopruler();
            if (Adim == "hepsi" || Adim == "yokus") yield return Yokus();

            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = null;
            Log("BITTI hepsi");
        }

        // ------------------------------------------------------------ yardımcılar

        private IEnumerator Bekle(float sn)
        {
            float t0 = Time.time;
            while (Time.time - t0 < sn)
                yield return null;
        }

        private void Durdur()
        {
            foreach (var rb in bus.GetComponentsInChildren<Rigidbody>())
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private IEnumerator Isinla(Vector3 p, Vector3 ileri)
        {
            ileri.y = 0f;
            OtobusIsinla.ZemineTasi(bus, p + Vector3.up * 0.3f, Quaternion.LookRotation(ileri.normalized), 0.3f);
            kor?.ArkayiHizala();
            akis?.Guncelle(false);
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                Durdur();
            }
        }

        private BusStop DuragaKoy(int hedef)
        {
            typeof(RouteTracker).GetProperty("NextStopIndex").SetValue(tracker, hedef);
            var durak = tracker.Route.GetStop(hedef);
            OtobusIsinla.ZemineTasi(bus, durak.transform.position, Quaternion.Euler(0f, durak.transform.eulerAngles.y, 0f), 0.2f);
            kor?.ArkayiHizala();
            akis?.Guncelle(false);
            return durak;
        }

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak, float fov = 60f)
        {
            bool rigAcik = rig.enabled;
            rig.enabled = false;
            float eskiFov = cam.fieldOfView;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, $"h3_{etiket}_{ad}.png");
            cam.fieldOfView = eskiFov;
            rig.enabled = rigAcik;
        }

        /// <summary>Ekranın kendisi (HUD, mini harita dahil).</summary>
        private IEnumerator Ekran(string ad, BusCameraRig.Mode kip)
        {
            rig.enabled = true;
            rig.SetMode(kip);
            yield return Bekle(1.2f);
            yield return new WaitForEndOfFrame();
            Y11Hat.Kaydet($"h3_{etiket}_{ad}.png");
        }

        private int AracSay(Vector3 p, float r)
        {
            int n = 0;
            foreach (var c in TrafficCar.Aktifler)
                if (c != null && (c.transform.position - p).sqrMagnitude < r * r) n++;
            return n;
        }

        /// <summary>Güzergâhta s metredeki nokta ve yön (RotaRehberi.Yol).</summary>
        private void Guzergah(float s, out Vector3 p, out Vector3 f)
        {
            var yol = rehber.Yol;
            float top = 0f;
            for (int i = 1; i < yol.Count; i++)
            {
                var a = yol[i - 1];
                var b = yol[i];
                float l = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                if (top + l >= s || i == yol.Count - 1)
                {
                    p = Vector3.Lerp(a, b, l > 0f ? Mathf.Clamp01((s - top) / l) : 0f);
                    f = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                    return;
                }
                top += l;
            }
            p = yol[0];
            f = Vector3.forward;
        }

        private float GuzergahKonum(Vector3 p)
        {
            var yol = rehber.Yol;
            float en = float.MaxValue, enS = 0f, top = 0f;
            for (int i = 1; i < yol.Count; i++)
            {
                var a = yol[i - 1];
                var b = yol[i];
                var ab = new Vector2(b.x - a.x, b.z - a.z);
                float l = ab.magnitude;
                float t = l > 0f ? Mathf.Clamp01(Vector2.Dot(new Vector2(p.x - a.x, p.z - a.z), ab) / (l * l)) : 0f;
                var q = new Vector2(a.x, a.z) + ab * t;
                float d = (new Vector2(p.x, p.z) - q).sqrMagnitude;
                if (d < en) { en = d; enS = top + t * l; }
                top += l;
            }
            return enS;
        }

        /// <summary>Güzergâhı izleyerek hedef hızla sürer (test sürücüsü; oyunda otomatik pilot yok). Durumu her karede çağıran yazar.</summary>
        private void Sur(float hedefKmh, float tamGaz = -1f)
        {
            float s = GuzergahKonum(bus.transform.position);
            Guzergah(s + 10f + bus.SpeedKmh * 0.15f, out var h, out _);
            var yerel = bus.transform.InverseTransformPoint(h);
            input.AutoSteer = Mathf.Clamp(Mathf.Atan2(yerel.x, yerel.z) * 1.6f, -0.8f, 0.8f);
            float fark = hedefKmh - bus.SpeedKmh;
            input.AutoThrottle = tamGaz >= 0f ? tamGaz : Mathf.Clamp01(0.4f + fark * 0.25f);
            input.AutoBrake = tamGaz < 0f && fark < -3f ? Mathf.Clamp01(-fark * 0.15f) : 0f;
        }

        private void Birak()
        {
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
        }

        // ------------------------------------------------------------ duraklar

        private static readonly string[] PerfDuraklari = { "Kızılay", "Ulus", "Dışkapı Hastanesi", "Altınpark", "Güneşevler" };

        private IEnumerator Duraklar()
        {
            var route = tracker.Route;
            int sayilan = 0, anonsCalan = 0;
            for (int i = 0; i < route.StopCount; i++)
            {
                rig.enabled = true;
                rig.SetMode(BusCameraRig.Mode.Chase);
                var durak = DuragaKoy(i);
                yield return Bekle(3f);
                Durdur();
                bus.Handbrake = true;
                int bekleyen = manager != null ? manager.WaitingAt(durak).Count : -1;
                int sayi0 = yolcular.Onboard;
                Log($"durak {i:00} {durak.StopName}: içinde={durak.Contains(bus.transform.position)} bekleyen={bekleyen} yolcu={sayi0} " +
                    $"araç150={AracSay(bus.transform.position, 150f)} araç400={AracSay(bus.transform.position, 400f)} toplam_araç={TrafficCar.Aktifler.Count}");

                if (System.Array.IndexOf(PerfDuraklari, durak.StopName) >= 0)
                    yield return Fps(durak.StopName);

                yield return Ekran($"durak{i:00}_ekran", BusCameraRig.Mode.Chase);
                var sag = bus.transform.right;
                yield return Cek($"durak{i:00}_hava", bus.transform.position - bus.transform.forward * 22f + sag * 16f + Vector3.up * 14f, bus.transform.position + sag * 3f);

                // kapılar: biniş/iniş, durak sayılıyor mu
                kapilar.ToggleAll();
                float t0 = Time.time;
                bool cekildi = false;
                while (Time.time - t0 < 35f && tracker.NextStopIndex == i)
                {
                    yield return null;
                    if (!cekildi && Time.time - t0 > 5f)
                    {
                        cekildi = true;
                        yield return Cek($"durak{i:00}_kapi", kapilar.DoorCenter(0) + sag * 4f + bus.transform.forward * 3f + Vector3.up * 1.2f, kapilar.DoorCenter(0));
                    }
                }
                bool sayildi = tracker.NextStopIndex > i;
                if (sayildi) sayilan++;
                Log($"durak {i:00} {durak.StopName}: sayıldı={sayildi} {Time.time - t0:F1} sn yolcu {sayi0}→{yolcular.Onboard} kalan_bekleyen={(manager != null ? manager.WaitingAt(durak).Count : -1)} meşgul={yolcular.IsBusy}");
                if (kapilar.AnyOpen)
                    kapilar.ToggleAll();
                yield return Bekle(2f);

                // kalkış: anons (kapılar kapalı, 12 km/s üstü)
                if (i < route.StopCount - 1)
                {
                    string once = anons != null ? anons.SonAnons : null;
                    bus.Handbrake = false;
                    bus.SetSelector(BusVehicle.GearSelector.Drive);
                    float t1 = Time.time;
                    while (Time.time - t1 < 9f && (anons == null || anons.SonAnons == once))
                    {
                        yield return null;
                        Sur(20f);
                    }
                    Birak();
                    bool caldi = anons != null && anons.SonAnons != once;
                    if (caldi) anonsCalan++;
                    Log($"durak {i:00} kalkış anonsu: {(caldi ? anons.SonAnons : "YOK")} hız={bus.SpeedKmh:F0} km/s");
                    yield return Bekle(2f);
                    bus.Handbrake = true;
                }
            }
            Log($"duraklar: sayılan {sayilan}/{route.StopCount}, anons {anonsCalan}/{route.StopCount - 1}");
        }

        /// <summary>Yalnız FPS: beş durakta (trafik ve bekleyen yolcularla) dış ve kokpit.</summary>
        private IEnumerator Perf()
        {
            var route = tracker.Route;
            for (int i = 0; i < route.StopCount; i++)
            {
                var durak = route.GetStop(i);
                if (System.Array.IndexOf(PerfDuraklari, durak.StopName) < 0) continue;
                DuragaKoy(i);
                yield return Bekle(4f);
                Durdur();
                Log($"perf {durak.StopName}: araç150={AracSay(bus.transform.position, 150f)} bekleyen={(manager != null ? manager.WaitingAt(durak).Count : -1)}");
                yield return Fps(durak.StopName);
            }
        }

        private IEnumerator Fps(string ad)
        {
            using var probe = new PerfProbe();
            foreach (var kip in new[] { BusCameraRig.Mode.Chase, BusCameraRig.Mode.Cockpit })
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
                Debug.Log(probe.Report($"h3_{GrafikAyarlari.Mevcut}_{AnonsSistemi.AnonsAdi(ad)}_{(kip == BusCameraRig.Mode.Chase ? "dis" : "kokpit")}"));
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
        }

        // ------------------------------------------------------------ ufuk

        private IEnumerator Ufuk()
        {
            var route = tracker.Route;
            foreach (int i in new[] { 0, 1, 3, 5, 9, 17 })
            {
                var durak = DuragaKoy(i);
                yield return Bekle(3f);
                var p = bus.transform.position + Vector3.up * 2.6f;
                var f = bus.transform.forward;
                string ad = AnonsSistemi.AnonsAdi(durak.StopName);
                string[] yonAd = { "ileri", "sag", "geri", "sol" };
                for (int y = 0; y < 4; y++)
                {
                    var d = Quaternion.Euler(0f, 90f * y, 0f) * f;
                    yield return Cek($"ufuk_{ad}_{yonAd[y]}", p + d * 0.5f, p + d * 100f + Vector3.up * 3f, 70f);
                }
                yield return Cek($"ufuk_{ad}_yukari", p - f * 30f + Vector3.up * 45f, p + f * 250f, 70f);
            }
        }

        // ------------------------------------------------------------ trafik

        private IEnumerator Trafik()
        {
            var isiklar = FindObjectsByType<TrafficSignal>();
            var noktalar = new List<(string ad, Vector3 p, Vector3 f)>();
            var d0 = tracker.Route.GetStop(0);
            noktalar.Add(("kizilay", d0.transform.position, d0.transform.forward));
            // otobüs güzergâhına en yakın iki ışık
            var sirali = new List<TrafficSignal>(isiklar);
            sirali.Sort((a, b) => GuzergahUzaklik(a.transform.position).CompareTo(GuzergahUzaklik(b.transform.position)));
            for (int k = 0; k < Mathf.Min(2, sirali.Count); k++)
            {
                float s = GuzergahKonum(sirali[k].transform.position);
                Guzergah(Mathf.Max(0f, s - 45f), out var p, out var f);
                noktalar.Add(($"isik_{sirali[k].name}", p, f));
            }
            foreach (var (ad, p, f) in noktalar)
            {
                yield return Isinla(p, f);
                bus.Handbrake = true;
                int durdu = 0, kirmizidaGecti = 0, gorulen = 0;
                var once = new Dictionary<TrafficCar, float>();
                float t0 = Time.time;
                int resim = 0;
                while (Time.time - t0 < 40f)
                {
                    yield return Bekle(0.25f);
                    foreach (var c in TrafficCar.Aktifler)
                    {
                        if (c == null || c.Lane == null || (c.transform.position - p).sqrMagnitude > 160f * 160f) continue;
                        gorulen++;
                        if (c.Lane.HasSignal && c.Lane.IsRed)
                        {
                            float kalan = c.Lane.StopLine - c.Distance;
                            if (kalan > 0f && kalan < 12f && c.Speed < 0.5f) durdu++;
                            if (once.TryGetValue(c, out float onceki) && onceki < c.Lane.StopLine && c.Distance >= c.Lane.StopLine) kirmizidaGecti++;
                        }
                        once[c] = c.Distance;
                    }
                    if (Time.time - t0 > resim * 13f)
                    {
                        resim++;
                        yield return Cek($"trafik_{ad}_{resim}", p - f * 30f + Vector3.Cross(Vector3.up, f) * 25f + Vector3.up * 30f, p + f * 40f, 65f);
                    }
                }
                Log($"trafik {ad}: araç160={AracSay(p, 160f)} toplam={TrafficCar.Aktifler.Count} (örnek {gorulen}), kırmızıda durma örneği={durdu}, kırmızıda çizgiyi geçen={kirmizidaGecti}");
            }
        }

        private float GuzergahUzaklik(Vector3 p)
        {
            Guzergah(GuzergahKonum(p), out var q, out _);
            return new Vector2(p.x - q.x, p.z - q.z).magnitude;
        }

        // ------------------------------------------------------------ köprüler

        /// <summary>Editor analizinden (H3Kurulum.AnalizBatch): güzergâh üstünden geçen yapılar (s, m).</summary>
        private static readonly (string ad, float s)[] KopruNoktalari =
        {
            ("sihhiye", 560f), ("sihhiye_ustgecit", 846f), ("opera_talatpasa", 1265f), ("veteriner_ustgecit", 4061f),
            ("aydinlikevler_1", 4380f), ("aydinlikevler_2", 4405f), ("aihl_ustgecit", 5047f), ("altinpark", 5512f),
        };

        private IEnumerator Kopruler()
        {
            foreach (var (ad, sk) in KopruNoktalari)
            {
                Guzergah(sk - 60f, out var p, out var f);
                yield return Isinla(p, f);
                bus.Handbrake = false;
                bus.SetSelector(BusVehicle.GearSelector.Drive);
                float t0 = Time.time, enDusuk = 99f, enYuksek = 0f;
                string temas = null;
                bool cekildi = false;
                float s0 = GuzergahKonum(bus.transform.position);
                while (Time.time - t0 < 25f)
                {
                    yield return new WaitForFixedUpdate();
                    Sur(20f);
                    float s = GuzergahKonum(bus.transform.position);
                    if (Time.time - t0 > 4f) enDusuk = Mathf.Min(enDusuk, bus.SpeedKmh);
                    enYuksek = Mathf.Max(enYuksek, bus.SpeedKmh);
                    if (temas == null) temas = OtobusIsinla.Temas(bus);
                    if (!cekildi && s > sk - 12f)
                    {
                        cekildi = true;
                        yield return Cek($"kopru_{ad}", bus.transform.position - bus.transform.forward * 18f + Vector3.up * 4f + bus.transform.right * 3f, bus.transform.position + bus.transform.forward * 8f + Vector3.up * 2f);
                        yield return Ekran($"kopru_{ad}_kokpit", BusCameraRig.Mode.Cockpit);
                        rig.SetMode(BusCameraRig.Mode.Chase);
                    }
                    if (s > sk + 40f) break;
                }
                float ilerleme = GuzergahKonum(bus.transform.position) - s0;
                Birak();
                Log($"köprü {ad} (s={sk:F0}): {Time.time - t0:F1} sn ilerleme={ilerleme:F0} m (beklenen 100) hız en düşük={enDusuk:F0} en yüksek={enYuksek:F0} km/s temas={temas ?? "yok"} " +
                    $"geçti={(ilerleme > 90f ? "evet" : "HAYIR")} rotadan_sapma={rehber.SapmaMesafesi:F1} m");
                yield return Bekle(1.5f);
                bus.Handbrake = true;
            }
        }

        // ------------------------------------------------------------ yokuş

        private IEnumerator Yokus()
        {
            // Mehmet Akif Ersoy yokuşu: Güneşevler Spor Tesisleri'nden son durağa (en dik %9 s≈8977, editör analizi)
            float bas = 8700f, son = 9080f;
            Guzergah(bas, out var p, out var f);
            yield return Isinla(p, f);
            var body = bus.GetComponent<Rigidbody>();
            float kutle0 = body.mass;
            body.mass += 5600f;   // 80 yolcu × 70 kg (oyunda yolcu kütlesi yok)
            yolcular.GetType().GetMethod("SetOnboard", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(yolcular, new object[] { yolcular.Capacity });
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            float t0 = Time.time, enDusuk = 99f, y0 = bus.transform.position.y;
            bool durdu = false;
            float yavasSure = 0f;
            int resim = 0;
            while (Time.time - t0 < 90f)
            {
                yield return new WaitForFixedUpdate();
                Sur(50f, 1f);
                float s = GuzergahKonum(bus.transform.position);
                if (Time.time - t0 > 6f)
                {
                    enDusuk = Mathf.Min(enDusuk, bus.SpeedKmh);
                    yavasSure = bus.SpeedKmh < 3f ? yavasSure + Time.fixedDeltaTime : 0f;
                    if (yavasSure > 5f) { durdu = true; break; }
                }
                if (Time.time - t0 > resim * 15f)
                {
                    resim++;
                    yield return Ekran($"yokus_{resim}", BusCameraRig.Mode.Chase);
                }
                if (s > son) break;
            }
            float sSon = GuzergahKonum(bus.transform.position);
            Log($"yokuş: kütle {kutle0:F0}+5600 kg, yolcu={yolcular.Onboard}, {Time.time - t0:F1} sn, s {bas:F0}→{sSon:F0} (hedef {son:F0}), yükselme={bus.transform.position.y - y0:F1} m, " +
                $"en düşük hız={enDusuk:F0} km/s, çıktı={(sSon > son - 5f ? "evet" : "HAYIR")}{(durdu ? " (takıldı)" : "")}");
            Birak();
            body.mass = kutle0;
            yield return Bekle(2f);
        }
    }
}
