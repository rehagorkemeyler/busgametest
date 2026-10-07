using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Hızı doğrudan Rigidbody'den okur. RCC dahil her Rigidbody tabanlı kontrolcüyle çalışır.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyTelemetry : MonoBehaviour, IVehicleTelemetry
    {
        [Tooltip("Bu hızın altında araç durmuş sayılır (km/s).")]
        [SerializeField] private float stoppedThresholdKmh = 1.5f;

        private Rigidbody body;

        public float SpeedKmh
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return body.linearVelocity.magnitude * 3.6f;
#else
                return body.velocity.magnitude * 3.6f;
#endif
            }
        }

        public bool IsStopped => SpeedKmh < stoppedThresholdKmh;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }
    }
}
