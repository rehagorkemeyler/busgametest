using System.Collections;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y16 anons testi (otomatik pilotla gerçek zamanlı sürüş): her durakta anons, yolcunun "durakta inecek var" sesi
    /// anonsla üst üste biniyor mu, anonsun ses düzeyi kameraya göre (durak sırasıyla kokpit, yolcu, dış kamera) ve FPS.
    /// Satırlar "[AnonsTest]"; anonsların kendisi "[Anons]". Y11 test build'inde "anons" görevi.
    /// </summary>
    public class AnonsTesti : MonoBehaviour
    {
        private static readonly BusCameraRig.Mode[] Kipler = { BusCameraRig.Mode.Cockpit, BusCameraRig.Mode.Interior, BusCameraRig.Mode.Chase };

        private BusVehicle bus;
        private BusCameraRig rig;
        private RouteTracker tracker;
        private AnonsSistemi anons;
        private BusAudio busAudio;
        private string hat;
        private int durak;
        private float anonsBasi = -100f;
        private string sonAnons;

        // FPS: 10 sn'lik pencereler ve bütün sefer
        private float pencereBasi, pencereSure, toplamSure;
        private int pencereKare, toplamKare;
        private float enKotuPencere = float.MaxValue, enUzunKare;
        private readonly System.Collections.Generic.List<float> kareler = new System.Collections.Generic.List<float>();

        private static void Log(string s) => Debug.Log("[AnonsTest] " + s);

        private IEnumerator Start()
        {
            hat = SceneManager.GetActiveScene().name;
            yield return new WaitForSeconds(1f);
            bus = FindAnyObjectByType<BusVehicle>();
            rig = FindAnyObjectByType<BusCameraRig>();
            tracker = bus.GetComponent<RouteTracker>();
            anons = bus.GetComponent<AnonsSistemi>();
            busAudio = bus.GetComponent<BusAudio>();
            tracker.StopServed += s => Durak(s, "tamamlandı");
            tracker.StopSkipped += s => Durak(s, "ATLANDI");
            tracker.RouteCompleted += Bitti;
            if (busAudio != null)
                busAudio.OneShotPlayed += SesCaldi;
            rig.SetMode(Kipler[0]);
            Log($"BASLA {hat} otobüs={bus.Definition?.displayName} anons={(anons != null ? "var" : "YOK")} kip={rig.CurrentMode} kalite={AnkaraBus.GrafikAyarlari.Mevcut}");
            pencereBasi = Time.unscaledTime;
        }

        private void Durak(BusStop s, string durum)
        {
            durak++;
            // sıradaki durağın anonsu yeni kipte duyulsun
            var kip = Kipler[durak % Kipler.Length];
            rig.SetMode(kip);
            Log($"durak {s.StopName} {durum}; sıradaki={tracker.NextStop?.StopName ?? "-"} kamera={kip}");
        }

        private void Bitti()
        {
            Log($"hat bitti {hat}");
            FpsOzet();
        }

        private void SesCaldi(string klip, float hacim)
        {
            bool caliyor = anons != null && anons.Caliyor;
            if (klip.ToLowerInvariant().Contains("stop") || klip.ToLowerInvariant().Contains("inecek") || klip.ToLowerInvariant().Contains("request"))
                Log($"inecek var sesi ({klip}) anons_çalıyor={caliyor} son_anonstan={Time.time - anonsBasi:F1} sn {(caliyor ? "ÜST ÜSTE" : "OK")}");
        }

        private void Update()
        {
            if (bus == null)
                return;
            if (anons != null && anons.SonAnons != sonAnons)
            {
                sonAnons = anons.SonAnons;
                anonsBasi = Time.time;
                var kaynak = AnonsKaynagi();
                Log($"anons {sonAnons} hız={bus.SpeedKmh:F0} km/s kapı_açık={bus.GetComponentInChildren<BusDoorController>()?.AnyOpen} " +
                    $"kamera={rig.CurrentMode} içeride={busAudio?.InteriorBlend:F2} anons_düzeyi={(kaynak != null ? kaynak.volume.ToString("F2") : "-")}");
            }

            float dt = Time.unscaledDeltaTime;
            pencereKare++;
            toplamKare++;
            toplamSure += dt;
            kareler.Add(dt);
            enUzunKare = Mathf.Max(enUzunKare, dt);
            if (Time.unscaledTime - pencereBasi >= 10f)
            {
                pencereSure = Time.unscaledTime - pencereBasi;
                float fps = pencereKare / pencereSure;
                enKotuPencere = Mathf.Min(enKotuPencere, fps);
                Log($"fps {fps:F1} (10 sn) ilerleme_durak={tracker.NextStopIndex} hız={bus.SpeedKmh:F0} kamera={rig.CurrentMode}");
                pencereBasi = Time.unscaledTime;
                pencereKare = 0;
            }
        }

        private AudioSource AnonsKaynagi()
        {
            // AnonsSistemi otobüsün kökündeki ikinci (yeni eklenen) AudioSource; 3B değil
            foreach (var a in bus.GetComponents<AudioSource>())
                if (a.spatialBlend == 0f && !a.loop)
                    return a;
            return null;
        }

        private void FpsOzet()
        {
            if (toplamKare == 0)
                return;
            kareler.Sort();
            float p99 = kareler[Mathf.Min(kareler.Count - 1, (int)(kareler.Count * 0.99f))];
            int yavas = 0;
            foreach (var k in kareler)
                if (k > 1f / 50f) yavas++;
            Log($"FPS {hat}: ortalama {toplamKare / toplamSure:F1}, en kötü 10 sn {enKotuPencere:F1}, 1% en yavaş kare {p99 * 1000f:F1} ms, " +
                $"en uzun kare {enUzunKare * 1000f:F0} ms, 20 ms üstü kare %{100f * yavas / kareler.Count:F2}, süre {toplamSure:F0} sn");
        }

        private void OnDestroy()
        {
            if (busAudio != null)
                busAudio.OneShotPlayed -= SesCaldi;
        }
    }
}
