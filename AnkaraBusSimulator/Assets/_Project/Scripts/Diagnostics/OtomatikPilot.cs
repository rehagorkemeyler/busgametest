using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Hat 1'i baştan sona kendisi süren test pilotu: bulvarın sağ şeridi, kavşakta sağa dönüş, Cinnah'ın sağ şeridi.
    /// Öndeki araca göre yavaşlar, her durakta durup kapıları açar, RouteTracker durağı tamamlayınca devam eder.
    /// Sonuçlar "[Surus]" satırları olarak loga yazılır. Yalnızca sürüş testi build'inde sahneye eklenir.
    /// </summary>
    public class OtomatikPilot : MonoBehaviour
    {
        [SerializeField] private string bulvarSeridi = "Serit_Bulvar_Gidis_3";
        [SerializeField] private string cinnahSeridi = "Serit_Cinnah_Gidis_2";
        [SerializeField] private float bulvarHizKmh = 45f;
        [SerializeField] private float cinnahHizKmh = 35f;
        [SerializeField] private float rahatYavaslama = 1.3f;
        [Tooltip("Bir yerde bu kadar saniye kalırsa (durak hariç) takıldı sayılır.")]
        [SerializeField] private float takilmaSuresi = 45f;

        private readonly List<Vector3> yol = new List<Vector3>();
        private readonly List<float> yolMesafe = new List<float>();
        private readonly List<float> yolHiz = new List<float>();
        private BusVehicle bus;
        private BusInput input;
        private BusDoorController doors;
        private RouteTracker tracker;
        private Collider[] ownColliders;
        private float ilerleme;
        private float baslangic;
        private TrafficCar[] trafik;
        private int aracIcinYavaslama;
        private bool aracOnde;

        private IEnumerator Start()
        {
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            doors = bus.GetComponentInChildren<BusDoorController>();
            tracker = bus.GetComponent<RouteTracker>();
            ownColliders = bus.GetComponentsInChildren<Collider>();
            if (!YoluKur())
                yield break;

            tracker.StopServed += stop => Debug.Log($"[Surus] DURAK {stop.StopName} tamamlandı, t={Time.time - baslangic:F0} sn");
            tracker.RouteCompleted += () => Debug.Log($"[Surus] HAT TAMAMLANDI, süre {Time.time - baslangic:F0} sn");

            yield return new WaitForSeconds(3f);
            baslangic = Time.time;
            trafik = FindObjectsByType<TrafficCar>();
            Debug.Log($"[Surus] BASLA yol={yolMesafe[yolMesafe.Count - 1]:F0} m, durak={tracker.Route.StopCount}, trafik={trafik.Length} araç");

            float sonIlerleme = 0f, sonIlerlemeZamani = Time.time;
            float logZamani = 0f;
            while (!tracker.Completed)
            {
                var stop = tracker.NextStop;
                float stopMesafe = MesafeYolBoyunca(stop.transform.position);

                // Durağa geldik mi: durak alanında ve yol üzerinde durak noktasına çok yakın
                if (stop.Contains(bus.transform.position) && stopMesafe - ilerleme < 1.5f)
                {
                    yield return DuraktaBekle(stop);
                    sonIlerlemeZamani = Time.time;
                    continue;
                }

                Sur(stopMesafe);

                if (ilerleme > sonIlerleme + 5f)
                {
                    sonIlerleme = ilerleme;
                    sonIlerlemeZamani = Time.time;
                }
                else if (Time.time - sonIlerlemeZamani > takilmaSuresi)
                {
                    Debug.LogError($"[Surus] TAKILDI konum={bus.transform.position} ilerleme={ilerleme:F0} m hız={bus.SpeedKmh:F0}");
                    sonIlerlemeZamani = Time.time;
                }

                if (Time.time > logZamani)
                {
                    logZamani = Time.time + 15f;
                    Debug.Log($"[Surus] t={Time.time - baslangic:F0} ilerleme={ilerleme:F0} m hız={bus.SpeedKmh:F0} vites={bus.Gear} " +
                              $"sıradaki={stop.StopName} yakın_hareketli_araç={YakinHareketliArac()} araç_için_yavaşlama={aracIcinYavaslama}");
                }
                yield return null;
            }

            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
        }

        private bool YoluKur()
        {
            TrafficLane bulvar = null, cinnah = null;
            foreach (var lane in FindObjectsByType<TrafficLane>())
            {
                if (lane.name == bulvarSeridi) bulvar = lane;
                if (lane.name == cinnahSeridi) cinnah = lane;
            }
            if (bulvar == null || cinnah == null)
            {
                Debug.LogError("[Surus] Şeritler bulunamadı");
                return false;
            }

            // Bulvar: Cinnah'ın başlangıcından ~15 m öncesine kadar
            cinnah.Sample(0f, out var cinnahBas, out var cinnahYon);
            for (float d = 0f; d < bulvar.Length; d += 2f)
            {
                bulvar.Sample(d, out var p, out var f);
                if (Vector3.Dot(cinnahBas - p, f) < 16f)
                    break;
                Ekle(p, bulvarHizKmh);
            }

            // Kavşakta sağa dönüş: kontrol noktası iki şeridin kesişimi olan Bezier eğrisi
            Vector3 a = yol[yol.Count - 1];
            Vector3 aYon = (a - yol[yol.Count - 2]).normalized;
            Vector3 kose = a + aYon * Vector3.Dot(cinnahBas - a, aYon);
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                Ekle((1f - t) * (1f - t) * a + 2f * t * (1f - t) * kose + t * t * cinnahBas, 15f);
            }

            for (float d = 2f; d <= cinnah.Length; d += 2f)
            {
                cinnah.Sample(d, out var p, out _);
                Ekle(p, cinnahHizKmh);
            }

            // Virajlarda hız: önümüzdeki 25 m'deki yön değişimine göre
            for (int i = 0; i < yol.Count; i++)
            {
                int j = Mathf.Min(yol.Count - 1, i + 12);
                if (j <= i + 1 || i == 0) continue;
                float aci = Vector3.Angle(Duz(yol[i] - yol[i - 1]), Duz(yol[j] - yol[j - 1]));
                if (aci > 12f)
                    yolHiz[i] = Mathf.Min(yolHiz[i], Mathf.Lerp(30f, 15f, Mathf.InverseLerp(12f, 60f, aci)));
            }
            return true;
        }

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
                float d = (Duz(yol[i] - p)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return yolMesafe[best];
        }

        private void Sur(float stopMesafe)
        {
            Vector3 pos = bus.transform.position;
            int index = EnYakin(pos);
            ilerleme = yolMesafe[index];
            float v = Mathf.Max(0f, bus.ForwardSpeed);

            // Pure pursuit
            float bakis = Mathf.Clamp(7f + v * 0.9f, 7f, 22f);
            int hedef = index;
            while (hedef < yol.Count - 1 && yolMesafe[hedef] - ilerleme < bakis)
                hedef++;
            Vector3 local = bus.transform.InverseTransformPoint(yol[hedef]);
            float alpha = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f));
            float wheelbase = 5.6f;
            float steerDeg = Mathf.Atan2(2f * wheelbase * Mathf.Sin(alpha), bakis) * Mathf.Rad2Deg;
            input.AutoSteer = Mathf.Clamp(steerDeg / Mathf.Max(bus.Spec.maxSteerAngle * 0.95f, 1f), -1f, 1f);

            // Hedef hız: yol hızı, durağa yaklaşma, öndeki araç
            float hedefHiz = yolHiz[Mathf.Min(yol.Count - 1, index + 6)] / 3.6f;
            float durakKalan = stopMesafe - ilerleme;
            if (durakKalan > 0f)
                hedefHiz = Mathf.Min(hedefHiz, Mathf.Sqrt(2f * rahatYavaslama * Mathf.Max(0f, durakKalan - 0.5f)) + 0.6f);
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

            float fark = hedefHiz - v;
            input.AutoThrottle = fark > 0.3f ? Mathf.Clamp01(fark * 0.35f + 0.15f) : 0f;
            input.AutoBrake = fark < -0.5f ? Mathf.Clamp01(-fark * 0.25f) : (hedefHiz < 0.2f ? 0.6f : 0f);
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

        private int EnYakin(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            // İleri doğru ara (geri kalan noktalara dönmemek için mevcut ilerlemenin biraz gerisinden başla)
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
            Vector3 origin = t.position + t.up * 1.5f + t.forward * 6.5f;
            var hits = Physics.SphereCastAll(origin, 1.1f, t.forward, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                if (System.Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.rigidbody == null) continue; // yol ve binalar yok sayılır, araçlar Rigidbody'li
                best = Mathf.Min(best, hit.distance);
            }
            return best;
        }

        private IEnumerator DuraktaBekle(BusStop stop)
        {
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            while (!bus.IsStopped)
                yield return null;
            yield return new WaitForSeconds(0.5f);
            int beklenen = tracker.NextStopIndex + 1;
            doors.ToggleAll();
            float t0 = Time.time;
            while (tracker.NextStopIndex < beklenen && Time.time - t0 < 20f)
                yield return null;
            if (tracker.NextStopIndex < beklenen)
                Debug.LogError($"[Surus] {stop.StopName} durağı 20 sn'de tamamlanmadı (kapılar açık={doors.AnyOpen})");
            if (doors.AnyOpen)
                doors.ToggleAll();
            yield return new WaitForSeconds(1.6f);
            input.AutoBrake = 0f;
        }
    }
}
