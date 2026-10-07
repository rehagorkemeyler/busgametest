using UnityEngine;

namespace AnkaraBus.Route
{
    /// <summary>
    /// Bir EGO hattı: hat numarası ve sıralı durak listesi.
    /// </summary>
    public class BusRoute : MonoBehaviour
    {
        [SerializeField] private string lineNumber = "000";
        [SerializeField] private string lineName = "Örnek Hat";
        [SerializeField] private BusStop[] stops;

        public string LineNumber => lineNumber;
        public string LineName => lineName;
        public int StopCount => stops.Length;
        public BusStop GetStop(int index) => stops[index];

        private void OnDrawGizmos()
        {
            if (stops == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < stops.Length; i++)
                if (stops[i - 1] && stops[i])
                    Gizmos.DrawLine(stops[i - 1].transform.position, stops[i].transform.position);
        }
    }
}
