using System;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Bir otobüs modelinin sürüş değerleri. BusDefinition içinde durur;
    /// yeni otobüs için kod değil, yalnızca bu değerler değişir.
    /// Varsayılanlar BMC Procity 12LF'nin Proton dosyalarından alındı.
    /// </summary>
    [Serializable]
    public class BusPhysicsSpec
    {
        [Header("Gövde")]
        [Min(1000f)] public float massKg = 16000f;
        [Tooltip("Yerel ağırlık merkezi. Devrilmemesi için gerçekten biraz alçak tutulur.")]
        public Vector3 centerOfMass = new Vector3(0f, 0.45f, 0.27f);
        [Tooltip("Hava direnci katsayısı × ön alan (m²).")]
        public float dragArea = 0.65f * 7.5f;
        [Tooltip("Yuvarlanma direnci katsayısı.")]
        public float rollingResistance = 0.008f;

        [Header("Motor")]
        public float idleRpm = 600f;
        public float maxRpm = 2300f;
        [Tooltip("X: devir, Y: tork (Nm).")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(600f, 650f),
            new Keyframe(1000f, 920f),
            new Keyframe(1500f, 1000f),
            new Keyframe(2000f, 880f),
            new Keyframe(2300f, 720f));
        [Tooltip("Gaz verilmeden viteste ilerleme torku, maks. torkun oranı.")]
        [Range(0f, 0.3f)] public float creepTorqueRatio = 0.06f;
        [Tooltip("Gaz bırakınca motor freni torku (Nm, motor tarafında).")]
        public float engineBrakeTorque = 120f;

        [Header("Şanzıman (otomatik)")]
        public float[] gearRatios = { 3.43f, 2.01f, 1.42f, 1.00f, 0.83f, 0.59f };
        public float reverseRatio = 4.84f;
        public float finalDrive = 6.5f;
        [Range(0.5f, 1f)] public float drivetrainEfficiency = 0.9f;
        [Tooltip("Tork konvertörü: araç dururken tam gazda motorun çıkabildiği devir.")]
        public float converterStallRpm = 1600f;
        [Tooltip("Tork konvertörünün dururken torku kaç kat artırdığı.")]
        public float converterStallTorqueRatio = 2.2f;
        [Tooltip("Tam gazda vites büyütme devri; hafif gazda daha erken büyütür.")]
        public float upshiftRpm = 2100f;
        public float upshiftRpmLightThrottle = 1300f;
        public float downshiftRpm = 1050f;
        [Tooltip("Vites değişimindeki tork kesintisi (sn).")]
        public float shiftTime = 0.45f;
        [Tooltip("Elektronik hız sınırı (km/s).")]
        public float maxSpeedKmh = 80f;

        [Header("Frenler")]
        [Tooltip("Tam frende hedeflenen yavaşlama (m/s²).")]
        public float serviceBrakeDecel = 5.5f;
        [Range(0f, 1f)] public float frontBrakeBias = 0.55f;
        [Tooltip("Havalı frenin dolma/boşalma süresi (sn).")]
        public float brakeApplyTime = 0.35f;
        public float brakeReleaseTime = 0.5f;
        [Tooltip("El freni, bu eğimin iki katında tutacak kadar güçlü (%).")]
        public float handbrakeHoldGradePercent = 18f;
        [Tooltip("Retarder (hidrolik yavaşlatıcı) maks. yavaşlatması (m/s²). Fren pedalının ilk kısmında devreye girer.")]
        public float retarderDecel = 1.2f;
        [Tooltip("Yokuş kalkış desteği: durunca fren bırakıldıktan sonra frenlerin tutma süresi (sn). Gaz verince bırakır.")]
        public float hillHoldTime = 1.5f;
        [Tooltip("Kapılar açıkken otomatik durak freni.")]
        public bool doorBrake = true;

        [Header("Direksiyon")]
        [Tooltip("Ön tekerleğin maks. dönme açısı (iç tekerlek).")]
        public float maxSteerAngle = 40f;
        [Tooltip("Bu hızda direksiyon açısı maks. açının aşağıdaki oranına iner.")]
        public float steerReductionSpeedKmh = 60f;
        [Range(0.1f, 1f)] public float steerAtReductionSpeed = 0.3f;
        [Tooltip("Tekerlek açısının değişme hızı (derece/sn).")]
        public float steerRate = 45f;
        [Tooltip("Direksiyon simidinin uçtan uca toplam dönüşü (derece).")]
        public float steeringWheelLockToLock = 900f;

        [Header("Tekerlek ve süspansiyon")]
        public float wheelRadius = 0.48f;
        public float frontWheelMass = 120f;
        public float rearWheelMass = 240f;
        public float suspensionDistance = 0.25f;
        [Tooltip("Havalı süspansiyon doğal frekansı (Hz). Yay sertliği kütleden hesaplanır.")]
        public float suspensionFrequency = 1.4f;
        [Range(0.1f, 1f)] public float suspensionDamping = 0.45f;
        [Tooltip("Viraj yatmasına karşı her aks için yay (N/m).")]
        public float antiRollStiffness = 120000f;
        [Range(0.3f, 2f)] public float tireGrip = 1f;
    }
}
