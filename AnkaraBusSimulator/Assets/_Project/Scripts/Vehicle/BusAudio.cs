using System;
using System.Collections.Generic;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüs sesleri. Motor: her ses kaydedildiği devirde (nativeRpm) çalınır, perdesi gerçek devre göre ayarlanır
    /// ve komşu devirdeki seslerle eşit güçte (cos/sin) çapraz geçiş yapar. Kokpit ve dış kamera için ayrı setler
    /// vardır; kamera değişince yumuşakça geçilir. Ayrıca retarder, şanzıman, tıkırtı, fren havası, el freni,
    /// vites düğmesi, geri vites uyarısı, kapılar, kentkart bip sesi, "dur" zili ve korna.
    /// Değerler BusVehicle, BusDoorController, BusPassengers ve RouteTracker'dan okunur; bunlara dokunmaz.
    /// Kurulum: Ankara Bus > Otobüs Seslerini Kur (Editor/OtobusSesKurucu.cs).
    /// </summary>
    [RequireComponent(typeof(BusVehicle))]
    public class BusAudio : MonoBehaviour
    {
        [Serializable]
        public class EngineLayer
        {
            public AudioClip clip;
            [Tooltip("Sesin kaydedildiği motor devri.")]
            public float nativeRpm = 1000f;
            [Tooltip("Kokpit (iç) seti mi, dış set mi?")]
            public bool interior;
            [Tooltip("Yük katmanı: yalnızca gaza basılınca duyulur.")]
            public bool load;
            [Range(0f, 2f)] public float volume = 1f;
            [NonSerialized] public AudioSource source;
        }

        [Header("Motor")]
        [SerializeField] private EngineLayer[] engine = Array.Empty<EngineLayer>();
        [Tooltip("Motorun yerel konumu (arkada).")]
        [SerializeField] private Vector3 enginePosition = new Vector3(0f, 0.9f, -3.4f);
        [SerializeField] private AudioClip engineStart;
        [SerializeField] private AudioClip engineStop;

        [Header("Sürüş")]
        [SerializeField] private AudioClip retarder;
        [SerializeField] private AudioClip transmission;
        [SerializeField] private AudioClip rattle;
        [SerializeField] private AudioClip brakeApply;
        [SerializeField] private AudioClip brakeRelease;
        [SerializeField] private AudioClip handbrakeOn;
        [SerializeField] private AudioClip handbrakeOff;
        [SerializeField] private AudioClip gearButton;
        [SerializeField] private AudioClip reverseBeep;
        [SerializeField] private AudioClip horn;

        [Header("Kapılar ve yolcular")]
        [Tooltip("Kapı sırasıyla (0 ön, 1 orta, 2 arka).")]
        [SerializeField] private AudioClip[] doorOpen = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] doorClose = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip validatorBeep;
        [SerializeField] private AudioClip stopRequest;

        [Header("Genel")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;

        private BusVehicle vehicle;
        private BusDoorController doors;
        private RouteTracker tracker;
        private BusPassengers passengers;
        private BusCameraRig cameraRig;

        private AudioSource retarderSource, transmissionSource, rattleSource, reverseSource, hornSource, cabSource;
        private readonly List<AudioSource> doorSources = new List<AudioSource>();
        private float interiorBlend;
        private bool brakeApplied;
        private bool lastHandbrake;
        private int lastOnboard;
        private float stopRequestTimer = -1f;

        /// <summary>Tek seferlik ses çalındı: (klip adı, ses düzeyi). Testler ve hata ayıklama için.</summary>
        public event Action<string, float> OneShotPlayed;

        public bool HornPlaying => hornSource != null && hornSource.isPlaying;
        public bool ReverseBeepPlaying => reverseSource != null && reverseSource.isPlaying;
        public float InteriorBlend => interiorBlend;

        /// <summary>Motor katmanlarının o anki durumu: (klip, iç set mi, ses düzeyi, perde).</summary>
        public IEnumerable<(string clip, bool interior, float volume, float pitch)> EngineState()
        {
            foreach (var l in engine)
                if (l.source != null)
                    yield return (l.clip.name, l.interior, l.source.volume, l.source.pitch);
        }

        public void Horn(bool on)
        {
            if (hornSource == null)
                return;
            if (on && !hornSource.isPlaying)
                hornSource.Play();
            else if (!on)
                hornSource.Stop();
        }

        private void Awake()
        {
            vehicle = GetComponent<BusVehicle>();
            doors = GetComponentInChildren<BusDoorController>();
            tracker = GetComponent<RouteTracker>();
        }

        private void OnEnable()
        {
            if (vehicle != null)
                vehicle.SelectorChanged += OnSelector;
            if (doors != null)
                doors.DoorChanged += OnDoor;
            if (tracker != null)
                tracker.StopServed += OnStopServed;
        }

        private void OnDisable()
        {
            if (vehicle != null)
                vehicle.SelectorChanged -= OnSelector;
            if (doors != null)
                doors.DoorChanged -= OnDoor;
            if (tracker != null)
                tracker.StopServed -= OnStopServed;
        }

        private void Start()
        {
            var engineAnchor = new GameObject("Ses_Motor").transform;
            engineAnchor.SetParent(transform, false);
            engineAnchor.localPosition = enginePosition;
            foreach (var layer in engine)
            {
                if (layer.clip == null)
                    continue;
                // kokpit seti 2B (kabin içinde her yerden duyulur), dış set motorun konumunda 3B
                layer.source = MakeSource(engineAnchor, layer.clip, true, layer.interior ? 0f : 1f, 4f, 150f);
                layer.source.volume = 0f;
                layer.source.time = UnityEngine.Random.Range(0f, layer.clip.length);
                layer.source.Play();
            }

            retarderSource = MakeLoop(engineAnchor, retarder, 1f);
            transmissionSource = MakeLoop(engineAnchor, transmission, 1f);
            rattleSource = MakeLoop(transform, rattle, 0f);
            reverseSource = MakeSource(engineAnchor, reverseBeep, true, 1f, 3f, 60f);
            hornSource = MakeSource(transform, horn, true, 1f, 5f, 200f);
            cabSource = MakeSource(transform, null, false, 0.3f, 2f, 40f);

            if (doors != null)
                for (int i = 0; i < doors.DoorCount; i++)
                {
                    var anchor = new GameObject($"Ses_Kapi_{i + 1}").transform;
                    anchor.SetParent(transform, false);
                    anchor.position = doors.DoorCenter(i);
                    doorSources.Add(MakeSource(anchor, null, false, 1f, 2f, 40f));
                }

            lastHandbrake = vehicle.Handbrake;
            if (engineStart != null)
                OneShot(cabSource, engineStart, masterVolume);
        }

        private void Update()
        {
            float rpm = vehicle.EngineRpm;
            float speedKmh = vehicle.SpeedKmh;
            float throttle = vehicle.Throttle;

            if (cameraRig == null)
                cameraRig = FindAnyObjectByType<BusCameraRig>();
            float targetInterior = cameraRig != null && cameraRig.IsInside ? 1f : 0f;
            interiorBlend = Mathf.MoveTowards(interiorBlend, targetInterior, Time.deltaTime * 3f);

            UpdateEngine(rpm, throttle);

            float speed01 = Mathf.Clamp01(speedKmh / 60f);
            if (retarderSource != null)
            {
                retarderSource.volume = masterVolume * vehicle.RetarderLevel * Mathf.Clamp01(speedKmh / 25f) * 0.9f;
                retarderSource.pitch = 0.8f + 0.5f * speed01;
            }
            if (transmissionSource != null)
            {
                transmissionSource.volume = masterVolume * speed01 * 0.25f;
                transmissionSource.pitch = 0.6f + 0.8f * speed01;
            }
            if (rattleSource != null)
                rattleSource.volume = masterVolume * Mathf.Clamp01(speedKmh / 45f) * 0.15f * interiorBlend;

            // fren havası: basınç artınca "tıs", bırakılınca hava boşaltma
            // (basınç bırakınca birkaç karede düşer; bu yüzden "basıldı" durumu tutulur)
            float brake = vehicle.BrakePressure;
            if (brake > 0.25f && !brakeApplied)
            {
                brakeApplied = true;
                if (brakeApply != null)
                    OneShot(cabSource, brakeApply, masterVolume * 0.6f);
            }
            else if (brake < 0.05f && brakeApplied)
            {
                brakeApplied = false;
                if (brakeRelease != null)
                    OneShot(cabSource, brakeRelease, masterVolume * 0.8f);
            }

            if (vehicle.Handbrake != lastHandbrake)
            {
                var clip = vehicle.Handbrake ? handbrakeOn : handbrakeOff;
                if (clip != null)
                    OneShot(cabSource, clip, masterVolume);
                lastHandbrake = vehicle.Handbrake;
            }

            if (reverseSource != null)
            {
                bool reversing = vehicle.Selector == BusVehicle.GearSelector.Reverse;
                if (reversing && !reverseSource.isPlaying)
                    reverseSource.Play();
                else if (!reversing && reverseSource.isPlaying)
                    reverseSource.Stop();
                reverseSource.volume = masterVolume;
            }

            // kentkart: her binen yolcu için ön kapıda bip
            if (passengers == null)
                passengers = GetComponent<BusPassengers>();
            if (passengers != null)
            {
                if (passengers.Onboard > lastOnboard && validatorBeep != null && doorSources.Count > 0)
                    OneShot(doorSources[0], validatorBeep, masterVolume * 0.8f);
                lastOnboard = passengers.Onboard;
            }

            // duraktan çıktıktan bir süre sonra bir yolcu "dur" düğmesine basar
            if (stopRequestTimer > 0f)
            {
                stopRequestTimer -= Time.deltaTime;
                if (stopRequestTimer <= 0f && stopRequest != null && (passengers == null || passengers.Onboard > 0))
                    OneShot(cabSource, stopRequest, masterVolume * (0.15f + 0.25f * interiorBlend));
            }
        }

        private void UpdateEngine(float rpm, float throttle)
        {
            float gain = masterVolume * (0.55f + 0.45f * throttle);
            ApplySet(rpm, true, gain * interiorBlend, throttle);
            ApplySet(rpm, false, gain * (1f - interiorBlend), throttle);
        }

        /// <summary>Bir setin (iç ya da dış) katmanlarına çapraz geçiş ağırlıklarını uygular.</summary>
        private void ApplySet(float rpm, bool interior, float gain, float throttle)
        {
            // set içindeki devir katmanlarını sırala (yük katmanları ayrı)
            EngineLayer below = null, above = null;
            foreach (var l in engine)
            {
                if (l.source == null || l.interior != interior || l.load)
                    continue;
                if (l.nativeRpm <= rpm && (below == null || l.nativeRpm > below.nativeRpm))
                    below = l;
                if (l.nativeRpm > rpm && (above == null || l.nativeRpm < above.nativeRpm))
                    above = l;
            }
            float t = 0f;
            if (below != null && above != null)
                t = Mathf.InverseLerp(Mathf.Log(below.nativeRpm), Mathf.Log(above.nativeRpm), Mathf.Log(Mathf.Max(rpm, 1f)));
            else if (below == null)
                t = 1f;

            foreach (var l in engine)
            {
                if (l.source == null || l.interior != interior)
                    continue;
                float w;
                if (l.load)
                    w = throttle * 0.8f;
                else if (l == below)
                    w = Mathf.Cos(t * Mathf.PI * 0.5f);
                else if (l == above)
                    w = Mathf.Sin(t * Mathf.PI * 0.5f);
                else
                    w = 0f;
                l.source.volume = gain * w * l.volume;
                l.source.pitch = Mathf.Clamp(rpm / Mathf.Max(l.nativeRpm, 1f), 0.5f, 2f);
            }
        }

        private void OnSelector(BusVehicle.GearSelector selector)
        {
            if (gearButton != null && cabSource != null)
                OneShot(cabSource, gearButton, masterVolume * 0.7f);
        }

        private void OnDoor(int index, bool open)
        {
            if (index < 0 || index >= doorSources.Count)
                return;
            var clips = open ? doorOpen : doorClose;
            if (index < clips.Length && clips[index] != null)
                OneShot(doorSources[index], clips[index], masterVolume);
        }

        private void OnStopServed(BusStop stop)
        {
            stopRequestTimer = UnityEngine.Random.Range(8f, 20f);
        }

        private void OneShot(AudioSource source, AudioClip clip, float volume)
        {
            source.PlayOneShot(clip, volume);
            OneShotPlayed?.Invoke(clip.name, volume);
        }

        private AudioSource MakeLoop(Transform parent, AudioClip clip, float spatial)
        {
            if (clip == null)
                return null;
            var s = MakeSource(parent, clip, true, spatial, 4f, 100f);
            s.volume = 0f;
            s.Play();
            return s;
        }

        private static AudioSource MakeSource(Transform parent, AudioClip clip, bool loop, float spatial, float minDistance, float maxDistance)
        {
            var s = parent.gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            return s;
        }
    }
}
