using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y17 yol tutuşu testi (Hat 1, trafik kaldırılır). Kuru (1), yağmur (0,85) ve kar (0,6) için BusVehicle.HavaTutusu doğrudan
    /// ayarlanır; her biri için:
    /// 1) fren: Atatürk Bulvarı'nda (başlangıç şeridi, düz) 50 km/s'den tam fren, duruş mesafesi ve süresi, en büyük teker kayması;
    /// 2) Cinnah yokuşu: en dik yerde fren tutarken duruştan kalkış (tam gaz ve yarım gaz): geri kayma, patinaj, 6 sn sonraki hız;
    /// 3) keskin dönüş: Bulvar → Cinnah bağlantı şeridi (sağa dönüş) 25 ve 35 km/s, şeridi izlerken en büyük sapma, kayma açısı.
    /// Satırlar "[Tutus]". Y11 test build'inde "tutus" görevi.
    /// </summary>
    public class TutusTesti : MonoBehaviour
    {
        private BusVehicle bus;
        private BusInput input;
        private Rigidbody on;
        private Rigidbody arka;
        private KorukluOtobus kor;
        private WheelCollider[] tekerler;
        private readonly Dictionary<string, TrafficLane> seritler = new Dictionary<string, TrafficLane>();

        private void Log(string s) => Debug.Log("[Tutus] " + s);

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            on = bus.GetComponent<Rigidbody>();
            kor = bus.GetComponent<KorukluOtobus>();
            arka = kor != null ? kor.ArkaGovde : null;
            var liste = new List<WheelCollider>(bus.GetComponentsInChildren<WheelCollider>());
            if (arka != null)
                liste.AddRange(arka.GetComponentsInChildren<WheelCollider>());
            tekerler = liste.ToArray();
            foreach (var s in FindObjectsByType<TrafficSpawner>())
                s.enabled = false;
            foreach (var c in FindObjectsByType<TrafficCar>())
                Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<TrafficLane>())
                seritler[l.name] = l;
            float havadan = BusVehicle.HavaTutusu;
            string otobus = bus.Definition?.displayName ?? bus.name;
            Log($"BASLA sahne={SceneManager.GetActiveScene().name} otobüs={otobus} kütle={on.mass:F0}{(arka != null ? $"+{arka.mass:F0}" : "")} kg teker={tekerler.Length} havadan_tutuş={havadan:F2}");

            var basPoz = (on.position, on.rotation);
            var bulvar = EnYakinSerit();
            seritler.TryGetValue("Serit_Cinnah_Gidis_2", out var cinnah);
            seritler.TryGetValue("Serit_Baglanti_Bulvar_Cinnah", out var baglanti);

            foreach (var (ad, g) in new[] { ("kuru", 1f), ("yagmur", 0.85f), ("kar", 0.6f) })
            {
                BusVehicle.HavaTutusu = g;
                if (bulvar != null)
                    yield return Fren(ad, basPoz.position, basPoz.rotation, bulvar);
                if (cinnah != null)
                {
                    yield return Kalkis(ad, cinnah, 1f);
                    yield return Kalkis(ad, cinnah, 0.5f);
                }
                if (baglanti != null)
                {
                    yield return Donus(ad, baglanti, 25f);
                    yield return Donus(ad, baglanti, 35f);
                }
            }
            BusVehicle.HavaTutusu = havadan;
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = null;
            Log($"BITTI {otobus}");
        }

        // ------------------------------------------------------------ fren

        private IEnumerator Fren(string ad, Vector3 konum, Quaternion donus, TrafficLane serit)
        {
            yield return Tasi(konum, donus);
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            on.linearVelocity = bus.transform.forward * (50f / 3.6f);
            kor?.ArkayiHizala();
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                Sur(serit, 50f, 18f, 0.3f);
                yield return null;
            }
            float v0 = bus.SpeedKmh;
            var p0 = on.position;
            float t0 = Time.time, kayma = 0f, sapma = 0f;
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            while (bus.SpeedKmh > 0.3f && Time.time - t0 < 15f)
            {
                yield return new WaitForFixedUpdate();
                kayma = Mathf.Max(kayma, EnBuyukKayma(true));
                serit.EnYakinKonum(on.position, out float u);
                sapma = Mathf.Max(sapma, u);
                // direksiyon şeritte tutulur (fren kayınca yön kaybı ölçülsün diye düzeltme yumuşak)
                Sur(serit, -1f, 18f, 0.3f);
            }
            var fark = on.position - p0;
            fark.y = 0f;
            Log($"fren {ad} tutuş={BusVehicle.HavaTutusu:F2}: {v0:F1} km/s'den duruş {fark.magnitude:F1} m, {Time.time - t0:F1} sn, " +
                $"en büyük ileri kayma {kayma:F2}, şeritten sapma {sapma:F2} m{(kor != null ? $" mafsal {kor.MafsalAcisi:F1}°" : "")}");
        }

        // ------------------------------------------------------------ Cinnah yokuşu

        private IEnumerator Kalkis(string ad, TrafficLane serit, float gaz)
        {
            // en dik 20 m
            float enDik = 0f, enIyiD = serit.Length * 0.5f;
            for (float d = 20f; d < serit.Length - 40f; d += 5f)
            {
                serit.Sample(d, out var a, out _);
                serit.Sample(d + 20f, out var b, out _);
                float e = (b.y - a.y) / 20f;
                if (e > enDik)
                {
                    enDik = e;
                    enIyiD = d;
                }
            }
            serit.Sample(enIyiD, out var p, out _);
            serit.Sample(enIyiD + 6f, out var p2, out _);
            // eğime oturtulur (düz konursa ön tekerler yokuşa gömülür)
            yield return Tasi(p + Vector3.up * 0.3f, Quaternion.LookRotation(p2 - p));
            // fren tutarken vitese al, sonra freni bırakıp gaza bas
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(1f);
            var p0 = on.position;
            var ileri = bus.transform.forward;
            ileri.y = 0f;
            ileri.Normalize();
            input.AutoBrake = 0f;
            input.AutoThrottle = gaz;
            float geri = 0f, patinaj = 0f, ilerleme = 0f, t3 = -1f;
            for (float t = 0f; t < 6f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                Sur(serit, -1f, 12f, 0.5f);
                input.AutoThrottle = gaz;
                ilerleme = Vector3.Dot(on.position - p0, ileri);
                geri = Mathf.Min(geri, ilerleme);
                patinaj = Mathf.Max(patinaj, EnBuyukKayma(true));
                if (t3 < 0f && bus.SpeedKmh >= 10f)
                    t3 = t;
            }
            Log($"kalkış {ad} tutuş={BusVehicle.HavaTutusu:F2} Cinnah eğim %{enDik * 100f:F1} gaz {gaz:F1}: geri kayma {-geri:F2} m, " +
                $"en büyük patinaj (ileri kayma) {patinaj:F2}, 10 km/s'ye {(t3 < 0f ? "ULAŞAMADI" : $"{t3:F1} sn")}, 6 sn'de {bus.SpeedKmh:F1} km/s, {ilerleme:F1} m");
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------ keskin dönüş

        private IEnumerator Donus(string ad, TrafficLane serit, float hiz)
        {
            // bağlantı şeridinin 25 m gerisinden (bulvar yönünde) giriş
            serit.Sample(0f, out var p, out var f);
            f.y = 0f;
            f.Normalize();
            yield return Tasi(p - f * 25f + Vector3.up * 0.3f, Quaternion.LookRotation(f));
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            on.linearVelocity = f * (hiz / 3.6f);
            kor?.ArkayiHizala();
            float sapma = 0f, aci = 0f, yanKayma = 0f, donusHizi = 0f, enDusuk = float.MaxValue, mafsal = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 12f)
            {
                yield return new WaitForFixedUpdate();
                float d = serit.EnYakinKonum(on.position, out float u);
                // şeridi 10 m ilerisinden, tam direksiyonla izle; hız sabit
                Sur(serit, hiz, 10f, 1f);
                if (d > 2f && d < serit.Length - 2f)
                {
                    sapma = Mathf.Max(sapma, u);
                    var v = on.linearVelocity;
                    v.y = 0f;
                    if (v.magnitude > 2f)
                        aci = Mathf.Max(aci, Vector3.Angle(v, Vector3.ProjectOnPlane(bus.transform.forward, Vector3.up)));
                    yanKayma = Mathf.Max(yanKayma, EnBuyukKayma(false));
                    donusHizi = Mathf.Max(donusHizi, Mathf.Abs(on.angularVelocity.y) * Mathf.Rad2Deg);
                    enDusuk = Mathf.Min(enDusuk, bus.SpeedKmh);
                    if (kor != null)
                        mafsal = Mathf.Max(mafsal, Mathf.Abs(kor.MafsalAcisi));
                }
                if (d >= serit.Length - 2f)
                    break;
            }
            Log($"dönüş {ad} tutuş={BusVehicle.HavaTutusu:F2} {hiz:F0} km/s (şerit {serit.Length:F0} m): en büyük sapma {sapma:F2} m, " +
                $"kayma açısı {aci:F1}°, teker yan kayması {yanKayma:F2}, dönüş hızı {donusHizi:F0}°/sn, en düşük hız {enDusuk:F0} km/s" +
                $"{(kor != null ? $", mafsal {mafsal:F0}°" : "")}{(Time.time - t0 >= 12f ? " (şerit sonuna varmadı)" : "")}");
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(1.5f);
        }

        // ------------------------------------------------------------ yardımcılar

        private float EnBuyukKayma(bool ileri)
        {
            float k = 0f;
            foreach (var w in tekerler)
                if (w != null && w.GetGroundHit(out var hit))
                    k = Mathf.Max(k, Mathf.Abs(ileri ? hit.forwardSlip : hit.sidewaysSlip));
            return k;
        }

        /// <summary>Şeridi 'bakis' m ilerisinden izler; hedefKmh &lt; 0 ise yalnızca direksiyon.</summary>
        private void Sur(TrafficLane serit, float hedefKmh, float bakis, float sinir)
        {
            float d = serit.EnYakinKonum(bus.transform.position, out _);
            serit.Sample(d + bakis, out var hedef, out _);
            var yerel = bus.transform.InverseTransformPoint(hedef);
            input.AutoSteer = Mathf.Clamp(Mathf.Atan2(yerel.x, yerel.z) * 1.5f, -sinir, sinir);
            if (hedefKmh < 0f)
                return;
            float fark = hedefKmh - bus.SpeedKmh;
            input.AutoThrottle = Mathf.Clamp01(0.55f + fark * 0.4f);
            input.AutoBrake = fark < -1f ? Mathf.Clamp01(-fark * 0.15f) : 0f;
        }

        private TrafficLane EnYakinSerit()
        {
            TrafficLane serit = null;
            float enYakin = 6f;
            foreach (var l in seritler.Values)
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
                Log("HATA başlangıç şeridi bulunamadı, fren atlandı");
            return serit;
        }

        private IEnumerator Tasi(Vector3 konum, Quaternion donus)
        {
            input.AutoThrottle = 0f;
            input.AutoBrake = 0f;
            input.AutoSteer = 0f;
            OtobusIsinla.ZemineTasi(bus, konum, donus, 0.3f);
            bus.Handbrake = true;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var rb in arka != null ? new[] { on, arka } : new[] { on })
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
