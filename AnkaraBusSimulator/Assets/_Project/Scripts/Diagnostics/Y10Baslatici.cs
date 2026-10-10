using System.Collections;
using AnkaraBus.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Test başlatıcısı: zamanı, havayı ve kaliteyi ayarlayıp görevi başlatır. Android'de ayarlar
    /// intent ile verilebilir: am start ... -e gorev olcum|y10|menu|koruklu|kamera -e zaman Gunduz|Aksam|Gece -e yagmur 0|1 -e kalite 0|1|2
    /// (olcum: Hat 1'de PerfBenchmark; y10: otomatik pilot + Y10Gozlemci, Hat 1 → Hat 2; menu: menü ekran görüntüsü; koruklu: KorukluTesti). -e otobus 0|1|2
    /// </summary>
    public class Y10Baslatici : MonoBehaviour
    {
        [SerializeField] private OyunSecimi.Zaman zaman = OyunSecimi.Zaman.Gece;
        [SerializeField] private bool yagmur = true;
        [SerializeField] private int kalite = -1;
        [SerializeField] private string ilkSahne = "Hat1_KizilayAtakule";

        /// <summary>Hat sahnelerindeki Y11Hat bunu okur.</summary>
        public static string Gorev = "y10";

        /// <summary>Sürüş testinde zaman çarpanı (intent "hiz"); ölçümde her zaman 1.</summary>
        public static float Hiz = 2f;

        public void Ayarla(OyunSecimi.Zaman z, bool y, int k) { zaman = z; yagmur = y; kalite = k; }

        private IEnumerator Start()
        {
            IntentOku();
            OyunSecimi.SeciliZaman = zaman;
            OyunSecimi.Yagmur = yagmur;
            if (kalite >= 0) GrafikAyarlari.Uygula((KaliteSeviyesi)kalite, kaydet: true);
            Debug.Log($"[Y11] BASLAT gorev={Gorev} zaman={zaman} yagmur={yagmur} kalite={GrafikAyarlari.Mevcut} cihaz={SystemInfo.deviceModel}");
            yield return null;
            if (Gorev == "menu")
            {
                foreach (var m in FindObjectsByType<MonoBehaviour>())
                    if (m.GetType().Name == "AnaMenu") m.SendMessage("Yenile", SendMessageOptions.DontRequireReceiver);
                yield return new WaitForSeconds(3f);
                yield return new WaitForEndOfFrame();
                Y11Hat.Kaydet($"y11_menu_{zaman}{(yagmur ? "_yagmur" : "")}.png");
                Debug.Log("[Y11] BITTI menu");
                yield break;
            }
            SceneManager.LoadScene(ilkSahne);
        }

        private void IntentOku()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = act.Call<AndroidJavaObject>("getIntent");
                string Al(string k) => intent.Call<string>("getStringExtra", k);
                if (!string.IsNullOrEmpty(Al("gorev"))) Gorev = Al("gorev");
                if (System.Enum.TryParse(Al("zaman") ?? "", true, out OyunSecimi.Zaman z)) zaman = z;
                if (!string.IsNullOrEmpty(Al("yagmur"))) yagmur = Al("yagmur") == "1";
                if (int.TryParse(Al("kalite") ?? "", out int k)) kalite = k;
                if (float.TryParse(Al("hiz") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hz)) Hiz = hz;
                if (!string.IsNullOrEmpty(Al("otobus")) && int.TryParse(Al("otobus"), out int ot)) OyunSecimi.Otobus = ot;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Y11] intent okunamadı: " + e.Message);
            }
#endif
        }
    }
}
