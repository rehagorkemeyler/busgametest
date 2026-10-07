using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüs kamerası: dış takip veya kokpit. Toggle() ile geçilir.
    /// Kokpit konumu otobüs prefabındaki "SurucuGozu" objesinden alınır.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BusCameraRig : MonoBehaviour
    {
        public enum Mode { Chase, Cockpit }

        [SerializeField] private BusVehicle target;
        [SerializeField] private Mode mode = Mode.Chase;

        [Header("Dış takip")]
        [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 4.5f, -14f);
        [SerializeField] private float lookHeight = 1.8f;
        [SerializeField] private float lookAhead = 4f;
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private float yawSharpness = 2.5f;
        [SerializeField] private float chaseFov = 55f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Kokpit")]
        [SerializeField] private float cockpitFov = 65f;
        [Tooltip("SurucuGozu yoksa kullanılan yerel konum.")]
        [SerializeField] private Vector3 cockpitFallback = new Vector3(-0.62f, 2.05f, 4.45f);
        [Tooltip("Sürücünün yola bakış açısı (aşağı, derece).")]
        [SerializeField] private float cockpitPitch = 8f;

        private Camera cam;
        private Transform eye;
        private float yaw;

        public Mode CurrentMode => mode;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            SetTarget(target);
        }

        public void SetTarget(BusVehicle vehicle)
        {
            target = vehicle;
            eye = vehicle != null ? FindChild(vehicle.transform, "SurucuGozu") : null;
            if (vehicle != null)
            {
                yaw = vehicle.transform.eulerAngles.y;
                SnapChase();
            }
        }

        public void Toggle() => mode = mode == Mode.Chase ? Mode.Cockpit : Mode.Chase;
        public void SetMode(Mode newMode) => mode = newMode;

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            if (mode == Mode.Cockpit)
                UpdateCockpit();
            else
                UpdateChase(Time.deltaTime);
        }

        private void UpdateCockpit()
        {
            var bus = target.transform;
            Vector3 position = eye != null ? eye.position : bus.TransformPoint(cockpitFallback);
            Quaternion rotation = (eye != null ? eye.rotation : bus.rotation) * Quaternion.Euler(cockpitPitch, 0f, 0f);
            transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = cockpitFov;
            cam.nearClipPlane = 0.05f;
        }

        private void SnapChase()
        {
            transform.position = ChasePosition(out var look);
            transform.LookAt(look);
        }

        private Vector3 ChasePosition(out Vector3 look)
        {
            var bus = target.transform;
            var flat = Quaternion.Euler(0f, yaw, 0f);
            look = bus.position + Vector3.up * lookHeight + flat * Vector3.forward * lookAhead;
            Vector3 desired = bus.position + flat * chaseOffset;

            // Kamera bina veya yokuşun içine girmesin
            Vector3 from = bus.position + Vector3.up * lookHeight;
            Vector3 toCamera = desired - from;
            if (Physics.SphereCast(from, 0.3f, toCamera.normalized, out var hit, toCamera.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(bus))
                desired = from + toCamera.normalized * Mathf.Max(hit.distance - 0.2f, 1f);
            return desired;
        }

        private void UpdateChase(float dt)
        {
            float busYaw = target.transform.eulerAngles.y;
            // Geri giderken kamera arkada kalsın diye otobüsün yönünü izler, hız yönünü değil
            yaw = Mathf.LerpAngle(yaw, busYaw, 1f - Mathf.Exp(-yawSharpness * dt));

            Vector3 desired = ChasePosition(out var look);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * dt));
            transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
            cam.fieldOfView = chaseFov;
            cam.nearClipPlane = 0.3f;
        }
    }
}
