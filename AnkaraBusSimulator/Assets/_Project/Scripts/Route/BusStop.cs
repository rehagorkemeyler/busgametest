using UnityEngine;

namespace AnkaraBus.Route
{
    /// <summary>
    /// Haritadaki bir durak. Otobüs bu noktanın yarıçapı içinde durup kapı açınca durağa yanaşmış sayılır.
    /// </summary>
    public class BusStop : MonoBehaviour
    {
        [SerializeField] private string stopName = "Kızılay";
        [SerializeField] private float radius = 6f;

        public string StopName => stopName;
        public float Radius => radius;

        /// <summary>Yatay (y'siz) uzaklık.</summary>
        public float HorizontalDistance(Vector3 worldPosition)
        {
            Vector3 offset = worldPosition - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 offset = worldPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
