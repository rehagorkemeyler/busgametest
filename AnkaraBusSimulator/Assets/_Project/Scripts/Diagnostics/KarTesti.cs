using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y17 kar testi: hava durumu (tutuş, sis, gök, palet malzemesi, kar parçacıkları, rüzgâr sesi) yazılır; ilk durakta ve son durakta
    /// dış ve kokpit kamerasıyla 10'ar sn FPS ve ekran görüntüsü; sonra trafik kaldırılıp 40 km/s sürerken kameranın çevresinde kar
    /// tanesi kalıyor mu (yayıcı otobüsün önüne kayıyor mu). Sonra sonraki hat. Satırlar "[Kar]", görüntüler y17_kar_*.png.
    /// Y11 test build'inde "kar" görevi (-e hava Kar).
    /// </summary>
    public class KarTesti : MonoBehaviour
    {
        private string sonrakiSahne;
        private BusVehicle bus;
        private BusInput input;
        private BusCameraRig rig;
        private Rigidbody on;
        private ParticleSystem kar;
        private AudioSource ruzgar;
        private string sahne;
        private ParticleSystem.Particle[] tampon;

        public void Ayarla(string sonraki) => sonrakiSahne = sonraki;

        private void Log(string s) => Debug.Log("[Kar] " + s);

        private IEnumerator Start()
        {
            sahne = SceneManager.GetActiveScene().name;
            yield return new WaitForSeconds(3f);
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            rig = FindAnyObjectByType<BusCameraRig>();
            on = bus.GetComponent<Rigidbody>();
            var karGo = GameObject.Find("Kar");
            kar = karGo != null ? karGo.GetComponent<ParticleSystem>() : null;
            foreach (var a in FindObjectsByType<AudioSource>())
                if (a.clip != null && a.clip.name == "Ruzgar")
                    ruzgar = a;
            Durum();

            var rota = FindAnyObjectByType<BusRoute>();
            var basPoz = (on.position, on.rotation);
            string ilkAd = rota != null && rota.StopCount > 0 ? Ad(rota.GetStop(0).StopName) : "baslangic";
            yield return Yer(ilkAd);

            if (rota != null && rota.StopCount > 1)
            {
                var son = rota.GetStop(rota.StopCount - 1);
                yield return Tasi(son.transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, son.transform.eulerAngles.y, 0f));
                yield return Yer(Ad(son.StopName));
            }

            // sürerken: trafik kaldırılır, başlangıç şeridinde 40 km/s
            foreach (var s in FindObjectsByType<TrafficSpawner>())
                s.enabled = false;
            foreach (var c in FindObjectsByType<TrafficCar>())
                Destroy(c.gameObject);
            yield return Tasi(basPoz.position, basPoz.rotation);
            var serit = Serit();
            if (serit == null)
                Log("HATA şerit bulunamadı, sürüş atlandı");
            else
            {
                bus.Handbrake = false;
                bus.SetSelector(BusVehicle.GearSelector.Drive);
                on.linearVelocity = bus.transform.forward * (40f / 3.6f);
                bus.GetComponent<KorukluOtobus>()?.ArkayiHizala();
                foreach (var kip in new[] { BusCameraRig.Mode.Chase, BusCameraRig.Mode.Cockpit })
                {
                    rig.SetMode(kip);
                    for (float t = 0f; t < 6f; t += Time.deltaTime)
                    {
                        Sur(serit, 40f);
                        yield return null;
                    }
                    Log($"sürerken {sahne} {BusCameraRig.ModeNames[(int)kip]} hız={bus.SpeedKmh:F0} km/s {Taneler()} rüzgâr={Ruzgar()}");
                    yield return Ekran($"surerken_{KipAd(kip)}");
                }
                input.AutoThrottle = 0f;
                input.AutoBrake = 1f;
                input.AutoSteer = null;
                rig.SetMode(BusCameraRig.Mode.Chase);
            }

            Log($"BITTI {sahne}");
            yield return new WaitForSeconds(2f);
            if (!string.IsNullOrEmpty(sonrakiSahne))
                SceneManager.LoadScene(sonrakiSahne);
            else
                Log("BITTI hepsi");
        }

        private void Durum()
        {
            string palet = "-";
            var binalar = GameObject.Find("Binalar");
            if (binalar != null)
            {
                var r = binalar.GetComponentInChildren<MeshRenderer>();
                if (r != null && r.sharedMaterial != null)
                    palet = r.sharedMaterial.name;
            }
            string ps = kar == null ? "YOK" :
                $"max={kar.main.maxParticles} oran={kar.emission.rateOverTime.constant:F0}/sn gürültü={kar.noise.enabled} " +
                $"boy={kar.main.startSize.constantMin:F2}–{kar.main.startSize.constantMax:F2} m";
            Log($"DURUM sahne={sahne} zaman={OyunSecimi.SeciliZaman} hava={OyunSecimi.SeciliHava} otobüs={bus.Definition?.displayName} " +
                $"kalite={GrafikAyarlari.Mevcut} tutuş={BusVehicle.HavaTutusu:F2} sis={(RenderSettings.fog ? $"{RenderSettings.fogStartDistance:F0}–{RenderSettings.fogEndDistance:F0} m renk={RenderSettings.fogColor}" : "kapalı")} " +
                $"gök={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "-")} palet={palet} güneş={(RenderSettings.sun != null ? RenderSettings.sun.intensity.ToString("F2") : "-")} " +
                $"kar=({ps}) rüzgâr_sesi={(ruzgar != null ? "var" : "YOK")}");
        }

        /// <summary>Bir yerde: dış ve kokpit kamerası, her biri 10 sn FPS + görüntü + kameranın çevresindeki tane sayısı + rüzgâr düzeyi.</summary>
        private IEnumerator Yer(string yer)
        {
            foreach (var kip in new[] { BusCameraRig.Mode.Chase, BusCameraRig.Mode.Cockpit })
            {
                rig.SetMode(kip);
                yield return new WaitForSeconds(1.5f);
                var kareler = new List<float>();
                float bas = Time.unscaledTime;
                while (Time.unscaledTime - bas < 10f)
                {
                    yield return null;
                    kareler.Add(Time.unscaledDeltaTime);
                }
                float toplam = 0f;
                foreach (var k in kareler)
                    toplam += k;
                kareler.Sort();
                float p99 = kareler[Mathf.Min(kareler.Count - 1, (int)(kareler.Count * 0.99f))];
                int yavas = 0;
                foreach (var k in kareler)
                    if (k > 1f / 50f) yavas++;
                Log($"{sahne} {yer} {BusCameraRig.ModeNames[(int)kip]} FPS {kareler.Count / toplam:F1}, 1% en yavaş {p99 * 1000f:F1} ms, " +
                    $"en uzun {kareler[kareler.Count - 1] * 1000f:F0} ms, 20 ms üstü %{100f * yavas / kareler.Count:F1} {Taneler()} rüzgâr={Ruzgar()}");
                yield return Ekran($"{yer}_{KipAd(kip)}");
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
        }

        /// <summary>Kar parçacıkları: toplam ve kameranın 15 m çevresinde (yatay), göz hizasının 3 m altı ile 8 m üstü arası.</summary>
        private string Taneler()
        {
            if (kar == null)
                return "kar=YOK";
            if (tampon == null || tampon.Length < kar.main.maxParticles)
                tampon = new ParticleSystem.Particle[kar.main.maxParticles];
            int n = kar.GetParticles(tampon);
            var cam = Camera.main != null ? Camera.main.transform.position : rig.transform.position;
            int yakin = 0;
            for (int i = 0; i < n; i++)
            {
                var d = tampon[i].position - cam;
                if (d.y > -3f && d.y < 8f && new Vector2(d.x, d.z).sqrMagnitude < 15f * 15f)
                    yakin++;
            }
            var yayici = kar.transform.position - cam;
            return $"tane={n} yakın(15 m)={yakin} yayıcı_kaydırma={new Vector2(yayici.x, yayici.z).magnitude:F0} m";
        }

        private string Ruzgar() => ruzgar != null ? $"{ruzgar.volume:F2} (içeride={rig.IsInside})" : "-";

        private IEnumerator Tasi(Vector3 konum, Quaternion donus)
        {
            OtobusIsinla.Tasi(bus, konum, donus);
            bus.Handbrake = true;
            var arka = bus.GetComponent<KorukluOtobus>()?.ArkaGovde;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var rb in arka != null ? new[] { on, arka } : new[] { on })
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            rig.SetTarget(bus);
            yield return new WaitForSeconds(1f);
        }

        private TrafficLane Serit()
        {
            TrafficLane serit = null;
            float enYakin = 6f;
            foreach (var l in FindObjectsByType<TrafficLane>())
            {
                float d = l.EnYakinKonum(bus.transform.position, out float u);
                l.Sample(d, out _, out var f);
                if (u < enYakin && Vector3.Dot(f, bus.transform.forward) > 0.9f)
                {
                    enYakin = u;
                    serit = l;
                }
            }
            return serit;
        }

        /// <summary>Şeridi 18 m ilerisinden izler, hızı gaz/frenle tutar (HizTesti ile aynı).</summary>
        private void Sur(TrafficLane serit, float hedefKmh)
        {
            float d = serit.EnYakinKonum(bus.transform.position, out _);
            serit.Sample(d + 18f, out var hedef, out _);
            var yerel = bus.transform.InverseTransformPoint(hedef);
            input.AutoSteer = Mathf.Clamp(Mathf.Atan2(yerel.x, yerel.z) * 1.5f, -0.3f, 0.3f);
            float fark = hedefKmh - bus.SpeedKmh;
            input.AutoThrottle = Mathf.Clamp01(0.55f + fark * 0.4f);
            input.AutoBrake = fark < -1f ? Mathf.Clamp01(-fark * 0.15f) : 0f;
        }

        private static string Ad(string s) =>
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');

        private static string KipAd(BusCameraRig.Mode kip) => kip == BusCameraRig.Mode.Cockpit ? "kokpit" : "dis";

        private IEnumerator Ekran(string ad)
        {
            yield return new WaitForEndOfFrame();
            if (Application.isBatchMode)
                yield break;
            Y11Hat.Kaydet($"y17_kar_{sahne}_{OyunSecimi.SeciliZaman}_{bus.name.Split('_')[0].ToLowerInvariant()}_{ad}.png");
        }
    }
}
