using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Tek bir kapı kanadı. Menteşeli (dönen), sürgülü (kayan) veya "plug" (önce dışarı, sonra gövde boyunca kayan;
    /// Conecto) çalışabilir. Kapalı konum, sahnedeki başlangıç konumudur.
    /// takip doluysa (körüklünün arka gövdesindeki kanat) kanat o gövdeye göre konumlanır: model kökün altında kalır
    /// ama arka gövde ayrı rigidbody olarak kırılır.
    /// </summary>
    public class BusDoor : MonoBehaviour
    {
        public enum Motion { Rotate, Slide, Plug }

        [SerializeField] private Motion motion = Motion.Rotate;
        [Tooltip("Rotate: açık konumdaki yerel açı farkı (derece).")]
        [SerializeField] private Vector3 openEulerOffset = new Vector3(0f, 90f, 0f);
        [Tooltip("Slide/Plug: açık konumdaki yerel konum farkı (metre, ebeveynin ekseninde).")]
        [SerializeField] private Vector3 openPositionOffset = new Vector3(0f, 0f, 0.8f);
        [Tooltip("Plug: ilk aşamada dışarı çıkış (metre, ebeveynin ekseninde); kalan kayma ikinci aşamada.")]
        [SerializeField] private Vector3 plugOutOffset = new Vector3(0.08f, 0f, 0f);
        [SerializeField] private float duration = 1.2f;
        [Tooltip("Boş değilse kanat bu gövdeyle birlikte hareket eder (körüklünün arka gövdesi).")]
        [SerializeField] private Transform takip;

        private const float DisariOrani = 0.25f;

        private Quaternion closedRotation;
        private Vector3 closedPosition;
        private Vector3 takipKonum;
        private Quaternion takipDonus;
        private Quaternion ebeveynTakipte;   // ebeveynin (kayma ekseni) takip gövdesine göre duruşu, kapalıyken
        private Vector3 ebeveynOlcek = Vector3.one;
        private float openAmount;
        private bool targetOpen;
        private bool degisti = true;

        public bool IsOpen => openAmount >= 1f;
        public bool IsClosed => openAmount <= 0f;

        public void SetOpen(bool open) => targetOpen = open;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            closedPosition = transform.localPosition;
            if (takip != null)
            {
                takipKonum = takip.InverseTransformPoint(transform.position);
                takipDonus = Quaternion.Inverse(takip.rotation) * transform.rotation;
                var ebeveyn = transform.parent;
                ebeveynTakipte = Quaternion.Inverse(takip.rotation) * (ebeveyn != null ? ebeveyn.rotation : Quaternion.identity);
                if (ebeveyn != null)
                    ebeveynOlcek = ebeveyn.lossyScale;
            }
        }

        private void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (!Mathf.Approximately(openAmount, target))
            {
                openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / Mathf.Max(duration, 0.01f));
                degisti = true;
            }
            if (takip == null && degisti)
            {
                Uygula();
                degisti = false;
            }
        }

        // arka gövde fizikle kırılır: her karede onun duruşundan
        private void LateUpdate()
        {
            if (takip != null)
                Uygula();
        }

        private Vector3 Kayma()
        {
            if (motion == Motion.Plug)
            {
                float disari = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openAmount / DisariOrani));
                float yana = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((openAmount - DisariOrani) / (1f - DisariOrani)));
                return plugOutOffset * disari + (openPositionOffset - plugOutOffset) * yana;
            }
            return motion == Motion.Slide ? openPositionOffset * Mathf.SmoothStep(0f, 1f, openAmount) : Vector3.zero;
        }

        private void Uygula()
        {
            Quaternion donus = motion == Motion.Rotate
                ? Quaternion.Euler(openEulerOffset * Mathf.SmoothStep(0f, 1f, openAmount))
                : Quaternion.identity;
            Vector3 kayma = Kayma();
            if (takip == null)
            {
                transform.localRotation = closedRotation * donus;
                transform.localPosition = closedPosition + kayma;
                return;
            }
            // kayma ebeveyn ekseninde tanımlı; arka gövde kırılınca onunla birlikte döner
            Vector3 dunyaKayma = takip.rotation * ebeveynTakipte * Vector3.Scale(ebeveynOlcek, kayma);
            transform.SetPositionAndRotation(takip.TransformPoint(takipKonum) + dunyaKayma, takip.rotation * takipDonus * donus);
        }
    }
}
