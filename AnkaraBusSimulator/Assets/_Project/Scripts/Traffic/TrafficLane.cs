using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Trafik araçlarının izlediği tek yönlü şerit: dünya koordinatında nokta dizisi.
    /// Harita kurucusu (Ankara Bus > Hat 1 Haritasını Kur) şeritleri yerleşim dosyasından oluşturur.
    /// </summary>
    public class TrafficLane : MonoBehaviour
    {
        /// <summary>Şeritten başka bir şeride geçiş (ör. kavşakta dönüş). Araç 'at' mesafesine gelince
        /// 'probability' olasılıkla 'target' şeridinin 'targetAt' noktasına geçer.</summary>
        [Serializable]
        public struct Exit
        {
            public float at;
            public TrafficLane target;
            public float targetAt;
            [Range(0f, 1f)] public float probability;
        }

        [SerializeField] private Vector3[] points;
        [SerializeField] private float speedLimitKmh = 50f;
        [SerializeField] private Exit[] exits = Array.Empty<Exit>();
        [Tooltip("Durma çizgisi (şerit başından metre); < 0 ise yok.")]
        [SerializeField] private float stopLine = -1f;
        [SerializeField] private TrafficSignal signal;
        [SerializeField] private int signalGroup;

        private float[] cumulative;
        private readonly List<(float at, IGecit gecit)> gecitler = new List<(float, IGecit)>();

        /// <summary>Sahnedeki etkin şeritler (yan şeride geçiş için).</summary>
        public static readonly List<TrafficLane> Hepsi = new List<TrafficLane>();

        /// <summary>Şeridi kesen yaya geçidi: doluyken araçlar önünde durur.</summary>
        public interface IGecit
        {
            bool Dolu { get; }
            /// <summary>Geçidin şerit boyunca yarı genişliği (m).</summary>
            float YariGenislik { get; }
        }

        /// <summary>Şerit üzerindeki yaya geçitleri (şerit başından metre).</summary>
        public IReadOnlyList<(float at, IGecit gecit)> Gecitler => gecitler;

        public void GecitEkle(float at, IGecit gecit)
        {
            gecitler.Add((at, gecit));
            gecitler.Sort((a, b) => a.at.CompareTo(b.at));
        }

        /// <summary>Şeritte p noktasına en yakın yer (şerit başından metre) ve yatay uzaklık.</summary>
        public float EnYakinKonum(Vector3 p, out float uzaklik)
        {
            uzaklik = float.MaxValue;
            if (points == null || points.Length < 2)
                return 0f;
            if (cumulative == null)
                Build();
            float enIyi = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                Vector3 ab = b - a;
                ab.y = 0f;
                Vector3 ap = p - a;
                ap.y = 0f;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                float d = (ap - ab * t).magnitude;
                if (d < uzaklik)
                {
                    uzaklik = d;
                    enIyi = Mathf.Lerp(cumulative[i - 1], cumulative[i], t);
                }
            }
            return enIyi;
        }

        public float Length { get; private set; }
        public float SpeedLimitKmh => speedLimitKmh;
        public Exit[] Exits => exits;
        public float StopLine => stopLine;
        public bool HasSignal => signal != null && stopLine >= 0f;
        /// <summary>Işık şu an kırmızı mı (sarı sayılmaz)? Oyuncunun kırmızıda geçişini saymak için.</summary>
        public bool IsRed => HasSignal && signal.State(signalGroup) == TrafficSignal.SignalState.Red;

        /// <summary>Durma çizgisinde durulmalı mı (kırmızı ya da sarı)? Sarı için 'canStop' araç durabiliyorsa.</summary>
        public bool MustStop(bool canStop) => signal != null && stopLine >= 0f && signal.MustStop(signalGroup, canStop);

        public void SetExits(Exit[] newExits)
        {
            exits = newExits ?? Array.Empty<Exit>();
            Array.Sort(exits, (a, b) => a.at.CompareTo(b.at));
        }

        public void SetSignal(TrafficSignal newSignal, int group, float line)
        {
            signal = newSignal;
            signalGroup = group;
            stopLine = line;
        }

        public void SetPoints(Vector3[] newPoints, float limitKmh)
        {
            points = newPoints;
            speedLimitKmh = limitKmh;
            Build();
        }

        private void Awake() => Build();

        private void OnEnable() => Hepsi.Add(this);

        private void OnDisable() => Hepsi.Remove(this);

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
            if (stopLine >= 0f)
            {
                Sample(stopLine, out var p, out var f);
                var side = Vector3.Cross(Vector3.up, f) * 1.6f;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(p - side + Vector3.up * 0.3f, p + side + Vector3.up * 0.3f);
            }
        }
    }
}
