using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Tek bir kapı kanadı. Menteşeli (dönen) veya sürgülü (kayan) çalışabilir.
    /// Kapalı konum, sahnedeki başlangıç konumudur.
    /// </summary>
    public class BusDoor : MonoBehaviour
    {
        public enum Motion { Rotate, Slide }

        [SerializeField] private Motion motion = Motion.Rotate;
        [Tooltip("Rotate: açık konumdaki yerel açı farkı (derece).")]
        [SerializeField] private Vector3 openEulerOffset = new Vector3(0f, 90f, 0f);
        [Tooltip("Slide: açık konumdaki yerel konum farkı (metre).")]
        [SerializeField] private Vector3 openPositionOffset = new Vector3(0f, 0f, 0.8f);
        [SerializeField] private float duration = 1.2f;

        private Quaternion closedRotation;
        private Vector3 closedPosition;
        private float openAmount;
        private bool targetOpen;

        public bool IsOpen => openAmount >= 1f;
        public bool IsClosed => openAmount <= 0f;

        public void SetOpen(bool open) => targetOpen = open;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            closedPosition = transform.localPosition;
        }

        private void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target))
                return;

            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / Mathf.Max(duration, 0.01f));
            float t = Mathf.SmoothStep(0f, 1f, openAmount);

            if (motion == Motion.Rotate)
                transform.localRotation = closedRotation * Quaternion.Euler(openEulerOffset * t);
            else
                transform.localPosition = closedPosition + openPositionOffset * t;
        }
    }
}
