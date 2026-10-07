using System;
using UnityEngine;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Trafik araçlarının izlediği tek yönlü şerit: dünya koordinatında nokta dizisi.
    /// Harita kurucusu (Ankara Bus > Hat 1 Haritasını Kur) şeritleri yerleşim dosyasından oluşturur.
    /// </summary>
    public class TrafficLane : MonoBehaviour
    {
        [SerializeField] private Vector3[] points;
        [SerializeField] private float speedLimitKmh = 50f;

        private float[] cumulative;

        public float Length { get; private set; }
        public float SpeedLimitKmh => speedLimitKmh;

        public void SetPoints(Vector3[] newPoints, float limitKmh)
        {
            points = newPoints;
            speedLimitKmh = limitKmh;
            Build();
        }

        private void Awake() => Build();

        private void Build()
        {
            if (points == null || points.Length < 2)
            {
                Length = 0f;
                return;
            }
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            Length = cumulative[points.Length - 1];
        }

        /// <summary>Şerit başından 'distance' metre ilerideki nokta ve yön.</summary>
        public void Sample(float distance, out Vector3 position, out Vector3 forward)
        {
            if (cumulative == null)
                Build();
            distance = Mathf.Clamp(distance, 0f, Length);
            int i = Array.BinarySearch(cumulative, distance);
            if (i < 0)
                i = ~i - 1;
            i = Mathf.Clamp(i, 0, points.Length - 2);
            float segment = cumulative[i + 1] - cumulative[i];
            float t = segment > 0f ? (distance - cumulative[i]) / segment : 0f;
            position = Vector3.Lerp(points[i], points[i + 1], t);
            forward = (points[i + 1] - points[i]).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null)
                return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(points[i - 1] + Vector3.up * 0.3f, points[i] + Vector3.up * 0.3f);
        }
    }
}
