using UnityEngine;
using UnityEngine.EventSystems;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Parmakla çevrilen sanal direksiyon. Bırakınca yavaşça ortaya döner.
    /// Value: -1 (tam sol) .. +1 (tam sağ). Dokunulmuyorsa IsHeld false.
    /// </summary>
    public class TouchSteeringWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Uçtan uca değil, ortadan bir yöne en fazla dönüş (derece).")]
        [SerializeField] private float maxDegrees = 270f;
        [Tooltip("Bırakınca ortaya dönme hızı (derece/sn).")]
        [SerializeField] private float returnSpeed = 360f;

        private RectTransform rect;
        private float angle;
        private Vector2 lastDirection;
        private int pointerId = int.MinValue;

        public float Value => angle / maxDegrees;
        public bool IsHeld => pointerId != int.MinValue;

        private void Awake() => rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld)
                return;
            pointerId = e.pointerId;
            lastDirection = Direction(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId)
                return;
            Vector2 direction = Direction(e);
            if (direction.sqrMagnitude < 0.0001f)
                return;
            // Saat yönünde çevirmek sağa dönüş
            float delta = -Vector2.SignedAngle(lastDirection, direction);
            angle = Mathf.Clamp(angle + delta, -maxDegrees, maxDegrees);
            lastDirection = direction;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId)
                pointerId = int.MinValue;
        }

        private Vector2 Direction(PointerEventData e)
        {
            // Ekran koordinatında: simit döndükçe kendi yerel ekseni de döndüğü için yerel koordinat kullanılmaz
            Vector2 center = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, rect.position);
            return (e.position - center).normalized;
        }

        private void Update()
        {
            if (!IsHeld)
                angle = Mathf.MoveTowards(angle, 0f, returnSpeed * Time.unscaledDeltaTime);
            rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
