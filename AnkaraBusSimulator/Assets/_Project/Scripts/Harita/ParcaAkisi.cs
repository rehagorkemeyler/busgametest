using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Harita
{
    /// <summary>
    /// Gerçek coğrafyalı büyük haritada (Hat 3, tools/harita/hat3) parça (250 m kare) görünürlüğü: otobüse uzak parçaların
    /// nesneleri kapatılır. Yarıçap grafik kalitesine göre (Düşük 450, Normal 650, Yüksek 900 m). Uzak şehir halkası
    /// (1 km'lik "uzak" parçalar) 3,2 km'ye kadar açık kalır; ufuk siste kaybolur. Hat3Parcalari (Editor) doldurur.
    /// </summary>
    public class ParcaAkisi : MonoBehaviour
    {
        [Serializable]
        public class Parca
        {
            public string ad;
            public Vector3 merkez;
            public bool uzak;
            public GameObject[] nesneler;
            [NonSerialized] public bool acik = true;
        }

        [SerializeField] private Parca[] parcalar = new Parca[0];
        [SerializeField] private float uzakYaricap = 3200f;
        [SerializeField] private float aralik = 0.5f;

        private Transform otobus;
        private float sayac;

        public IReadOnlyList<Parca> Parcalar => parcalar;

        public void Ayarla(Parca[] liste) => parcalar = liste;

        public static float Yaricap => GrafikAyarlari.Mevcut == KaliteSeviyesi.Dusuk ? 450f
            : GrafikAyarlari.Mevcut == KaliteSeviyesi.Yuksek ? 900f : 650f;

        private void Start()
        {
            Bul();
            Guncelle(true);
        }

        private void Bul()
        {
            if (otobus != null)
                return;
            var bus = FindAnyObjectByType<AnkaraBus.Vehicle.BusVehicle>();
            if (bus != null)
                otobus = bus.transform;
            else if (Camera.main != null)
                otobus = Camera.main.transform;
        }

        private void Update()
        {
            sayac -= Time.unscaledDeltaTime;
            if (sayac > 0f)
                return;
            sayac = aralik;
            Bul();
            Guncelle(false);
        }

        /// <summary>Otobüs ışınlandığında (ör. durak seçimi) hemen çağrılabilir.</summary>
        public void Guncelle(bool hepsi)
        {
            if (otobus == null)
                return;
            Vector3 p = otobus.position;
            float r = Yaricap + 180f;          // parça merkezinden köşesine ~177 m
            foreach (var pc in parcalar)
            {
                float dx = pc.merkez.x - p.x, dz = pc.merkez.z - p.z;
                float lim = pc.uzak ? uzakYaricap : r;
                bool ac = dx * dx + dz * dz < lim * lim;
                if (ac == pc.acik && !hepsi)
                    continue;
                pc.acik = ac;
                foreach (var go in pc.nesneler)
                    if (go != null)
                        go.SetActive(ac);
            }
        }
    }
}
