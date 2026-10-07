using System;
using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Route
{
    /// <summary>
    /// Otobüsün üzerinde durur; hattaki sıradaki durağı takip eder.
    /// Otobüs durak alanında durup kapıları belirli bir süre açık tutunca durak tamamlanır.
    /// </summary>
    public class RouteTracker : MonoBehaviour
    {
        [SerializeField] private BusRoute route;
        [Tooltip("Durağın tamamlanması için kapıların açık kalması gereken süre (sn).")]
        [SerializeField] private float requiredDwellSeconds = 4f;

        private IVehicleTelemetry telemetry;
        private BusDoorController doors;
        private float dwellTimer;

        public BusRoute Route => route;
        public int NextStopIndex { get; private set; }
        public bool Completed => route == null || NextStopIndex >= route.StopCount;
        public BusStop NextStop => Completed ? null : route.GetStop(NextStopIndex);
        public float DwellProgress => Mathf.Clamp01(dwellTimer / requiredDwellSeconds);

        public event Action<BusStop> StopServed;
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
        }

        private void Update()
        {
            if (Completed)
                return;

            bool serving = NextStop.Contains(transform.position)
                           && telemetry != null && telemetry.IsStopped
                           && doors != null && doors.AnyOpen;

            dwellTimer = serving ? dwellTimer + Time.deltaTime : 0f;
            if (dwellTimer < requiredDwellSeconds)
                return;

            var served = NextStop;
            NextStopIndex++;
            dwellTimer = 0f;
            StopServed?.Invoke(served);
            if (Completed)
                RouteCompleted?.Invoke();
        }
    }
}
