using UnityEngine;
using UnityEngine.InputSystem;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Klavye ve dokunmatik girdiyi birleştirip BusVehicle'a yazar.
    /// Dokunmatik arayüz Touch* özelliklerini ve buton metotlarını kullanır.
    /// Klavye: W/S veya oklar gaz/fren, A/D direksiyon, Space el freni,
    /// 1/2/3 = D/N/R, K kapılar.
    /// </summary>
    [RequireComponent(typeof(BusVehicle))]
    public class BusInput : MonoBehaviour
    {
        [Tooltip("Klavyede direksiyonun merkeze dönme ve sapma hızı (1/sn).")]
        [SerializeField] private float keyboardSteerSpeed = 1.6f;
        [Tooltip("Gaz verilince el freni kendiliğinden bırakılır.")]
        [SerializeField] private bool autoReleaseHandbrake = true;

        private BusVehicle vehicle;
        private BusDoorController doors;
        private float keyboardSteer;

        public float TouchThrottle { get; set; }
        public float TouchBrake { get; set; }
        /// <summary>Sanal direksiyon: -1 sol, +1 sağ. Dokunulmuyorsa null.</summary>
        public float? TouchSteer { get; set; }
        /// <summary>Otomatik sürüş için gaz (performans ölçümü, testler).</summary>
        public float AutoThrottle { get; set; }

        public BusVehicle Vehicle => vehicle;

        private void Awake()
        {
            vehicle = GetComponent<BusVehicle>();
            doors = GetComponentInChildren<BusDoorController>();
        }

        private void Update()
        {
            float throttle = Mathf.Max(TouchThrottle, AutoThrottle);
            float brake = TouchBrake;
            float steerKeys = 0f;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerKeys -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerKeys += 1f;

                if (kb.spaceKey.wasPressedThisFrame) ToggleHandbrake();
                if (kb.digit1Key.wasPressedThisFrame) SelectDrive();
                if (kb.digit2Key.wasPressedThisFrame) SelectNeutral();
                if (kb.digit3Key.wasPressedThisFrame) SelectReverse();
                if (kb.kKey.wasPressedThisFrame) ToggleDoors();
            }

            keyboardSteer = Mathf.MoveTowards(keyboardSteer, steerKeys, keyboardSteerSpeed * Time.deltaTime);

            vehicle.Throttle = throttle;
            vehicle.Brake = brake;
            vehicle.Steer = TouchSteer ?? keyboardSteer;

            if (autoReleaseHandbrake && vehicle.Handbrake && throttle > 0.1f && brake < 0.1f)
                vehicle.Handbrake = false;
        }

        // Dokunmatik arayüz butonları (Button.onClick)
        public void ToggleHandbrake() => vehicle.Handbrake = !vehicle.Handbrake;
        public void SelectDrive() => vehicle.SetSelector(BusVehicle.GearSelector.Drive);
        public void SelectNeutral() => vehicle.SetSelector(BusVehicle.GearSelector.Neutral);
        public void SelectReverse() => vehicle.SetSelector(BusVehicle.GearSelector.Reverse);

        public void ToggleDoors()
        {
            if (doors != null)
                doors.ToggleAll();
        }
    }
}
