using System.Collections;
using System.Reflection;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y14 elle yapılan test: direksiyon "Telefonu eğ"e alınır, 60 sn boyunca eğim açısı ve direksiyon değeri loglanır
    /// (kişi telefonu direksiyon gibi sola/sağa çevirir); aynı sürede yolcunun durak isteği sesi (stop.wav) 6 sn'de bir,
    /// sırayla kokpit ve dış kamerada, oyundaki düzeyiyle çalınır (kulakla Türkçe mi, duyuluyor mu).
    /// Sonuçlar "[Egim]" satırları. Y11 test build'inde "egim" görevi.
    /// </summary>
    public class EgimTesti : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            var bus = FindAnyObjectByType<BusVehicle>();
            var rig = FindAnyObjectByType<BusCameraRig>();
            var ses = bus.GetComponent<BusAudio>();
            var onceki = KontrolAyarlari.Secili;
            KontrolAyarlari.Secili = KontrolAyarlari.Direksiyon.Egim;
            KontrolAyarlari.Ortala();
            Debug.Log($"[Egim] BASLA ivmeölçer={(KontrolAyarlari.EgimVar ? "var" : "YOK")} merkez={KontrolAyarlari.Merkez:F1}° tam_açı={KontrolAyarlari.TamAci:F0}° " +
                      $"yön={Screen.orientation}");

            var bayrak = BindingFlags.NonPublic | BindingFlags.Instance;
            var cab = typeof(BusAudio).GetField("cabSource", bayrak)?.GetValue(ses) as AudioSource;
            var klip = typeof(BusAudio).GetField("stopRequest", bayrak)?.GetValue(ses) as AudioClip;
            float master = (float)(typeof(BusAudio).GetField("masterVolume", bayrak)?.GetValue(ses) ?? 1f);
            var oneShot = typeof(BusAudio).GetMethod("OneShot", bayrak);
            ses.OneShotPlayed += (k, d) => Debug.Log($"[Egim] ses '{k}' düzey {d:F2} kamera {rig.CurrentMode} iç_karışım {ses.InteriorBlend:F2}");
            Debug.Log($"[Egim] yolcu sesi klibi '{klip?.name}' {klip?.length:F2} sn, kanal {klip?.channels}, {klip?.frequency} Hz");

            float bitis = Time.time + 60f, sonrakiSes = Time.time + 2f, sonrakiLog = 0f;
            int sesSayisi = 0;
            float enSol = 0f, enSag = 0f;
            while (Time.time < bitis)
            {
                float? aci = KontrolAyarlari.EgimAcisi();
                float direksiyon = bus.Steer;
                enSol = Mathf.Min(enSol, direksiyon);
                enSag = Mathf.Max(enSag, direksiyon);
                if (Time.time > sonrakiLog)
                {
                    sonrakiLog = Time.time + 0.5f;
                    Debug.Log($"[Egim] açı={(aci.HasValue ? aci.Value.ToString("F1") : "yok")}° direksiyon={direksiyon:+0.00;-0.00} hız={bus.SpeedKmh:F0}");
                }
                if (Time.time > sonrakiSes && cab != null && klip != null && oneShot != null)
                {
                    sonrakiSes = Time.time + 6f;
                    rig.SetMode(sesSayisi % 2 == 0 ? BusCameraRig.Mode.Cockpit : BusCameraRig.Mode.Chase);
                    yield return new WaitForSeconds(1f);   // iç/dış ses karışımı yerine otursun
                    oneShot.Invoke(ses, new object[] { cab, klip, master * (0.15f + 0.25f * ses.InteriorBlend) });
                    sesSayisi++;
                }
                yield return null;
            }
            KontrolAyarlari.Secili = onceki;
            Debug.Log($"[Egim] SONUC direksiyon aralığı {enSol:+0.00;-0.00} … {enSag:+0.00;-0.00}, ses {sesSayisi} kez");
            Debug.Log("[Egim] BITTI");
        }
    }
}
