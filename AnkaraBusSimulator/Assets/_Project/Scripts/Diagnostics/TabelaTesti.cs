using System.Collections;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y16 LED hat tabelası testi: otobüs başlangıçta durur, kamera önüne, arkasına ve yanına konup ekran görüntüsü alınır.
    /// Her kaplamada ön/arka/yan; ilk kaplamada ön tabelanın iki sayfası (sabit ve kayan), yakın çekimler, Türkçe harf sınaması,
    /// Yüksek ↔ Düşük kalite (Millennium malzeme dokusu), körüklüde arka gövde kırıkken arka tabela ve "SERVİS DIŞI".
    /// Üç otobüs sırayla (sahne yeniden yüklenir), sonra sonraki hat. Görüntüler y16_tabela_*.png, satırlar "[Tabela]".
    /// Y11 test build'inde "tabela" görevi.
    /// </summary>
    public class TabelaTesti : MonoBehaviour
    {
        private static int otobusSayisi = -1;   // bu hatta denenen otobüs sayısı (sahne yeniden yüklenince sürer)

        private string sonrakiSahne;
        private BusVehicle bus;
        private BusCameraRig rig;
        private Camera cam;
        private HatTabelasi tabela;
        private RouteTracker tracker;
        private OtobusOlcusu olcu;
        private string otobus, hat;

        public void Ayarla(string sonraki) => sonrakiSahne = sonraki;

        private static void Log(string s) => Debug.Log("[Tabela] " + s);

        private IEnumerator Start()
        {
            if (otobusSayisi < 0)
            {
                // her hat BMC'den başlar: sahne kayıtlı otobüsle kurulduysa BMC ile yeniden yüklenir
                otobusSayisi = 0;
                if (OyunSecimi.Otobus != 0)
                {
                    OyunSecimi.Otobus = 0;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    yield break;
                }
            }
            yield return new WaitForSeconds(2.5f);
            bus = FindAnyObjectByType<BusVehicle>();
            rig = FindAnyObjectByType<BusCameraRig>();
            tabela = bus.GetComponent<HatTabelasi>();
            tracker = bus.GetComponent<RouteTracker>();
            olcu = OtobusOlcusu.Olc(bus);
            otobus = KapiTesti.KisaAd(bus.name);
            hat = SceneManager.GetActiveScene().name.StartsWith("Hat1") ? "hat1" : "hat3";
            var livery = bus.GetComponent<BusLivery>();
            var kor = bus.GetComponent<KorukluOtobus>();
            bus.Handbrake = true;
            rig.enabled = false;
            cam = rig.GetComponentInChildren<Camera>() ?? Camera.main;
            Log($"BASLA {hat} otobüs={bus.Definition?.displayName} zaman={OyunSecimi.SeciliZaman} kalite={GrafikAyarlari.Mevcut} " +
                $"tabela={(tabela != null ? "var" : "YOK")} kaplama_sayısı={livery?.Count ?? 0} sayfalar=[{string.Join(" | ", Sayfalar())}] " +
                $"levhalar={Levhalar()}");

            int kaplamaSayisi = Mathf.Max(1, livery != null ? livery.Count : 1);
            for (int k = 0; k < kaplamaSayisi; k++)
            {
                if (livery != null && livery.Count > 0)
                    livery.Select(k);
                string kap = livery != null ? Ad(livery.SelectedName ?? k.ToString()) : "k" + k;
                yield return SayfaBekle(0, 0.3f);
                yield return Cek($"{kap}_on", Ileri(9f, 2.0f), OnNokta());
                yield return Cek($"{kap}_arka", Ileri(9f, 2.0f, arka: true), ArkaNokta());
                yield return Cek($"{kap}_yan_sag", bus.transform.TransformPoint(7f, 2.2f, 0f), bus.transform.TransformPoint(0f, 2.2f, 0f));
                if (k > 0)
                    continue;
                yield return Cek($"{kap}_yan_sol", bus.transform.TransformPoint(-7f, 2.2f, 0f), bus.transform.TransformPoint(0f, 2.2f, 0f));
                // yakın: ön sayfa 1 (sabit), sonra sayfa 2 kayarken iki an
                yield return SayfaBekle(0, 0.3f);
                yield return Cek("on_yakin_sayfa1", Ileri(3.2f, 2.5f), OnNokta());
                yield return SayfaBekle(1, 1.5f);
                yield return Cek("on_yakin_sayfa2_kayma1", Ileri(3.2f, 2.5f), OnNokta());
                yield return new WaitForSeconds(2.5f);
                yield return Cek("on_yakin_sayfa2_kayma2", Ileri(3.2f, 2.5f), OnNokta());
                yield return SayfaBekle(0, 0f);
                yield return SayfaBekle(1, 0f);
                Log($"{otobus} {hat} sayfa geçişi: {SayfaZamanlari()}");
                yield return Cek("arka_yakin", Ileri(2.6f, 2.6f, arka: true), ArkaNokta());
            }

            // Türkçe harfler (yalnızca bu sınama için sayfa değiştirilir)
            SayfaYaz("İŞÇĞÖÜ ıi");
            yield return Cek("on_yakin_turkce", Ileri(3.2f, 2.5f), OnNokta());

            // kalite: Düşük ↔ Yüksek (Millennium'da tabela modelin malzemesinde)
            var eski = GrafikAyarlari.Mevcut;
            Tazele();
            int adim = 0;
            foreach (var q in new[] { KaliteSeviyesi.Dusuk, KaliteSeviyesi.Yuksek, KaliteSeviyesi.Dusuk, eski })
            {
                GrafikAyarlari.Uygula(q, kaydet: false);
                yield return new WaitForSeconds(1f);
                Log($"{otobus} {hat} kalite={q} tabela_malzemesi={MalzemeDurumu()} tam_kalite_model={bus.GetComponent<BusKaliteModeli>()?.TamKaliteAktif}");
                yield return Cek($"kalite{++adim}_{q}_on", Ileri(6f, 2.2f), OnNokta());
            }
            Tazele();

            // körüklü: arka gövde kırıkken arka tabela arka gövdeyle dönüyor mu
            if (kor != null && kor.ArkaGovde != null)
            {
                var input = bus.GetComponent<BusInput>();
                bus.Handbrake = false;
                bus.SetSelector(BusVehicle.GearSelector.Drive);
                float bitis = Time.time + 7f;
                while (Time.time < bitis && Mathf.Abs(kor.MafsalAcisi) < 30f)
                {
                    input.AutoThrottle = bus.SpeedKmh < 10f ? 0.5f : 0f;
                    input.AutoSteer = 1f;
                    yield return null;
                }
                input.AutoThrottle = 0f;
                input.AutoBrake = 1f;
                yield return new WaitForSeconds(2f);
                input.AutoBrake = 0f;
                input.AutoSteer = 0f;
                bus.Handbrake = true;
                var arka = kor.ArkaGovde.transform;
                var levha = arka.Find("HatTabelasi_Arka");
                string aci = levha != null ? $"{Vector3.Angle(levha.forward, arka.forward):F1}° (0 = arka gövdeyle aynı yön)" : "levha YOK";
                Log($"{otobus} {hat} mafsal={kor.MafsalAcisi:F0}° arka_levha_ebeveyn={(levha != null ? levha.parent.name : "-")} arka_levha_açısı={aci}");
                if (levha != null)
                {
                    // levhanın ileri yönü gövdenin içine bakar: kamera dışarıda, levhanın arkasında
                    yield return Cek("koruklu_kirik_arka", levha.position - arka.forward * 8f - arka.right * 0.7f, levha.position);
                    yield return Cek("koruklu_kirik_arka_yakin", levha.position - levha.forward * 2.2f, levha.position);
                    yield return Cek("koruklu_kirik_ust", bus.transform.TransformPoint(4f, 14f, -8f), arka.position);
                }
            }

            // hat bitti: SERVİS DIŞI
            typeof(RouteTracker).GetProperty("NextStopIndex").SetValue(tracker, tracker.Route.StopCount);
            yield return new WaitForSeconds(0.5f);
            Log($"{otobus} {hat} hat bitti: sayfalar=[{string.Join(" | ", Sayfalar())}]");
            yield return Cek("servis_disi_on", Ileri(4f, 2.4f), OnNokta());
            yield return Cek("servis_disi_arka", Ileri(4f, 2.4f, arka: true), ArkaNokta());

            Log($"BITTI {otobus} {hat}");
            rig.enabled = true;
            otobusSayisi++;
            if (otobusSayisi < 4)   // Y19: dört otobüs (iki körüklü)
            {
                OyunSecimi.Otobus = otobusSayisi;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield break;
            }
            otobusSayisi = -1;
            OyunSecimi.Otobus = 0;
            if (!string.IsNullOrEmpty(sonrakiSahne))
                SceneManager.LoadScene(sonrakiSahne);
            else
                Log("BITTI hepsi");
        }

        private Vector3 OnNokta() => bus.transform.TransformPoint(0f, 2.55f, olcu.On);
        private Vector3 ArkaNokta() => bus.transform.TransformPoint(0f, 2.6f, -olcu.Arka);

        /// <summary>Burnun önünde ya da (arka: true) kuyruğun arkasında, mesafe kadar uzakta.</summary>
        private Vector3 Ileri(float mesafe, float yukseklik, bool arka = false) =>
            bus.transform.TransformPoint(0.3f, yukseklik, arka ? -olcu.Arka - mesafe : olcu.On + mesafe);

        private IEnumerator Cek(string ad, Vector3 konum, Vector3 bak)
        {
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            yield return null;
            yield return Goruntu.KareSonu();
            Goruntu.Kaydet(cam, $"y16_tabela_{hat}_{otobus}_{OyunSecimi.SeciliZaman.ToString().ToLowerInvariant()}_{ad}.png");
        }

        // HatTabelasi iç durumu (yalnızca test)
        private const BindingFlags Ozel = BindingFlags.NonPublic | BindingFlags.Instance;
        private T Alan<T>(string ad) => tabela != null ? (T)typeof(HatTabelasi).GetField(ad, Ozel).GetValue(tabela) : default;
        private string[] Sayfalar() => Alan<string[]>("sayfalar") ?? new string[0];

        private readonly System.Collections.Generic.List<string> gecisler = new System.Collections.Generic.List<string>();
        private float sonGecis = -1f;

        /// <summary>Ön tabela istenen sayfaya geçene kadar bekler; geçişlerin süresini not eder.</summary>
        private IEnumerator SayfaBekle(int sayfa, float sonra)
        {
            if (tabela == null)
                yield break;
            float bitis = Time.time + 25f;
            int onceki = Alan<int>("sayfa");
            while (Time.time < bitis)
            {
                int s = Alan<int>("sayfa");
                if (s != onceki)
                {
                    if (sonGecis > 0f)
                        gecisler.Add($"sayfa{onceki + 1}={Time.time - sonGecis:F1}sn");
                    sonGecis = Time.time;
                    onceki = s;
                    if (s == sayfa)
                        break;
                }
                yield return null;
            }
            yield return new WaitForSeconds(sonra);
        }

        private string SayfaZamanlari() => gecisler.Count > 0 ? string.Join(" ", gecisler) : "ölçülmedi";

        private void SayfaYaz(string yazi)
        {
            if (tabela == null)
                return;
            typeof(HatTabelasi).GetField("sayfalar", Ozel).SetValue(tabela, new[] { yazi });
            typeof(HatTabelasi).GetField("sayfa", Ozel).SetValue(tabela, 0);
            typeof(HatTabelasi).GetMethod("SayfaAc", Ozel).Invoke(tabela, null);
        }

        /// <summary>Gerçek sayfaları geri getirir (rota değişmiş gibi).</summary>
        private void Tazele()
        {
            if (tabela != null)
                typeof(HatTabelasi).GetMethod("Guncelle", Ozel).Invoke(tabela, new object[] { true });
        }

        private string Levhalar()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var sr in bus.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.name.StartsWith("HatTabelasi"))
                    sb.Append($"{sr.name}@{bus.transform.InverseTransformPoint(sr.transform.position)} ");
            var kor = bus.GetComponent<KorukluOtobus>();
            if (kor != null && kor.ArkaGovde != null)
                foreach (var sr in kor.ArkaGovde.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.name.StartsWith("HatTabelasi"))
                        sb.Append($"{sr.name}(arka gövde)@{bus.transform.InverseTransformPoint(sr.transform.position)} ");
            return sb.Length > 0 ? sb.ToString().Trim() : "yok";
        }

        /// <summary>Tabela malzemesi (Millennium "vmatrix") olan alt malzemelerden kaçında tabela dokusu var.</summary>
        private string MalzemeDurumu()
        {
            string ad = Alan<string>("malzemeAdi");
            var doku = Alan<Texture2D>("onDoku");
            if (string.IsNullOrEmpty(ad))
                return "levha";
            int toplam = 0, tamam = 0;
            var blok = new MaterialPropertyBlock();
            foreach (var r in bus.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(ad))
                        continue;
                    toplam++;
                    r.GetPropertyBlock(blok, i);
                    var t = blok.GetTexture(mats[i].HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex");
                    if (t == doku && r.enabled && r.gameObject.activeInHierarchy)
                        tamam++;
                }
            }
            return $"{tamam}/{toplam} ({ad})";
        }

        private static string Ad(string s) =>
            s.ToLowerInvariant().Replace(' ', '_').Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c').Replace('İ', 'i');
    }
}
