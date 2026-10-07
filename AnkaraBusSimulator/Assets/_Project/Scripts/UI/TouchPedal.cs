using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Analog pedal: parmak pedalın altına yakınsa hafif, üstüne yakınsa tam basılır.
    /// Value: 0..1.
    /// </summary>
    public class TouchPedal : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Pedala dokununca en az bu kadar basılır.")]
        [SerializeField, Range(0f, 1f)] private float minPress = 0.3f;
        [SerializeField] private Image fill;

        private RectTransform rect;
        private int pointerId = int.MinValue;

        public float Value { get; private set; }

        public Image Fill { get => fill; set => fill = value; }

        private void Awake() => rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId != int.MinValue)
                return;
            pointerId = e.pointerId;
            UpdateValue(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointerId)
                UpdateValue(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId)
                return;
            pointerId = int.MinValue;
            Value = 0f;
            Refresh();
        }

        private void UpdateValue(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
            float t = Mathf.InverseLerp(rect.rect.yMin, rect.rect.yMax, local.y);
            Value = Mathf.Lerp(minPress, 1f, t);
            Refresh();
        }

        private void Refresh()
        {
            if (fill != null)
                fill.fillAmount = Value;
        }
    }
}
