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
    /// Y20: otobüste koltuk noktaları varsa (OtobusKoltuklari) binen yolcu kapıdan girip koridordan boş bir koltuğa yürür ve
    /// oturur (koltuklar doluysa koridorda tutunarak durur); inen yolcu koltuğundan kalkıp en yakın açık kapıdan iner.
    /// Görünen yolcu sayısı kaliteyle sınırlı (Düşük 12, Normal 20, Yüksek 30); fazlası yalnız sayıda tutulur.
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
        private OtobusKoltuklari koltuklar;
        private AnkaraBus.Vehicle.KorukluOtobus koruklu;
        private readonly List<Passenger> icerde = new List<Passenger>();

        private static int MaxGorunur => GrafikAyarlari.Mevcut == KaliteSeviyesi.Dusuk ? 12
            : GrafikAyarlari.Mevcut == KaliteSeviyesi.Normal ? 20 : 30;

        public int Onboard => onboard;
        public int Capacity => capacity;
        public bool IsBusy { get; private set; }
        /// <summary>Bu sıradaki durakta iniş-biniş yapıldı ve bitti mi? (RouteTracker durağı bununla tamamlar.)</summary>
        public bool Served(int stopIndex) => servedIndex == stopIndex && !IsBusy;

        public event System.Action<int> OnboardChanged;

        private void Awake()
        {
            tracker = GetComponent<RouteTracker>();
            telemetry = GetComponent<IVehicleTelemetry>();
            doors = GetComponentInChildren<BusDoorController>();
            koltuklar = GetComponent<OtobusKoltuklari>();
            koruklu = GetComponent<AnkaraBus.Vehicle.KorukluOtobus>();
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
                var away = manager.SidewalkPoint(stop, Random.Range(-6f, 6f)) + transform.right * Random.Range(1f, 3f);
                // görünen bir yolcu varsa (oturan/ayakta) o kalkıp iner; yoksa kapıda belirir
                var icerdeki = icerde.Count > 0 ? icerde[Random.Range(0, icerde.Count)] : null;
                int door = icerdeki != null ? PickExitDoor(icerdeki.transform.position) : PickExitDoor(transform.position);
                if (door < 0)
                    break;
                SetOnboard(onboard - 1);
                walking++;
                if (icerdeki != null)
                {
                    icerde.Remove(icerdeki);
                    var yol = InisYolu(icerdeki, door);
                    koltuklar.Birak(icerdeki);
                    var inen = icerdeki;
                    inen.Yuru(yol, () =>
                    {
                        inen.transform.SetParent(manager.transform, true);
                        inen.WalkTo(away, () => { walking--; manager.Release(inen); });
                    });
                }
                else
                {
                    var p = manager.Get(DoorOutside(door), Quaternion.LookRotation(transform.right));
                    if (p == null)
                    {
                        walking--;
                        break;
                    }
                    p.WalkTo(away, () => { walking--; manager.Release(p); });
                }
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
                    if (!Yerles(passenger, 0))
                        manager.Release(passenger);
                });
                yield return new WaitForSeconds(interval);
            }

            while (walking > 0 && Time.time < deadline && doors.AnyOpen)
                yield return null;
            IsBusy = false;
        }

        /// <summary>Yolcuya en yakın açık iniş kapısı (ön kapı dışındakiler; hiçbiri açık değilse ön kapı).</summary>
        private int PickExitDoor(Vector3 yakin)
        {
            int enIyi = -1;
            float en = float.MaxValue;
            for (int i = 1; i < doors.DoorCount; i++)
            {
                if (!doors.IsDoorOpen(i))
                    continue;
                float d = (doors.DoorCenter(i) - yakin).sqrMagnitude + Random.Range(0f, 2f);
                if (d < en)
                {
                    en = d;
                    enIyi = i;
                }
            }
            if (enIyi >= 0)
                return enIyi;
            return doors.IsDoorOpen(0) ? 0 : -1;
        }

        // ------------------------------------------------------------ otobüs içi (Y20)

        /// <summary>Kapıdan giren yolcuyu boş koltuğa (yoksa ayakta yere) yürütür. Yer yoksa ya da görünen sayı dolduysa false.</summary>
        private bool Yerles(Passenger p, int door)
        {
            if (koltuklar == null || icerde.Count >= MaxGorunur)
                return false;
            var kapiUzay = Uzay(doors.DoorCenter(door));
            var yol = new List<Passenger.Nokta>();
            KapidanKoridora(yol, door, kapiUzay);
            var koltuk = koltuklar.BosKoltuk(doors.DoorCenter(door), kapiUzay);
            if (koltuk != null)
            {
                koltuk.yolcu = p;
                Gecis(yol, kapiUzay, koltuk.Uzay);
                yol.Add(new Passenger.Nokta(koltuk.Uzay, koltuk.koridor));
                yol.Add(new Passenger.Nokta(koltuk.Uzay, koltuk.on));
                p.transform.SetParent(kapiUzay, true);
                icerde.Add(p);
                var k = koltuk;
                p.Yuru(yol, () => p.Oturt(k.nokta));
                return true;
            }
            var yer = koltuklar.BosAyakta(doors.DoorCenter(door));
            if (yer == null)
                return false;
            yer.yolcu = p;
            Gecis(yol, kapiUzay, yer.Uzay);
            var hedef = yer.Uzay.InverseTransformPoint(yer.nokta.position);
            yol.Add(new Passenger.Nokta(yer.Uzay, new Vector3(0f, hedef.y, hedef.z)));
            p.transform.SetParent(kapiUzay, true);
            icerde.Add(p);
            var y = yer;
            p.Yuru(yol, () => p.Tutunarak(y.nokta));
            return true;
        }

        /// <summary>Koltuğundan/yerinden kapının dışına yol (son nokta dünyada, kapının önünde).</summary>
        private List<Passenger.Nokta> InisYolu(Passenger p, int door)
        {
            var yol = new List<Passenger.Nokta>();
            Transform uzay = transform;
            foreach (var k in koltuklar.Koltuklar)
                if (k.yolcu == p)
                {
                    uzay = k.Uzay;
                    yol.Add(new Passenger.Nokta(uzay, k.on));
                    yol.Add(new Passenger.Nokta(uzay, k.koridor));
                }
            foreach (var a in koltuklar.Ayakta)
                if (a.yolcu == p)
                    uzay = a.Uzay;
            p.transform.SetParent(uzay, true);
            var kapiUzay = Uzay(doors.DoorCenter(door));
            Gecis(yol, uzay, kapiUzay);
            var ters = new List<Passenger.Nokta>();
            KapidanKoridora(ters, door, kapiUzay);
            ters.Reverse();
            yol.AddRange(ters);
            yol.Add(new Passenger.Nokta(null, DoorOutside(door)));
            return yol;
        }

        /// <summary>Kapının iç tarafı ve koridordaki hizası (kapının gövdesinin uzayında).</summary>
        private void KapidanKoridora(List<Passenger.Nokta> yol, int door, Transform uzay)
        {
            var yerel = uzay.InverseTransformPoint(doors.DoorCenter(door));
            float y = koltuklar.KoridorY(uzay, yerel.z);
            float isaret = Mathf.Sign(yerel.x == 0f ? 1f : yerel.x);
            yol.Add(new Passenger.Nokta(uzay, new Vector3(isaret * 0.55f, y, yerel.z)));
            yol.Add(new Passenger.Nokta(uzay, new Vector3(0f, y, yerel.z)));
        }

        /// <summary>Körüklüde bir gövdeden ötekine koridor boyunca mafsalın iki yanından geçiş noktaları.</summary>
        private void Gecis(List<Passenger.Nokta> yol, Transform nereden, Transform nereye)
        {
            if (koruklu == null || koruklu.ArkaGovde == null || nereden == nereye)
                return;
            var arka = koruklu.ArkaGovde.transform;
            float mz = transform.InverseTransformPoint(arka.position).z;    // mafsal (ön gövde uzayında)
            var on = new Passenger.Nokta(transform, new Vector3(0f, koltuklar.KoridorY(transform, mz + 0.7f), mz + 0.7f));
            var geri = new Passenger.Nokta(arka, new Vector3(0f, koltuklar.KoridorY(arka, -0.7f), -0.7f));
            if (nereden == transform)
            {
                yol.Add(on);
                yol.Add(geri);
            }
            else
            {
                yol.Add(geri);
                yol.Add(on);
            }
        }

        /// <summary>Dünya noktasının bulunduğu gövde: körüklüde mafsalın gerisi arka gövde.</summary>
        private Transform Uzay(Vector3 dunya)
        {
            if (koruklu == null || koruklu.ArkaGovde == null)
                return transform;
            var arka = koruklu.ArkaGovde.transform;
            return arka.InverseTransformPoint(dunya).z < 0f ? arka : transform;
        }

        /// <summary>Kapının dışında, yerdeki nokta (kapılar otobüsün sağında).</summary>
        private Vector3 DoorOutside(int door)
        {
            var p = doors.DoorCenter(door) + transform.right * 0.8f;
            p.y = transform.position.y;
            // durak sundurmasının (çarpışma kutusu) içine düşüyorsa açık önüne, otobüsle sundurma arasındaki şeride (Y20)
            return YayaYolu.Disari(p);
        }

        private float Distance(Passenger p) => (p.transform.position - DoorOutside(0)).sqrMagnitude;

        private void SetOnboard(int value)
        {
            onboard = Mathf.Clamp(value, 0, capacity);
            OnboardChanged?.Invoke(onboard);
        }
    }
}
