using System;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Route
{
    /// <summary>
    /// Otobüsün üzerinde durur; hattaki sıradaki durağı takip eder.
    /// Otobüs durak alanında durup kapıları belirli bir süre açık tutunca ya da yolcu iniş-binişi bitince durak tamamlanır.
    /// </summary>
    public class RouteTracker : MonoBehaviour
    {
        [SerializeField] private BusRoute route;
        [Tooltip("Durağın tamamlanması için kapıların açık kalması gereken süre (sn).")]
        [SerializeField] private float requiredDwellSeconds = 4f;
        [Tooltip("Durağın yanından geçip bu kadar metre uzaklaşınca durak atlanmış sayılır.")]
        [SerializeField] private float skipDistance = 25f;

        private IVehicleTelemetry telemetry;
        private BusDoorController doors;
        private float dwellTimer;
        private bool nearNextStop;
        private AnkaraBus.Passengers.BusPassengers passengers;

        public BusRoute Route => route;
        public int NextStopIndex { get; private set; }
        public bool Completed => route == null || NextStopIndex >= route.StopCount;
        public BusStop NextStop => Completed ? null : route.GetStop(NextStopIndex);
        public float DwellProgress => Mathf.Clamp01(dwellTimer / requiredDwellSeconds);

        public event Action<BusStop> StopServed;
        /// <summary>Otobüs durmadan durağı geçti; sıradaki durağa geçilir.</summary>
        public event Action<BusStop> StopSkipped;
        public event Action RouteCompleted;

        private void Awake()
        {
            telemetry = GetComponent<IVehicleTelemetry>();
            doors = GetComponentInChildren<BusDoorController>();
        }

        public void AssignRoute(BusRoute newRoute)
        {
            route = newRoute;
            NextStopIndex = 0;
            dwellTimer = 0f;
            nearNextStop = false;
        }

        private void Update()
        {
            if (Completed)
                return;

            bool serving = NextStop.Contains(transform.position)
                           && telemetry != null && telemetry.IsStopped
                           && doors != null && doors.AnyOpen;
            // yolcu inip binerken durak tamamlanmaz (BusPassengers sahnede yolcu sistemi varsa eklenir)
            if (passengers == null)
                passengers = GetComponent<AnkaraBus.Passengers.BusPassengers>();
            bool exchanging = passengers != null && passengers.IsBusy;
            if (exchanging)
            {
                dwellTimer = Mathf.Min(dwellTimer, requiredDwellSeconds * 0.5f);
                return;
            }
            // yolcu indirip bindirdiyse durak tamamlanmıştır; kapıyı hemen kapatıp kalkan otobüs "atladı" sayılmasın
            if (passengers != null && passengers.Served(NextStopIndex))
            {
                Advance(true);
                return;
            }

            // durağın yanına gelip (karşı şeritten geçse bile) uzaklaşan otobüs durağı atlamıştır
            float distance = NextStop.HorizontalDistance(transform.position);
            if (distance <= NextStop.Radius + 8f)
                nearNextStop = true;
            else if (nearNextStop && distance > NextStop.Radius + skipDistance)
            {
                Advance(false);
                return;
            }

            dwellTimer = serving ? dwellTimer + Time.deltaTime : 0f;
            if (dwellTimer < requiredDwellSeconds)
                return;
            Advance(true);
        }

        private void Advance(bool served)
        {
            var stop = NextStop;
            NextStopIndex++;
            dwellTimer = 0f;
            nearNextStop = false;
            if (served)
                StopServed?.Invoke(stop);
            else
                StopSkipped?.Invoke(stop);
            if (Completed)
                RouteCompleted?.Invoke();
        }
    }
}
