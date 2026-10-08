using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Ana menüdeki döner otobüs vitrini: platform yavaşça döner, parmakla (ya da fareyle) çevrilebilir.
    /// Kamera, otobüs ekranın sağ yarısında kalacak şekilde ekran oranına göre yerleştirilir (sol yarıda menü var).
    /// </summary>
    public class MenuVitrini : MonoBehaviour
    {
        [SerializeField] private Transform platform;
        [SerializeField] private Camera vitrinKamerasi;
        [Tooltip("Otobüs merkezinin ekrandaki yatay konumu (0 sol, 1 sağ).")]
        [Range(0.5f, 0.9f)] [SerializeField] private float ekranX = 0.7f;
        [SerializeField] private float uzaklik = 21f;
        [SerializeField] private float yukseklik = 3.2f;
        [SerializeField] private Vector3 hedef = new Vector3(0f, 1.5f, 0f);
        [Tooltip("Kendiliğinden dönüş hızı (derece/sn).")]
        [SerializeField] private float donusHizi = 9f;
        [SerializeField] private float surukleHassasiyeti = 0.25f;

        private float aci = 210f;
        private float bekleme;
        private bool surukleniyor;

        private void LateUpdate()
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame)
                    surukleniyor = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                if (!pointer.press.isPressed)
                    surukleniyor = false;
                if (surukleniyor)
                {
                    aci -= pointer.delta.ReadValue().x * surukleHassasiyeti;
                    bekleme = 2f;
                }
            }

            if (bekleme > 0f)
                bekleme -= Time.deltaTime;
            else
                aci += donusHizi * Time.deltaTime;
            if (platform != null)
                platform.localRotation = Quaternion.Euler(0f, aci, 0f);

            YerlestirKamera();
        }

        private void YerlestirKamera()
        {
            if (vitrinKamerasi == null)
                return;
            // önden-sağdan (kapı tarafı) bakış; sonra kamerayı sola çevirerek otobüsü ekranın sağına kaydır
            Vector3 yon = new Vector3(0.55f, 0f, 0.84f).normalized;
            Vector3 konum = hedef + yon * uzaklik + Vector3.up * (yukseklik - hedef.y);
            var bakis = Quaternion.LookRotation(hedef - konum, Vector3.up);
            float yatayYariAci = Mathf.Atan(Mathf.Tan(vitrinKamerasi.fieldOfView * 0.5f * Mathf.Deg2Rad) * vitrinKamerasi.aspect);
            float kaydirma = Mathf.Atan((ekranX * 2f - 1f) * Mathf.Tan(yatayYariAci)) * Mathf.Rad2Deg;
            vitrinKamerasi.transform.SetPositionAndRotation(konum, Quaternion.AngleAxis(-kaydirma, Vector3.up) * bakis);
        }
    }
}
