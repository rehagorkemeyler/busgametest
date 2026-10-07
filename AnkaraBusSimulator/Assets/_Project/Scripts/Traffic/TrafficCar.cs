using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Şerit izleyen basit trafik aracı. Fizik simülasyonu yok (kinematik): mobilde ucuz.
    /// Önündeki araca veya otobüse (Rigidbody'si olan her şeye) göre yavaşlar ve durur.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCar : MonoBehaviour
    {
        [SerializeField] private float maxSpeedKmh = 50f;
        [SerializeField] private float acceleration = 2.5f;
        [SerializeField] private float braking = 6f;
        [Tooltip("Öndeki araçla bırakılan boşluk (m).")]
        [SerializeField] private float safeGap = 5f;
        [SerializeField] private float wheelRadius = 0.3f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private Rigidbody body;
        private Transform[] wheels;
        private float speed;
        private float frontOffset = 2.2f;
        private Vector3 castHalfExtents = new Vector3(0.8f, 0.4f, 0.1f);

        public TrafficLane Lane { get; private set; }
        public float Distance { get; private set; }
        public bool ReachedEnd => Lane != null && Distance >= Lane.Length - 0.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var found = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Teker"))
                    found.Add(t);
            wheels = found.ToArray();

            if (TryGetComponent<BoxCollider>(out var box))
            {
                frontOffset = box.center.z + box.size.z * 0.5f;
                castHalfExtents = new Vector3(box.size.x * 0.45f, 0.4f, 0.1f);
            }
        }

        /// <summary>Aracı şeridin belirli bir noktasına ışınlar.</summary>
        public void Place(TrafficLane lane, float distance, float maxSpeed)
        {
            Lane = lane;
            Distance = distance;
            maxSpeedKmh = maxSpeed;
            speed = maxSpeed / 3.6f * 0.7f;
            lane.Sample(distance, out var position, out var forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void FixedUpdate()
        {
            if (Lane == null)
                return;

            float dt = Time.fixedDeltaTime;
            float target = Mathf.Min(maxSpeedKmh, Lane.SpeedLimitKmh) / 3.6f;
            float free = FreeDistanceAhead(safeGap + speed * speed / (2f * braking) + 3f);
            if (free < float.MaxValue)
                target = Mathf.Min(target, Mathf.Sqrt(2f * braking * Mathf.Max(0f, free - safeGap)));

            speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : braking) * dt);
            Distance += speed * dt;

            Lane.Sample(Distance, out var position, out var forward);
            body.MovePosition(position);
            body.MoveRotation(Quaternion.LookRotation(forward, Vector3.up));

            float degrees = speed * dt / wheelRadius * Mathf.Rad2Deg;
            foreach (var w in wheels)
                w.Rotate(Vector3.right, degrees, Space.Self);
        }

        /// <summary>Öndeki en yakın hareketli cisme (araç, otobüs) mesafe. Yol ve binalar yok sayılır.</summary>
        private float FreeDistanceAhead(float range)
        {
            var origin = transform.position + transform.forward * (frontOffset + 0.2f) + transform.up * 0.8f;
            int n = Physics.BoxCastNonAlloc(origin, castHalfExtents, transform.forward, Hits, transform.rotation,
                                            range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var other = Hits[i].rigidbody;
                if (other != null && other != body && Hits[i].distance < nearest)
                    nearest = Hits[i].distance;
            }
            return nearest;
        }
    }
}
