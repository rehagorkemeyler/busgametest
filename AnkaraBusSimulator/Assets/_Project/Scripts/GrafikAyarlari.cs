using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AnkaraBus
{
    public enum KaliteSeviyesi { Dusuk, Normal, Yuksek }

    /// <summary>
    /// Görüntü kalitesi: Düşük / Normal / Yüksek. Her seviye Project Settings > Quality'de aynı adlı
    /// kalite seviyesidir ve kendi URP ayarını kullanır (ProjectSetup kurar).
    /// Seçim cihazda saklanır; ilk açılışta GPU'ya göre bir seviye önerilir.
    /// </summary>
    public static class GrafikAyarlari
    {
        private const string PrefsKey = "grafik_kalitesi";

        /// <summary>Kalite seviyelerinin adları; QualitySettings'teki adlarla aynı olmalı.</summary>
        public static readonly string[] SeviyeAdlari = { "Düşük", "Normal", "Yüksek" };

        public static KaliteSeviyesi Mevcut { get; private set; } = KaliteSeviyesi.Normal;
        public static KaliteSeviyesi Onerilen => CihazaGoreOner(SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize);

        public static event Action<KaliteSeviyesi> Degisti;

        /// <summary>Kayıtlı seçimi, yoksa cihaza göre öneriyi uygular. Oyun açılırken çağrılır.</summary>
        public static void KayitliyiUygula()
        {
            var seviye = PlayerPrefs.HasKey(PrefsKey)
                ? (KaliteSeviyesi)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey), 0, SeviyeAdlari.Length - 1)
                : Onerilen;
            Uygula(seviye, kaydet: false);
        }

        public static void Uygula(KaliteSeviyesi seviye, bool kaydet = true)
        {
            int index = Array.IndexOf(QualitySettings.names, SeviyeAdlari[(int)seviye]);
            if (index < 0)
            {
                Debug.LogWarning($"[Grafik] '{SeviyeAdlari[(int)seviye]}' kalite seviyesi yok. Ankara Bus > Proje Ayarlarını Uygula.");
                return;
            }

            QualitySettings.SetQualityLevel(index, true);
            Mevcut = seviye;
            if (kaydet)
            {
                PlayerPrefs.SetInt(PrefsKey, (int)seviye);
                PlayerPrefs.Save();
            }
            Degisti?.Invoke(seviye);
        }

        /// <summary>
        /// GPU adına göre kaba sınıflandırma. Yalnızca ilk açılıştaki varsayılanı belirler;
        /// oyuncu menüden her zaman değiştirebilir.
        /// </summary>
        public static KaliteSeviyesi CihazaGoreOner(string gpu, int ramMb)
        {
            gpu ??= "";
            if (ramMb > 0 && ramMb < 3500)
                return KaliteSeviyesi.Dusuk;

            // Güncel üst sınıf: Samsung Xclipse, Arm Immortalis, Adreno 7xx/8xx, Mali-G710 ve sonrası, Apple
            if (Regex.IsMatch(gpu, @"Xclipse|Immortalis|Apple|Adreno.*\b[78]\d\d\b|Mali-G[7-9]\d\d", RegexOptions.IgnoreCase))
                return KaliteSeviyesi.Yuksek;

            // Giriş seviyesi: Mali-G3x/G5x/T serisi, Adreno 5xx ve altı, Adreno 60x-62x, PowerVR
            if (Regex.IsMatch(gpu, @"Mali-G[35]\d\b|Mali-T|PowerVR|Adreno.*\b([1-5]\d\d|6[0-2]\d)\b", RegexOptions.IgnoreCase))
                return KaliteSeviyesi.Dusuk;

            return KaliteSeviyesi.Normal;
        }
    }
}
