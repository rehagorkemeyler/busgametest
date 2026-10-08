using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using TMPro;
using UnityEngine;

namespace AnkaraBus.UI
{
    /// <summary>
    /// Basit sürüş göstergesi: hız, hat ve sıradaki durak.
    /// </summary>
    public class BusHud : MonoBehaviour
    {
        [SerializeField] private RouteTracker tracker;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text lineText;
        [SerializeField] private TMP_Text nextStopText;
        [Tooltip("İsteğe bağlı: yolcu sayısı (ör. 'Yolcu 24/90').")]
        [SerializeField] private TMP_Text passengerText;

        private IVehicleTelemetry telemetry;
        private AnkaraBus.Passengers.BusPassengers passengers;

        private void Start()
        {
            telemetry = tracker.GetComponent<IVehicleTelemetry>();
        }

        private void Update()
        {
            if (telemetry != null)
                speedText.text = $"{Mathf.RoundToInt(telemetry.SpeedKmh)} km/s";

            if (tracker.Route != null)
                lineText.text = $"{tracker.Route.LineNumber}  {tracker.Route.LineName}";

            if (passengerText != null)
            {
                if (passengers == null)
                    passengers = tracker.GetComponent<AnkaraBus.Passengers.BusPassengers>();
                passengerText.text = passengers != null ? $"Yolcu {passengers.Onboard}/{passengers.Capacity}" : "";
            }

            nextStopText.text = tracker.Completed
                ? "Hat tamamlandı"
                : $"Sıradaki durak: {tracker.NextStop.StopName}";
        }
    }
}
