using System;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// WheelCollider tabanlı otobüs sürüşü: tork konvertörlü otomatik şanzıman,
    /// gecikmeli havalı fren, retarder, kapı açıkken durak freni, hıza göre azalan direksiyon.
    /// Girdiler (Throttle, Brake, Steer, Handbrake) BusInput veya dokunmatik arayüz tarafından yazılır.
    /// Tüm sayısal değerler BusDefinition.physics içindedir.
    /// Körüklü otobüste tekerlekler iki Rigidbody'ye dağılır (KorukluOtobus): ön aks yön verir, ön gövdenin arka aksı
    /// pasiftir, çeken aks arka gövdededir; kütle trailerMassRatio ile bölünür.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BusVehicle : MonoBehaviour, IVehicleTelemetry
    {
        public enum GearSelector { Reverse, Neutral, Drive }

        [Serializable]
        public class Wheel
        {
            public WheelCollider collider;
            public Transform visual;
            public bool front;
            public bool left;
            [Tooltip("Ne direksiyon ne çekiş (körüklüde ön gövdenin arka aksı).")]
            public bool passive;
            [NonSerialized] public Quaternion visualOffset;
        }

        [SerializeField] private BusDefinition definition;
        [SerializeField] private Wheel[] wheels;
        [Tooltip("Direksiyon simidi (görsel). Yerel dönme ekseni aşağıda.")]
        [SerializeField] private Transform steeringWheel;
        [SerializeField] private Vector3 steeringWheelAxis = Vector3.forward;
        [Tooltip("Bu hızın altında araç durmuş sayılır (km/s).")]
        [SerializeField] private float stoppedThresholdKmh = 1.5f;

        private const float ConverterLockupRatio = 0.85f;
        private const float EngineRpmRate = 2500f;

        private readonly BusPhysicsSpec fallbackSpec = new BusPhysicsSpec();
        private Rigidbody body;
        private BusDoorController doors;
        private Quaternion steeringWheelRest;
        private float wheelbase;
        private float track;
        private float shiftTimer;
        private float shiftCooldown;
        private float steerAngle;
        private float hillHoldTimer;
        private bool initialized;

        private float throttle;
        private float brake;
        private float steer;

        public BusPhysicsSpec Spec => definition != null ? definition.physics : fallbackSpec;
        public BusDefinition Definition => definition;

        // Girdiler
        public float Throttle { get => throttle; set => throttle = Mathf.Clamp01(value); }
        public float Brake { get => brake; set => brake = Mathf.Clamp01(value); }
        public float Steer { get => steer; set => steer = Mathf.Clamp(value, -1f, 1f); }
        public bool Handbrake { get; set; } = true;
        public GearSelector Selector { get; private set; } = GearSelector.Drive;

        // Durum
        public float ForwardSpeed => Vector3.Dot(body.linearVelocity, transform.forward);
        public float SpeedKmh => body.linearVelocity.magnitude * 3.6f;
        public bool IsStopped => SpeedKmh < stoppedThresholdKmh;
        public float EngineRpm { get; private set; }
        /// <summary>İleri vites numarası (1..n). Geri ve boşta 0.</summary>
        public int Gear { get; private set; } = 1;
        public bool IsShifting => shiftTimer > 0f;
        public float BrakePressure { get; private set; }
        public float RetarderLevel { get; private set; }
        public bool DoorBrakeActive { get; private set; }
        public bool HillHoldActive => hillHoldTimer > 0f;

        public event Action<GearSelector> SelectorChanged;

        private void Awake() => Initialize();

        /// <summary>Awake'te çağrılır. Edit modundaki testler de doğrudan çağırabilir.</summary>
        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;

            body = GetComponent<Rigidbody>();
            doors = GetComponentInChildren<BusDoorController>();
            var spec = Spec;

            body.mass = spec.massKg * (1f - spec.trailerMassRatio);
            body.centerOfMass = spec.centerOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            // körüklüde arka gövde (tekerleklerin bağlı olduğu diğer Rigidbody) kütlenin kalanını taşır
            foreach (var w in wheels)
            {
                var rb = w.collider.attachedRigidbody;
                if (rb != null && rb != body)
                    rb.mass = spec.massKg * Mathf.Max(spec.trailerMassRatio, 0.05f);
            }
            float omega = 2f * Mathf.PI * spec.suspensionFrequency;

            Vector3 frontSum = Vector3.zero, rearSum = Vector3.zero;
            int frontCount = 0, rearCount = 0;
            float frontLeftX = 0f, frontRightX = 0f;

            var configured = new System.Collections.Generic.HashSet<Rigidbody>();
            foreach (var w in wheels)
            {
                var c = w.collider;
                var rb = c.attachedRigidbody != null ? c.attachedRigidbody : body;
                int onBody = 0;
                foreach (var o in wheels)
                    if ((o.collider.attachedRigidbody != null ? o.collider.attachedRigidbody : body) == rb)
                        onBody++;
                float sprungMass = rb.mass / Mathf.Max(1, onBody);
                float spring = sprungMass * omega * omega;
                float damper = 2f * spec.suspensionDamping * Mathf.Sqrt(spring * sprungMass);
                if (configured.Add(rb))
                    c.ConfigureVehicleSubsteps(5f, 12, 15);
                c.mass = w.front ? spec.frontWheelMass : spec.rearWheelMass;
                c.radius = spec.wheelRadius;
                c.suspensionDistance = spec.suspensionDistance;
                c.forceAppPointDistance = 0.2f;
                c.suspensionSpring = new JointSpring { spring = spring, damper = damper, targetPosition = 0.5f };
                c.forwardFriction = Friction(0.4f, 1f, 0.8f, 0.6f, spec.tireGrip);
                c.sidewaysFriction = Friction(0.25f, 1f, 0.6f, 0.75f, spec.tireGrip);
                c.wheelDampingRate = 1f;

                Vector3 local = transform.InverseTransformPoint(c.transform.position);
                if (w.front) { frontSum += local; frontCount++; if (w.left) frontLeftX = local.x; else frontRightX = local.x; }
                else if (rb == body) { rearSum += local; rearCount++; }   // dingil: aynı gövdedeki arka aks

                if (w.visual != null)
                    w.visualOffset = Quaternion.Inverse(c.transform.rotation) * w.visual.rotation;
            }

            wheelbase = frontCount > 0 && rearCount > 0
                ? Mathf.Abs(frontSum.z / frontCount - rearSum.z / rearCount)
                : 5.5f;
            track = Mathf.Max(1f, Mathf.Abs(frontRightX - frontLeftX));

            if (steeringWheel != null)
                steeringWheelRest = steeringWheel.localRotation;

            EngineRpm = spec.idleRpm;
        }

        private static WheelFrictionCurve Friction(float extSlip, float extValue, float asymSlip, float asymValue, float stiffness)
        {
            return new WheelFrictionCurve
            {
                extremumSlip = extSlip,
                extremumValue = extValue,
                asymptoteSlip = asymSlip,
                asymptoteValue = asymValue,
                stiffness = stiffness,
            };
        }

        /// <summary>D/N/R seçimi. Yön değişimi yalnızca araç neredeyse dururken yapılır.</summary>
        public bool SetSelector(GearSelector selector)
        {
            if (selector == Selector)
                return true;
            bool directionChange = selector != GearSelector.Neutral && Selector != GearSelector.Neutral;
            bool movingAgainst = selector == GearSelector.Drive ? ForwardSpeed < -0.8f : selector == GearSelector.Reverse && ForwardSpeed > 0.8f;
            if ((directionChange || movingAgainst) && SpeedKmh > 3f)
                return false;

            Selector = selector;
            Gear = 1;
            shiftTimer = 0f;
            SelectorChanged?.Invoke(selector);
            return true;
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>Bir fizik adımı. Edit modundaki testler Physics.Simulate'ten önce çağırır.</summary>
        public void Step(float dt)
        {
            var spec = Spec;
            float speedKmh = SpeedKmh;

            // Kapılar açıkken: gaz kesilir, fren tutulur
            DoorBrakeActive = spec.doorBrake && doors != null && doors.AnyOpen;
            float throttleIn = DoorBrakeActive ? 0f : throttle;
            float brakeDemand = DoorBrakeActive ? Mathf.Max(brake, 0.4f) : brake;

            // Yokuş kalkış desteği
            if (throttleIn > 0.05f)
                hillHoldTimer = 0f;
            else if (IsStopped && brake > 0.3f)
                hillHoldTimer = spec.hillHoldTime;
            else
                hillHoldTimer = Mathf.Max(0f, hillHoldTimer - dt);
            if (HillHoldActive)
                brakeDemand = Mathf.Max(brakeDemand, 0.4f);

            // Havalı fren gecikmesi
            float rate = brakeDemand > BrakePressure ? spec.brakeApplyTime : spec.brakeReleaseTime;
            BrakePressure = Mathf.MoveTowards(BrakePressure, brakeDemand, dt / Mathf.Max(rate, 0.01f));

            float driveTorque = UpdateDriveline(spec, throttleIn, speedKmh, dt);
            ApplyWheelTorques(spec, driveTorque, speedKmh);
            ApplySteering(spec, speedKmh, dt);
            ApplyAntiRoll(spec);
            ApplyResistance(spec);
        }

        private float UpdateDriveline(BusPhysicsSpec spec, float throttleIn, float speedKmh, float dt)
        {
            shiftTimer = Mathf.Max(0f, shiftTimer - dt);
            shiftCooldown = Mathf.Max(0f, shiftCooldown - dt);

            float wheelRpm = 0f;
            int driven = 0;
            foreach (var w in wheels)
                if (!w.front && !w.passive) { wheelRpm += w.collider.rpm; driven++; }
            wheelRpm = driven > 0 ? wheelRpm / driven : 0f;

            int direction = Selector == GearSelector.Reverse ? -1 : 1;
            float ratio = Selector switch
            {
                GearSelector.Drive => spec.gearRatios[Mathf.Clamp(Gear - 1, 0, spec.gearRatios.Length - 1)],
                GearSelector.Reverse => spec.reverseRatio,
                _ => 0f,
            };
            float turbineRpm = wheelRpm * direction * ratio * spec.finalDrive;
            bool inGear = ratio > 0f;

            // Motor devri: tork konvertörü motoru stall devrine kadar serbest bırakır
            float targetRpm = inGear
                ? Mathf.Max(turbineRpm, Mathf.Lerp(spec.idleRpm, spec.converterStallRpm, throttleIn))
                : Mathf.Lerp(spec.idleRpm, spec.maxRpm * 0.8f, throttleIn);
            EngineRpm = Mathf.Clamp(Mathf.MoveTowards(EngineRpm, targetRpm, EngineRpmRate * dt), spec.idleRpm, spec.maxRpm);

            if (!inGear)
                return 0f;

            if (Selector == GearSelector.Drive)
                AutoShift(spec, throttleIn, turbineRpm);

            float maxTorque = spec.torqueCurve.Evaluate(EngineRpm);
            float engineTorque;
            if (throttleIn > 0.01f)
                engineTorque = maxTorque * throttleIn;
            else if (turbineRpm > spec.idleRpm * 1.05f)
                engineTorque = -spec.engineBrakeTorque;
            else
                engineTorque = maxTorque * spec.creepTorqueRatio;

            // Devir sınırı ve elektronik hız sınırı
            if (EngineRpm >= spec.maxRpm - 30f || speedKmh > spec.maxSpeedKmh)
                engineTorque = Mathf.Min(engineTorque, 0f);

            float speedRatio = EngineRpm > 1f ? Mathf.Clamp01(turbineRpm / EngineRpm) : 0f;
            float multiplication = engineTorque > 0f
                ? Mathf.Lerp(spec.converterStallTorqueRatio, 1f, speedRatio / ConverterLockupRatio)
                : 1f;

            if (IsShifting)
                return 0f;
            return engineTorque * multiplication * ratio * spec.finalDrive * spec.drivetrainEfficiency * direction;
        }

        private void AutoShift(BusPhysicsSpec spec, float throttleIn, float turbineRpm)
        {
            if (IsShifting || shiftCooldown > 0f)
                return;

            int count = spec.gearRatios.Length;
            float upRpm = Mathf.Lerp(spec.upshiftRpmLightThrottle, spec.upshiftRpm, throttleIn);
            float downRpm = Mathf.Lerp(spec.downshiftRpm * 0.7f, spec.downshiftRpm, throttleIn);

            if (Gear < count && turbineRpm > upRpm)
                ChangeGear(spec, Gear + 1);
            else if (Gear > 1)
            {
                float lowerRpm = turbineRpm / spec.gearRatios[Gear - 1] * spec.gearRatios[Gear - 2];
                bool kickdown = throttleIn > 0.9f && lowerRpm < spec.upshiftRpm * 0.85f;
                if (turbineRpm < downRpm || kickdown)
                    ChangeGear(spec, Gear - 1);
            }
        }

        private void ChangeGear(BusPhysicsSpec spec, int gear)
        {
            Gear = gear;
            shiftTimer = spec.shiftTime;
            shiftCooldown = spec.shiftTime + 0.8f;
        }

        private void ApplyWheelTorques(BusPhysicsSpec spec, float driveTorque, float speedKmh)
        {
            const float g = 9.81f;
            float r = spec.wheelRadius;
            float serviceTotal = spec.massKg * spec.serviceBrakeDecel * r * BrakePressure;

            // Retarder: fren pedalıyla birlikte, hız düştükçe etkisi azalır
            RetarderLevel = BrakePressure > 0.05f ? Mathf.Clamp01(speedKmh / 15f) : 0f;
            float retarderTotal = spec.massKg * spec.retarderDecel * r * RetarderLevel * Mathf.Clamp01(BrakePressure / 0.3f);

            float handbrakeTotal = Handbrake
                ? spec.massKg * g * (spec.handbrakeHoldGradePercent / 100f) * 2f * r
                : 0f;

            int frontCount = 0, rearCount = 0, drivenCount = 0;
            foreach (var w in wheels)
            {
                if (w.front) frontCount++; else rearCount++;
                if (!w.front && !w.passive) drivenCount++;
            }

            foreach (var w in wheels)
            {
                float brakeTorque;
                if (w.front)
                {
                    brakeTorque = serviceTotal * spec.frontBrakeBias / Mathf.Max(1, frontCount);
                    w.collider.motorTorque = 0f;
                }
                else
                {
                    brakeTorque = (serviceTotal * (1f - spec.frontBrakeBias) + retarderTotal + handbrakeTotal) / Mathf.Max(1, rearCount);
                    w.collider.motorTorque = w.passive ? 0f : driveTorque / Mathf.Max(1, drivenCount);
                }
                w.collider.brakeTorque = brakeTorque;
            }
        }

        private void ApplySteering(BusPhysicsSpec spec, float speedKmh, float dt)
        {
            float speedFactor = Mathf.Lerp(1f, spec.steerAtReductionSpeed, Mathf.Clamp01(speedKmh / spec.steerReductionSpeedKmh));
            float target = steer * spec.maxSteerAngle * speedFactor;
            steerAngle = Mathf.MoveTowards(steerAngle, target, spec.steerRate * dt);

            // Ackermann: steerAngle iç tekerleğin açısıdır
            float inner = Mathf.Abs(steerAngle);
            float outer = inner;
            if (inner > 0.01f)
            {
                float innerRadius = wheelbase / Mathf.Tan(inner * Mathf.Deg2Rad);
                outer = Mathf.Atan(wheelbase / (innerRadius + track)) * Mathf.Rad2Deg;
            }
            bool right = steerAngle > 0f;
            foreach (var w in wheels)
            {
                if (!w.front)
                    continue;
                bool isInner = right ? !w.left : w.left;
                w.collider.steerAngle = Mathf.Sign(steerAngle) * (isInner ? inner : outer);
            }
        }

        private void ApplyAntiRoll(BusPhysicsSpec spec)
        {
            AntiRollAxle(spec, true, false);
            AntiRollAxle(spec, false, false);
            AntiRollAxle(spec, false, true);
        }

        private void AntiRollAxle(BusPhysicsSpec spec, bool front, bool passive)
        {
            WheelCollider left = null, right = null;
            foreach (var w in wheels)
            {
                if (w.front != front || (!front && w.passive != passive)) continue;
                if (w.left) left = w.collider; else right = w.collider;
            }
            if (left == null || right == null)
                return;

            bool leftGrounded = left.GetGroundHit(out var leftHit);
            bool rightGrounded = right.GetGroundHit(out var rightHit);
            float leftTravel = leftGrounded ? Travel(left, leftHit) : 1f;
            float rightTravel = rightGrounded ? Travel(right, rightHit) : 1f;
            float force = (leftTravel - rightTravel) * spec.antiRollStiffness * spec.suspensionDistance;

            // aksın bağlı olduğu gövdeye (körüklüde arka aks arka gövdede)
            var axleBody = left.attachedRigidbody != null ? left.attachedRigidbody : body;
            if (leftGrounded)
                axleBody.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (rightGrounded)
                axleBody.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        /// <summary>0: tam sıkışmış, 1: tam açık.</summary>
        private static float Travel(WheelCollider c, WheelHit hit)
        {
            return (-c.transform.InverseTransformPoint(hit.point).y - c.radius) / Mathf.Max(c.suspensionDistance, 0.01f);
        }

        private void ApplyResistance(BusPhysicsSpec spec)
        {
            Vector3 v = body.linearVelocity;
            float speed = v.magnitude;
            if (speed < 0.05f)
                return;
            float drag = 0.5f * 1.2f * spec.dragArea * speed * speed;
            float rolling = spec.rollingResistance * spec.massKg * 9.81f * Mathf.Clamp01(speed / 0.5f);
            body.AddForce(-v / speed * (drag + rolling));
        }

        private void LateUpdate() => UpdateVisuals();

        public void UpdateVisuals()
        {
            foreach (var w in wheels)
            {
                if (w.visual == null)
                    continue;
                w.collider.GetWorldPose(out var position, out var rotation);
                w.visual.SetPositionAndRotation(position, rotation * w.visualOffset);
            }

            if (steeringWheel != null)
            {
                float maxAngle = Mathf.Max(Spec.maxSteerAngle, 0.01f);
                float wheelTurn = steerAngle / maxAngle * Spec.steeringWheelLockToLock * 0.5f;
                steeringWheel.localRotation = steeringWheelRest * Quaternion.AngleAxis(wheelTurn, steeringWheelAxis);
            }
        }
    }
}
