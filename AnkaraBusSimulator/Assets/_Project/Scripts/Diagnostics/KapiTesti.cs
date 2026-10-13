using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y18 kapı testi: otobüs ilk durakta durur. Kapı grupları ve kanatları (hareket türü, takip) yazılır; kapalı/açılırken/açık
    /// görüntüler (dış yan, her kapı yakın, yolcu ve kapı kamerası), kanadın gövdeye göre yer değiştirmesi zamanla (önce dışarı, sonra
    /// yana mı), kaplamalar, her kapının sesi, yolcuların hangi kapıdan inip bindiği, körüklüde arka gövde kırıkken 3–4. kapı (titreme),
    /// Yüksek ↔ Düşük kalitede kanatların yeri. Sıra: Conecto, BMC, Millennium (sahne yeniden yüklenir). Satırlar "[Kapi]",
    /// görüntüler y18_kapi_*.png. Y11 test build'inde ve Editor'de (Y18Kurulum) "kapi" görevi.
    /// </summary>
    public class KapiTesti : MonoBehaviour
    {
        private static int sira = -1;
        private static readonly int[] Otobusler = { 2, 3, 0, 1 };   // Y19: önce iki körüklü (bütün adımlar), sonra tek gövdeliler

        private BusVehicle bus;
        private BusCameraRig rig;
        private Camera cam;
        private BusDoorController kapilar;
        private KorukluOtobus kor;
        private OtobusOlcusu olcu;
        private string otobus;

        private static void Log(string s) => Debug.Log("[Kapi] " + s);

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
            kapilar = bus.GetComponentInChildren<BusDoorController>();
            kor = bus.GetComponent<KorukluOtobus>();
            olcu = OtobusOlcusu.Olc(bus);
            otobus = KisaAd(bus.name);
            bus.Handbrake = true;
            bool tam = bus.GetComponent<KorukluOtobus>() != null;   // körüklüler: bütün adımlar; tek gövdeliler: kısa tur

            Log($"BASLA otobüs={bus.Definition?.displayName} zaman={OyunSecimi.SeciliZaman} kalite={GrafikAyarlari.Mevcut} cihaz={SystemInfo.deviceModel}");
            Yapi();

            // kapalı
            yield return Disaridan("kapali");
            yield return Iceriden("kapali");

            // açılış: kanatların yer değiştirmesi zamanla
            var izle = StartCoroutine(Hareket("acilis"));
            kapilar.ToggleAll();
            yield return new WaitForSeconds(0.35f);
            yield return Cek("acilirken_yan", Yan(8f), Yan(0f));
            yield return izle;
            yield return Disaridan("acik");
            yield return Iceriden("acik");

            // kaplamalar (kapılar açık)
            var livery = bus.GetComponent<BusLivery>();
            if (livery != null)
                for (int k = 0; k < livery.Count; k++)
                {
                    livery.Select(k);
                    yield return null;
                    string ad = Ad(livery.NameAt(k));
                    yield return Cek($"kaplama_{ad}_yan", Yan(8f), Yan(0f));
                    yield return Cek($"kaplama_{ad}_kapi1", KapiOnu(0, 3.2f), kapilar.DoorCenter(0));
                    Log($"kaplama {ad}: kanat malzemeleri {KanatMalzemeleri(livery)}");
                    if (!tam)
                        break;
                }

            // kapanış
            izle = StartCoroutine(Hareket("kapanis"));
            kapilar.ToggleAll();
            yield return izle;

            // kapı sesleri: her kapı tek tek
            yield return Sesler();

            if (tam)
            {
                yield return Kalite();
                if (kor != null && kor.ArkaGovde != null)
                    yield return Korukte();
                yield return Yolcular();
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

        private void Yapi()
        {
            var sb = new System.Text.StringBuilder();
            var gruplar = (System.Array)typeof(BusDoorController).GetField("doors", Ozel).GetValue(kapilar);
            foreach (var g in gruplar)
            {
                string ad = (string)g.GetType().GetField("name").GetValue(g);
                var kanatlar = (BusDoor[])g.GetType().GetField("leaves").GetValue(g);
                sb.Append($"\"{ad}\": ");
                foreach (var d in kanatlar)
                {
                    var takip = (Transform)typeof(BusDoor).GetField("takip", Ozel).GetValue(d);
                    var mf = d.GetComponent<MeshFilter>();
                    sb.Append($"{Yol(d.transform)} [{typeof(BusDoor).GetField("motion", Ozel).GetValue(d)}, takip={(takip != null ? takip.name : "-")}, " +
                              $"mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount + " köşe" : "YOK")}, " +
                              $"dışarı={V(Alan<Vector3>(d, "plugOutOffset"))} açık={V(Alan<Vector3>(d, "openPositionOffset"))} süre={Alan<float>(d, "duration"):F1}] ");
                }
                sb.Append("; ");
            }
            Log($"{otobus} kapı grupları ({kapilar.DoorCount}): {sb}");
        }

        private string Yol(Transform t)
        {
            var parcalar = new List<string>();
            for (var p = t; p != null && p != bus.transform; p = p.parent)
                parcalar.Insert(0, p.name);
            return string.Join("/", parcalar);
        }

        /// <summary>Kanatların kapalı konumlarına göre yer değiştirmesi (öndeki gövdede otobüs ekseninde, arkadakilerde arka gövdenin
        /// ekseninde): x dışarı, z boyuna. Yaklaşık 0,1 sn arayla 2,2 sn.</summary>
        private IEnumerator Hareket(string ad)
        {
            var leaves = Kanatlar();
            var bas = new Vector3[leaves.Count];
            for (int i = 0; i < leaves.Count; i++)
                bas[i] = Govde(leaves[i]).InverseTransformPoint(leaves[i].transform.position);
            var satirlar = new List<string>[leaves.Count];
            for (int i = 0; i < leaves.Count; i++)
                satirlar[i] = new List<string>();
            float t0 = Time.time;
            float son = -1f;
            while (Time.time - t0 < 2.2f)
            {
                yield return null;
                float t = Time.time - t0;
                if (t - son < 0.1f)
                    continue;
                son = t;
                for (int i = 0; i < leaves.Count; i++)
                {
                    var d = Govde(leaves[i]).InverseTransformPoint(leaves[i].transform.position) - bas[i];
                    satirlar[i].Add($"{t:F1}:({d.x:+0.00;-0.00},{d.z:+0.00;-0.00})");
                }
            }
            for (int i = 0; i < leaves.Count; i++)
                Log($"{otobus} {ad} {leaves[i].name}: {string.Join(" ", satirlar[i])}");
        }

        private Transform Govde(BusDoor d)
        {
            var takip = (Transform)typeof(BusDoor).GetField("takip", Ozel).GetValue(d);
            return takip != null ? takip : bus.transform;
        }

        private List<BusDoor> Kanatlar()
        {
            var list = new List<BusDoor>();
            var gruplar = (System.Array)typeof(BusDoorController).GetField("doors", Ozel).GetValue(kapilar);
            foreach (var g in gruplar)
                list.AddRange((BusDoor[])g.GetType().GetField("leaves").GetValue(g));
            return list;
        }

        private IEnumerator Disaridan(string durum)
        {
            yield return Cek($"{durum}_yan", Yan(8f), Yan(0f));
            yield return Cek($"{durum}_yan_capraz", bus.transform.TransformPoint(6f, 1.8f, olcu.On + 4f), bus.transform.TransformPoint(1f, 1.4f, 0f));
            for (int i = 0; i < kapilar.DoorCount; i++)
                yield return Cek($"{durum}_kapi{i + 1}", KapiOnu(i, 3.2f), kapilar.DoorCenter(i));
        }

        private IEnumerator Iceriden(string durum)
        {
            rig.enabled = true;
            foreach (var kip in new[] { BusCameraRig.Mode.Interior, BusCameraRig.Mode.Door })
            {
                rig.SetMode(kip);
                yield return new WaitForSeconds(0.6f);
                yield return Kaydet($"{durum}_{(kip == BusCameraRig.Mode.Door ? "kapi_kamerasi" : "yolcu")}");
            }
            // yolcu gözü: içeriden her kapıya bak (kapıların karşısından, koltuk sırasından)
            rig.enabled = false;
            for (int i = 0; i < kapilar.DoorCount; i++)
            {
                var merkez = kapilar.DoorCenter(i);
                var ic = merkez - bus.transform.right * 1.9f + Vector3.up * 0.65f;
                yield return Cek($"{durum}_ic_kapi{i + 1}", ic, merkez + Vector3.up * 0.2f);
            }
            rig.SetMode(BusCameraRig.Mode.Chase);
        }

        private Vector3 Yan(float mesafe) => bus.transform.TransformPoint(olcu.YariGenislik + mesafe, mesafe > 0f ? 1.8f : 1.4f, (olcu.On - olcu.Arka) * 0.5f);

        /// <summary>Körük kırıkken kapının sağ önünden, biraz yukarıdan.</summary>
        private Vector3 Korukten(int i) => kapilar.DoorCenter(i) + kor.ArkaGovde.transform.right * 5f + kor.ArkaGovde.transform.forward * 2.5f + Vector3.up * 1.6f;

        private Vector3 KapiOnu(int i, float mesafe)
        {
            var g = Govde(KapiKanatlari(i)[0]);
            return kapilar.DoorCenter(i) + g.right * mesafe + Vector3.up * 0.4f - g.forward * 0.6f;
        }

        private BusDoor[] KapiKanatlari(int i)
        {
            var gruplar = (System.Array)typeof(BusDoorController).GetField("doors", Ozel).GetValue(kapilar);
            return (BusDoor[])gruplar.GetValue(i).GetType().GetField("leaves").GetValue(gruplar.GetValue(i));
        }

        private string KanatMalzemeleri(BusLivery livery)
        {
            // kanat yüzü gövde malzemesini (kaplama dokusu) taşıyor mu: gövdeyle aynı property block dokusu
            var blok = new MaterialPropertyBlock();
            var sb = new System.Text.StringBuilder();
            foreach (var d in Kanatlar())
            {
                var r = d.GetComponent<Renderer>();
                if (r == null) { sb.Append($"{d.name}=renderer YOK "); continue; }
                var mats = r.sharedMaterials;
                var adlar = new List<string>();
                for (int i = 0; i < mats.Length; i++)
                {
                    r.GetPropertyBlock(blok, i);
                    var t = blok.GetTexture("_BaseMap");
                    adlar.Add($"{(mats[i] != null ? mats[i].name : "null")}{(t != null ? "→" + t.name : "")}");
                }
                sb.Append($"{d.name}=[{string.Join(",", adlar)}] ");
                break;   // bir kanat yeter (hepsi aynı malzeme dizisi)
            }
            return sb.ToString();
        }

        private IEnumerator Sesler()
        {
            var audio = bus.GetComponent<BusAudio>();
            if (audio == null)
            {
                Log($"{otobus} ses: BusAudio YOK");
                yield break;
            }
            var kaynaklar = (List<AudioSource>)typeof(BusAudio).GetField("doorSources", Ozel).GetValue(audio);
            var acik = (AudioClip[])typeof(BusAudio).GetField("doorOpen", Ozel).GetValue(audio);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < kapilar.DoorCount; i++)
            {
                kapilar.SetDoor(i, true);
                yield return null;
                bool caliyor = i < kaynaklar.Count && kaynaklar[i] != null && kaynaklar[i].isPlaying;
                float uzak = i < kaynaklar.Count && kaynaklar[i] != null ? Vector3.Distance(kaynaklar[i].transform.position, kapilar.DoorCenter(i)) : -1f;
                sb.Append($"kapı{i + 1}: kaynak={(i < kaynaklar.Count ? "var" : "YOK")} klip={(i < acik.Length && acik[i] != null ? acik[i].name : "YOK")} " +
                          $"çalıyor={caliyor} kapıya_uzaklık={uzak:F2} m; ");
                yield return new WaitForSeconds(1.8f);
                kapilar.SetDoor(i, false);
                yield return new WaitForSeconds(1.8f);
            }
            Log($"{otobus} sesler: {sb}");
        }

        /// <summary>Yolcular: otobüste 20 yolcu varken ikinci durağa ışınlanır, kapılar açılır; çıkan her yolcu en yakın kapıya göre
        /// sayılır, binenler ön kapıya yürüyor mu.</summary>
        private IEnumerator Yolcular()
        {
            var r = bus.GetComponent<RouteTracker>();
            int son = r != null && r.Route != null ? Mathf.Min(3, r.Route.StopCount - 2) : 0;
            for (int d = 1; d <= son; d++)
                yield return Yolcular(d);
        }

        private IEnumerator Yolcular(int hedef)
        {
            var tracker = bus.GetComponent<RouteTracker>();
            var yolcular = bus.GetComponent<BusPassengers>();
            if (tracker == null || yolcular == null || tracker.Route == null || tracker.Route.StopCount < 2)
            {
                Log($"{otobus} yolcular: atlandı (rota/yolcu yok)");
                yield break;
            }
            typeof(BusPassengers).GetField("onboard", Ozel).SetValue(yolcular, 30);
            typeof(RouteTracker).GetProperty("NextStopIndex").SetValue(tracker, hedef);
            var durak = tracker.Route.GetStop(hedef);
            // otobüsü durağa koy: yönü durak yönü, sağ yanı kaldırıma (durak kutusunun ortası ön kapı hizası)
            OtobusIsinla.ZemineTasi(bus, durak.transform.position, Quaternion.Euler(0f, durak.transform.eulerAngles.y, 0f), 0.2f);
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
                bus.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            }
            rig.enabled = true;
            rig.SetTarget(bus);
            yield return new WaitForSeconds(1.5f);
            Log($"{otobus} yolcular: durak={durak.StopName} içinde={durak.Contains(bus.transform.position)} bekleyen={PassengerManager.Instance?.WaitingAt(durak).Count} " +
                $"içerideki={yolcular.Onboard}");

            // havuzdan gelen yolcular: bir önceki bakışta etkin olmayan her yolcu yeni inen sayılır
            var gorulen = new HashSet<Passenger>(FindObjectsByType<Passenger>());
            var inen = new int[kapilar.DoorCount];
            int binen = 0;
            int ilkIcerideki = yolcular.Onboard;
            kapilar.ToggleAll();
            rig.enabled = false;
            bool cekildi = false;
            float bitis = Time.time + 25f;
            while (Time.time < bitis)
            {
                yield return new WaitForSeconds(0.15f);
                var simdi = new HashSet<Passenger>(FindObjectsByType<Passenger>());
                foreach (var p in simdi)
                    if (!gorulen.Contains(p) && (p.transform.position - bus.transform.position).sqrMagnitude < 30f * 30f)
                        inen[EnYakinKapi(p.transform.position)]++;
                gorulen = simdi;
                if (!cekildi && Time.time > bitis - 20f)
                {
                    cekildi = true;
                    yield return Cek($"yolcular{hedef}_yan", Yan(9f), Yan(0f));
                    yield return Cek($"yolcular{hedef}_arka_kapilar", KapiOnu(kapilar.DoorCount - 1, 6f), kapilar.DoorCenter(kapilar.DoorCount - 2));
                }
                if (!yolcular.IsBusy && Time.time > bitis - 20f)
                    break;
            }
            binen = Mathf.Max(0, yolcular.Onboard - (ilkIcerideki - Toplam(inen)));
            Log($"{otobus} yolcular: inen kapı başına [{string.Join(", ", inen)}] (toplam {Toplam(inen)}), binen={binen} (ön kapıdan; BusPassengers yalnız ön kapıdan bindirir), " +
                $"içerideki {ilkIcerideki} → {yolcular.Onboard}, meşgul={yolcular.IsBusy}");
            kapilar.ToggleAll();
            yield return new WaitForSeconds(2f);
        }

        private int EnYakinKapi(Vector3 p)
        {
            int en = 0;
            float d = float.MaxValue;
            for (int i = 0; i < kapilar.DoorCount; i++)
            {
                float x = (kapilar.DoorCenter(i) - p).sqrMagnitude;
                if (x < d) { d = x; en = i; }
            }
            return en;
        }

        private static int Toplam(int[] a)
        {
            int s = 0;
            foreach (var x in a) s += x;
            return s;
        }

        /// <summary>Yüksek ↔ Düşük: kanatların gövdeye göre yeri ve mesh'i değişiyor mu (kapalı ve açık).</summary>
        private IEnumerator Kalite()
        {
            var eski = GrafikAyarlari.Mevcut;
            foreach (bool acik in new[] { false, true })
            {
                if (acik)
                {
                    kapilar.ToggleAll();
                    yield return new WaitForSeconds(2f);
                }
                foreach (var q in new[] { KaliteSeviyesi.Dusuk, KaliteSeviyesi.Yuksek, KaliteSeviyesi.Dusuk })
                {
                    GrafikAyarlari.Uygula(q, kaydet: false);
                    yield return new WaitForSeconds(0.6f);
                    var sb = new System.Text.StringBuilder();
                    foreach (var d in Kanatlar())
                    {
                        var r = d.GetComponent<Renderer>();
                        var b = r != null ? r.bounds : new Bounds();
                        var m = Govde(d).InverseTransformPoint(b.center);
                        sb.Append($"{d.name}@{V(m)} {(r != null && r.enabled ? "" : "GİZLİ ")}");
                    }
                    Log($"{otobus} kalite={q} kapılar={(acik ? "açık" : "kapalı")} tam_kalite={bus.GetComponent<BusKaliteModeli>()?.TamKaliteAktif} kanat_merkezleri: {sb}");
                    yield return Cek($"kalite_{q}_{(acik ? "acik" : "kapali")}_yan", bus.transform.TransformPoint(6f, 1.8f, olcu.On + 4f), bus.transform.TransformPoint(1f, 1.4f, 0f));
                }
                if (acik)
                {
                    kapilar.ToggleAll();
                    yield return new WaitForSeconds(2f);
                }
            }
            GrafikAyarlari.Uygula(eski, kaydet: false);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Virajda körük kırılır, durulur; 3. ve 4. kapı açılır: kanatlar arka gövdeye göre yerinde mi, titriyor mu.</summary>
        private IEnumerator Korukte()
        {
            var input = bus.GetComponent<BusInput>();
            var arka = kor.ArkaGovde.transform;
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
            input.AutoBrake = 0f;
            yield return new WaitForSeconds(0.5f);

            var arkaKanatlar = new List<BusDoor>();
            for (int i = 2; i < kapilar.DoorCount; i++)
                arkaKanatlar.AddRange(KapiKanatlari(i));
            var kapali = new Dictionary<BusDoor, Vector3>();
            foreach (var d in arkaKanatlar)
                kapali[d] = arka.InverseTransformPoint(d.transform.position);
            Log($"{otobus} körük: mafsal={kor.MafsalAcisi:F0}° hız={bus.SpeedKmh:F1} km/s; kapalı kanatlar arka gövdeye göre: " +
                string.Join(" ", arkaKanatlar.ConvertAll(d => $"{d.name}@{V(kapali[d])}")));
            yield return Cek("korukte_kapali_kapi3", Korukten(2), kapilar.DoorCenter(2));

            var izle = StartCoroutine(Hareket("korukte_acilis"));
            for (int i = 2; i < kapilar.DoorCount; i++)
                kapilar.SetDoor(i, true);
            yield return izle;

            // açıkken 3 sn: arka gövdeye göre en büyük sapma (titreme)
            var ilk = new Dictionary<BusDoor, Vector3>();
            var sapma = new Dictionary<BusDoor, float>();
            foreach (var d in arkaKanatlar)
            {
                ilk[d] = arka.InverseTransformPoint(d.transform.position);
                sapma[d] = 0f;
            }
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                yield return Goruntu.KareSonu();
                foreach (var d in arkaKanatlar)
                    sapma[d] = Mathf.Max(sapma[d], (arka.InverseTransformPoint(d.transform.position) - ilk[d]).magnitude);
            }
            Log($"{otobus} körük açık: mafsal={kor.MafsalAcisi:F0}° " +
                string.Join(" ", arkaKanatlar.ConvertAll(d => $"{d.name} açık-kapalı={V(ilk[d] - kapali[d])} titreme(3 sn en büyük)={sapma[d] * 1000f:F1} mm")));
            yield return Cek("korukte_acik_kapi3", Korukten(2), kapilar.DoorCenter(2));
            if (kapilar.DoorCount > 3)
                yield return Cek("korukte_acik_kapi4", Korukten(3), kapilar.DoorCenter(3));
            yield return Cek("korukte_acik_kapi2_3", bus.transform.TransformPoint(7f, 3f, -1f), kor.ArkaGovde.position + Vector3.up * 1.2f);
            yield return Cek("korukte_acik_ust", bus.transform.TransformPoint(7f, 9f, -4f), arka.position);
            for (int i = 2; i < kapilar.DoorCount; i++)
                kapilar.SetDoor(i, false);
            yield return new WaitForSeconds(2f);
        }

        // ---- görüntü ----

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak)
        {
            bool rigAcik = rig.enabled;
            rig.enabled = false;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Kaydet(ad);
            rig.enabled = rigAcik;
        }

        private IEnumerator Kaydet(string ad)
        {
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, $"y18_kapi_{otobus}_{ad}.png");
        }

        // ---- yardımcılar ----

        private const BindingFlags Ozel = BindingFlags.NonPublic | BindingFlags.Instance;
        private static T Alan<T>(BusDoor d, string ad) => (T)typeof(BusDoor).GetField(ad, Ozel).GetValue(d);
        private static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

        private static string Ad(string s) => s == null ? "-" :
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c').Replace("i̇", "i");

        /// <summary>Dosya adı için kısa otobüs adı: bmc, bmc18, caio, caiog, mb.</summary>
        public static string KisaAd(string ad)
        {
            ad = ad.ToLowerInvariant();
            if (ad.StartsWith("bmc")) return ad.Contains("18") ? "bmc18" : "bmc";
            if (ad.StartsWith("caio")) return ad.EndsWith("_g") ? "caiog" : "caio";
            return ad.Split('_')[0];
        }
    }

    /// <summary>Test görüntüsü: kamerayı 1280×720 RenderTexture'a çizip persistentDataPath'e (Editor'de Builds/Y18) PNG yazar.
    /// ScreenCapture Editor'de oyun penceresine bağlı; bu her yerde aynı çalışır.</summary>
    public static class Goruntu
    {
        public static string Klasor
        {
            get
            {
#if UNITY_EDITOR
                string k = Path.Combine(Application.dataPath, "..", "Builds", "Y18");
#else
                string k = Application.persistentDataPath;
#endif
                Directory.CreateDirectory(k);
                return k;
            }
        }

        private static readonly WaitForEndOfFrame kareSonu = new WaitForEndOfFrame();

        /// <summary>Batch modda (Editor testi) WaitForEndOfFrame hiç dönmez; orada bir kare beklenir.</summary>
        public static object KareSonu() => Application.isBatchMode ? null : kareSonu;

        public static void Kaydet(Camera cam, string ad)
        {
            int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            var eski = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = eski;
            var onceki = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = onceki;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(Klasor, ad), tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[Y11] görüntü: {ad}");
        }
    }
}
