using System.Collections;
using System.IO;
using AnkaraBus.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y11 test build'inde hat sahnesine eklenir; Y10Baslatici.Gorev'e göre ölçüm turunu (PerfBenchmark)
    /// ya da Y10 sürüş gözlemini (OtomatikPilot + Y10Gozlemci) başlatır.
    /// </summary>
    public class Y11Hat : MonoBehaviour
    {
        [SerializeField] private string sonrakiSahne;

        public void Ayarla(string sonraki) => sonrakiSahne = sonraki;

        private void Awake()
        {
            string sahne = SceneManager.GetActiveScene().name;
            string etiket = $"{sahne}_{OyunSecimi.SeciliZaman}{(OyunSecimi.Yagmur ? "_yagmur" : "")}";
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
