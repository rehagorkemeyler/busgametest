using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Şerit izleyen basit trafik aracı. Fizik simülasyonu yok (kinematik): mobilde ucuz.
    /// Önündeki araca veya otobüse (Rigidbody'si olan her şeye) göre yavaşlar ve durur; kırmızıda ve dolu yaya
    /// geçidinde durur. Duran bir engelin (ör. duraktaki otobüs) arkasında kalırsa yan şerit boşsa sollar,
    /// yoksa bir süre sonra korna çalar. Dolmuşlar duraklarda yanaşıp yolcu alır gibi bekler.
    /// Araç şerit çizgisinden yana kayabilir (lateral): sollama ve durağa yanaşma bununla yumuşak yapılır.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCar : MonoBehaviour
    {
        [SerializeField] private float maxSpeedKmh = 50f;
        [SerializeField] private float acceleration = 2.5f;
        [SerializeField] private float braking = 6f;
        [Tooltip("Öndeki araçla bırakılan boşluk (m).")]
        [SerializeField] private float safeGap = 5f;
        [SerializeField] private float wheelRadius = 0.3f;
        [Tooltip("Duran engelin arkasında bu kadar sn bekleyince sollamayı dener.")]
        [SerializeField] private float overtakeAfter = 2.5f;
        [Tooltip("Otobüs önünü kapatınca bu kadar sn sonra korna çalar.")]
        [SerializeField] private float hornAfter = 4f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly List<TrafficCar> Active = new List<TrafficCar>();
        private static AudioClip[] hornClips;
        private static BusStop[] stops;
        private static int stopsFrame = -1;

        private Rigidbody body;
        private Transform[] wheels;
        private float speed;
        private float frontOffset = 2.2f;
        private int nextExit;
        private Vector3 castHalfExtents = new Vector3(0.8f, 0.4f, 0.1f);

        // yana kayma (+ sağ): sollama ve durağa yanaşma
        private float lateral, lateralTarget;
        // bekleme: engel arkasında, korna
        private float blockedTime, hornTimer, nextLaneTry;
        private int hornCount;
        private AudioSource hornSource;
        // dolmuş
        private bool isDolmus;
        private BusStop dolmusStop;
        private float dolmusStopAt, dolmusSide, dolmusWait, dolmusCooldown;

        /// <summary>Sahnedeki etkin trafik araçları.</summary>
        public static IReadOnlyList<TrafficCar> Aktifler => Active;

        public TrafficLane Lane { get; private set; }
        public float Distance { get; private set; }
        public float Speed => speed;
        public bool ReachedEnd => Lane != null && Distance >= Lane.Length - 0.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var found = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Teker"))
                    found.Add(t);
            wheels = found.ToArray();

            if (TryGetComponent<BoxCollider>(out var box))
            {
                frontOffset = box.center.z + box.size.z * 0.5f;
                castHalfExtents = new Vector3(box.size.x * 0.45f, 0.4f, 0.1f);
            }
            isDolmus = name.Contains("Dolmus");
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>Aracı şeridin belirli bir noktasına ışınlar.</summary>
        public void Place(TrafficLane lane, float distance, float maxSpeed)
        {
            Lane = lane;
            Distance = distance;
            nextExit = FirstExitAfter(distance);
            maxSpeedKmh = maxSpeed;
            speed = maxSpeed / 3.6f * 0.7f;
            lateral = lateralTarget = 0f;
            blockedTime = hornTimer = 0f;
            hornCount = 0;
            dolmusStop = null;
            dolmusCooldown = 0f;
            lane.Sample(distance, out var position, out var forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void FixedUpdate()
        {
            if (Lane == null)
                return;

            float dt = Time.fixedDeltaTime;
            float target = Mathf.Min(maxSpeedKmh, Lane.SpeedLimitKmh) / 3.6f;
            float range = safeGap + speed * speed / (2f * braking) + 3f;
            float free = FreeDistanceAhead(range, out var obstacle);
            float otobus = OtobusSeritBoyunca(Mathf.Max(range, 25f), out var otobusGovdesi);
            if (otobus < free)
            {
                free = otobus;
                obstacle = otobusGovdesi;
            }
            bool blockedByObstacle = false;
            if (free < float.MaxValue)
            {
                float limit = Mathf.Sqrt(2f * braking * Mathf.Max(0f, free - safeGap));
                if (limit < target)
                {
                    target = limit;
                    blockedByObstacle = free < safeGap + 4f;
                }
            }

            bool heldByRule = false;
            // trafik ışığı: önü durma çizgisine gelmeden dur
            if (Lane.StopLine >= 0f)
            {
                float toLine = Lane.StopLine - (Distance + frontOffset);
                if (toLine > -0.5f)
                {
                    bool canStop = toLine > speed * speed / (2f * braking * 1.2f);
                    if (Lane.MustStop(canStop))
                    {
                        target = Mathf.Min(target, Mathf.Sqrt(2f * braking * Mathf.Max(0f, toLine - 0.5f)));
                        heldByRule |= toLine < 15f;
                    }
                }
            }
            // yaya geçidi: üzerinde yaya varsa önünde dur
            foreach (var (at, gecit) in Lane.Gecitler)
            {
                float toCrossing = at - gecit.YariGenislik - 1.5f - (Distance + frontOffset);
                if (toCrossing < -0.5f || toCrossing > 40f)
                    continue;
                if (gecit.Dolu)
                {
                    target = Mathf.Min(target, Mathf.Sqrt(2f * braking * Mathf.Max(0f, toCrossing)));
                    heldByRule |= toCrossing < 15f;
                }
                break;
            }
            target = Mathf.Min(target, DolmusTarget(dt));

            Waiting(dt, blockedByObstacle && !heldByRule, obstacle);

            speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : braking) * dt);
            Distance += speed * dt;
            TakeExits();

            // yana kayma: hızlıyken daha çabuk, dururken yavaşça
            float lateralRate = Mathf.Max(0.9f, speed * 0.3f);
            float previousLateral = lateral;
            lateral = Mathf.MoveTowards(lateral, lateralTarget, lateralRate * dt);
            float lateralSpeed = (lateral - previousLateral) / dt;

            Lane.Sample(Distance, out var position, out var forward);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            float yaw = Mathf.Atan2(lateralSpeed, Mathf.Max(speed, 1f)) * Mathf.Rad2Deg;
            body.MovePosition(position + right * lateral);
            body.MoveRotation(Quaternion.LookRotation(Quaternion.AngleAxis(yaw, Vector3.up) * forward, Vector3.up));

            float degrees = speed * dt / wheelRadius * Mathf.Rad2Deg;
            foreach (var w in wheels)
                w.Rotate(Vector3.right, degrees, Space.Self);
        }

        /// <summary>Duran bir engelin arkasında: önce sollamayı dene, olmazsa (engel otobüsse) korna.</summary>
        private void Waiting(float dt, bool blocked, Rigidbody obstacle)
        {
            if (!blocked || speed > 1f || dolmusStop != null)
            {
                blockedTime = Mathf.Max(0f, blockedTime - dt * 2f);
                if (blockedTime <= 0f)
                    hornCount = 0;
                return;
            }
            blockedTime += dt;
            // şerit arama pahalı (tüm şeritler): en çok saniyede bir
            if (blockedTime > overtakeAfter && Time.time >= nextLaneTry && IsStill(obstacle) && !NearRule()
                && Mathf.Approximately(lateral, lateralTarget) && TryChangeLane(out nextLaneTry))
            {
                blockedTime = 0f;
                return;
            }
            bool playerBus = obstacle != null && !obstacle.isKinematic;
            if (playerBus && blockedTime > hornAfter && hornCount < 3)
            {
                hornTimer -= dt;
                if (hornTimer <= 0f)
                {
                    Horn();
                    hornCount++;
                    hornTimer = Random.Range(4f, 8f);
                }
            }
        }

        private static bool IsStill(Rigidbody obstacle)
        {
            if (obstacle == null)
                return true;
            if (!obstacle.isKinematic)
                return obstacle.linearVelocity.sqrMagnitude < 0.25f;
            return !obstacle.TryGetComponent<TrafficCar>(out var car) || car.speed < 0.5f;
        }

        /// <summary>Önde ışık ya da yaya geçidi var mı? Oradaki kuyrukta şerit değiştirilmez.</summary>
        private bool NearRule()
        {
            if (Lane.StopLine >= 0f && Lane.StopLine - Distance > -2f && Lane.StopLine - Distance < 80f)
                return true;
            foreach (var (at, _) in Lane.Gecitler)
                if (at - Distance > -2f && at - Distance < 30f)
                    return true;
            return false;
        }

        /// <summary>Aynı yöndeki yan şeride (2,5–4,5 m yanda) boşsa geçer; araç yerinde kalır, yana kayarak girer.</summary>
        private bool TryChangeLane(out float retryAt)
        {
            retryAt = Time.time + 1f;
            Vector3 p = transform.position;
            Vector3 fwd = transform.forward;
            foreach (var other in TrafficLane.Hepsi)
            {
                if (other == Lane)
                    continue;
                float d = other.EnYakinKonum(p, out float dist);
                if (dist < 2.5f || dist > 4.6f || d < 5f || d > other.Length - 30f)
                    continue;
                other.Sample(d, out var q, out var f);
                if (Vector3.Dot(f, fwd) < 0.95f)
                    continue;
                if (!LaneFree(other, d))
                    continue;
                // yeni şeride göre bugünkü yan konum: araç yerinden sıçramasın
                var right = Vector3.Cross(Vector3.up, f).normalized;
                lateral = Vector3.Dot(p - q, right);
                lateralTarget = 0f;
                Lane = other;
                Distance = d;
                nextExit = FirstExitAfter(d);
                return true;
            }
            return false;
        }

        private bool LaneFree(TrafficLane lane, float d)
        {
            foreach (var car in Active)
                if (car != this && car.Lane == lane && car.Distance > d - 12f && car.Distance < d + 14f)
                    return false;
            // otobüs (ya da başka fiziksel cisim) o şeritte mi?
            lane.Sample(d + 2f, out var q, out var f);
            var size = new Vector3(1.1f, 0.8f, 7f);
            var cols = Physics.OverlapBox(q + Vector3.up * 1f, size, Quaternion.LookRotation(f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
                if (c.attachedRigidbody != null && c.attachedRigidbody != body)
                    return false;
            return true;
        }

        // ---------------------------------------------------------------- dolmuş

        /// <summary>Dolmuş: önündeki durağa (otobüs orada değilse) yanaşır, 4–9 sn bekler, yola döner.</summary>
        private float DolmusTarget(float dt)
        {
            if (!isDolmus)
                return float.MaxValue;
            if (dolmusStop == null)
            {
                dolmusCooldown -= dt;
                if (dolmusCooldown > 0f)
                    return float.MaxValue;
                dolmusCooldown = 1f;
                FindDolmusStop();
                return float.MaxValue;
            }
            float toStop = dolmusStopAt - Distance;
            // durak cebine ancak cebin başında girilir (önce kaldırıma çıkmasın)
            if (toStop < 22f && dolmusWait <= 0f)
                lateralTarget = dolmusSide;
            if (dolmusWait > 0f)
            {
                dolmusWait -= dt;
                if (dolmusWait <= 0f)
                {
                    lateralTarget = 0f;
                    dolmusStop = null;
                    dolmusCooldown = 20f;
                }
                return 0f;
            }
            if (toStop < 0.3f)
            {
                dolmusWait = Random.Range(4f, 9f);
                return 0f;
            }
            // yanaşırken yavaşla
            return Mathf.Max(1.5f, Mathf.Sqrt(2f * braking * 0.5f * toStop));
        }

        private void FindDolmusStop()
        {
            if (stops == null || stopsFrame < 0 || stops.Length > 0 && stops[0] == null)
            {
                stops = FindObjectsByType<BusStop>();
                stopsFrame = Time.frameCount;
            }
            Vector3 p = transform.position;
            foreach (var stop in stops)
            {
                if (stop == null)
                    continue;
                float at = Lane.EnYakinKonum(stop.transform.position, out float dist);
                float ahead = at - Distance;
                if (ahead < 25f || ahead > 70f || dist < 1.5f || dist > 5f)
                    continue;
                Lane.Sample(at, out var q, out var f);
                var right = Vector3.Cross(Vector3.up, f).normalized;
                float side = Vector3.Dot(stop.transform.position - q, right);
                if (side < 1.5f || Vector3.Dot(stop.transform.forward, f) < 0.8f)
                    continue; // durak sağda ve aynı yönde olmalı
                if (BusNear(stop.transform.position, 28f) || DolmusAt(stop))
                    continue;
                dolmusStop = stop;
                dolmusStopAt = at + 4f;
                dolmusSide = Mathf.Min(side, 3.4f);
                return;
            }
        }

        private static bool BusNear(Vector3 p, float radius)
        {
            var cols = Physics.OverlapSphere(p, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                    return true;
            return false;
        }

        private bool DolmusAt(BusStop stop)
        {
            foreach (var car in Active)
                if (car != this && car.dolmusStop == stop)
                    return true;
            return false;
        }

        // ---------------------------------------------------------------- korna

        private void Horn()
        {
            if (hornSource == null)
            {
                hornSource = gameObject.AddComponent<AudioSource>();
                hornSource.spatialBlend = 1f;
                hornSource.rolloffMode = AudioRolloffMode.Linear;
                hornSource.minDistance = 4f;
                hornSource.maxDistance = 90f;
                hornSource.dopplerLevel = 0f;
                hornSource.playOnAwake = false;
            }
            hornClips ??= new[] { HornClip(415f, 0.35f, 1), HornClip(392f, 0.18f, 2), HornClip(466f, 0.6f, 1) };
            hornSource.pitch = Random.Range(0.92f, 1.08f);
            hornSource.PlayOneShot(hornClips[Random.Range(0, hornClips.Length)], 0.8f);
        }

        /// <summary>İki tonlu araba kornası: kare dalgaya yakın, yumuşak giriş/çıkış; 'count' kez kısa basış.</summary>
        private static AudioClip HornClip(float hz, float seconds, int count)
        {
            const int rate = 22050;
            float gap = 0.09f;
            int n = Mathf.CeilToInt((seconds * count + gap * (count - 1)) * rate);
            var data = new float[n];
            for (int k = 0; k < count; k++)
            {
                int start = Mathf.RoundToInt(k * (seconds + gap) * rate);
                int len = Mathf.RoundToInt(seconds * rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((seconds - t) / 0.03f);
                    float a = Mathf.Sin(2f * Mathf.PI * hz * t), b = Mathf.Sin(2f * Mathf.PI * hz * 1.26f * t);
                    float tone = Mathf.Clamp(a * 2.2f, -1f, 1f) * 0.5f + Mathf.Clamp(b * 2.2f, -1f, 1f) * 0.5f;
                    data[start + i] = tone * env * 0.45f;
                }
            }
            var clip = AudioClip.Create("Korna_" + hz, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------------------------------------------------------------- şerit

        /// <summary>Geçilen çıkış noktalarında olasılığa göre başka şeride geçer (kavşakta dönüş).</summary>
        private void TakeExits()
        {
            var exits = Lane.Exits;
            while (nextExit < exits.Length && Distance >= exits[nextExit].at)
            {
                var exit = exits[nextExit];
                nextExit++;
                // sollama ya da yanaşma sırasında dönüş yapılmaz
                if (exit.target != null && Mathf.Abs(lateral) < 0.3f && dolmusStop == null && Random.value < exit.probability)
                {
                    Distance = exit.targetAt + (Distance - exit.at);
                    Lane = exit.target;
                    nextExit = FirstExitAfter(Distance);
                    return;
                }
            }
        }

        private int FirstExitAfter(float distance)
        {
            var exits = Lane.Exits;
            int i = 0;
            while (i < exits.Length && exits[i].at <= distance)
                i++;
            return i;
        }

        // oyuncu otobüsünün gövde kutuları (körüklüde arka gövde ayrı Rigidbody), bütün araçlar paylaşır
        private static readonly List<BoxCollider> OtobusKutulari = new List<BoxCollider>();
        private static readonly List<BoxCollider> KutuTampon = new List<BoxCollider>();
        private static float kutularZamani = -10f;

        private static List<BoxCollider> Kutular()
        {
            if (Time.time - kutularZamani < 2f && (OtobusKutulari.Count == 0 || OtobusKutulari[0] != null))
                return OtobusKutulari;
            kutularZamani = Time.time;
            OtobusKutulari.Clear();
            var bus = FindAnyObjectByType<BusVehicle>();
            if (bus == null)
                return OtobusKutulari;
            void Ekle(Component kok)
            {
                kok.GetComponentsInChildren(KutuTampon);
                foreach (var k in KutuTampon)
                    if (!k.isTrigger && !OtobusKutulari.Contains(k))
                        OtobusKutulari.Add(k);
            }
            Ekle(bus);
            var koruklu = bus.GetComponent<KorukluOtobus>();
            if (koruklu != null && koruklu.ArkaGovde != null)
                Ekle(koruklu.ArkaGovde);
            return OtobusKutulari;
        }

        /// <summary>
        /// Şerit boyunca (kavşak bağlantısında ve virajda da) önümüzdeki 'range' metrede otobüsün gövdesine kalan mesafe.
        /// Düz ileri kutu ışını eğri şeritte yolu kesen otobüsü görmüyordu: Cinnah'tan Kızılay'a sola dönen araç, ışık değişince
        /// kavşakta hâlâ dönmekte olan körüklünün sol önüne 35 km/s giriyor, kinematik olduğu için onu itip kilitliyordu
        /// (docs/RAPOR_Y15.md S3). Şeridin sonunda kesin (olasılığı 1) çıkış varsa sonraki şeritte devam edilir.
        /// </summary>
        private float OtobusSeritBoyunca(float range, out Rigidbody govde)
        {
            govde = null;
            var kutular = Kutular();
            if (kutular.Count == 0 || kutular[0] == null)
                return float.MaxValue;
            float yakin = range + 25f;
            if ((kutular[0].transform.position - transform.position).sqrMagnitude > yakin * yakin)
                return float.MaxValue;
            var lane = Lane;
            float d0 = Distance + frontOffset;
            int cikis = nextExit;
            for (float d = 0f; d <= range; d += 2f)
            {
                float at = d0 + d;
                // kesin çıkış: sonraki şeride geç
                var exits = lane.Exits;
                if (cikis < exits.Length && at >= exits[cikis].at && exits[cikis].probability >= 0.999f && exits[cikis].target != null)
                {
                    d0 = exits[cikis].targetAt - d + (d0 + d - exits[cikis].at);
                    lane = exits[cikis].target;
                    cikis = int.MaxValue;
                    at = d0 + d;
                }
                if (at > lane.Length)
                    break;
                lane.Sample(at, out var p, out var f);
                p.y += 1f;
                // yana kayarken (sollama, dolmuşun cebe yanaşması) hem şimdiki hem hedef yan konumda
                var sag = Vector3.Cross(Vector3.up, f).normalized;
                if (OtobusaDegiyor(kutular, p + sag * lateral, out govde)
                    || (Mathf.Abs(lateralTarget - lateral) > 0.3f && OtobusaDegiyor(kutular, p + sag * lateralTarget, out govde)))
                    return d;
            }
            return float.MaxValue;
        }

        private static bool OtobusaDegiyor(List<BoxCollider> kutular, Vector3 p, out Rigidbody govde)
        {
            foreach (var k in kutular)
                if (k != null && k.enabled && (k.ClosestPoint(p) - p).sqrMagnitude < 1.3f * 1.3f)
                {
                    govde = k.attachedRigidbody;
                    return true;
                }
            govde = null;
            return false;
        }

        // Y15 teşhis: otobüse çarpan trafik aracının durumu (kinematik araç otobüsü itebilir)
        private void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody == null || c.rigidbody.isKinematic || Lane == null)
                return;
            Debug.Log($"[Trafik] {name} otobüse çarptı: şerit {Lane.name} {Distance:F1} m, yan {lateral:F2}→{lateralTarget:F2} m, " +
                      $"hız {speed * 3.6f:F0} km/s, dolmuş_durağı={(dolmusStop != null ? dolmusStop.StopName : "yok")}, " +
                      $"otobüste {c.rigidbody.transform.InverseTransformPoint(transform.position):F1}");
        }

        /// <summary>Öndeki en yakın hareketli cisme (araç, otobüs) mesafe. Yol ve binalar yok sayılır.</summary>
        private float FreeDistanceAhead(float range, out Rigidbody obstacle)
        {
            obstacle = null;
            var origin = transform.position + transform.forward * (frontOffset + 0.2f) + transform.up * 0.8f;
            int n = Physics.BoxCastNonAlloc(origin, castHalfExtents, transform.forward, Hits, transform.rotation,
                                            range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var other = Hits[i].rigidbody;
                if (other != null && other != body && Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    obstacle = other;
                }
            }
            return nearest;
        }
    }
}
