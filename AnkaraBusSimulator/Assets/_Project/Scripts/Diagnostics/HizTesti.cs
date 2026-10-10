using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y15 hız sınırı testi: Hat 1'de Kızılay'dan Atatürk Bulvarı'nda (düz, %2) trafik kaldırılır, otobüs başladığı şeridi
    /// izleyerek sırayla 72, 74,5 ve 78 km/s'de 8'er sn tutulur; her bölümde gelen "Hız sınırı" cezaları sayılır (sınır 70, ceza
    /// 75 üstünde 3 sn'de bir). İlk cezada ekran görüntüsü alınır. Sonuçlar "[Hiz]" satırları. Y11 test build'inde "hiz" görevi.
    /// </summary>
    public class HizTesti : MonoBehaviour
    {
        private BusVehicle bus;
        private BusInput input;
        private TrafficLane serit;
        private int ceza;
        private string sonCeza;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            var puan = bus.GetComponent<SeferPuanlama>();
            puan.Puanlandi += (neden, p) =>
            {
                if (neden.StartsWith("Hız sınırı"))
                {
                    ceza++;
                    sonCeza = neden;
                }
            };
            foreach (var s in FindObjectsByType<TrafficSpawner>())
                s.enabled = false;
            foreach (var c in FindObjectsByType<TrafficCar>())
                Destroy(c.gameObject);

            float enYakin = float.MaxValue;
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
            if (serit == null)
            {
                Debug.Log("[Hiz] HATA şerit bulunamadı");
                yield break;
            }
            Debug.Log($"[Hiz] BASLA otobüs={bus.Definition?.displayName ?? bus.name} şerit={serit.name} ({enYakin:F1} m) sınır {serit.SpeedLimitKmh:F0} km/s");

            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            bus.GetComponent<Rigidbody>().linearVelocity = bus.transform.forward * (72f / 3.6f);
            bus.GetComponent<KorukluOtobus>()?.ArkayiHizala();

            bool goruntu = false;
            foreach (float hedef in new[] { 72f, 74.5f, 78f })
            {
                int once = ceza;
                float enAz = float.MaxValue, enCok = 0f, ilk = -1f;
                for (float t = 0f; t < 8f; t += Time.deltaTime)
                {
                    Sur(hedef);
                    // ilk 1,5 sn hız oturur, ölçüme girmez
                    if (t > 1.5f)
                    {
                        enAz = Mathf.Min(enAz, bus.SpeedKmh);
                        enCok = Mathf.Max(enCok, bus.SpeedKmh);
                    }
                    if (ceza > once && ilk < 0f)
                        ilk = t;
                    if (ceza > 0 && !goruntu)
                    {
                        goruntu = true;
                        StartCoroutine(Goruntu());
                    }
                    yield return null;
                }
                Debug.Log($"[Hiz] hedef {hedef:F1} km/s: ölçülen {enAz:F1}–{enCok:F1} km/s, ceza {ceza - once}" +
                          (ilk >= 0f ? $" (ilki {ilk:F1} sn'de: '{sonCeza}')" : ""));
            }
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = null;
            Debug.Log($"[Hiz] BITTI toplam hız cezası {ceza}");
        }

        /// <summary>Şeridi 18 m ilerisinden izler, hızı gaz/frenle tutar.</summary>
        private void Sur(float hedefKmh)
        {
            var p = bus.transform.position;
            float d = serit.EnYakinKonum(p, out _);
            serit.Sample(d + 18f, out var hedef, out _);
            var yerel = bus.transform.InverseTransformPoint(hedef);
            input.AutoSteer = Mathf.Clamp(Mathf.Atan2(yerel.x, yerel.z) * 1.5f, -0.3f, 0.3f);
            float fark = hedefKmh - bus.SpeedKmh;
            input.AutoThrottle = Mathf.Clamp01(0.55f + fark * 0.4f);
            input.AutoBrake = fark < -1f ? Mathf.Clamp01(-fark * 0.15f) : 0f;
        }

        private static IEnumerator Goruntu()
        {
            yield return new WaitForSeconds(0.4f);
            yield return new WaitForEndOfFrame();
            Y11Hat.Kaydet("y15_hiz_cezasi.png");
        }
    }
}
