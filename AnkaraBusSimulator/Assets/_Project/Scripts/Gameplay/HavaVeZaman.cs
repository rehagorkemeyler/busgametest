using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Gameplay
{
    /// <summary>
    /// Ana menüde seçilen zamanı (gündüz/akşam/gece) ve yağmuru hat sahnesine uygular. Sahneye eklenmesi gerekmez:
    /// içinde otobüs ve rota olan her sahne yüklenince kendiliğinden oluşur.
    /// <list type="bullet">
    /// <item>Akşam/gece: o sahne için bake edilmiş lightmap ve problar (IsikSeti; sokak lambaları yanık),
    /// güneş/ay, gökyüzü, sis, yanan pencereler (palet malzemesinin gece kopyası), lamba başlarında parlama,
    /// otobüsün farları, park ve iç lambaları; Normal/Yüksek kalitede gerçek zamanlı far ışığı.</item>
    /// <item>Yağmur: kameranın çevresinde damla parçacıkları, ıslak (parlak) yollar ve binalar, gri gök, yoğun sis, yağmur sesi.</item>
    /// <item>Her zaman: fren lambası frene basınca yanar.</item>
    /// </list>
    /// Malzemeler ve yağmur prefabı: Ankara Bus > Hava ve Gece Malzemelerini Kur. Bake: Ankara Bus > Akşam ve Gece Işığını Bake Et.
    /// Ayrıntı: docs/HAVA_ZAMAN.md
    /// </summary>
    public class HavaVeZaman : MonoBehaviour
    {
        public const string KaynakKlasoru = "Hava/";

        private OyunSecimi.Zaman zaman;
        private bool yagmur;
        private ZamanAyarlari.Ayar ayar;
        private BusVehicle otobus;
        private BusCameraRig kamera;
        private GameObject frenLambasi;
        private AudioSource yagmurSesi;
        private Transform yagmurEfekti;
        private Material gokKopyasi;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Baslat()
        {
            SceneManager.sceneLoaded -= SahneYuklendi;
            SceneManager.sceneLoaded += SahneYuklendi;
            SahneYuklendi(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            if (FindAnyObjectByType<HavaVeZaman>() != null || FindAnyObjectByType<RouteTracker>() == null)
                return;
            new GameObject("HavaVeZaman").AddComponent<HavaVeZaman>();
        }

        private void Start()
        {
            zaman = OyunSecimi.SeciliZaman;
            yagmur = OyunSecimi.Yagmur;
            ayar = ZamanAyarlari.Al(zaman);
            var tracker = FindAnyObjectByType<RouteTracker>();
            otobus = tracker != null ? tracker.GetComponent<BusVehicle>() : null;
            kamera = FindAnyObjectByType<BusCameraRig>();
            frenLambasi = Bul(otobus, "Lamba_Fren");

            if (zaman != OyunSecimi.Zaman.Gunduz)
                ZamaniUygula();
            if (yagmur)
                YagmuruUygula();
            if (zaman != OyunSecimi.Zaman.Gunduz || yagmur)
                PaletiDegistir();
        }

        private void OnDestroy()
        {
            if (gokKopyasi != null)
                Destroy(gokKopyasi);
        }

        private void Update()
        {
            if (otobus == null)
                return;
            if (frenLambasi != null)
                frenLambasi.SetActive(otobus.Brake > 0.05f || otobus.BrakePressure > 0.1f);
            if (yagmurSesi != null)
            {
                bool icerde = kamera != null && kamera.IsInside;
                yagmurSesi.volume = Mathf.MoveTowards(yagmurSesi.volume, icerde ? 0.22f : 0.5f, Time.deltaTime);
            }
            if (yagmurEfekti != null && Camera.main != null)
                yagmurEfekti.position = Camera.main.transform.position + Vector3.up * 12f;
        }

        // ------------------------------------------------------------ zaman

        private void ZamaniUygula()
        {
            string sahne = SceneManager.GetActiveScene().name;
            var set = Resources.Load<IsikSeti>(IsikSeti.Yol(sahne, zaman));
            if (set != null)
                set.Uygula();
            else
                Debug.LogWarning($"[HavaVeZaman] {sahne} için {zaman} ışığı bake edilmemiş (Ankara Bus > Akşam ve Gece Işığını Bake Et); " +
                                 "binalar gündüz ışığında kalır.");

            var gunes = RenderSettings.sun;
            if (gunes == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional)
                        gunes = l;
            if (gunes != null)
            {
                gunes.transform.rotation = Quaternion.Euler(ayar.gunesAcisi);
                gunes.color = ayar.gunesRengi;
                gunes.intensity = ayar.gunesSiddeti * (yagmur ? 0.5f : 1f);
                gunes.shadowStrength = ayar.golgeGucu;
            }
            GokyuzuVeSis();
            if (ayar.lamba > 0f)
                LambalariYak();
            if (ayar.farlar)
                OtobusIsiklari();
        }

        private void GokyuzuVeSis()
        {
            if (RenderSettings.skybox != null)
            {
                gokKopyasi = new Material(RenderSettings.skybox);
                if (zaman != OyunSecimi.Zaman.Gunduz)
                    ZamanAyarlari.GokyuzuAyarla(gokKopyasi, ayar, yagmur);
                else
                    ZamanAyarlari.GokyuzuAyarla(gokKopyasi, Gunduz(), yagmur);
                RenderSettings.skybox = gokKopyasi;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            Color sis = zaman == OyunSecimi.Zaman.Gunduz ? new Color(0.72f, 0.76f, 0.8f) : ayar.sisRengi;
            float bas = zaman == OyunSecimi.Zaman.Gunduz ? 250f : ayar.sisBaslangic;
            float bit = zaman == OyunSecimi.Zaman.Gunduz ? 1500f : ayar.sisBitis;
            if (yagmur)
            {
                sis = Color.Lerp(sis, new Color(0.5f, 0.53f, 0.56f) * (zaman == OyunSecimi.Zaman.Gece ? 0.15f : 1f), 0.6f);
                bas = 25f;
                bit = Mathf.Min(bit, 420f);
            }
            RenderSettings.fogColor = sis;
            RenderSettings.fogStartDistance = bas;
            RenderSettings.fogEndDistance = bit;
        }

        private static ZamanAyarlari.Ayar Gunduz() => new ZamanAyarlari.Ayar
        {
            gokRengi = new Color(0.5f, 0.5f, 0.5f), zeminRengi = new Color(0.37f, 0.35f, 0.34f),
            gokParlaklik = 1.3f, atmosfer = 1f, gunesBoyu = 0.04f,
        };

        /// <summary>Sokak lambalarının başlarına tek bir birleşik parlama mesh'i (tek çizim).</summary>
        private void LambalariYak()
        {
            var malzeme = Resources.Load<Material>(KaynakKlasoru + "M_LambaParlama");
            if (malzeme == null)
                return;
            var kaynak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var kure = kaynak.GetComponent<MeshFilter>().sharedMesh;
            Destroy(kaynak);
            var parcalar = new List<CombineInstance>();
            foreach (var grup in Gruplar("Lambalar"))
                foreach (Transform lamba in grup)
                    foreach (var bas in ZamanAyarlari.LambaBaslari)
                        parcalar.Add(new CombineInstance
                        {
                            mesh = kure,
                            transform = lamba.localToWorldMatrix * Matrix4x4.TRS(bas, Quaternion.identity, new Vector3(0.75f, 0.22f, 0.32f)),
                        });
            if (parcalar.Count == 0)
                return;
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "LambaParlamalari" };
            mesh.CombineMeshes(parcalar.ToArray(), true, true);
            var go = new GameObject("LambaParlamalari", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = malzeme;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
        }

        private void OtobusIsiklari()
        {
            if (otobus == null)
                return;
            foreach (var ad in new[] { "Lamba_KisaFar", "Lamba_Park", "Lamba_Ic1", "Lamba_Ic2", "Lamba_Ic3", "Lamba_IcSerit" })
            {
                var lamba = Bul(otobus, ad);
                if (lamba != null)
                    lamba.SetActive(true);
            }
            var bus = otobus.transform;
            // kabin: kokpit ve yolcu kamerasında içerisi karanlık kalmasın
            var ic = new GameObject("IcIsik").AddComponent<Light>();
            ic.transform.SetParent(bus, false);
            ic.transform.localPosition = new Vector3(0f, 2.4f, 0.5f);
            ic.type = LightType.Point;
            ic.range = 9f;
            ic.intensity = 1.6f;
            ic.color = new Color(0.9f, 0.95f, 1f);
            ic.shadows = LightShadows.None;
            // far: Düşük kalitede yalnızca lamba camları yanar (gerçek zamanlı ışık doluluk maliyeti)
            if (GrafikAyarlari.Mevcut == KaliteSeviyesi.Dusuk)
                return;
            var far = new GameObject("Far").AddComponent<Light>();
            far.transform.SetParent(bus, false);
            far.transform.localPosition = new Vector3(0f, 1.0f, 6.2f);
            far.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            far.type = LightType.Spot;
            far.range = 45f;
            far.spotAngle = 75f;
            far.innerSpotAngle = 40f;
            far.intensity = zaman == OyunSecimi.Zaman.Gece ? 3.5f : 1.8f;
            far.color = new Color(1f, 0.96f, 0.88f);
            far.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ yağmur

        private void YagmuruUygula()
        {
            if (zaman == OyunSecimi.Zaman.Gunduz)
            {
                // gündüz yağmuru: güneşi kıs (otobüs ve araçlar), gökyüzü gri
                var gunes = RenderSettings.sun;
                if (gunes != null)
                    gunes.intensity *= 0.45f;
                GokyuzuVeSis();
            }
            var prefab = Resources.Load<GameObject>(KaynakKlasoru + "Yagmur");
            if (prefab != null)
            {
                yagmurEfekti = Instantiate(prefab).transform;
                yagmurEfekti.name = "Yagmur";
            }
            else
                Debug.LogWarning("[HavaVeZaman] Yağmur prefabı yok (Ankara Bus > Hava ve Gece Malzemelerini Kur).");

            yagmurSesi = gameObject.AddComponent<AudioSource>();
            yagmurSesi.clip = YagmurSesiUret();
            yagmurSesi.loop = true;
            yagmurSesi.spatialBlend = 0f;
            yagmurSesi.volume = 0f;
            yagmurSesi.Play();
        }

        /// <summary>4 sn'lik döngüsel yağmur sesi: süzülmüş gürültü + rastgele damla tıkırtıları.</summary>
        private static AudioClip YagmurSesiUret()
        {
            const int hz = 22050;
            int n = hz * 4;
            var veri = new float[n];
            var rnd = new System.Random(7);
            float alcak = 0f, bant = 0f;
            for (int i = 0; i < n; i++)
            {
                float beyaz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                alcak += (beyaz - alcak) * 0.08f;      // hışırtı
                bant += (beyaz - bant) * 0.5f;
                veri[i] = alcak * 0.9f + (bant - alcak) * 0.12f;
            }
            for (int d = 0; d < 900; d++)
            {
                int bas = rnd.Next(n);
                float guc = (float)(0.15 + rnd.NextDouble() * 0.35);
                for (int k = 0; k < 220; k++)
                    veri[(bas + k) % n] += guc * Mathf.Exp(-k / 35f) * (float)(rnd.NextDouble() * 2.0 - 1.0);
            }
            // döngü başı ve sonu yumuşak birleşsin
            int ort = hz / 10;
            for (int i = 0; i < ort; i++)
            {
                float t = i / (float)ort;
                veri[i] = veri[i] * t + veri[n - ort + i] * (1f - t);
            }
            var clip = AudioClip.Create("Yagmur", n - ort, 1, hz, false);
            var kesik = new float[n - ort];
            System.Array.Copy(veri, kesik, kesik.Length);
            clip.SetData(kesik, 0);
            return clip;
        }

        // ------------------------------------------------------------ palet

        /// <summary>
        /// Haritadaki tüm modeller tek palet malzemesini paylaşır. Gece pencereleri yanan, yağmurda parlak (ıslak)
        /// kopyasıyla değiştirilir. Kopyalar Resources'ta hazır durur (çalışma anında anahtar kelime açmak build'de
        /// çıkarılmış shader varyantına takılır).
        /// </summary>
        private void PaletiDegistir()
        {
            bool gece = ayar.lamba > 0f;
            string ad = gece && yagmur ? "M_AnkaraPalet_GeceIslak" : gece ? "M_AnkaraPalet_Gece" : "M_AnkaraPalet_Islak";
            var yeni = Resources.Load<Material>(KaynakKlasoru + ad);
            if (yeni == null)
            {
                Debug.LogWarning($"[HavaVeZaman] {ad} yok (Ankara Bus > Hava ve Gece Malzemelerini Kur).");
                return;
            }
            if (gece && ayar.lamba < 1f)
            {
                // akşam: pencereler daha sönük (kopya, Resources varlığına yazılmasın)
                var kopya = new Material(yeni);
                kopya.SetColor("_EmissionColor", yeni.GetColor("_EmissionColor") * ayar.lamba);
                yeni = kopya;
            }
            foreach (var r in FindObjectsByType<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                bool degisti = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name == "M_AnkaraPalet")
                    {
                        mats[i] = yeni;
                        degisti = true;
                    }
                if (degisti)
                    r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------ yardımcılar

        private static GameObject Bul(Component kok, string ad)
        {
            if (kok == null)
                return null;
            foreach (var t in kok.GetComponentsInChildren<Transform>(true))
                if (t.name == ad)
                    return t.gameObject;
            return null;
        }

        private static IEnumerable<Transform> Gruplar(string ad)
        {
            foreach (var kok in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform cocuk in kok.transform)
                    if (cocuk.name == ad)
                        yield return cocuk;
        }
    }
}
