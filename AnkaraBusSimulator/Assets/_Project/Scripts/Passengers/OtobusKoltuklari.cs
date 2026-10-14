using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Otobüsün yolcu koltukları ve ayakta durma yerleri (Y20). OtobusKurucu kurar: tools/blender/koltuk_cikar.py'nin çıkardığı
    /// "&lt;Ad&gt;_koltuklar.json" → "Koltuklar" (kök) ve körüklüde "Koltuklar_Arka" (ArkaGovde) altında noktalar.
    /// Koltuk noktası oturan yolcunun kökü (yönü yolcunun baktığı yön); "on" ve "koridor" aynı gövdenin uzayında (noktanın
    /// ebeveyni) yolcunun koltuğa gelirken geçtiği yerler. Ayakta yerler koridorda, tavan borusunun altında.
    /// </summary>
    public class OtobusKoltuklari : MonoBehaviour
    {
        [Serializable]
        public class Koltuk
        {
            public Transform nokta;
            public Vector3 on;          // nokta.parent uzayında
            public Vector3 koridor;     // nokta.parent uzayında
            [NonSerialized] public Passenger yolcu;
            public Transform Uzay => nokta != null ? nokta.parent : null;
        }

        [Serializable]
        public class AyaktaYer
        {
            public Transform nokta;
            [NonSerialized] public Passenger yolcu;
            public Transform Uzay => nokta != null ? nokta.parent : null;
        }

        [SerializeField] private Koltuk[] koltuklar = new Koltuk[0];
        [SerializeField] private AyaktaYer[] ayakta = new AyaktaYer[0];

        public IReadOnlyList<Koltuk> Koltuklar => koltuklar;
        public IReadOnlyList<AyaktaYer> Ayakta => ayakta;

        /// <summary>Yolcu kamerasının konumu (otobüs kökünün uzayında; BusCameraRig verir). Yakınındaki koltuk ve ayakta yerlere
        /// yolcu konmaz: kamera oturanın kafasının içinde kalıyordu (BMC Koltuk_12).</summary>
        public Vector3? KameraYerel { get; set; }

        private const float KameraBoslugu = 0.5f;

        private bool KamerayaYakin(Transform nokta)
        {
            if (KameraYerel == null)
                return false;
            var fark = transform.InverseTransformPoint(nokta.position) - KameraYerel.Value;
            fark.y = 0f;
            return fark.sqrMagnitude < KameraBoslugu * KameraBoslugu;
        }

        /// <summary>Verilen noktaya (dünya) yakın, rastgele bir boş koltuk; tercihen verilen gövdede. Yoksa null.</summary>
        public Koltuk BosKoltuk(Vector3 yakin, Transform tercihUzay = null)
        {
            Koltuk enIyi = null;
            float enIyiPuan = float.MaxValue;
            foreach (var k in koltuklar)
            {
                if (k.yolcu != null || k.nokta == null || KamerayaYakin(k.nokta))
                    continue;
                // yakın koltuk biraz önde ama yolcular otobüse dağılsın (körüklüde arka gövdeye de geçilsin)
                float puan = Vector3.Distance(k.nokta.position, yakin) + UnityEngine.Random.Range(0f, 12f);
                if (tercihUzay != null && k.Uzay != tercihUzay)
                    puan += 3f;                 // kapının olduğu gövde biraz önde
                if (puan < enIyiPuan)
                {
                    enIyiPuan = puan;
                    enIyi = k;
                }
            }
            return enIyi;
        }

        public AyaktaYer BosAyakta(Vector3 yakin)
        {
            AyaktaYer enIyi = null;
            float enIyiPuan = float.MaxValue;
            foreach (var a in ayakta)
            {
                if (a.yolcu != null || a.nokta == null || KamerayaYakin(a.nokta))
                    continue;
                float puan = (a.nokta.position - yakin).sqrMagnitude + UnityEngine.Random.Range(0f, 4f);
                if (puan < enIyiPuan)
                {
                    enIyiPuan = puan;
                    enIyi = a;
                }
            }
            return enIyi;
        }

        /// <summary>Yolcunun tuttuğu koltuğu/yeri boşaltır.</summary>
        public void Birak(Passenger yolcu)
        {
            foreach (var k in koltuklar)
                if (k.yolcu == yolcu)
                    k.yolcu = null;
            foreach (var a in ayakta)
                if (a.yolcu == yolcu)
                    a.yolcu = null;
        }

        /// <summary>Uzaydaki koridor zemin yüksekliği: bu gövdedeki en yakın koltuğun koridor noktasının yüksekliği.</summary>
        public float KoridorY(Transform uzay, float z)
        {
            float y = 0.4f, en = float.MaxValue;
            foreach (var k in koltuklar)
                if (k.Uzay == uzay && Mathf.Abs(k.koridor.z - z) < en)
                {
                    en = Mathf.Abs(k.koridor.z - z);
                    y = k.koridor.y;
                }
            return y;
        }
    }
}
