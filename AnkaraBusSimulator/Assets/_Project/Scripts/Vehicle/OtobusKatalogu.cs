using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Oyunda seçilebilen otobüsler (Resources/OtobusKatalogu). Sıra menüdeki sıradır; ilki sahnelere konan
    /// varsayılan otobüstür (BMC). Ankara Bus > ... Prefabını Kur her çalıştığında kendiliğinden güncellenir.
    /// </summary>
    public class OtobusKatalogu : ScriptableObject
    {
        public BusDefinition[] otobusler = new BusDefinition[0];

        private static OtobusKatalogu yuklu;

        public static OtobusKatalogu Yukle() => yuklu != null ? yuklu : yuklu = Resources.Load<OtobusKatalogu>("OtobusKatalogu");

        public int Sayi => otobusler?.Length ?? 0;

        /// <summary>Menüde seçilen otobüs (yoksa ilki).</summary>
        public BusDefinition Secili
        {
            get
            {
                if (Sayi == 0)
                    return null;
                return otobusler[Mathf.Clamp(Gameplay.OyunSecimi.Otobus, 0, Sayi - 1)];
            }
        }
    }
}
