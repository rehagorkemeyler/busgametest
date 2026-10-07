using System;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüsün kapı gruplarını yönetir. Mobil arayüzdeki kapı butonları
    /// Button.onClick üzerinden ToggleDoor(index) veya ToggleAll() çağırır.
    /// </summary>
    public class BusDoorController : MonoBehaviour
    {
        [Serializable]
        public class DoorGroup
        {
            public string name = "Ön kapı";
            public BusDoor[] leaves;
            [NonSerialized] public bool open;
        }

        [SerializeField] private DoorGroup[] doors;
        [Tooltip("Bu hızın üstünde kapılar açılmaz (km/s).")]
        [SerializeField] private float maxOpenSpeedKmh = 3f;

        private IVehicleTelemetry telemetry;

        public event Action<int, bool> DoorChanged;

        public int DoorCount => doors.Length;

        public bool AnyOpen
        {
            get
            {
                foreach (var door in doors)
                    if (door.open) return true;
                return false;
            }
        }

        private void Awake()
        {
            telemetry = GetComponentInParent<IVehicleTelemetry>();
        }

        public void ToggleDoor(int index) => SetDoor(index, !doors[index].open);

        public void ToggleAll()
        {
            bool open = !AnyOpen;
            for (int i = 0; i < doors.Length; i++)
                SetDoor(i, open);
        }

        public void SetDoor(int index, bool open)
        {
            if (index < 0 || index >= doors.Length)
                return;
            if (open && telemetry != null && telemetry.SpeedKmh > maxOpenSpeedKmh)
                return;

            var door = doors[index];
            if (door.open == open)
                return;

            door.open = open;
            foreach (var leaf in door.leaves)
                leaf.SetOpen(open);
            DoorChanged?.Invoke(index, open);
        }
    }
}
