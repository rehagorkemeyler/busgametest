using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Otobüsün yolcu sayısı ve duraktaki iniş-biniş. Otobüs sıradaki durakta durup kapıları açınca:
    /// önce inenler arka kapılardan iner, sonra bekleyenler ön kapıdan biner (Türkiye usulü).
    /// İniş-biniş sürerken RouteTracker durağı tamamlamaz (IsBusy).
    /// PassengerManager oyuncunun otobüsüne bunu kendisi ekler.
    /// </summary>
    public class BusPassengers : MonoBehaviour
    {
        [SerializeField] private int capacity = 90;
        [SerializeField] private int onboard;
        [Tooltip("İki yolcunun kapıya ulaşması arasındaki süre (sn).")]
        [SerializeField] private float interval = 0.7f;
        [Tooltip("Bir durakta iniş-binişin en uzun süresi (sn); sonra yarım kalanlar bırakılır.")]
        [SerializeField] private float timeout = 25f;

        private RouteTracker tracker;
        private IVehicleTelemetry telemetry;
        private BusDoorController doors;
        private int servedIndex = -1;
        private int walking;

        public int Onboard => onboard;
        public int Capacity => capacity;
        public bool IsBusy { get; private set; }

        public event System.Action<int> OnboardChanged;

        private void Awake()
        {
            tracker = GetComponent<RouteTracker>();
            telemetry = GetComponent<IVehicleTelemetry>();
            doors = GetComponentInChildren<BusDoorController>();
            var vehicle = GetComponent<BusVehicle>();
            if (vehicle != null && vehicle.Definition != null)
                capacity = vehicle.Definition.passengerCapacity;
        }

        private void Update()
        {
            if (IsBusy || tracker == null || doors == null || PassengerManager.Instance == null || tracker.Completed)
                return;
            var stop = tracker.NextStop;
            if (stop == null || servedIndex == tracker.NextStopIndex)
                return;
            if (stop.Contains(transform.position) && telemetry != null && telemetry.IsStopped && doors.AnyOpen)
                StartCoroutine(Exchange(stop, tracker.NextStopIndex == tracker.Route.StopCount - 1));
        }

        private IEnumerator Exchange(BusStop stop, bool lastStop)
        {
            IsBusy = true;
            servedIndex = tracker.NextStopIndex;
            var manager = PassengerManager.Instance;
            float deadline = Time.time + timeout;

            // İnenler: arka kapılardan (yoksa ön kapıdan)
            int alight = lastStop ? onboard : Random.Range(0, Mathf.Min(onboard, 10) + 1);
            for (int i = 0; i < alight && Time.time < deadline; i++)
            {
                int door = PickExitDoor();
                if (door < 0)
                    break;
                var p = manager.Get(DoorOutside(door), Quaternion.LookRotation(transform.right));
                if (p == null)
                    break;
                SetOnboard(onboard - 1);
                walking++;
                var away = manager.SidewalkPoint(stop, Random.Range(-6f, 6f)) + transform.right * Random.Range(1f, 3f);
                p.WalkTo(away, () => { walking--; manager.Release(p); });
                yield return new WaitForSeconds(interval);
            }

            // Binenler: ön kapıdan. Son durakta (hat sonu) kimse binmez.
            var queue = lastStop ? new List<Passenger>() : new List<Passenger>(manager.WaitingAt(stop));
            queue.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
            foreach (var p in queue)
            {
                if (onboard + walking >= capacity || Time.time >= deadline || !doors.IsDoorOpen(0))
                    break;
                manager.RemoveWaiting(stop, p);
                walking++;
                var passenger = p;
                passenger.WalkTo(DoorOutside(0), () =>
                {
                    walking--;
                    SetOnboard(onboard + 1);
                    manager.Release(passenger);
                });
                yield return new WaitForSeconds(interval);
            }

            while (walking > 0 && Time.time < deadline && doors.AnyOpen)
                yield return null;
            IsBusy = false;
        }

        private int PickExitDoor()
        {
            var open = new List<int>();
            for (int i = 1; i < doors.DoorCount; i++)
                if (doors.IsDoorOpen(i))
                    open.Add(i);
            if (open.Count > 0)
                return open[Random.Range(0, open.Count)];
            return doors.IsDoorOpen(0) ? 0 : -1;
        }

        /// <summary>Kapının dışında, yerdeki nokta (kapılar otobüsün sağında).</summary>
        private Vector3 DoorOutside(int door)
        {
            var p = doors.DoorCenter(door) + transform.right * 0.8f;
            p.y = transform.position.y;
            return p;
        }

        private float Distance(Passenger p) => (p.transform.position - DoorOutside(0)).sqrMagnitude;

        private void SetOnboard(int value)
        {
            onboard = Mathf.Clamp(value, 0, capacity);
            OnboardChanged?.Invoke(onboard);
        }
    }
}
