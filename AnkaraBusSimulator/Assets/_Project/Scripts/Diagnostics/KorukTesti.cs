using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Traffic;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y19 körüklü testi (BMC Procity 18, Millennium II körüklü): üç kaplamada dıştan sağ/sol/ön/arka/üst, körük yakın (iki yan),
    /// içeriden koridor boyunca (ön gövde → körük → arka gövde) ve kokpit; Yüksek ↔ Düşük'te parçaların görünürlüğü ve yeri;
    /// mafsal kararlılığı (dururken, 40 km/s düz giderken, sert frende); sola dönüşte körük bükülmesi (dış/iç köşe, üstten, içeriden)
    /// ve takip kamerası. Satırlar "[Koruk]", görüntüler y19_koruk_*.png. "koruk" görevi; fırlama bekçisi (KorukluTesti) yanında.
    /// </summary>
    public class KorukTesti : MonoBehaviour
    {
        private static int sira = -1;
        private static readonly int[] Otobusler = { 2, 3 };

        private BusVehicle bus;
        private BusInput input;
        private BusCameraRig rig;
        private Camera cam;
        private KorukluOtobus kor;
        private Rigidbody on;
        private Transform arka;
        private OtobusOlcusu olcu;
        private string otobus;

        private static void Log(string s) => Debug.Log("[Koruk] " + s);

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
            kor = bus.GetComponent<KorukluOtobus>();
            on = bus.GetComponent<Rigidbody>();
            olcu = OtobusOlcusu.Olc(bus);
            otobus = KapiTesti.KisaAd(bus.name);
            bus.Handbrake = true;
            if (kor == null || kor.ArkaGovde == null)
            {
                Log($"HATA {bus.name} körüklü değil");
                yield break;
            }
            arka = kor.ArkaGovde.transform;
            Log($"BASLA otobüs={bus.Definition?.displayName} zaman={OyunSecimi.SeciliZaman} kalite={GrafikAyarlari.Mevcut} kütle={on.mass:F0}+{kor.ArkaGovde.mass:F0} kg " +
                $"boy={olcu.Uzunluk:F1} m cihaz={SystemInfo.deviceModel}");

            // 3. görünüm: üç kaplama
            var livery = bus.GetComponent<BusLivery>();
            int adet = livery != null ? Mathf.Max(1, livery.Count) : 1;
            for (int k = 0; k < adet; k++)
            {
                livery?.Select(k);
                string kap = livery != null ? Ad(livery.NameAt(k)) : "k0";
                yield return null;
                yield return Dis(kap, ilk: k == 0);
            }
            livery?.Select(0);
            yield return Ic();
            yield return Kalite();

            // 4. mafsal kararlılığı ve körük bükülmesi
            yield return Kararlilik();
            yield return Bukulme();

            Log($"BITTI {otobus}");
            sira++;
            if (sira < Otobusler.Length)
            {
                OyunSecimi.Otobus = Otobusler[sira];
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield break;
            }
            sira = -1;
            Log("BITTI hepsi");
        }

        private Vector3 Orta(float y) => bus.transform.TransformPoint(0f, y, (olcu.On - olcu.Arka) * 0.5f);
        private Vector3 Mafsal(float y) => arka.position + Vector3.up * y;

        private IEnumerator Dis(string kap, bool ilk)
        {
            float d = olcu.Uzunluk * 0.62f;
            yield return Cek($"{kap}_sag", bus.transform.TransformPoint(d, 2.4f, (olcu.On - olcu.Arka) * 0.5f), Orta(1.5f));
            yield return Cek($"{kap}_sol", bus.transform.TransformPoint(-d, 2.4f, (olcu.On - olcu.Arka) * 0.5f), Orta(1.5f));
            yield return Cek($"{kap}_on", bus.transform.TransformPoint(-3f, 2.4f, olcu.On + 9f), bus.transform.TransformPoint(0f, 1.6f, olcu.On));
            yield return Cek($"{kap}_arka", bus.transform.TransformPoint(-3f, 2.4f, -olcu.Arka - 9f), bus.transform.TransformPoint(0f, 1.6f, -olcu.Arka));
            if (!ilk)
                yield break;
            yield return Cek($"{kap}_ust", bus.transform.TransformPoint(6f, 16f, (olcu.On - olcu.Arka) * 0.5f), Orta(0f));
            yield return Cek($"{kap}_koruk_sag", Mafsal(1.7f) + bus.transform.right * 4.5f + bus.transform.forward * 1.5f, Mafsal(1.5f));
            yield return Cek($"{kap}_koruk_sol", Mafsal(1.7f) - bus.transform.right * 4.5f - bus.transform.forward * 1.5f, Mafsal(1.5f));
            yield return Cek($"{kap}_koruk_ust", Mafsal(6f) + bus.transform.right * 2.5f, Mafsal(2.5f));
        }

        /// <summary>İçeriden: yolcu kamerası, kokpit, koridor boyunca öne ve arkaya bakış (körük içi dahil).</summary>
        private IEnumerator Ic()
        {
            rig.enabled = true;
            foreach (var kip in new[] { BusCameraRig.Mode.Interior, BusCameraRig.Mode.Cockpit })
            {
                rig.SetMode(kip);
                yield return new WaitForSeconds(0.6f);
                yield return Goruntu.KareSonu();
                Goruntu.Kaydet(cam, Dosya(kip == BusCameraRig.Mode.Cockpit ? "ic_kokpit" : "ic_yolcu"));
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
            float mafsalZ = bus.transform.InverseTransformPoint(arka.position).z;
            var noktalar = new (string ad, float z, bool arkaya)[]
            {
                ("ic_on_arkaya", olcu.On - 3f, true),
                ("ic_koruk_onune", mafsalZ + 3.5f, true),
                ("ic_koruk_ici", mafsalZ + 0.6f, true),
                ("ic_arka_one", -olcu.Arka + 2.5f, false),
                ("ic_koruk_arkadan", mafsalZ - 3.5f, false),
            };
            foreach (var (ad, z, arkaya) in noktalar)
            {
                var g = bus.transform.TransformPoint(0.2f, 1.75f, z);
                var h = bus.transform.TransformPoint(0f, 1.5f, z + (arkaya ? -6f : 6f));
                yield return Cek(ad, g, h);
            }
        }

        /// <summary>Yüksek ↔ Düşük: görünür renderer sayısı ve her parçanın sınır kutusu merkezinin kayması.</summary>
        private IEnumerator Kalite()
        {
            var eski = GrafikAyarlari.Mevcut;
            var ilk = new Dictionary<Renderer, Vector3>();
            foreach (var q in new[] { KaliteSeviyesi.Dusuk, KaliteSeviyesi.Yuksek, KaliteSeviyesi.Dusuk })
            {
                GrafikAyarlari.Uygula(q, kaydet: false);
                yield return new WaitForSeconds(0.8f);
                int gorunur = 0, toplam = 0;
                float enBuyuk = 0f;
                string enBuyukAd = "-";
                foreach (var r in Rendererlar())
                {
                    toplam++;
                    if (!r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    gorunur++;
                    var govde = r.transform.IsChildOf(arka) || r.name.EndsWith("_Arka") ? arka : bus.transform;
                    var m = govde.InverseTransformPoint(r.bounds.center);
                    if (ilk.TryGetValue(r, out var o))
                    {
                        float k = (m - o).magnitude;
                        if (k > enBuyuk) { enBuyuk = k; enBuyukAd = r.name; }
                    }
                    else
                        ilk[r] = m;
                }
                Log($"{otobus} kalite={q} tam_kalite={bus.GetComponent<BusKaliteModeli>()?.TamKaliteAktif} görünür renderer {gorunur}/{toplam}, " +
                    $"parça merkezi en büyük kayma {enBuyuk * 100f:F1} cm ({enBuyukAd})");
                yield return Cek($"kalite_{q}_sag", bus.transform.TransformPoint(7f, 2.2f, (olcu.On - olcu.Arka) * 0.5f + 3f), Mafsal(1.5f));
                yield return Cek($"kalite_{q}_koruk", Mafsal(1.7f) + bus.transform.right * 4.5f + bus.transform.forward * 1.5f, Mafsal(1.5f));
            }
            GrafikAyarlari.Uygula(eski, kaydet: false);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerable<Renderer> Rendererlar()
        {
            foreach (var r in bus.GetComponentsInChildren<Renderer>(true))
                yield return r;
            if (!arka.IsChildOf(bus.transform))
                foreach (var r in arka.GetComponentsInChildren<Renderer>(true))
                    yield return r;
        }

        /// <summary>Mafsal açısı ve açısal hızı: dururken 5 sn, 40 km/s düz 8 sn, 40'tan sert fren.</summary>
        private IEnumerator Kararlilik()
        {
            yield return Olc("dururken", 5f, null);
            var serit = Serit();
            if (serit == null)
            {
                Log($"{otobus} HATA şerit bulunamadı, sürüş ölçümü atlandı");
                yield break;
            }
            foreach (var sp in FindObjectsByType<TrafficSpawner>())
                sp.enabled = false;
            foreach (var c in FindObjectsByType<TrafficCar>())
                c.gameObject.SetActive(false);
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            // 40 km/s'ye çık
            for (float t = 0f; t < 15f && bus.SpeedKmh < 38f; t += Time.deltaTime)
            {
                Sur(serit, 40f);
                yield return null;
            }
            rig.enabled = true;
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return Olc("40kmh_duz", 8f, () => Sur(serit, 40f));
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, Dosya("surerken_dis"));
            // sert fren
            yield return Olc("sert_fren", 4f, () =>
            {
                Sur(serit, 0f);
                input.AutoThrottle = 0f;
                input.AutoBrake = 1f;
            });
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
            yield return new WaitForSeconds(1f);
            bus.Handbrake = true;
            input.AutoBrake = 0f;
        }

        private IEnumerator Olc(string ad, float sure, System.Action her)
        {
            var acilar = new List<float>();
            float enHizli = 0f, onceki = kor.MafsalAcisi, enHiz = 0f;
            int yonDegisimi = 0;
            float oncekiTurev = 0f;
            for (float t = 0f; t < sure; t += Time.deltaTime)
            {
                her?.Invoke();
                yield return null;
                float a = kor.MafsalAcisi;
                float turev = (a - onceki) / Mathf.Max(Time.deltaTime, 1e-4f);
                if (Mathf.Abs(turev) > 1f && Mathf.Sign(turev) != Mathf.Sign(oncekiTurev) && oncekiTurev != 0f)
                    yonDegisimi++;
                if (Mathf.Abs(turev) > 1f)
                    oncekiTurev = turev;
                enHizli = Mathf.Max(enHizli, Mathf.Abs(turev));
                enHiz = Mathf.Max(enHiz, bus.SpeedKmh);
                onceki = a;
                acilar.Add(a);
            }
            float ort = 0f;
            foreach (var a in acilar) ort += a;
            ort /= Mathf.Max(1, acilar.Count);
            float sapma = 0f, min = float.MaxValue, max = float.MinValue;
            foreach (var a in acilar)
            {
                sapma += (a - ort) * (a - ort);
                min = Mathf.Min(min, a);
                max = Mathf.Max(max, a);
            }
            sapma = Mathf.Sqrt(sapma / Mathf.Max(1, acilar.Count));
            Log($"{otobus} mafsal {ad}: ort {ort:F2}° aralık {min:F2}…{max:F2}° std {sapma:F3}° en hızlı {enHizli:F1}°/sn " +
                $"yön değişimi (>1°/sn) {yonDegisimi} en yüksek hız {enHiz:F0} km/s");
        }

        /// <summary>Sola dönüşle mafsal ~30°: körük dış (sağ) ve iç (sol) köşe, üstten, içeriden; takip kamerası.</summary>
        private IEnumerator Bukulme()
        {
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            rig.enabled = true;
            rig.SetMode(BusCameraRig.Mode.Chase);
            bool cekildi = false;
            float bitis = Time.time + 12f;
            while (Time.time < bitis && Mathf.Abs(kor.MafsalAcisi) < 30f)
            {
                input.AutoThrottle = bus.SpeedKmh < 10f ? 0.5f : 0f;
                input.AutoSteer = -1f;   // sola: sağda kaldırım ve durak
                if (!cekildi && Mathf.Abs(kor.MafsalAcisi) > 15f)
                {
                    cekildi = true;
                    yield return Goruntu.KareSonu();
                    Goruntu.Kaydet(cam, Dosya("donerken_takip_kamerasi"));
                }
                yield return null;
            }
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(2.5f);
            input.AutoSteer = 0f;
            bus.Handbrake = true;
            input.AutoBrake = 0f;
            var deri = bus.GetComponentInChildren<SkinnedMeshRenderer>();
            Log($"{otobus} büküm: mafsal={kor.MafsalAcisi:F0}° deri={(deri != null ? $"{deri.name} sınır {deri.bounds.size}" : "YOK")}");
            var m = Mafsal(1.6f);
            var ileri = (bus.transform.forward + arka.forward).normalized;
            var sag = Vector3.Cross(Vector3.up, ileri);
            yield return Cek("bukum_dis_kose", m + sag * 4.5f + Vector3.up * 0.4f, m);
            yield return Cek("bukum_ic_kose", m - sag * 4.5f + Vector3.up * 0.4f, m);
            yield return Cek("bukum_ust", m + Vector3.up * 8f + sag * 1.5f, m - Vector3.up * 1.6f);
            yield return Cek("bukum_ic", bus.transform.TransformPoint(0.2f, 1.75f, bus.transform.InverseTransformPoint(arka.position).z + 3.5f), arka.TransformPoint(0f, 1.5f, -4f));
            rig.enabled = true;
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return new WaitForSeconds(1f);
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, Dosya("bukum_takip_kamerasi"));
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

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak)
        {
            bool rigAcik = rig.enabled;
            rig.enabled = false;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, Dosya(ad));
            rig.enabled = rigAcik;
        }

        private string Dosya(string ad) => $"y19_koruk_{otobus}_{OyunSecimi.SeciliZaman.ToString().ToLowerInvariant()}_{ad}.png";

        private static string Ad(string s) => s == null ? "-" :
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c').Replace("i̇", "i");
    }
}
