using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnkaraBus.Gameplay;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.UI;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Ses ve puan sistemini test eder. Önce durakta: rölanti, kamera geçişi, boşta gaz, el freni, fren,
    /// geri vites, korna. Sonra OtomatikPilot'u "normal sürücü" gibi (yarım gaz) başlatır, bir durağı bilerek
    /// atlar ve kırmızıda geçer. Hat sonunda özet panelini okur ve TEKRAR'a basar.
    /// Sonuçlar "[SesPuan]" satırları olarak loga yazılır; en sonda "[SesPuan] BITTI".
    /// Yalnızca SurusTestiEditor (Ses ve Puan Testi) tarafından sahneye eklenir.
    /// </summary>
    public class SesPuanSenaryosu : MonoBehaviour
    {
        [SerializeField] private float normalGaz = 0.5f;
        [SerializeField] private int atlanacakDurak = 1;
        [SerializeField] private bool kirmizidaGec = true;

        private static bool tekrarBekleniyor;

        private BusVehicle bus;
        private BusInput input;
        private BusAudio audioSys;
        private BusCameraRig rig;
        private SeferPuanlama puanlama;
        private RouteTracker tracker;
        private readonly Dictionary<string, int> sesSayisi = new Dictionary<string, int>();
        private int biletPuani, cezaSayisi;
        private bool biletMesajiKontrolEdildi;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Sifirla()
        {
            tekrarBekleniyor = false;
            // Sahne yeniden yüklenince bu bileşen de yok olur; dinleyici statik ve kalıcı
            SceneManager.sceneLoaded -= SahneYuklendi;
            SceneManager.sceneLoaded += SahneYuklendi;
        }

        private static void SahneYuklendi(Scene scene, LoadSceneMode mode)
        {
            if (!tekrarBekleniyor)
                return;
            tekrarBekleniyor = false;
            var yeniBus = Object.FindAnyObjectByType<BusVehicle>();
            var yeniPuan = yeniBus != null ? yeniBus.GetComponent<SeferPuanlama>() : null;
            Debug.Log($"[SesPuan] TEKRAR: '{scene.name}' yeniden yüklendi, otobüs={(yeniBus != null ? "var" : "YOK")}, " +
                      $"puan={(yeniPuan != null ? yeniPuan.Puan.ToString() : "-")}");
            Debug.Log("[SesPuan] BITTI");
        }

        private IEnumerator Start()
        {
            bus = FindAnyObjectByType<BusVehicle>();
            input = bus.GetComponent<BusInput>();
            audioSys = bus.GetComponent<BusAudio>();
            rig = FindAnyObjectByType<BusCameraRig>();
            puanlama = bus.GetComponent<SeferPuanlama>();
            tracker = bus.GetComponent<RouteTracker>();

            var config = AudioSettings.GetConfiguration();
            Debug.Log($"[SesPuan] BASLA ses_sürücü={(AudioSettings.driverCapabilities)} örnekleme={config.sampleRate} " +
                      $"dinleyici={(FindAnyObjectByType<AudioListener>() != null ? "var" : "YOK")} " +
                      $"BusAudio={(audioSys != null ? "var" : "YOK")} puanlama={(puanlama != null ? "var" : "YOK")} " +
                      $"puan_arayüzü={(bus.GetComponent<PuanGostergesi>() != null ? "var" : "YOK")}");

            if (audioSys != null)
                audioSys.OneShotPlayed += (clip, volume) =>
                {
                    sesSayisi[clip] = sesSayisi.TryGetValue(clip, out int n) ? n + 1 : 1;
                    if (clip != "IBIS_piep" || sesSayisi[clip] <= 2)
                        Debug.Log($"[SesPuan] SES {clip} düzey={volume:F2} hız={bus.SpeedKmh:F0}");
                };
            if (puanlama != null)
                puanlama.Puanlandi += (aciklama, puan) =>
                {
                    if (puan < 0) cezaSayisi++;
                    if (aciklama.Contains("bilet")) biletPuani += puan;
                    var p = bus.GetComponent<BusPassengers>();
                    Debug.Log($"[SesPuan] PUAN {puan:+0;-0;0} {aciklama} hız={bus.SpeedKmh:F0} " +
                              $"yük={(p != null ? p.Onboard : 0)} sıradaki={tracker.NextStop?.StopName}");
                };
            tracker.StopSkipped += s =>
                Debug.Log($"[SesPuan] ATLANDI {s.StopName}, hat ilerledi: sıradaki={tracker.NextStop?.StopName}");

            yield return new WaitForSeconds(2f);
            yield return DuraktaSesler();

            // Sürüş: normal sürücü (yarım gaz), bir durağı atla, Kuğulu'da kırmızıda geç
            var pilot = gameObject.AddComponent<OtomatikPilot>();
            pilot.Senaryo(normalGaz, atlanacakDurak, kirmizidaGec, 1f);
            StartCoroutine(BiletMesajiniKontrolEt());

            while (puanlama != null && !puanlama.Bitti)
                yield return null;
            yield return new WaitForSeconds(2f);
            Debug.Log($"[SesPuan] SESLER {string.Join(", ", sesSayisi.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"))}");
            Debug.Log($"[SesPuan] BILET toplam bilet puanı={biletPuani}, ceza sayısı={cezaSayisi}");
            yield return OzetVeTekrar();
        }

        private IEnumerator DuraktaSesler()
        {
            Motor("rölanti, dış kamera");
            rig.SetMode(BusCameraRig.Mode.Cockpit);
            yield return new WaitForSeconds(1.2f);
            Motor("rölanti, kokpit");
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return new WaitForSeconds(1.2f);
            Motor("rölanti, dış kamera (geri)");

            // Boşta gaz: devir ve perde yükselmeli. El freni bırakılmasın diye hafif fren.
            input.SelectNeutral();
            input.AutoBrake = 0.2f;
            input.AutoThrottle = 1f;
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.7f);
                Motor($"N'de tam gaz {0.7f * (i + 1):F1} sn");
            }
            input.AutoThrottle = 0f;
            yield return new WaitForSeconds(2.5f);
            Motor("gaz bırakıldı");
            input.AutoBrake = 0f;
            input.SelectDrive();
            yield return new WaitForSeconds(0.8f);

            Debug.Log("[SesPuan] ADIM el freni bırak / çek");
            input.ToggleHandbrake();
            yield return new WaitForSeconds(1f);
            input.ToggleHandbrake();
            yield return new WaitForSeconds(1f);

            Debug.Log("[SesPuan] ADIM fren bas / bırak");
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(1.5f);
            input.AutoBrake = 0f;
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[SesPuan] ADIM geri vites");
            input.SelectReverse();
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[SesPuan] R: geri vites uyarısı çalıyor={audioSys.ReverseBeepPlaying}");
            input.SelectDrive();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[SesPuan] D: geri vites uyarısı çalıyor={audioSys.ReverseBeepPlaying}");

            input.TouchHorn = true;
            yield return new WaitForSeconds(1f);
            Debug.Log($"[SesPuan] H basılı: korna çalıyor={audioSys.HornPlaying}");
            input.TouchHorn = false;
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[SesPuan] H bırakıldı: korna çalıyor={audioSys.HornPlaying}");
        }

        private void Motor(string etiket)
        {
            var katmanlar = audioSys.EngineState().ToList();
            float ic = katmanlar.Where(k => k.interior).Sum(k => k.volume);
            float dis = katmanlar.Where(k => !k.interior).Sum(k => k.volume);
            var baskin = katmanlar.OrderByDescending(k => k.volume).First();
            Debug.Log($"[SesPuan] MOTOR {etiket}: devir={bus.EngineRpm:F0} iç_set={ic:F2} dış_set={dis:F2} " +
                      $"iç_karışım={audioSys.InteriorBlend:F2} baskın={baskin.clip} perde={baskin.pitch:F2}");
        }

        private IEnumerator BiletMesajiniKontrolEt()
        {
            // İlk bilet puanından sonra ekrandaki kısa mesajı oku
            while (biletPuani == 0)
                yield return null;
            yield return null;
            var gosterge = bus.GetComponent<PuanGostergesi>();
            var yazilar = Yazilar(gosterge != null ? gosterge.transform : null).Concat(Yazilar(null))
                .Where(t => t.Contains("bilet")).Distinct().ToList();
            Debug.Log($"[SesPuan] EKRAN bilet mesajı: {(yazilar.Count > 0 ? string.Join(" | ", yazilar) : "GÖRÜNMÜYOR")}");
        }

        private IEnumerator OzetVeTekrar()
        {
            var panel = GameObject.Find("SeferOzeti");
            Debug.Log($"[SesPuan] OZET paneli={(panel != null && panel.activeInHierarchy ? "açık" : "YOK")}");
            if (panel == null)
            {
                Debug.Log("[SesPuan] BITTI");
                yield break;
            }
            Debug.Log($"[SesPuan] OZET yazılar: {string.Join(" | ", Yazilar(panel.transform).Where(t => t.Trim().Length > 0))}");

            var tekrar = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "TEKRAR");
            if (tekrar == null)
            {
                Debug.Log("[SesPuan] TEKRAR düğmesi YOK");
                Debug.Log("[SesPuan] BITTI");
                yield break;
            }
            tekrarBekleniyor = true;
            tekrar.onClick.Invoke();
            yield return new WaitForSeconds(5f);
            if (tekrarBekleniyor)
            {
                tekrarBekleniyor = false;
                Debug.Log("[SesPuan] TEKRAR: sahne YENİDEN YÜKLENMEDİ");
                Debug.Log("[SesPuan] BITTI");
            }
        }

        private static IEnumerable<string> Yazilar(Transform kok)
        {
            var texts = kok != null ? kok.GetComponentsInChildren<Text>(true) : FindObjectsByType<Text>();
            foreach (var t in texts)
                yield return t.text.Replace("\n", " / ");
            var tmps = kok != null ? kok.GetComponentsInChildren<TMPro.TMP_Text>(true) : FindObjectsByType<TMPro.TMP_Text>();
            foreach (var t in tmps)
                yield return t.text.Replace("\n", " / ");
        }
    }
}
