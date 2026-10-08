using System;
using System.Collections.Generic;
using AnkaraBus.Traffic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Route
{
    /// <summary>
    /// Yol gösterici: hattın güzergâhını sahnedeki trafik şeritlerinden (TrafficLane, çıkışlarıyla) kendisi bulur:
    /// otobüsün başlangıcından her durağa en kısa şerit yolu (Dijkstra). Sonra her karede otobüsün güzergâhtaki
    /// yerini, sıradaki durağa kalan mesafeyi, yaklaşan dönüşü ve rotadan çıkılıp çıkılmadığını verir.
    /// Ekran: UI/MiniHarita. Ayrıntı: docs/YOL_GOSTERICI.md
    /// </summary>
    [RequireComponent(typeof(RouteTracker))]
    public class RotaRehberi : MonoBehaviour
    {
        public enum Donus { Yok, Sag, Sol }

        [Tooltip("Şerit örnekleme aralığı (m).")]
        [SerializeField] private float adim = 5f;
        [Tooltip("Bu kadar ilerideki dönüşler bildirilir (m).")]
        [SerializeField] private float donusUzakligi = 220f;
        [Tooltip("15 m'lik pencerede bu açıdan büyük yön değişimi dönüş sayılır (derece).")]
        [SerializeField] private float donusAcisi = 40f;
        [Tooltip("Güzergâha bu kadar uzaklaşınca 'rotadan çıktın' (m).")]
        [SerializeField] private float sapmaSiniri = 25f;

        private RouteTracker tracker;
        private readonly List<Vector3> yol = new List<Vector3>();
        private float[] kumulatif = Array.Empty<float>();
        private float[] durakKonumu = Array.Empty<float>();
        private float s;

        /// <summary>Güzergâh noktaları (dünya). Bulunamazsa duraklar arası düz çizgi.</summary>
        public IReadOnlyList<Vector3> Yol => yol;
        public bool Hazir => yol.Count >= 2;
        /// <summary>Otobüsün güzergâh boyunca konumu (m).</summary>
        public float Konum => s;
        public float Uzunluk => kumulatif.Length > 0 ? kumulatif[kumulatif.Length - 1] : 0f;
        public float SapmaMesafesi { get; private set; }
        public bool RotadanCikti => SapmaMesafesi > sapmaSiniri;
        /// <summary>Sıradaki durağa güzergâh boyunca kalan mesafe (m); durak yoksa -1.</summary>
        public float DurakaKalan { get; private set; } = -1f;
        public Donus SiradakiDonus { get; private set; }
        public float DonuseKalan { get; private set; }
        public RouteTracker Tracker => tracker;

        /// <summary>
        /// Otobüs prefabı yeniden kurulmamış olsa da (OtobusKurucu ekler) yol gösterici çalışsın: rotası olan otobüse
        /// sahne yüklenince RotaRehberi ve MiniHarita eklenir.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Baslat()
        {
            SceneManager.sceneLoaded -= SahneYuklendi;
            SceneManager.sceneLoaded += SahneYuklendi;
            SahneYuklendi(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            foreach (var t in FindObjectsByType<RouteTracker>())
            {
                if (t.Route == null || t.GetComponent<RotaRehberi>() != null)
                    continue;
                t.gameObject.AddComponent<RotaRehberi>();
                if (t.GetComponent<AnkaraBus.UI.MiniHarita>() == null)
                    t.gameObject.AddComponent<AnkaraBus.UI.MiniHarita>();
            }
        }

        private void Awake() => tracker = GetComponent<RouteTracker>();

        private void Start() => Kur();

        /// <summary>Güzergâhı yeniden hesaplar (rota değişince çağrılabilir).</summary>
        public void Kur()
        {
            yol.Clear();
            var route = tracker.Route;
            if (route == null || route.StopCount == 0)
                return;

            var graf = new SeritGrafi(FindObjectsByType<TrafficLane>(), adim);
            Vector3 onceki = transform.position;
            Vector3 oncekiYon = transform.forward;
            yol.Add(onceki);
            for (int i = 0; i < route.StopCount; i++)
            {
                var stop = route.GetStop(i);
                if (stop == null)
                    continue;
                var parca = graf.Yol(onceki, oncekiYon, stop.transform.position, stop.transform.forward);
                if (parca != null)
                    yol.AddRange(parca);
                yol.Add(stop.transform.position);
                onceki = stop.transform.position;
                oncekiYon = stop.transform.forward;
            }
            Sadelestir();

            kumulatif = new float[yol.Count];
            for (int i = 1; i < yol.Count; i++)
                kumulatif[i] = kumulatif[i - 1] + Yatay(yol[i] - yol[i - 1]).magnitude;
            durakKonumu = new float[route.StopCount];
            float ara = 0f;
            for (int i = 0; i < route.StopCount; i++)
            {
                var stop = route.GetStop(i);
                durakKonumu[i] = stop != null ? Izdusur(stop.transform.position, ara, float.MaxValue, out _) : ara;
                ara = durakKonumu[i];
            }
            s = Izdusur(transform.position, 0f, 200f, out _);
        }

        /// <summary>i. durağın güzergâhtaki konumu (m).</summary>
        public float DurakKonumu(int i) => i >= 0 && i < durakKonumu.Length ? durakKonumu[i] : -1f;

        private void Update()
        {
            if (!Hazir)
                return;
            // önce son konumun çevresinde ara; çok uzaklaştıysa (ör. geri dönüp başka yoldan) tüm güzergâhta
            float yeni = Izdusur(transform.position, s - 80f, s + 120f, out float sapma);
            if (sapma > sapmaSiniri)
            {
                float tum = Izdusur(transform.position, 0f, float.MaxValue, out float tumSapma);
                if (tumSapma < sapma)
                {
                    yeni = tum;
                    sapma = tumSapma;
                }
            }
            s = yeni;
            SapmaMesafesi = sapma;

            int sira = tracker.NextStopIndex;
            DurakaKalan = sira < durakKonumu.Length && !tracker.Completed ? Mathf.Max(0f, durakKonumu[sira] - s) : -1f;
            DonusBul();
        }

        private void DonusBul()
        {
            SiradakiDonus = Donus.Yok;
            DonuseKalan = 0f;
            const float pencere = 15f;
            for (float d = pencere; d <= donusUzakligi; d += 5f)
            {
                Vector3 a = Nokta(s + d - pencere), b = Nokta(s + d), c = Nokta(s + d + pencere);
                Vector3 g = Yatay(b - a), h = Yatay(c - b);
                if (g.sqrMagnitude < 1f || h.sqrMagnitude < 1f)
                    continue;
                float aci = Vector3.SignedAngle(g, h, Vector3.up);
                if (Mathf.Abs(aci) >= donusAcisi)
                {
                    SiradakiDonus = aci > 0f ? Donus.Sag : Donus.Sol;
                    DonuseKalan = d;
                    return;
                }
            }
        }

        /// <summary>Güzergâhta 'konum' metredeki nokta.</summary>
        public Vector3 Nokta(float konum)
        {
            if (!Hazir)
                return transform.position;
            konum = Mathf.Clamp(konum, 0f, Uzunluk);
            int i = Array.BinarySearch(kumulatif, konum);
            if (i >= 0)
                return yol[i];
            i = ~i;
            if (i <= 0)
                return yol[0];
            if (i >= yol.Count)
                return yol[yol.Count - 1];
            float t = Mathf.InverseLerp(kumulatif[i - 1], kumulatif[i], konum);
            return Vector3.Lerp(yol[i - 1], yol[i], t);
        }

        /// <summary>p noktasının [bas, son] aralığındaki en yakın güzergâh konumu ve yatay uzaklığı.</summary>
        private float Izdusur(Vector3 p, float bas, float son, out float uzaklik)
        {
            uzaklik = float.MaxValue;
            float enIyi = bas;
            for (int i = 1; i < yol.Count; i++)
            {
                if (kumulatif.Length == yol.Count && (kumulatif[i] < bas || kumulatif[i - 1] > son))
                    continue;
                Vector3 a = Yatay(yol[i - 1]), b = Yatay(yol[i]), q = Yatay(p);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - q).magnitude;
                if (d < uzaklik)
                {
                    uzaklik = d;
                    enIyi = kumulatif.Length == yol.Count ? Mathf.Lerp(kumulatif[i - 1], kumulatif[i], t) : 0f;
                }
            }
            return enIyi;
        }

        /// <summary>Art arda çok yakın noktaları atar.</summary>
        private void Sadelestir()
        {
            for (int i = yol.Count - 2; i > 0; i--)
                if (Yatay(yol[i] - yol[i - 1]).sqrMagnitude < 1f)
                    yol.RemoveAt(i);
        }

        private static Vector3 Yatay(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Şeritlerin 'adim' aralıklı noktalarından yönlü grafik: şerit boyunca, çıkışlar ve yan şeride geçiş.</summary>
        private class SeritGrafi
        {
            private readonly List<Vector3> nokta = new List<Vector3>();
            private readonly List<Vector3> yon = new List<Vector3>();
            private readonly List<List<(int to, float w)>> kenar = new List<List<(int, float)>>();

            public SeritGrafi(TrafficLane[] seritler, float adim)
            {
                var bas = new Dictionary<TrafficLane, (int ilk, int adet)>();
                foreach (var serit in seritler)
                {
                    if (serit.Length < 1f)
                        continue;
                    int adet = Mathf.Max(2, Mathf.CeilToInt(serit.Length / adim) + 1);
                    int ilk = nokta.Count;
                    for (int k = 0; k < adet; k++)
                    {
                        serit.Sample(Mathf.Min(k * adim, serit.Length), out var p, out var f);
                        nokta.Add(p);
                        yon.Add(Yatay(f).normalized);
                        kenar.Add(new List<(int, float)>());
                        if (k > 0)
                            kenar[ilk + k - 1].Add((ilk + k, Vector3.Distance(nokta[ilk + k - 1], p)));
                    }
                    bas[serit] = (ilk, adet);
                }
                // kavşak dönüşleri
                foreach (var serit in seritler)
                {
                    if (!bas.TryGetValue(serit, out var kaynak))
                        continue;
                    foreach (var cikis in serit.Exits)
                    {
                        if (cikis.target == null || !bas.TryGetValue(cikis.target, out var hedef))
                            continue;
                        int a = kaynak.ilk + Mathf.Clamp(Mathf.RoundToInt(cikis.at / adim), 0, kaynak.adet - 1);
                        int b = hedef.ilk + Mathf.Clamp(Mathf.RoundToInt(cikis.targetAt / adim), 0, hedef.adet - 1);
                        kenar[a].Add((b, Vector3.Distance(nokta[a], nokta[b]) + 1f));
                    }
                }
                // aynı yöndeki yan şeride geçiş (ileriye, küçük cezayla)
                var izgara = new Dictionary<Vector2Int, List<int>>();
                for (int i = 0; i < nokta.Count; i++)
                {
                    var hucre = Hucre(nokta[i]);
                    if (!izgara.TryGetValue(hucre, out var liste))
                        izgara[hucre] = liste = new List<int>();
                    liste.Add(i);
                }
                for (int i = 0; i < nokta.Count; i++)
                {
                    var h = Hucre(nokta[i]);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!izgara.TryGetValue(new Vector2Int(h.x + dx, h.y + dz), out var liste))
                            continue;
                        foreach (int j in liste)
                        {
                            if (j == i || Vector3.Dot(yon[i], yon[j]) < 0.95f)
                                continue;
                            Vector3 d = Yatay(nokta[j] - nokta[i]);
                            float ileri = Vector3.Dot(d, yon[i]);
                            float yan = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, yon[i])));
                            if (ileri > 4f && ileri < 16f && yan > 1f && yan < 4.5f)
                                kenar[i].Add((j, d.magnitude + 8f));
                        }
                    }
                }
            }

            private static Vector2Int Hucre(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / 16f), Mathf.FloorToInt(p.z / 16f));

            /// <summary>'bas' noktasından (yönü 'basYon') 'hedef' noktasına (yönü 'hedefYon') şerit yolu; bulunamazsa null.</summary>
            public List<Vector3> Yol(Vector3 bas, Vector3 basYon, Vector3 hedef, Vector3 hedefYon)
            {
                int kaynak = EnYakin(bas, Yatay(basYon).normalized);
                int varis = EnYakin(hedef, Yatay(hedefYon).normalized);
                if (kaynak < 0 || varis < 0)
                    return null;

                var mesafe = new float[nokta.Count];
                var onceki = new int[nokta.Count];
                for (int i = 0; i < mesafe.Length; i++)
                {
                    mesafe[i] = float.MaxValue;
                    onceki[i] = -1;
                }
                mesafe[kaynak] = 0f;
                var kuyruk = new SortedSet<(float d, int i)>();
                kuyruk.Add((0f, kaynak));
                while (kuyruk.Count > 0)
                {
                    var (d, u) = kuyruk.Min;
                    kuyruk.Remove(kuyruk.Min);
                    if (u == varis)
                        break;
                    foreach (var (v, w) in kenar[u])
                    {
                        float yeni = d + w;
                        if (yeni >= mesafe[v])
                            continue;
                        if (mesafe[v] < float.MaxValue)
                            kuyruk.Remove((mesafe[v], v));
                        mesafe[v] = yeni;
                        onceki[v] = u;
                        kuyruk.Add((yeni, v));
                    }
                }
                if (mesafe[varis] == float.MaxValue)
                    return null;
                var sonuc = new List<Vector3>();
                for (int i = varis; i >= 0; i = onceki[i])
                    sonuc.Add(nokta[i]);
                sonuc.Reverse();
                return sonuc;
            }

            /// <summary>Yönü uyan (aynı yöne giden şerit) en yakın nokta; 40 m'den uzaksa -1.</summary>
            private int EnYakin(Vector3 p, Vector3 istenenYon)
            {
                int enIyi = -1;
                float enIyiD = 40f * 40f;
                for (int i = 0; i < nokta.Count; i++)
                {
                    if (istenenYon.sqrMagnitude > 0.1f && Vector3.Dot(yon[i], istenenYon) < 0.5f)
                        continue;
                    float d = Yatay(nokta[i] - p).sqrMagnitude;
                    if (d < enIyiD)
                    {
                        enIyiD = d;
                        enIyi = i;
                    }
                }
                return enIyi;
            }
        }
    }
}
