using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y18 gece görünümü testi: otobüs başlangıçta durur. Lamba parçaları (açık mı, malzeme, gölgelendirici), dolgu ışığı (rendering
    /// layer: otobüs renderer'larında bit var mı, ışığın maskesi), dolgu ışığı binaları da aydınlatıyor mu (otobüs görünmezken açık/kapalı
    /// parlaklık farkı), her kaplamada önden, iki yandan, arkadan, frende arkadan, yolcu ve kokpit görüntüsü; Yüksek ↔ Düşük'te lambalar;
    /// körüklüde arka gövde kırıkken arka lambalar; dolgu ışığı açık/kapalı 10'ar sn FPS. Sıra: Conecto (üç kaplama), BMC, Millennium.
    /// Satırlar "[Gece]", görüntüler y18_gece_*.png. "gece" görevi.
    /// </summary>
    public class GeceTesti : MonoBehaviour
    {
        private static int sira = -1;
        private static readonly int[] Otobusler = { 2, 0, 1 };

        private BusVehicle bus;
        private BusCameraRig rig;
        private Camera cam;
        private OtobusOlcusu olcu;
        private string otobus, etiket;

        private static void Log(string s) => Debug.Log("[Gece] " + s);

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
            rig = FindAnyObjectByType<BusCameraRig>();
            cam = rig.GetComponentInChildren<Camera>() ?? Camera.main;
            olcu = OtobusOlcusu.Olc(bus);
            otobus = bus.name.Split('_')[0].ToLowerInvariant();
            etiket = $"{OyunSecimi.SeciliZaman}{(OyunSecimi.SeciliHava != OyunSecimi.Hava.Acik ? "_" + OyunSecimi.SeciliHava : "")}".ToLowerInvariant();
            bus.Handbrake = true;
            bool tam = sira == 0;
            var dolgu = GameObject.Find("OtobusDolguIsigi")?.GetComponent<Light>();

            Log($"BASLA otobüs={bus.Definition?.displayName} zaman={OyunSecimi.SeciliZaman} hava={OyunSecimi.SeciliHava} kalite={GrafikAyarlari.Mevcut} cihaz={SystemInfo.deviceModel}");
            Log($"{otobus} lambalar: {Lambalar()}");
            Log($"{otobus} dolgu ışığı: {DolguDurumu(dolgu)}");
            if (tam && dolgu != null)
                yield return BinaFarki(dolgu);

            var livery = bus.GetComponent<BusLivery>();
            int adet = livery != null ? Mathf.Max(1, livery.Count) : 1;
            for (int k = 0; k < (tam ? adet : 1); k++)
            {
                livery?.Select(k);
                string kap = livery != null ? Ad(livery.NameAt(k)) : "k0";
                yield return null;
                yield return Cek($"{kap}_on", bus.transform.TransformPoint(-3f, 2.2f, olcu.On + 9f), bus.transform.TransformPoint(0f, 1.6f, olcu.On));
                yield return Cek($"{kap}_yan_sag", bus.transform.TransformPoint(olcu.YariGenislik + 9f, 2f, (olcu.On - olcu.Arka) * 0.5f), bus.transform.TransformPoint(0f, 1.5f, (olcu.On - olcu.Arka) * 0.5f));
                yield return Cek($"{kap}_yan_sol", bus.transform.TransformPoint(-olcu.YariGenislik - 9f, 2f, (olcu.On - olcu.Arka) * 0.5f), bus.transform.TransformPoint(0f, 1.5f, (olcu.On - olcu.Arka) * 0.5f));
                yield return Cek($"{kap}_arka", ArkaKamera(9f), ArkaNokta());
                yield return Icerisi(kap);
            }

            // fren: stop lambası (gece ve gündüz)
            var input = bus.GetComponent<BusInput>();
            var fren = Bul("Lamba_Fren");
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(0.5f);
            Log($"{otobus} fren: Lamba_Fren={(fren == null ? "YOK" : fren.activeInHierarchy ? "yanıyor" : "SÖNÜK")} fren={bus.Brake:F2}");
            yield return Cek("frende_arka", ArkaKamera(7f), ArkaNokta());
            input.AutoBrake = 0f;
            yield return new WaitForSeconds(0.5f);
            Log($"{otobus} fren bırakıldı: Lamba_Fren={(fren == null ? "YOK" : fren.activeInHierarchy ? "YANIYOR" : "sönük")}");

            if (tam)
            {
                // kalite: lambalar kayboluyor mu
                var eski = GrafikAyarlari.Mevcut;
                foreach (var q in new[] { KaliteSeviyesi.Yuksek, KaliteSeviyesi.Dusuk })
                {
                    GrafikAyarlari.Uygula(q, kaydet: false);
                    yield return new WaitForSeconds(0.8f);
                    Log($"{otobus} kalite={q} tam_kalite={bus.GetComponent<BusKaliteModeli>()?.TamKaliteAktif} lambalar: {Lambalar()}");
                    yield return Cek($"kalite_{q}_on", bus.transform.TransformPoint(-3f, 2.2f, olcu.On + 9f), bus.transform.TransformPoint(0f, 1.6f, olcu.On));
                    yield return Cek($"kalite_{q}_yan_sag", bus.transform.TransformPoint(olcu.YariGenislik + 9f, 2f, (olcu.On - olcu.Arka) * 0.5f), bus.transform.TransformPoint(0f, 1.5f, (olcu.On - olcu.Arka) * 0.5f));
                }
                GrafikAyarlari.Uygula(eski, kaydet: false);
                yield return new WaitForSeconds(0.8f);

                var kor = bus.GetComponent<KorukluOtobus>();
                if (kor != null && kor.ArkaGovde != null)
                    yield return Korukte(kor, input);

                if (dolgu != null)
                    yield return Fps(dolgu);
            }

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

        private Vector3 ArkaNokta() => bus.transform.TransformPoint(0f, 1.6f, -olcu.Arka);

        private Vector3 ArkaKamera(float d)
        {
            var kor = bus.GetComponent<KorukluOtobus>();
            if (kor != null && kor.ArkaGovde != null)
            {
                var a = kor.ArkaGovde.transform;
                var uc = bus.transform.TransformPoint(0f, 1.6f, -olcu.Arka);
                return uc - a.forward * d + a.right * 2.5f + Vector3.up * 0.6f;
            }
            return bus.transform.TransformPoint(2.5f, 2.2f, -olcu.Arka - d);
        }

        private IEnumerator Icerisi(string kap)
        {
            rig.enabled = true;
            foreach (var kip in new[] { BusCameraRig.Mode.Interior, BusCameraRig.Mode.Cockpit })
            {
                rig.SetMode(kip);
                yield return new WaitForSeconds(0.6f);
                yield return Goruntu.KareSonu();
                Goruntu.Kaydet(cam, Dosya($"{kap}_{(kip == BusCameraRig.Mode.Cockpit ? "kokpit" : "yolcu")}"));
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
            rig.enabled = false;
        }

        private GameObject Bul(string ad)
        {
            foreach (var t in bus.GetComponentsInChildren<Transform>(true))
                if (t.name == ad)
                    return t.gameObject;
            var kor = bus.GetComponent<KorukluOtobus>();
            if (kor != null && kor.ArkaGovde != null)
                foreach (var t in kor.ArkaGovde.GetComponentsInChildren<Transform>(true))
                    if (t.name == ad)
                        return t.gameObject;
            return null;
        }

        private IEnumerable<Transform> Parcalar()
        {
            foreach (var t in bus.GetComponentsInChildren<Transform>(true))
                yield return t;
            var kor = bus.GetComponent<KorukluOtobus>();
            if (kor != null && kor.ArkaGovde != null && !kor.ArkaGovde.transform.IsChildOf(bus.transform))
                foreach (var t in kor.ArkaGovde.GetComponentsInChildren<Transform>(true))
                    yield return t;
        }

        private string Lambalar()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in Parcalar())
            {
                if (!t.name.StartsWith("Lamba_"))
                    continue;
                var r = t.GetComponent<Renderer>();
                string mal = "-";
                if (r != null && r.sharedMaterial != null)
                {
                    var m = r.sharedMaterial;
                    mal = $"{m.name}/{m.shader.name.Replace("Universal Render Pipeline/", "URP/")}{(m.IsKeywordEnabled("_ALPHATEST_ON") ? "/kesme" : "")}" +
                          $"{(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? "/" + m.GetTexture("_BaseMap").name : "")}";
                }
                sb.Append($"{t.name}={(t.gameObject.activeInHierarchy ? "açık" : "kapalı")}{(r != null && !r.enabled ? "(renderer KAPALI)" : "")}[{mal}] ");
            }
            return sb.Length > 0 ? sb.ToString() : "yok";
        }

        private string DolguDurumu(Light dolgu)
        {
            if (dolgu == null)
                return "YOK";
            uint bit = 1u << HavaVeZaman.OtobusIsikKatmani;
            int toplam = 0, bitli = 0;
            foreach (var t in Parcalar())
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                toplam++;
                if ((r.renderingLayerMask & bit) != 0) bitli++;
            }
            string urp = "-";
            var ek = dolgu.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (ek != null)
                urp = $"renderingLayers={ek.renderingLayers} customShadowLayers={ek.customShadowLayers}";
            int binaRenderer = 0, binaBitli = 0;
            var binalar = GameObject.Find("Binalar");
            if (binalar != null)
                foreach (var r in binalar.GetComponentsInChildren<Renderer>())
                {
                    binaRenderer++;
                    if ((r.renderingLayerMask & bit) != 0) binaBitli++;
                }
            return $"şiddet={dolgu.intensity:F2} renk={dolgu.color} light.renderingLayerMask={dolgu.renderingLayerMask} URP:{urp} " +
                   $"otobüs renderer {bitli}/{toplam} bitli, bina renderer {binaBitli}/{binaRenderer} bitli; güneş={(RenderSettings.sun != null ? RenderSettings.sun.name + " " + RenderSettings.sun.intensity.ToString("F2") : "-")}";
        }

        /// <summary>Otobüs görünmezken (kamera otobüsün solunda, sola bakıyor) dolgu ışığı açık/kapalı ortalama parlaklık.</summary>
        private IEnumerator BinaFarki(Light dolgu)
        {
            var konum = bus.transform.TransformPoint(-olcu.YariGenislik - 1f, 2.5f, 0f);
            var bak = konum - bus.transform.right * 20f;
            float acik = 0f, kapali = 0f;
            for (int i = 0; i < 2; i++)
            {
                dolgu.enabled = i == 0;
                cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum));
                rig.enabled = false;
                yield return null;
                yield return Goruntu.KareSonu();
                float p = Parlaklik();
                if (i == 0) acik = p; else kapali = p;
                Goruntu.Kaydet(cam, Dosya($"binalar_dolgu_{(i == 0 ? "acik" : "kapali")}"));
            }
            dolgu.enabled = true;
            Log($"{otobus} binalar (otobüs kadrajda değil): ortalama parlaklık dolgu açık={acik:F4} kapalı={kapali:F4} fark={acik - kapali:F4} " +
                $"({(Mathf.Abs(acik - kapali) < 0.002f ? "binalara etkisi yok" : "BİNALARI DA AYDINLATIYOR")})");

            // aynı karşılaştırma otobüs kadrajdayken (otobüse etkisi)
            var k2 = bus.transform.TransformPoint(olcu.YariGenislik + 9f, 2f, (olcu.On - olcu.Arka) * 0.5f);
            for (int i = 0; i < 2; i++)
            {
                dolgu.enabled = i == 0;
                cam.transform.SetPositionAndRotation(k2, Quaternion.LookRotation(bus.transform.TransformPoint(0f, 1.5f, (olcu.On - olcu.Arka) * 0.5f) - k2));
                yield return null;
                yield return Goruntu.KareSonu();
                float p = Parlaklik();
                if (i == 0) acik = p; else kapali = p;
                Goruntu.Kaydet(cam, Dosya($"otobus_dolgu_{(i == 0 ? "acik" : "kapali")}"));
            }
            dolgu.enabled = true;
            Log($"{otobus} otobüs kadrajda: ortalama parlaklık dolgu açık={acik:F4} kapalı={kapali:F4} fark={acik - kapali:F4}");
        }

        private float Parlaklik()
        {
            const int w = 320, h = 180;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var onceki = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = onceki;
            RenderTexture.ReleaseTemporary(rt);
            double s = 0;
            foreach (var c in tex.GetPixels32())
                s += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0;
            Destroy(tex);
            return (float)(s / (w * h));
        }

        private IEnumerator Korukte(KorukluOtobus kor, BusInput input)
        {
            var arka = kor.ArkaGovde.transform;
            var adlar = new[] { "Lamba_Park_Arka", "Lamba_Fren", "Lamba_Pencere_Arka", "Lamba_IcSerit_Arka" };
            var once = new Dictionary<string, Vector3>();
            foreach (var a in adlar)
            {
                var g = Bul(a);
                if (g != null) once[a] = arka.InverseTransformPoint(Merkez(g));
            }
            bus.Handbrake = false;
            bus.SetSelector(BusVehicle.GearSelector.Drive);
            float bitis = Time.time + 9f;
            while (Time.time < bitis && Mathf.Abs(kor.MafsalAcisi) < 30f)
            {
                input.AutoThrottle = bus.SpeedKmh < 10f ? 0.5f : 0f;
                input.AutoSteer = -1f;   // sola: sağda kaldırım ve durak
                yield return null;
            }
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(2.5f);
            input.AutoSteer = 0f;
            bus.Handbrake = true;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in once)
            {
                var g = Bul(kv.Key);
                var simdi = arka.InverseTransformPoint(Merkez(g));
                sb.Append($"{kv.Key}: arka gövdeye göre kayma {(simdi - kv.Value).magnitude * 100f:F1} cm ({(g.activeInHierarchy ? "açık" : "kapalı")}); ");
            }
            Log($"{otobus} körük mafsal={kor.MafsalAcisi:F0}°: {sb}");
            yield return Cek("korukte_arka_frende", ArkaKamera(8f), ArkaNokta());
            input.AutoBrake = 0f;
            yield return new WaitForSeconds(0.3f);
            yield return Cek("korukte_ust", bus.transform.TransformPoint(7f, 9f, -4f), arka.position);
            yield return Cek("korukte_arka", ArkaKamera(8f), ArkaNokta());
        }

        private static Vector3 Merkez(GameObject g)
        {
            var r = g.GetComponent<Renderer>();
            return r != null ? r.bounds.center : g.transform.position;
        }

        private IEnumerator Fps(Light dolgu)
        {
            rig.enabled = true;
            rig.SetTarget(bus);
            rig.SetMode(BusCameraRig.Mode.Chase);
            foreach (bool acik in new[] { false, true, false, true })
            {
                dolgu.enabled = acik;
                yield return new WaitForSeconds(1.5f);
                var kareler = new List<float>();
                float bas = Time.unscaledTime;
                while (Time.unscaledTime - bas < 10f)
                {
                    yield return null;
                    kareler.Add(Time.unscaledDeltaTime);
                }
                float toplam = 0f;
                foreach (var k in kareler) toplam += k;
                kareler.Sort();
                float p99 = kareler[Mathf.Min(kareler.Count - 1, (int)(kareler.Count * 0.99f))];
                Log($"{otobus} FPS dolgu={(acik ? "açık" : "kapalı")} kalite={GrafikAyarlari.Mevcut}: {kareler.Count / toplam:F1}, 1% en yavaş {p99 * 1000f:F1} ms");
            }
            dolgu.enabled = true;
        }

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak)
        {
            rig.enabled = false;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, Dosya(ad));
        }

        private string Dosya(string ad) => $"y18_gece_{etiket}_{otobus}_{ad}.png";

        private static string Ad(string s) => s == null ? "-" :
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c').Replace("i̇", "i");
    }
}
