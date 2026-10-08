using System.Collections.Generic;
using AnkaraBus.Route;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Sahnedeki tüm duraklarda bekleyen yolcuları yönetir. Otobüs bir durağa yaklaşınca o durağın yolcularını
    /// kaldırımda oluşturur, uzaklaşınca kaldırır (havuz). Oyuncunun otobüsüne yoksa BusPassengers ekler.
    /// Harita kurucusu (Ankara Bus > Hat 1 Haritasını Kur) bu bileşeni modellerle birlikte kurar.
    /// </summary>
    public class PassengerManager : MonoBehaviour
    {
        [SerializeField] private GameObject[] models;
        [Tooltip("Yolcu modellerine uygulanacak ortak palet materyali.")]
        [SerializeField] private Material paletteMaterial;
        [Tooltip("Durak başına bekleyen yolcu sayısı (en az, en çok).")]
        [SerializeField] private Vector2Int waitingRange = new Vector2Int(2, 8);
        [Tooltip("Otobüs bu mesafeye girince durağın yolcuları görünür (m).")]
        [SerializeField] private float activeRadius = 160f;
        [Tooltip("Boşsa 'Player' etiketli obje, o da yoksa sahnedeki BusVehicle aranır.")]
        [SerializeField] private Transform player;

        private class StopState
        {
            public BusStop stop;
            public Vector3 lineOrigin;
            public Vector3 lineDirection;
            public Vector3 roadSide;
            public readonly List<Passenger> waiting = new List<Passenger>();
            public bool spawned;
        }

        private readonly List<StopState> stops = new List<StopState>();
        private readonly Stack<Passenger> pool = new Stack<Passenger>();
        private float timer;

        public static PassengerManager Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (player == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }
            if (player == null)
            {
                var bus = FindAnyObjectByType<AnkaraBus.Vehicle.BusVehicle>();
                if (bus != null)
                    player = bus.transform;
            }
            if (player != null && player.GetComponent<BusPassengers>() == null)
                player.gameObject.AddComponent<BusPassengers>();

            foreach (var stop in FindObjectsByType<BusStop>())
                stops.Add(Measure(stop));
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f || player == null)
                return;
            timer = 0.5f;
            foreach (var s in stops)
            {
                bool near = (s.stop.transform.position - player.position).sqrMagnitude < activeRadius * activeRadius;
                if (near && !s.spawned)
                    SpawnWaiting(s);
                else if (!near && s.spawned)
                    ClearWaiting(s);
            }
        }

        /// <summary>Bu durakta bekleyen yolcular (değiştirmeyin; RemoveWaiting kullanın).</summary>
        public IReadOnlyList<Passenger> WaitingAt(BusStop stop)
        {
            var s = stops.Find(x => x.stop == stop);
            return s != null ? s.waiting : (IReadOnlyList<Passenger>)System.Array.Empty<Passenger>();
        }

        public void RemoveWaiting(BusStop stop, Passenger passenger)
        {
            stops.Find(x => x.stop == stop)?.waiting.Remove(passenger);
        }

        /// <summary>Durağın bekleme alanında, kaldırım üzerinde bir nokta (inen yolcuların gideceği yer).</summary>
        public Vector3 SidewalkPoint(BusStop stop, float along)
        {
            var s = stops.Find(x => x.stop == stop);
            if (s == null)
                return stop.transform.position;
            return Ground(s.lineOrigin + s.lineDirection * along, s.lineOrigin.y);
        }

        public Passenger Get(Vector3 position, Quaternion rotation)
        {
            Passenger p;
            if (pool.Count > 0)
            {
                p = pool.Pop();
                p.transform.SetPositionAndRotation(position, rotation);
                p.gameObject.SetActive(true);
                return p;
            }
            if (models == null || models.Length == 0)
                return null;
            var go = Instantiate(models[Random.Range(0, models.Length)], position, rotation, transform);
            if (paletteMaterial != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = paletteMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            p = go.GetComponent<Passenger>();
            if (p == null)
                p = go.AddComponent<Passenger>();
            return p;
        }

        public void Release(Passenger passenger)
        {
            if (passenger == null)
                return;
            passenger.Stand(passenger.transform.forward);
            passenger.gameObject.SetActive(false);
            pool.Push(passenger);
        }

        private void SpawnWaiting(StopState s)
        {
            s.spawned = true;
            int count = Random.Range(waitingRange.x, waitingRange.y + 1);
            for (int i = 0; i < count; i++)
            {
                var spot = Ground(s.lineOrigin + s.lineDirection * Random.Range(-5f, 5f) +
                                  s.roadSide * -Random.Range(0f, 0.9f), s.lineOrigin.y);
                var p = Get(spot, Quaternion.LookRotation(s.roadSide));
                if (p == null)
                    return;
                p.Stand(s.roadSide + s.lineDirection * Random.Range(-0.4f, 0.4f));
                s.waiting.Add(p);
            }
        }

        private void ClearWaiting(StopState s)
        {
            s.spawned = false;
            foreach (var p in s.waiting)
                Release(p);
            s.waiting.Clear();
        }

        /// <summary>Durağın sağındaki bordürü (kaldırım yükselmesini) arayıp bekleme çizgisini belirler.</summary>
        private static StopState Measure(BusStop stop)
        {
            var t = stop.transform;
            var origin = t.position;
            var right = t.right;
            float baseY = origin.y;
            float curb = 3f;
            for (float off = 1f; off < 7f; off += 0.25f)
            {
                var from = origin + right * off + Vector3.up * 5f;
                if (Physics.Raycast(from, Vector3.down, out var hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.point.y > baseY + 0.08f && hit.point.y < baseY + 0.6f)
                {
                    curb = off;
                    break;
                }
            }
            var line = origin + right * (curb + 1.1f);
            return new StopState
            {
                stop = stop,
                lineOrigin = Ground(line, baseY + 0.15f),
                lineDirection = t.forward,
                roadSide = -right,
            };
        }

        /// <summary>Noktanın altındaki zemini bulur; çatı vb. yüksek bir şeye çarparsa varsayılan yüksekliği kullanır.</summary>
        private static Vector3 Ground(Vector3 point, float fallbackY)
        {
            var from = new Vector3(point.x, fallbackY + 2f, point.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.point.y < fallbackY + 0.6f)
                return hit.point;
            return new Vector3(point.x, fallbackY, point.z);
        }
    }
}
