using System.Collections;
using System.IO;
using AnkaraBus.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y11 test build'inde hat sahnesine eklenir; Y10Baslatici.Gorev'e göre ölçüm turunu (PerfBenchmark)
    /// ya da Y10 sürüş gözlemini (OtomatikPilot + Y10Gozlemci) ya da körüklü fırlama testini (KorukluTesti) başlatır.
    /// </summary>
    public class Y11Hat : MonoBehaviour
    {
        [SerializeField] private string sonrakiSahne;

        public void Ayarla(string sonraki) => sonrakiSahne = sonraki;

        private void Awake()
        {
            string sahne = SceneManager.GetActiveScene().name;
            string etiket = $"{sahne}_{OyunSecimi.SeciliZaman}{(OyunSecimi.SeciliHava != OyunSecimi.Hava.Acik ? "_" + OyunSecimi.SeciliHava : "")}";
            if (Y10Baslatici.Gorev == "koruklu")
            {
                // Y14: fırlama zorlama senaryoları, Hat 1 → Hat 2
                Time.timeScale = 1f;
                gameObject.AddComponent<KorukluTesti>().Ayarla(true, sonrakiSahne);
                return;
            }
            if (Y10Baslatici.Gorev == "egim")
            {
                // Y14: elle eğerek direksiyon ve yolcu sesi (kulakla)
                Time.timeScale = 1f;
                gameObject.AddComponent<EgimTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "kamera")
            {
                // Y14: kamera kipleri ve ekran görüntüleri (yalnızca bu hat)
                Time.timeScale = 1f;
                gameObject.AddComponent<KameraTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "hiz")
            {
                // Y15: hız sınırı cezası 75 km/s üstünde mi (yalnızca bu hat)
                Time.timeScale = 1f;
                gameObject.AddComponent<HizTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "kar")
            {
                // Y17: karlı hava görüntüleri (Kızılay ve son durak, dış/kokpit, durur ve sürerken), FPS, rüzgâr sesi; sonra sonraki hat
                Time.timeScale = 1f;
                gameObject.AddComponent<KarTesti>().Ayarla(sonrakiSahne);
                return;
            }
            if (Y10Baslatici.Gorev == "tutus")
            {
                // Y17: kuru / yağmur / kar tutuşu: 50 km/s'den fren, Cinnah'ta kalkış, keskin dönüş (yalnızca bu hat)
                Time.timeScale = 1f;
                gameObject.AddComponent<TutusTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "kapi")
            {
                // Y18: kapı kanatları (Conecto plug kapı), yolcular, sesler, körük, kalite; Conecto → BMC → Millennium (yalnızca bu hat)
                Time.timeScale = 1f;
                gameObject.AddComponent<KapiTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "koruk")
            {
                // Y19: körüklü BMC ve Millennium görünüm, kalite, mafsal kararlılığı, körük bükülmesi (fırlama bekçisi yanında)
                Time.timeScale = 1f;
                gameObject.AddComponent<KorukluTesti>();
                gameObject.AddComponent<KorukTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "gece")
            {
                // Y18: gece lambaları, dolgu ışığı (rendering layer), kaplamalar, fren, kalite, FPS; Conecto → BMC → Millennium
                Time.timeScale = 1f;
                gameObject.AddComponent<GeceTesti>();
                return;
            }
            if (Y10Baslatici.Gorev == "tabela")
            {
                // Y16: LED hat tabelası, üç otobüs ve kaplamalar, sonra sonraki hat
                Time.timeScale = 1f;
                gameObject.AddComponent<TabelaTesti>().Ayarla(sonrakiSahne);
                return;
            }
            if (Y10Baslatici.Gorev == "anons")
            {
                // Y16: gerçek zamanlı otomatik pilot, durak anonsları ve FPS; bir durak atlanır (sıradaki durak anonsu yine gelmeli)
                Time.timeScale = 1f;
                gameObject.AddComponent<KorukluTesti>();
                var pilot = gameObject.AddComponent<OtomatikPilot>();
                pilot.Configure(null, sonrakiSahne);
                pilot.Senaryo(1f, 2, false, 0f);
                gameObject.AddComponent<AnonsTesti>();
                return;
            }
            // her görevde fırlama bekçisi (yalnızca izler)
            gameObject.AddComponent<KorukluTesti>();
            if (Y10Baslatici.Gorev == "olcum")
            {
                // ölçüm yalnızca Hat 1'de (Kızılay ve Kuğulu bu hatta)
                gameObject.AddComponent<PerfBenchmark>().Ayarla(etiket, "");
                StartCoroutine(Ekran(etiket));
                return;
            }
            Time.timeScale = Y10Baslatici.Hiz;
            gameObject.AddComponent<OtomatikPilot>().Configure(null, sonrakiSahne);
            gameObject.AddComponent<Y10Gozlemci>();
        }

        private static IEnumerator Ekran(string etiket)
        {
            // ilk durak (Kızılay), dış kamera: PerfBenchmark ısınma süresinin sonunda
            yield return new WaitForSeconds(9f);
            yield return new WaitForEndOfFrame();
            Kaydet($"y11_surus_{etiket}.png");
        }

        /// <summary>Ekranı persistentDataPath'e PNG olarak hemen yazar (WaitForEndOfFrame'den sonra çağrılmalı).</summary>
        public static void Kaydet(string ad)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, ad), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[Y11] görüntü: {ad}");
        }
    }
}
