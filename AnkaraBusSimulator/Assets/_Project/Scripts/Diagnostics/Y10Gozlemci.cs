using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.UI;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y10 testi: OtomatikPilot hattı sürerken yol gösterici, mini harita, gece/akşam ışıkları, yağmur,
    /// yaya geçitleri, yayalar, sollama/korna ve dolmuşları gözler. Sonuçlar "[Y10]" satırları,
    /// ekran görüntüleri Application.persistentDataPath altına (y10_*.png).
    /// Yalnızca test build'lerinde (AndroidBuild Y10) ya da Editor'de sahneye eklenir.
    /// </summary>
    public class Y10Gozlemci : MonoBehaviour
    {
        private BusVehicle bus;
        private BusCameraRig rig;
        private RotaRehberi rehber;
        private MiniHarita mini;
        private RouteTracker tracker;
        private Touchscreen ts;
        private string sahne;
        private int hata;

        private readonly HashSet<string> bilgiler = new HashSet<string>();
        private string sonBilgi;
        private bool sagaGoruldu;
        private float haritaBusFarkMax;
        private int frenOrnek, frenYanikDogru;

        // trafik
        private readonly Dictionary<TrafficCar, (float lateral, float lateralTarget, int horn, bool dolmus, BusStop durak, TrafficLane serit)> arac =
            new Dictionary<TrafficCar, (float, float, int, bool, BusStop, TrafficLane)>();
        private int sollama, korna, dolmusYanasma, dolmusDevam, kaldirim, yayaGecis, yayaIcinDuran;
        private readonly HashSet<object> gecenYayalar = new HashSet<object>();
        private readonly HashSet<TrafficCar> yayaIcinDuranlar = new HashSet<TrafficCar>();

        private void Log(string s) => Debug.Log("[Y10] " + s);

        private void Check(bool ok, string what)
        {
            if (!ok) hata++;
            Log($"{(ok ? "OK  " : "HATA")} {what}");
        }

        private IEnumerator Start()
        {
            sahne = SceneManager.GetActiveScene().name;
            ts = InputSystem.AddDevice<Touchscreen>();
            yield return new WaitForSeconds(1.5f);
            bus = FindAnyObjectByType<BusVehicle>();
            rig = FindAnyObjectByType<BusCameraRig>();
            rehber = bus.GetComponent<RotaRehberi>();
            mini = bus.GetComponent<MiniHarita>() ?? FindAnyObjectByType<MiniHarita>();
            tracker = bus.GetComponent<RouteTracker>();
            // yolcu durak isteği sesi ("Durakta inecek var!", stop.wav): ne zaman ve hangi düzeyde çaldığı
            var ses = bus.GetComponent<BusAudio>();
            float sonKalkis = -1f;
            if (ses != null)
                ses.OneShotPlayed += (klip, duzey) =>
                {
                    if (klip.StartsWith("DoorClose")) sonKalkis = Time.time;
                    if (klip == "stop")
                        Log($"yolcu sesi: '{klip}' düzey {duzey:F2}, kapı kapandıktan {Time.time - sonKalkis:F1} sn sonra, kamera {rig.CurrentMode}");
                };

            Log($"BASLA sahne={sahne} zaman={OyunSecimi.SeciliZaman} yağmur={OyunSecimi.Yagmur} kalite={GrafikAyarlari.Mevcut} " +
                $"rehber={(rehber != null ? "var" : "YOK")} miniharita={(mini != null ? "var" : "YOK")}");
            yield return HavaKontrol();
            Rota();
            yield return Ekran("baslangic_dis");
            rig.SetMode(BusCameraRig.Mode.Cockpit);
            yield return new WaitForSeconds(1f);
            HavaSesi("kokpit");
            yield return Ekran("baslangic_kokpit");
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return new WaitForSeconds(1f);
            HavaSesi("dış");
            Cakisma();
            yield return BuyukHarita();
            Gecitler();
            // hat bitince rehber "Hat tamamlandı" der: rotadan çıkma uyarısı sefer başında denenir
            yield return RotadanCik();

            StartCoroutine(Izle());
            while (tracker != null && !tracker.Completed)
                yield return null;
            yield return new WaitForSeconds(4f);

            Log($"BİLGİ BANDI ({bilgiler.Count} farklı): {string.Join(" | ", bilgiler.Take(25))}");
            Check(sagaGoruldu || sahne != "Hat1_KizilayAtakule", "bilgi bandında Kuğulu'dan Cinnah'a '… m sonra SAĞA' çıktı");
            Check(haritaBusFarkMax < 3f, $"mini harita otobüsle dönüyor (en büyük açı farkı {haritaBusFarkMax:F1}°)");
            if (frenOrnek > 0)
                Check(frenYanikDogru >= frenOrnek * 0.98f, $"fren lambası frenle birlikte ({frenYanikDogru}/{frenOrnek} örnek)");
            Log($"TRAFIK sollama={sollama} korna={korna} dolmuş_yanaşma={dolmusYanasma} dolmuş_devam={dolmusDevam} " +
                $"kaldırıma_çıkan={kaldirim} yaya_geçişi={yayaGecis} yaya_için_duran_araç={yayaIcinDuranlar.Count}");
            Check(kaldirim == 0, "dolmuşlar kaldırıma çıkmıyor");
            Log($"SONUC sahne={sahne} hata={hata}");
            Log("BITTI " + sahne);
        }

        // ---------------- Hava ve zaman ----------------

        private IEnumerator HavaKontrol()
        {
            var lm = LightmapSettings.lightmaps;
            string lmAd = lm.Length > 0 && lm[0].lightmapColor != null ? lm[0].lightmapColor.name : "-";
            Log($"ışık: lightmap={lm.Length} ilki='{lmAd}' sis={RenderSettings.fog} gök={RenderSettings.skybox?.name} " +
                $"güneş={RenderSettings.sun?.intensity:F2}");
            var lambalar = bus.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Lamba_")).ToList();
            string acik = string.Join(",", lambalar.Where(t => t.gameObject.activeInHierarchy).Select(t => t.name.Substring(6)));
            Log($"otobüs lambaları açık: {(acik.Length > 0 ? acik : "yok")}; ışıklar: " +
                string.Join(",", bus.GetComponentsInChildren<Light>(true).Where(l => l.isActiveAndEnabled).Select(l => $"{l.name}({l.type})")));
            bool gece = OyunSecimi.SeciliZaman != OyunSecimi.Zaman.Gunduz;
            if (gece)
            {
                Check(lmAd.Contains(OyunSecimi.SeciliZaman.ToString()) || lmAd.ToLowerInvariant().Contains(OyunSecimi.SeciliZaman.ToString().ToLowerInvariant()),
                    $"{OyunSecimi.SeciliZaman} lightmap seti yüklendi ('{lmAd}')");
                Check(lambalar.Any(t => t.name.Contains("Far") && t.gameObject.activeInHierarchy), "otobüs farı yanıyor");
                Check(lambalar.Any(t => t.name.Contains("Ic") && t.gameObject.activeInHierarchy), "otobüs iç lambaları yanıyor");
                var parlama = FindObjectsByType<MeshRenderer>().FirstOrDefault(r => r.name.ToLowerInvariant().Contains("parlama") || r.name.ToLowerInvariant().Contains("lambabas"));
                Log($"lamba başı parlaması: {(parlama != null ? parlama.name : "bulunamadı")}");
            }
            if (OyunSecimi.Yagmur)
            {
                yield return new WaitForSeconds(2f);
                var ps = FindObjectsByType<ParticleSystem>().FirstOrDefault(p => p.name.ToLowerInvariant().Contains("yagmur"));
                Check(ps != null && ps.isPlaying && ps.particleCount > 50, $"yağmur damlaları ({ps?.particleCount ?? 0} parçacık, en çok {ps?.main.maxParticles ?? 0})");
                Check(RenderSettings.fog, $"yağmurda sis ({RenderSettings.fogStartDistance:F0}–{RenderSettings.fogEndDistance:F0} m)");
            }
        }

        private void HavaSesi(string kamera)
        {
            if (!OyunSecimi.Yagmur) return;
            var s = FindObjectsByType<AudioSource>().FirstOrDefault(a => a.clip != null && a.clip.name.ToLowerInvariant().Contains("yagmur"));
            Log($"yağmur sesi ({kamera}): {(s != null ? $"çalıyor={s.isPlaying} düzey={s.volume:F2}" : "YOK")}");
        }

        // ---------------- Yol gösterici ----------------

        private void Rota()
        {
            if (rehber == null || !rehber.Hazir)
            {
                Check(false, "güzergâh hazır");
                return;
            }
            var route = tracker.Route;
            var yol = rehber.Yol;
            float enUzak = 0f;
            for (int i = 0; i < route.StopCount; i++)
            {
                var p = route.GetStop(i).transform.position;
                float d = yol.Min(q => Vector3.Distance(new Vector3(q.x, 0, q.z), new Vector3(p.x, 0, p.z)));
                enUzak = Mathf.Max(enUzak, d);
            }
            Check(enUzak < 6f, $"sarı güzergâh {yol.Count} nokta, {rehber.Uzunluk:F0} m, her duraktan geçiyor (en uzak {enUzak:F1} m)");
            if (sahne == "Hat1_KizilayAtakule")
            {
                // Kuğulu kavşağında bulvardan Cinnah'a: güzergâhta x 12–28, z 1285–1300 arasında nokta olmalı (sağa dönüş yayı)
                bool donus = yol.Any(q => q.x > 12f && q.x < 29f && q.z > 1284f && q.z < 1300f);
                bool kuzeyeGitmez = !yol.Any(q => q.x < 15f && q.z > 1320f);
                Check(donus && kuzeyeGitmez, "Hat 1 güzergâhı Kuğulu'dan Cinnah'a sağa dönüyor (bulvarda kuzeye devam etmiyor)");
            }
        }

        private IEnumerator Izle()
        {
            var bilgiText = (Text)Alan(mini, "bilgi");
            var harita = (RectTransform)Alan(mini, "harita");
            while (tracker != null && !tracker.Completed)
            {
                if (bilgiText != null && bilgiText.text != sonBilgi)
                {
                    sonBilgi = bilgiText.text;
                    string tek = sonBilgi.Replace("\n", " / ");
                    if (bilgiler.Add(System.Text.RegularExpressions.Regex.Replace(tek, @"\d+", "#")))
                        Log($"bant: '{tek}' (ilerleme {rehber.Konum:F0} m, hız {bus.SpeedKmh:F0})");
                    if (tek.Contains("SAĞA") && sahne == "Hat1_KizilayAtakule" && bus.transform.position.z > 1000f && !sagaGoruldu)
                    {
                        sagaGoruldu = true;
                        float kavsaga = 1292f - bus.transform.position.z;
                        Log($"'SAĞA' kavşağa {kavsaga:F0} m kala çıktı (rehber {rehber.DonuseKalan:F0} m)");
                        StartCoroutine(Ekran("saga_donus"));
                    }
                }
                // mini harita otobüsle dönmeli: haritanın dönüşü + otobüsün yönü sabit
                if (harita != null)
                {
                    float fark = Mathf.Abs(Mathf.DeltaAngle(harita.eulerAngles.z, bus.transform.eulerAngles.y));
                    float fark2 = Mathf.Abs(Mathf.DeltaAngle(-harita.eulerAngles.z, bus.transform.eulerAngles.y));
                    haritaBusFarkMax = Mathf.Max(haritaBusFarkMax, Mathf.Min(fark, fark2));
                }
                // fren lambası
                var fren = bus.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Lamba_Fren");
                if (fren != null && bus.SpeedKmh > 5f && Time.frameCount % 20 == 0)
                {
                    bool frende = bus.BrakePressure > 0.3f, bosta = bus.BrakePressure < 0.02f;
                    if (frende || bosta)
                    {
                        frenOrnek++;
                        if (fren.gameObject.activeInHierarchy == frende) frenYanikDogru++;
                        if (frende && frenOrnek % 15 == 1) StartCoroutine(Ekran("fren_lambasi"));
                    }
                }
                Trafik();
                yield return null;
            }
        }

        private void Trafik()
        {
            foreach (var car in TrafficCar.Aktifler)
            {
                if (car == null || car.Lane == null) continue;
                float lat = (float)Alan(car, "lateral"), latT = (float)Alan(car, "lateralTarget");
                int horn = (int)Alan(car, "hornCount");
                bool dolmus = (bool)Alan(car, "isDolmus");
                var durak = (BusStop)Alan(car, "dolmusStop");
                if (arac.TryGetValue(car, out var o))
                {
                    // sollama şerit değiştirir: yan konum yeni şeride göre bir anda ~3,5 m olur, sonra 0'a kayar
                    if (!dolmus && car.Lane != o.serit && Mathf.Abs(lat) > 2f) { sollama++; Log($"sollama: {car.name} şerit {car.Lane.name} hız {car.Speed * 3.6f:F0}"); }
                    if (horn > o.horn) { korna++; Log($"korna: {car.name} ({horn}. kez), otobüse {Vector3.Distance(car.transform.position, bus.transform.position):F0} m"); }
                    if (dolmus && durak != null && o.durak == null) { dolmusYanasma++; Log($"dolmuş yanaşıyor: {durak.StopName}"); }
                    if (dolmus && durak == null && o.durak != null) { dolmusDevam++; Log($"dolmuş devam: {o.durak.StopName}"); }
                }
                if (dolmus && Mathf.Abs(lat) > 5.5f) kaldirim++;
                arac[car] = (lat, latT, horn, dolmus, durak, car.Lane);
            }

            var gecitler = Liste(FindAnyObjectByType<YayaGecitleri>(), "gecitler");
            foreach (var g in gecitler)
            {
                bool dolu = (bool)g.GetType().GetProperty("Dolu").GetValue(g);
                foreach (var y in Liste(g, "yayalar"))
                    if ((bool)y.GetType().GetProperty("YoldaMi").GetValue(y) && gecenYayalar.Add(y)) yayaGecis++;
                if (!dolu) continue;
                var merkez = (Vector3)g.GetType().GetField("Merkez").GetValue(g);
                foreach (var car in TrafficCar.Aktifler)
                    if (car != null && car.Speed < 0.3f && Vector3.Distance(car.transform.position, merkez) < 15f && yayaIcinDuranlar.Add(car))
                        Log($"yaya için duran araç: {car.name}, geçide {Vector3.Distance(car.transform.position, merkez):F0} m");
            }
        }

        private void Gecitler()
        {
            var yg = FindAnyObjectByType<YayaGecitleri>();
            var gecitler = Liste(yg, "gecitler");
            Log($"yaya geçidi sayısı: {gecitler.Count}");
            foreach (var g in gecitler)
            {
                var m = (Vector3)g.GetType().GetField("Merkez").GetValue(g);
                float ymin = (float)g.GetType().GetField("YanMin").GetValue(g), ymax = (float)g.GetType().GetField("YanMax").GetValue(g);
                var enYakinDurak = Enumerable.Range(0, tracker.Route.StopCount).Select(i => tracker.Route.GetStop(i))
                    .OrderBy(s => Vector3.Distance(s.transform.position, m)).First();
                Log($"geçit: merkez {m:F0}, genişlik {ymax - ymin:F1} m, en yakın durak {enYakinDurak.StopName} ({Vector3.Distance(enYakinDurak.transform.position, m):F0} m)");
            }
        }

        private void Cakisma()
        {
            var harita = (RectTransform)Alan(mini, "harita");
            var bilgiText = (Text)Alan(mini, "bilgi");
            var parca = new List<(string, Rect)>();
            if (harita != null) parca.Add(("mini harita", EkranRect((RectTransform)harita.parent ?? harita)));
            if (bilgiText != null) parca.Add(("bilgi bandı", EkranRect(bilgiText.rectTransform)));
            foreach (var ad in new[] { "EL FRENİ", "KAPILAR", "D", "N", "R" })
            {
                var b = FindObjectsByType<Button>().FirstOrDefault(x => x.name == ad);
                if (b != null) parca.Add((ad, EkranRect((RectTransform)b.transform)));
            }
            var cakisan = new List<string>();
            for (int i = 0; i < parca.Count; i++)
                for (int j = i + 1; j < parca.Count; j++)
                    // bilgi bandı bilerek haritanın altına biniyor (harita etiketi gibi)
                    if (parca[i].Item2.Overlaps(parca[j].Item2) && !(parca[i].Item1 == "mini harita" && parca[j].Item1 == "bilgi bandı") && (parca[i].Item1.Contains("harita") || parca[i].Item1.Contains("bant") || parca[j].Item1.Contains("harita") || parca[j].Item1.Contains("bant")))
                        cakisan.Add($"{parca[i].Item1}×{parca[j].Item1}");
            Check(cakisan.Count == 0, $"mini harita / bilgi bandı düğmelerle çakışmıyor{(cakisan.Count > 0 ? ": " + string.Join(", ", cakisan) : "")}");
        }

        private IEnumerator BuyukHarita()
        {
            var buyuk = (GameObject)Alan(mini, "buyuk");
            var harita = (RectTransform)Alan(mini, "harita");
            var hedef = harita != null ? (RectTransform)harita.parent : null;
            if (buyuk == null || hedef == null) { Check(false, "büyük harita bulunamadı"); yield break; }
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, hedef.position);
            yield return Dokunus(p);
            yield return new WaitForSeconds(0.5f);
            bool acildi = buyuk.activeInHierarchy;
            yield return Ekran("buyuk_harita");
            yield return Dokunus(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return new WaitForSeconds(0.5f);
            Check(acildi && !buyuk.activeInHierarchy, $"mini haritaya dokununca büyük harita açıldı ({acildi}), dokununca kapandı ({!buyuk.activeInHierarchy})");
        }

        private IEnumerator RotadanCik()
        {
            var body = bus.GetComponent<Rigidbody>();
            var eski = body.position;
            var donus = body.rotation;
            OtobusIsinla.Tasi(bus, eski + bus.transform.right * 45f + Vector3.up * 2f, donus);
            yield return new WaitForSeconds(1.5f);
            var bilgiText = (Text)Alan(mini, "bilgi");
            Check(rehber.RotadanCikti && bilgiText != null && bilgiText.text.Contains("ROTADAN"),
                $"rotadan çıkınca uyarı ('{bilgiText?.text.Replace("\n", " / ")}', sapma {rehber.SapmaMesafesi:F0} m)");
            yield return Ekran("rotadan_cikti");
            OtobusIsinla.Tasi(bus, eski, donus);
            yield return new WaitForSeconds(1f);
        }

        // ---------------- Yardımcılar ----------------

        private IEnumerator Ekran(string ad)
        {
            yield return new WaitForEndOfFrame();
            Y11Hat.Kaydet($"y10_{sahne}_{OyunSecimi.SeciliZaman}{(OyunSecimi.Yagmur ? "_yagmur" : "")}_{ad}.png");
        }

        private static object Alan(object o, string ad)
        {
            if (o == null) return null;
            var f = o.GetType().GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return f?.GetValue(o);
        }

        private static List<object> Liste(object o, string ad) =>
            Alan(o, ad) is System.Collections.IEnumerable e ? e.Cast<object>().ToList() : new List<object>();

        private static Rect EkranRect(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private void Dokun(int id, Vector2 pos, TouchPhase phase) =>
            InputSystem.QueueStateEvent(ts, new TouchState { touchId = id, position = pos, phase = phase, pressure = 1f });

        private IEnumerator Dokunus(Vector2 p)
        {
            Dokun(9, p, TouchPhase.Began);
            yield return null;
            Dokun(9, p, TouchPhase.Ended);
            yield return null;
        }
    }
}
