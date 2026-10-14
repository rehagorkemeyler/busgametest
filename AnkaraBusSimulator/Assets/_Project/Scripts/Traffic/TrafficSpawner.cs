using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Oyuncunun (otobüsün) çevresinde sabit sayıda trafik aracı tutar. Uzaklaşan ya da şeridin sonuna
    /// gelen araçları oyuncunun göremeyeceği bir noktaya taşır (havuzlama: Instantiate/Destroy yok).
    /// Araç türleri ağırlıkla seçilir (ör. %30 taksi).
    /// </summary>
    public class TrafficSpawner : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public GameObject prefab;
            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] private Entry[] vehicles;
        [Tooltip("Tüm araçlara uygulanacak ortak palet materyali (FBX'lerin kendi materyalinin yerine).")]
        [SerializeField] private Material paletteMaterial;
        [SerializeField] private int count = 24;
        [Tooltip("Boşsa 'Player' etiketli obje, o da yoksa sahnedeki BusVehicle aranır.")]
        [SerializeField] private Transform player;
        [SerializeField] private float minSpawnDistance = 70f;
        [SerializeField] private float maxDistance = 280f;
        [SerializeField] private float minGap = 14f;
        [SerializeField] private Vector2 speedRangeKmh = new Vector2(35f, 55f);

        private readonly List<TrafficCar> cars = new List<TrafficCar>();
        private TrafficLane[] lanes;
        private float retryTimer;

        // şerit örnekleri (20 m'de bir): büyük haritada (Hat 3, 9 km) rastgele şerit/konum çoğunlukla oyuncudan
        // uzağa düşüyordu, araçlar kayboluyordu; yeniden konumlandırma yalnız oyuncunun çevresindeki örneklerden seçer
        private const float OrnekAraligi = 20f;
        private readonly List<(TrafficLane lane, float d, Vector3 p)> ornekler = new List<(TrafficLane, float, Vector3)>();
        private readonly List<int> adaylar = new List<int>();

        private void Start()
        {
            lanes = GetComponentsInChildren<TrafficLane>();
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
            if (lanes.Length == 0 || vehicles == null || vehicles.Length == 0)
                return;
            foreach (var lane in lanes)
                for (float d = 0f; d < lane.Length * 0.9f; d += OrnekAraligi)
                {
                    lane.Sample(d, out var p, out _);
                    ornekler.Add((lane, d, p));
                }

            for (int i = 0; i < count; i++)
            {
                var car = Create();
                if (car != null)
                    Recycle(car, initial: true);
            }
        }

        private void Update()
        {
            retryTimer -= Time.deltaTime;
            bool retry = retryTimer <= 0f;
            if (retry)
                retryTimer = 1f;

            foreach (var car in cars)
            {
                if (!car.gameObject.activeSelf)
                {
                    if (retry)
                        Recycle(car, initial: false);
                    continue;
                }
                bool tooFar = player != null && (car.transform.position - player.position).sqrMagnitude > maxDistance * maxDistance;
                if (car.ReachedEnd || tooFar)
                    Recycle(car, initial: false);
            }
        }

        private TrafficCar Create()
        {
            var prefab = Pick();
            if (prefab == null)
                return null;

            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            go.name = prefab.name;
            if (paletteMaterial != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = paletteMaterial;

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers)
                        bounds.Encapsulate(r.bounds);
                    var box = go.AddComponent<BoxCollider>();
                    box.center = go.transform.InverseTransformPoint(bounds.center);
                    box.size = bounds.size;
                }
            }
            if (!go.TryGetComponent<Rigidbody>(out _))
                go.AddComponent<Rigidbody>();
            var car = go.GetComponent<TrafficCar>();
            if (car == null)
                car = go.AddComponent<TrafficCar>();
            cars.Add(car);
            return car;
        }

        private GameObject Pick()
        {
            float total = 0f;
            foreach (var v in vehicles)
                if (v.prefab != null)
                    total += v.weight;
            float roll = Random.value * total;
            foreach (var v in vehicles)
            {
                if (v.prefab == null)
                    continue;
                roll -= v.weight;
                if (roll <= 0f)
                    return v.prefab;
            }
            return null;
        }

        private void Recycle(TrafficCar car, bool initial)
        {
            float minDistance = initial ? 25f : minSpawnDistance;
            adaylar.Clear();
            if (player != null)
            {
                Vector3 pp = player.position;
                for (int i = 0; i < ornekler.Count; i++)
                {
                    float sq = (ornekler[i].p - pp).sqrMagnitude;
                    if (sq >= minDistance * minDistance && sq <= maxDistance * maxDistance)
                        adaylar.Add(i);
                }
            }
            for (int attempt = 0; attempt < 20; attempt++)
            {
                TrafficLane lane;
                float d;
                if (player != null)
                {
                    if (adaylar.Count == 0)
                        break;
                    var o = ornekler[adaylar[Random.Range(0, adaylar.Count)]];
                    lane = o.lane;
                    d = Mathf.Clamp(o.d + Random.Range(0f, OrnekAraligi), 0f, lane.Length * 0.9f);
                }
                else
                {
                    lane = lanes[Random.Range(0, lanes.Length)];
                    d = Random.Range(0f, lane.Length * 0.9f);
                }
                if (Crowded(lane, d, car))
                    continue;

                car.gameObject.SetActive(true);
                car.Place(lane, d, Random.Range(speedRangeKmh.x, speedRangeKmh.y));
                return;
            }
            car.gameObject.SetActive(false);
        }

        private bool Crowded(TrafficLane lane, float distance, TrafficCar self)
        {
            foreach (var other in cars)
                if (other != self && other.gameObject.activeSelf && other.Lane == lane &&
                    Mathf.Abs(other.Distance - distance) < minGap)
                    return true;
            return false;
        }
    }
}
