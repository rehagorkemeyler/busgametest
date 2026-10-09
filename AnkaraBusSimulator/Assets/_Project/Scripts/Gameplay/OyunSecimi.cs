using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Gameplay
{
    /// <summary>
    /// Ana menüdeki seçimler (hat, kaplama) ve hat listesi. Seçimler PlayerPrefs'te tutulur;
    /// hat sahnesindeki otobüs kaplamayı BusLivery üzerinden buradan alır.
    /// </summary>
    public static class OyunSecimi
    {
        public class Hat
        {
            public string numara;
            public string ad;
            public string duraklar;
            public string sahne;
        }

        public const string MenuSahnesi = "AnaMenu";

        public static readonly Hat[] Hatlar =
        {
            new Hat { numara = "1", ad = "Kızılay AVM – Atakule", duraklar = "Kızılay · Meclis · Kuğulu Park · Cinnah · Atakule", sahne = "Hat1_KizilayAtakule" },
            new Hat { numara = "2", ad = "Kızılay – Ulus", duraklar = "Kızılay · Sıhhiye · Opera · Ulus", sahne = "Hat2_KizilayUlus" },
        };

        private const string HatKey = "SeciliHat";
        private const string KaplamaKey = "Kaplama";

        /// <summary>Seçili hattın Hatlar içindeki sırası.</summary>
        public static int SeciliHat
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HatKey, 0), 0, Hatlar.Length - 1);
            set { PlayerPrefs.SetInt(HatKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Otobüs sırası (Resources/OtobusKatalogu); 0 BMC Procity.</summary>
        public static int Otobus
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt("Otobus", 0));
            set { PlayerPrefs.SetInt("Otobus", value); PlayerPrefs.Save(); }
        }

        /// <summary>Kaplama sırası; -1 rastgele, kayıt yoksa -2 (prefabın kendi kaplaması).</summary>
        public static int Kaplama
        {
            get => PlayerPrefs.GetInt(KaplamaKey, -2);
            set { PlayerPrefs.SetInt(KaplamaKey, value); PlayerPrefs.Save(); }
        }

        public enum Zaman { Gunduz, Aksam, Gece }

        public static readonly string[] ZamanAdlari = { "Gündüz", "Akşam", "Gece" };

        /// <summary>Günün saati (ışık, lambalar). Hat sahnesinde HavaVeZaman uygular.</summary>
        public static Zaman SeciliZaman
        {
            get => (Zaman)Mathf.Clamp(PlayerPrefs.GetInt("Zaman", 0), 0, ZamanAdlari.Length - 1);
            set { PlayerPrefs.SetInt("Zaman", (int)value); PlayerPrefs.Save(); }
        }

        public static bool Yagmur
        {
            get => PlayerPrefs.GetInt("Yagmur", 0) == 1;
            set { PlayerPrefs.SetInt("Yagmur", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string EnIyiPuanAnahtari(string hatNumarasi) =>
            "EnIyiPuan_" + (string.IsNullOrEmpty(hatNumarasi) ? "Hat" : hatNumarasi);

        /// <summary>Hattın en iyi puanı; hiç oynanmadıysa null.</summary>
        public static int? EnIyiPuan(string hatNumarasi)
        {
            string key = EnIyiPuanAnahtari(hatNumarasi);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }

        public static bool MenuVar => Application.CanStreamedLevelBeLoaded(MenuSahnesi);

        public static void AnaMenuyeDon()
        {
            if (MenuVar)
                SceneManager.LoadScene(MenuSahnesi);
        }
    }
}
