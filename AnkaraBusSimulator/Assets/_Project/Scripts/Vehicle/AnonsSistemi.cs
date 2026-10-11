using System.Collections;
using AnkaraBus.Route;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüs içi Türkçe durak anonsu: duraktan kalkınca (kapılar kapalı, 12 km/s üstü) gong + "Sıradaki durak: X."
    /// (sıradaki son duraksa "… Son durak."), son durakta "Son durağa geldik…". Klipler Resources/Anonslar/
    /// (tools/ses/anonslar.py): gong, hat_sonu, sonraki_&lt;ad&gt;, sonraki_son_&lt;ad&gt;; ad = AnonsAdi(durak adı).
    /// Klibi olmayan durakta yalnızca gong çalar. İçeride yüksek, dışarıda kısık duyulur (BusAudio.InteriorBlend).
    /// BusVehicle.Awake ekler (rotalı otobüslerde).
    /// </summary>
    public class AnonsSistemi : MonoBehaviour
    {
        private const float KalkisHizi = 12f; // km/s

        private RouteTracker tracker;
        private BusVehicle vehicle;
        private BusDoorController doors;
        private BusAudio busAudio;
        private AudioSource kaynak;
        private AudioClip gong;
        private bool bekleyen;
        private Coroutine calan;

        /// <summary>Anons çalıyor mu (yolcunun "inecek var" sesi üst üste binmesin).</summary>
        public bool Caliyor => calan != null;

        /// <summary>Son çalınan anons (testler için).</summary>
        public string SonAnons { get; private set; }

        public static string AnonsAdi(string durak)
        {
            var sb = new System.Text.StringBuilder();
            bool bosluk = false;
            foreach (char ham in durak.Trim())
            {
                char c = ham switch
                {
                    'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'I' or 'İ' or 'i' => 'i', 'ö' or 'Ö' => 'o',
                    'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u', _ => char.ToLowerInvariant(ham),
                };
                if (char.IsWhiteSpace(c))
                {
                    bosluk = sb.Length > 0;
                    continue;
                }
                if (bosluk)
                    sb.Append('_');
                bosluk = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void Awake()
        {
            tracker = GetComponent<RouteTracker>();
            vehicle = GetComponent<BusVehicle>();
            doors = GetComponentInChildren<BusDoorController>();
            busAudio = GetComponent<BusAudio>();
            kaynak = gameObject.AddComponent<AudioSource>();
            kaynak.playOnAwake = false;
            kaynak.spatialBlend = 0f; // otobüsün hoparlörleri: kameraya göre konumlanmaz, düzeyi içeride/dışarıda ayarlanır
            gong = Resources.Load<AudioClip>("Anonslar/gong");
        }

        private void OnEnable()
        {
            if (tracker == null)
                return;
            tracker.StopServed += DuraktanSonra;
            tracker.StopSkipped += DuraktanSonra;
            tracker.RouteCompleted += HatSonu;
        }

        private void OnDisable()
        {
            if (tracker == null)
                return;
            tracker.StopServed -= DuraktanSonra;
            tracker.StopSkipped -= DuraktanSonra;
            tracker.RouteCompleted -= HatSonu;
        }

        private void DuraktanSonra(BusStop durak) => bekleyen = !tracker.Completed;

        private void HatSonu()
        {
            bekleyen = false;
            Cal(Resources.Load<AudioClip>("Anonslar/hat_sonu"), "hat_sonu");
        }

        private void Update()
        {
            if (busAudio != null)
                kaynak.volume = Mathf.Lerp(0.25f, 0.9f, busAudio.InteriorBlend);
            if (!bekleyen || tracker == null || tracker.Completed || vehicle == null)
                return;
            if (vehicle.SpeedKmh < KalkisHizi || (doors != null && doors.AnyOpen))
                return;
            bekleyen = false;
            var route = tracker.Route;
            var durak = tracker.NextStop;
            if (durak == null)
                return;
            bool son = tracker.NextStopIndex == route.StopCount - 1;
            string ad = (son ? "sonraki_son_" : "sonraki_") + AnonsAdi(durak.StopName);
            Cal(Resources.Load<AudioClip>("Anonslar/" + ad), ad);
        }

        private void Cal(AudioClip klip, string ad)
        {
            if (calan != null)
                StopCoroutine(calan);
            SonAnons = ad;
            Debug.Log($"[Anons] {ad}{(klip == null ? " (klip yok, yalnızca gong)" : "")}");
            calan = StartCoroutine(Oynat(klip));
        }

        private IEnumerator Oynat(AudioClip klip)
        {
            if (gong != null)
            {
                kaynak.PlayOneShot(gong);
                yield return new WaitForSeconds(gong.length * 0.8f);
            }
            if (klip != null)
            {
                kaynak.PlayOneShot(klip);
                yield return new WaitForSeconds(klip.length);
            }
            calan = null;
        }
    }
}
