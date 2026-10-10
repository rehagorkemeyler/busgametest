using UnityEngine;
using UnityEngine.InputSystem;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Direksiyon kontrolü: ekrandaki direksiyon ya da telefonu eğme (ivmeölçer). Seçim ve eğim ayarları PlayerPrefs'te.
    /// Eğim: telefon yatay tutulup direksiyon gibi çevrilir (ekran düzleminde dönüş); sağa çevirmek sağa döndürür.
    /// BusInput okur; seçim AYARLAR panelinden (BusTouchControls) ve ana menüden yapılır.
    /// </summary>
    public static class KontrolAyarlari
    {
        public enum Direksiyon { Ekran, Egim }

        public static readonly string[] DireksiyonAdlari = { "Ekran", "Telefonu eğ" };

        private const string SeciliKey = "Direksiyon";
        private const string MerkezKey = "EgimMerkez";
        private const string AciKey = "EgimAci";

        public static Direksiyon Secili
        {
            get => (Direksiyon)Mathf.Clamp(PlayerPrefs.GetInt(SeciliKey, 0), 0, 1);
            set { PlayerPrefs.SetInt(SeciliKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Direksiyonun ortası sayılan eğim (derece); "Ortala" ile o anki tutuş kaydedilir.</summary>
        public static float Merkez
        {
            get => PlayerPrefs.GetFloat(MerkezKey, 0f);
            set { PlayerPrefs.SetFloat(MerkezKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Tam direksiyon için gereken eğim (derece). Küçük = hassas.</summary>
        public static float TamAci
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(AciKey, 30f), 15f, 60f);
            set { PlayerPrefs.SetFloat(AciKey, Mathf.Clamp(value, 15f, 60f)); PlayerPrefs.Save(); }
        }

        /// <summary>Bu cihazda ivmeölçer (ya da yerçekimi sensörü) var mı?</summary>
        public static bool EgimVar => Accelerometer.current != null || GravitySensor.current != null;

        /// <summary>Yerçekimi yönü (cihaz ekseni, g). Önce yerçekimi sensörü (titremesiz), yoksa ivmeölçer. Android'de sensörler
        /// kapalı başlar: açılır ve örnekleme hızı verilir (verilmeyince S24 FE'de değer hiç gelmiyordu, Y14).</summary>
        public static Vector3? Yercekimi()
        {
            var gs = GravitySensor.current;
            if (gs != null)
            {
                Ac(gs);
                var g = gs.gravity.ReadValue();
                if (g.sqrMagnitude > 0.01f)
                    return g;
            }
            var acc = Accelerometer.current;
            if (acc == null)
                return null;
            Ac(acc);
            return acc.acceleration.ReadValue();
        }

        private static void Ac(Sensor s)
        {
            if (!s.enabled)
                InputSystem.EnableDevice(s);
            if (s.samplingFrequency < 30f)
                s.samplingFrequency = 60f;
        }

        /// <summary>
        /// Telefonun ekran düzlemindeki dönüşü (derece, saat yönü +). İvmeölçer yoksa ya da telefon neredeyse
        /// düz yatıyorsa (yerçekimi ekran düzleminde değil) null.
        /// </summary>
        public static float? EgimAcisi()
        {
            var oku = Yercekimi();
            if (oku == null)
                return null;
            Vector3 g = oku.Value; // cihaz ekseninde yerçekimi yönü (dik tutulunca y = -1)
            float sx, sy;
            switch (Screen.orientation)
            {
                case ScreenOrientation.LandscapeLeft: sx = -g.y; sy = g.x; break;
                case ScreenOrientation.LandscapeRight: sx = g.y; sy = -g.x; break;
                case ScreenOrientation.PortraitUpsideDown: sx = -g.x; sy = -g.y; break;
                default: sx = g.x; sy = g.y; break;
            }
            if (sx * sx + sy * sy < 0.04f)
                return null; // telefon masada düz duruyor
            return Mathf.Atan2(sx, -sy) * Mathf.Rad2Deg;
        }

        /// <summary>Eğimden direksiyon değeri (-1 sol .. +1 sağ); ölü bölge 1,5°.</summary>
        public static float? EgimDireksiyonu()
        {
            float? aci = EgimAcisi();
            if (aci == null)
                return null;
            float fark = Mathf.DeltaAngle(Merkez, aci.Value);
            float olu = 1.5f;
            if (Mathf.Abs(fark) < olu)
                return 0f;
            return Mathf.Clamp((fark - Mathf.Sign(fark) * olu) / (TamAci - olu), -1f, 1f);
        }

        /// <summary>O anki tutuşu orta kabul eder.</summary>
        public static void Ortala()
        {
            float? aci = EgimAcisi();
            if (aci != null)
                Merkez = aci.Value;
        }
    }
}
