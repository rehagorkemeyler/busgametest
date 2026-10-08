using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüs kamerası. Görünümler (KAMERA düğmesi ya da C tuşu ile sırayla):
    /// <list type="bullet">
    /// <item>Dış: arkadan takip. Sürükleyerek otobüsün çevresinde dönülür; otobüs giderken bir süre sonra arkaya döner.</item>
    /// <item>Kokpit: sürücü gözü ("SurucuGozu"). Sürükleyerek etrafa bakılır, bırakınca yola döner.</item>
    /// <item>Yolcu: yolcu salonu, sürükleyerek serbestçe bakılır.</item>
    /// <item>Kapı: sağ ön köşeden yan tarafa (kapılar ve kaldırım), durağa yanaşırken.</item>
    /// <item>Serbest: dış görünüm gibi ama bırakılan açıda kalır (ör. yandan izleyerek sürmek).</item>
    /// </list>
    /// Sürükleme yalnızca arayüze (direksiyon, pedal, düğme) değmeyen parmakla olur; iki parmak ya da fare
    /// tekerleği yakınlaştırır. Çift dokunuş görünümü sıfırlar.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BusCameraRig : MonoBehaviour
    {
        public enum Mode { Chase, Cockpit, Interior, Door, Free }

        public static readonly string[] ModeNames = { "DIŞ", "KOKPİT", "YOLCU", "KAPI", "SERBEST" };

        [SerializeField] private BusVehicle target;
        [SerializeField] private Mode mode = Mode.Chase;

        [Header("Dış takip")]
        [SerializeField] private float chaseDistance = 14.3f;
        [Tooltip("Varsayılan bakış açısı (aşağı, derece).")]
        [SerializeField] private float chasePitch = 11f;
        [SerializeField] private float lookHeight = 1.8f;
        [SerializeField] private float lookAhead = 4f;
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private float yawSharpness = 2.5f;
        [SerializeField] private float chaseFov = 55f;
        [SerializeField] private Vector2 distanceRange = new Vector2(7f, 40f);
        [SerializeField] private Vector2 orbitPitchRange = new Vector2(-2f, 70f);
        [Tooltip("Otobüs giderken, bırakıldıktan bu kadar sn sonra kamera arkaya döner.")]
        [SerializeField] private float returnDelay = 2f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Kokpit")]
        [SerializeField] private float cockpitFov = 65f;
        [Tooltip("SurucuGozu yoksa kullanılan yerel konum.")]
        [SerializeField] private Vector3 cockpitFallback = new Vector3(-0.62f, 2.05f, 4.45f);
        [Tooltip("Sürücünün yola bakış açısı (aşağı, derece).")]
        [SerializeField] private float cockpitPitch = 8f;

        [Header("Yolcu salonu ve kapı")]
        [Tooltip("Ayakta yolcunun göz hizası (yerel).")]
        [SerializeField] private Vector3 interiorPosition = new Vector3(0.45f, 2.0f, -1.2f);
        [SerializeField] private float interiorFov = 70f;
        [Tooltip("Sağ ön köşe, gövdeden ~2 m dışarıda (yerel): kapılar, ön tekerlek, bordür ve kaldırım görünür.")]
        [SerializeField] private Vector3 doorPosition = new Vector3(3.4f, 2.2f, 6.2f);
        [Tooltip("Kapı kamerasının baktığı nokta (yerel): sağ yan ve kaldırım.")]
        [SerializeField] private Vector3 doorLookAt = new Vector3(1.27f, 0.9f, -1f);
        [SerializeField] private float doorFov = 60f;

        [Header("Dokunmatik")]
        [Tooltip("Ekran yüksekliği kadar sürükleme kaç derece döndürür.")]
        [SerializeField] private float dragDegrees = 200f;
        [SerializeField] private Vector2 lookYawRange = new Vector2(-160f, 160f);
        [SerializeField] private Vector2 lookPitchRange = new Vector2(-40f, 55f);

        private Camera cam;
        private Transform eye;
        private float yaw;

        // dış ve serbest görünüm
        private float orbitYaw, orbitPitch, distance;
        // içeriden bakış (kokpit, yolcu, kapı)
        private float lookYaw, lookPitch, insideFov;
        private float idleTime = 99f;
        private float movingTime;

        // girdi
        private readonly List<int> freeTouches = new List<int>();
        private readonly List<Vector2> touchPositions = new List<Vector2>(4);
        private readonly Dictionary<int, Vector2> touchStart = new Dictionary<int, Vector2>();
        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private bool mouseDragging;
        private float lastPinch = -1f;
        private float lastTapTime = -1f;

        public Mode CurrentMode => mode;
        public string ModeName => ModeNames[(int)mode];
        /// <summary>Kamera otobüsün içinde mi (sesler için)?</summary>
        public bool IsInside => mode == Mode.Cockpit || mode == Mode.Interior;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            orbitPitch = chasePitch;
            distance = chaseDistance;
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

        /// <summary>Sıradaki görünüme geçer.</summary>
        public void Toggle() => SetMode((Mode)(((int)mode + 1) % ModeNames.Length));

        public void SetMode(Mode newMode)
        {
            bool wasOutside = mode == Mode.Chase || mode == Mode.Free;
            mode = newMode;
            ResetLook();
            if (mode == Mode.Chase && target != null && !wasOutside)
                SnapChase();
        }

        /// <summary>Bakış açısını ve yakınlaştırmayı varsayılana döndürür.</summary>
        public void ResetLook()
        {
            lookYaw = lookPitch = 0f;
            insideFov = 0f;
            orbitYaw = 0f;
            orbitPitch = chasePitch;
            distance = chaseDistance;
        }

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
            float dt = Time.deltaTime;

            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame)
                Toggle();

            ReadInput(out var drag, out float zoom);
            if (drag != Vector2.zero)
                idleTime = 0f;
            else
                idleTime += dt;
            float degPerPixel = dragDegrees / Mathf.Max(Screen.height, 1);
            // dururken fizik titreşimi hızı bir anlık 3 km/s'yi geçebilir; "gidiyor" sayılması için yarım saniye sürmeli
            movingTime = target.SpeedKmh > 3f ? movingTime + dt : 0f;

            switch (mode)
            {
                case Mode.Chase:
                case Mode.Free:
                    orbitYaw = Mathf.Repeat(orbitYaw + drag.x * degPerPixel + 180f, 360f) - 180f;
                    orbitPitch = Mathf.Clamp(orbitPitch - drag.y * degPerPixel, orbitPitchRange.x, orbitPitchRange.y);
                    distance = Mathf.Clamp(distance * (1f - zoom), distanceRange.x, distanceRange.y);
                    // dış takip: otobüs giderken ve parmak bırakılmışken kamera arkaya döner
                    if (mode == Mode.Chase && idleTime > returnDelay && movingTime > 0.5f)
                    {
                        float k = 1f - Mathf.Exp(-1.5f * dt);
                        orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, k);
                        orbitPitch = Mathf.Lerp(orbitPitch, chasePitch, k);
                    }
                    UpdateOutside(dt);
                    break;
                default:
                    lookYaw = Mathf.Clamp(lookYaw + drag.x * degPerPixel, lookYawRange.x, lookYawRange.y);
                    lookPitch = Mathf.Clamp(lookPitch - drag.y * degPerPixel, lookPitchRange.x, lookPitchRange.y);
                    float baseFov = mode == Mode.Cockpit ? cockpitFov : mode == Mode.Interior ? interiorFov : doorFov;
                    if (insideFov <= 0f)
                        insideFov = baseFov;
                    insideFov = Mathf.Clamp(insideFov * (1f - zoom), 35f, 85f);
                    // kokpit ve kapı: bırakınca yola/kapıya geri bak
                    if (mode != Mode.Interior && idleTime > returnDelay)
                    {
                        float k = 1f - Mathf.Exp(-2f * dt);
                        lookYaw = Mathf.Lerp(lookYaw, 0f, k);
                        lookPitch = Mathf.Lerp(lookPitch, 0f, k);
                    }
                    UpdateInside();
                    break;
            }
        }

        private void UpdateInside()
        {
            var bus = target.transform;
            Vector3 position;
            Quaternion baseRotation;
            switch (mode)
            {
                case Mode.Cockpit:
                    position = eye != null ? eye.position : bus.TransformPoint(cockpitFallback);
                    baseRotation = (eye != null ? eye.rotation : bus.rotation);
                    baseRotation *= Quaternion.Euler(0f, lookYaw, 0f) * Quaternion.Euler(cockpitPitch + lookPitch, 0f, 0f);
                    break;
                case Mode.Interior:
                    position = bus.TransformPoint(interiorPosition);
                    baseRotation = bus.rotation * Quaternion.Euler(0f, lookYaw, 0f) * Quaternion.Euler(5f + lookPitch, 0f, 0f);
                    break;
                default: // Door
                    position = bus.TransformPoint(doorPosition);
                    var look = Quaternion.LookRotation(bus.TransformPoint(doorLookAt) - position, bus.up);
                    baseRotation = Quaternion.AngleAxis(lookYaw, bus.up) * look * Quaternion.Euler(lookPitch, 0f, 0f);
                    break;
            }
            transform.SetPositionAndRotation(position, baseRotation);
            cam.fieldOfView = insideFov;
            cam.nearClipPlane = mode == Mode.Door ? 0.2f : 0.05f;
        }

        private void SnapChase()
        {
            transform.position = OutsidePosition(out var look);
            transform.LookAt(look);
        }

        private Vector3 OutsidePosition(out Vector3 look)
        {
            var bus = target.transform;
            Vector3 pivot = bus.position + Vector3.up * lookHeight;
            float totalYaw = yaw + orbitYaw;
            // arkadan bakarken biraz ileri bakılır; yana/öne dönünce otobüsün ortasına
            float ahead = lookAhead * Mathf.Clamp01(1f - Mathf.Abs(orbitYaw) / 45f);
            look = pivot + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * ahead;
            Vector3 desired = pivot + Quaternion.Euler(orbitPitch, totalYaw, 0f) * new Vector3(0f, 0f, -distance);

            // Kamera bina, yokuş ya da zeminin içine girmesin
            Vector3 toCamera = desired - pivot;
            if (Physics.SphereCast(pivot, 0.3f, toCamera.normalized, out var hit, toCamera.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(bus))
                desired = pivot + toCamera.normalized * Mathf.Max(hit.distance - 0.2f, 1f);
            return desired;
        }

        private void UpdateOutside(float dt)
        {
            float busYaw = target.transform.eulerAngles.y;
            // Geri giderken kamera arkada kalsın diye otobüsün yönünü izler, hız yönünü değil
            yaw = Mathf.LerpAngle(yaw, busYaw, 1f - Mathf.Exp(-yawSharpness * dt));

            Vector3 desired = OutsidePosition(out var look);
            // sürüklerken anında, sürüşte yumuşak takip
            float sharp = idleTime < 0.2f ? 30f : followSharpness;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-sharp * dt));
            transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
            cam.fieldOfView = chaseFov;
            cam.nearClipPlane = 0.3f;
        }

        /// <summary>
        /// Arayüze değmeyen parmakların/farenin sürüklemesi (piksel) ve yakınlaştırma (+ yakın, - uzak; oran).
        /// </summary>
        private void ReadInput(out Vector2 drag, out float zoom)
        {
            drag = Vector2.zero;
            zoom = 0f;

            var screen = Touchscreen.current;
            bool anyTouch = false;
            if (screen != null)
            {
                var positions = touchPositions;
                positions.Clear();
                foreach (var touch in screen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    if (!touch.press.isPressed)
                    {
                        if (freeTouches.Remove(id))
                            CheckDoubleTap(touchStart.TryGetValue(id, out var start) ? start : Vector2.zero, touch.position.ReadValue());
                        touchStart.Remove(id);
                        continue;
                    }
                    anyTouch = true;
                    Vector2 position = touch.position.ReadValue();
                    if (touch.press.wasPressedThisFrame && !OverUI(position) && !freeTouches.Contains(id))
                    {
                        freeTouches.Add(id);
                        touchStart[id] = position;
                    }
                    if (!freeTouches.Contains(id))
                        continue;
                    positions.Add(position);
                    if (freeTouches.Count == 1)
                        drag += touch.delta.ReadValue();
                }
                if (positions.Count >= 2)
                {
                    float pinch = Vector2.Distance(positions[0], positions[1]);
                    if (lastPinch > 0f)
                        zoom = (pinch - lastPinch) / Mathf.Max(Screen.height, 1) * 1.5f;
                    lastPinch = pinch;
                }
                else
                    lastPinch = -1f;
            }

            var mouse = Mouse.current;
            if (!anyTouch && mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    mouseDragging = !OverUI(mouse.position.ReadValue());
                if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
                    mouseDragging = false;
                if (mouseDragging)
                    drag += mouse.delta.ReadValue();
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f && !OverUI(mouse.position.ReadValue()))
                    zoom += Mathf.Sign(scroll) * 0.1f;
                if (mouse.middleButton.wasPressedThisFrame)
                    ResetLook();
            }
        }

        /// <summary>Kısa ve hareketsiz iki dokunuş art arda gelirse görünüm sıfırlanır.</summary>
        private void CheckDoubleTap(Vector2 start, Vector2 end)
        {
            if ((end - start).sqrMagnitude > 30f * 30f)
                return;
            if (Time.unscaledTime - lastTapTime < 0.3f)
            {
                ResetLook();
                lastTapTime = -1f;
            }
            else
                lastTapTime = Time.unscaledTime;
        }

        private static bool OverUI(Vector2 screenPosition)
        {
            var events = EventSystem.current;
            if (events == null)
                return false;
            var data = new PointerEventData(events) { position = screenPosition };
            uiHits.Clear();
            events.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }
    }
}
