using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y14 kamera testi: her kamera kipini (DIŞ, KOKPİT, YOLCU, KAPI, SERBEST) durakta, binaların arasında, düz yolda,
    /// dönüşte (körüklüde mafsal kırıkken) ve geri giderken dener. Her çekimde: kamera otobüsün gövde kutusunun içinde mi
    /// (dış/kapı/serbest), bir binanın/durağın içinde mi, 1 sn boyunca kareden kareye otobüse göre sıçrıyor mu, titriyor mu.
    /// Ayrıca yolcu kamerasından yan duvarlara ve teker yuvalarına bakan görüntüler (Y13 kontrolleri).
    /// Sonuçlar "[Kamera]" satırları, görüntüler persistentDataPath/y15_kamera_*.png. Y11 test build'inde "kamera" görevi.
    /// </summary>
    public class KameraTesti : MonoBehaviour
    {
        private BusVehicle bus;
        private BusInput input;
        private BusCameraRig rig;
        private KorukluOtobus kor;
        private Rigidbody on;
        private OtobusOlcusu olcu;
        private string otobus;
        private string sahne;
        private readonly List<Collider> kendi = new List<Collider>();
        private int sorun;

        private void Log(string s) => Debug.Log("[Kamera] " + s);

        private IEnumerator Start()
        {
            sahne = SceneManager.GetActiveScene().name;
            yield return new WaitForSeconds(2f);
            bus = FindAnyObjectByType<BusVehicle>();
            rig = FindAnyObjectByType<BusCameraRig>();
            input = bus.GetComponent<BusInput>();
            kor = bus.GetComponent<KorukluOtobus>();
            on = bus.GetComponent<Rigidbody>();
            olcu = OtobusOlcusu.Olc(bus);
            otobus = bus.name.Split('_')[0].ToLowerInvariant();
            kendi.AddRange(bus.GetComponentsInChildren<Collider>(true));
            if (kor != null && kor.ArkaGovde != null)
                kendi.AddRange(kor.ArkaGovde.GetComponentsInChildren<Collider>(true));
            Log($"BASLA sahne={sahne} otobüs={bus.Definition?.displayName} kalite={AnkaraBus.GrafikAyarlari.Mevcut} boy=+{olcu.On:F1}/-{olcu.Arka:F1}");

            // 1) başlangıç: Kızılay, binaların arasında, duruyor
            yield return Kipler("baslangic");
            // yolcu kamerasından yanlara (Y13: Conecto iç renkler, Millennium teker yuvaları, BMC etiketleri)
            foreach (var (ad, yaw, pitch) in new[] { ("yolcu_sag_on", 50f, 22f), ("yolcu_sol_on", -50f, 22f), ("yolcu_sag", 95f, 10f),
                                                     ("yolcu_sol", -95f, 10f), ("yolcu_arka", 175f, 5f), ("kokpit_sag", 60f, 5f) })
            {
                rig.SetMode(ad.StartsWith("kokpit") ? BusCameraRig.Mode.Cockpit : BusCameraRig.Mode.Interior);
                Alan("lookYaw", yaw);
                Alan("lookPitch", pitch);
                // kokpitte bırakınca yola döner: dokunma süresi sıfırlansın
                Alan("idleTime", 0f);
                yield return null;
                Alan("idleTime", 0f);
                yield return Ekran(ad);
            }

            // 2) durakta (ikinci durak)
            var rota = FindAnyObjectByType<BusRoute>();
            var durak = rota != null && rota.StopCount > 1 ? rota.GetStop(1) : null;
            if (durak != null)
            {
                OtobusIsinla.Tasi(bus, durak.transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, durak.transform.eulerAngles.y, 0f));
                bus.Handbrake = true;
                yield return new WaitForSeconds(2f);
                rig.SetTarget(bus);
                yield return Kipler("durak_" + Ad(durak.StopName));
            }

            // 3) düz yol: ilk durağın önünden 30 km/s
            var ilk = rota != null && rota.StopCount > 0 ? rota.GetStop(0) : null;
            if (ilk != null)
            {
                OtobusIsinla.Tasi(bus, ilk.transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, ilk.transform.eulerAngles.y, 0f));
                yield return new WaitForSeconds(1f);
                rig.SetTarget(bus);
            }
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            yield return Kipler("duz", hedefHiz: 8f, direksiyon: 0f, tekKip: true);

            // 4) dönüş: 15 km/s tam sağ (körüklüde mafsal kırılır)
            yield return Kipler("donus", hedefHiz: 4f, direksiyon: 1f, tekKip: true);

            // 5) geri: dur, geri vites 8 km/s
            yield return Dur();
            bus.SetSelector(BusVehicle.GearSelector.Reverse);
            yield return Kipler("geri", hedefHiz: -2.2f, direksiyon: 0.4f, tekKip: true);
            yield return Dur();
            bus.SetSelector(BusVehicle.GearSelector.Drive);

            Log($"SONUC sahne={sahne} otobüs={otobus} sorun={sorun}");
            Log("BITTI " + otobus);
        }

        private static string Ad(string s) =>
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');

        private void Alan(string ad, float deger) =>
            typeof(BusCameraRig).GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(rig, deger);

        private IEnumerator Dur()
        {
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
            float t = Time.time;
            while (bus.SpeedKmh > 0.5f && Time.time - t < 6f)
                yield return null;
            yield return new WaitForSeconds(0.5f);
            input.AutoBrake = 0f;
        }

        /// <summary>Beş kip sırayla: her birinde 1,2 sn ölçüm (sıçrama, titreme, içine girme), sonra ekran görüntüsü.</summary>
        private IEnumerator Kipler(string yer, float hedefHiz = 0f, float direksiyon = 0f, bool tekKip = false)
        {
            for (int k = 0; k < BusCameraRig.ModeNames.Length; k++)
            {
                var kip = (BusCameraRig.Mode)k;
                rig.SetMode(kip);
                // ölçüm: kamera otobüse göre kareden kareye
                Vector3? onceki = null;
                float maksSicrama = 0f, maksDonusSicramasi = 0f, titreme = 0f;
                int kare = 0, icinde = 0, duvarda = 0;
                Quaternion? oncekiDonus = null;
                var konumlar = new List<Vector3>();
                // Y15 K6: engel küre ışınının kısalttığı kamera mesafesi (DIŞ/SERBEST), en kısa ve en uzun
                var engelAlani = typeof(BusCameraRig).GetField("engelMesafesi", BindingFlags.NonPublic | BindingFlags.Instance);
                float engelEnAz = float.MaxValue, engelEnCok = 0f;
                // Y15 K6: sıçrama karesinin süresi ve dünya çerçevesinde (otobüs dönüşünden bağımsız) kamera ofsetinin ikinci farkı
                float sicramaDt = 0f, enUzunDt = 0f, dunyaSarsinti = 0f;
                Vector3? dunyaOnceki = null, dunyaHiz = null;
                float bitis = Time.time + 1.2f;
                while (Time.time < bitis)
                {
                    Sur(hedefHiz, direksiyon);
                    yield return new WaitForEndOfFrame();
                    var cam = rig.transform;
                    var yerel = bus.transform.InverseTransformPoint(cam.position);
                    var yerelDonus = Quaternion.Inverse(bus.transform.rotation) * cam.rotation;
                    var dunya = cam.position - bus.transform.position;
                    if (dunyaOnceki.HasValue && kare > 3)
                    {
                        var v = (dunya - dunyaOnceki.Value) / Mathf.Max(Time.deltaTime, 1e-4f);
                        if (dunyaHiz.HasValue)
                            dunyaSarsinti = Mathf.Max(dunyaSarsinti, (v - dunyaHiz.Value).magnitude * Time.deltaTime);
                        dunyaHiz = v;
                        enUzunDt = Mathf.Max(enUzunDt, Time.deltaTime);
                    }
                    dunyaOnceki = dunya;
                    if (onceki.HasValue && kare > 3)
                    {
                        float sic = (yerel - onceki.Value).magnitude;
                        if (sic > maksSicrama) sicramaDt = Time.deltaTime;
                        maksSicrama = Mathf.Max(maksSicrama, sic);
                        maksDonusSicramasi = Mathf.Max(maksDonusSicramasi, Quaternion.Angle(yerelDonus, oncekiDonus.Value));
                    }
                    onceki = yerel;
                    oncekiDonus = yerelDonus;
                    if (kare > 3) konumlar.Add(yerel);
                    if (engelAlani != null && (kip == BusCameraRig.Mode.Chase || kip == BusCameraRig.Mode.Free))
                    {
                        float e = (float)engelAlani.GetValue(rig);
                        if (e < 1000f) { engelEnAz = Mathf.Min(engelEnAz, e); engelEnCok = Mathf.Max(engelEnCok, e); }
                    }
                    if (kip != BusCameraRig.Mode.Cockpit && kip != BusCameraRig.Mode.Interior && olcu.Icinde(cam.position, 0f))
                        icinde++;
                    if (DuvarIcinde(cam.position))
                        duvarda++;
                    kare++;
                }
                // titreme: ardışık farkların işaret değiştirmesi (ileri-geri) — ikinci farkın ortalaması
                for (int i = 2; i < konumlar.Count; i++)
                    titreme += (konumlar[i] - 2f * konumlar[i - 1] + konumlar[i - 2]).magnitude;
                titreme /= Mathf.Max(1, konumlar.Count - 2);

                string mafsal = kor != null ? $" mafsal={kor.MafsalAcisi:F0}°" : "";
                var sorunlar = new List<string>();
                if (icinde > 0) sorunlar.Add($"OTOBÜSÜN İÇİNDE {icinde}/{kare} kare");
                if (duvarda > 0) sorunlar.Add($"DUVARIN İÇİNDE {duvarda}/{kare} kare");
                // içeride kameralar otobüse sabit (0); dışarıda takip yumuşak, kare başına 0,5 m üstü sıçramadır
                if (maksSicrama > 0.5f) sorunlar.Add($"SIÇRAMA {maksSicrama:F2} m/kare");
                if (maksDonusSicramasi > 4f) sorunlar.Add($"DÖNÜŞ SIÇRAMASI {maksDonusSicramasi:F1}°/kare");
                if (titreme > 0.05f) sorunlar.Add($"TİTREME {titreme:F3} m");
                sorun += sorunlar.Count;
                Log($"{otobus} {yer} {BusCameraRig.ModeNames[k]} hız={bus.SpeedKmh:F0} km/s{mafsal} sıçrama={maksSicrama:F3} m dönüş={maksDonusSicramasi:F2}° " +
                    $"titreme={titreme:F4}{(engelEnAz < float.MaxValue ? $" engel_mesafesi={engelEnAz:F1}–{engelEnCok:F1} m" : "")} sıçrama_karesi={sicramaDt * 1000f:F0} ms en_uzun_kare={enUzunDt * 1000f:F0} ms dünya_sarsıntı={dunyaSarsinti:F3} m kamera_yerel={bus.transform.InverseTransformPoint(rig.transform.position)} " +
                    $"{(sorunlar.Count == 0 ? "OK" : "SORUN " + string.Join(", ", sorunlar))}");
                yield return Ekran($"{yer}_{BusCameraRig.ModeNames[k]}");
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
        }

        private void Sur(float hedef, float direksiyon)
        {
            if (Mathf.Approximately(hedef, 0f))
                return;
            float v = bus.ForwardSpeed * Mathf.Sign(hedef);
            input.AutoThrottle = v < Mathf.Abs(hedef) ? 0.6f : 0f;
            input.AutoBrake = v > Mathf.Abs(hedef) + 1f ? 0.3f : 0f;
            input.AutoSteer = direksiyon;
            bus.Handbrake = false;
        }

        private bool DuvarIcinde(Vector3 p)
        {
            foreach (var c in Physics.OverlapSphere(p, 0.05f, ~0, QueryTriggerInteraction.Ignore))
                if (!kendi.Contains(c) && !(c is WheelCollider))
                    return true;
            return false;
        }

        private IEnumerator Ekran(string ad)
        {
            yield return new WaitForEndOfFrame();
            if (Application.isBatchMode)
                yield break;
            string dosya = $"y15_kamera_{otobus}_{AnkaraBus.GrafikAyarlari.Mevcut}_{Ad(ad)}.png";
            Y11Hat.Kaydet(dosya);
        }
    }
}
