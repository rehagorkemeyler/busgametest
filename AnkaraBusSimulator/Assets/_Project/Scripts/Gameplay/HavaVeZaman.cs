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

        /// <summary>Akşam/gece otobüs ve trafik araçlarının ışık probu parlaklığı (bkz. IsikSeti.Uygula).</summary>
        public const float GeceProbCarpani = 1.8f, AksamProbCarpani = 1.25f;

        private OyunSecimi.Zaman zaman;
        private bool yagmur;
        private bool kar;
        private AudioSource ruzgarSesi;
        private Transform karEfekti;
        private ZamanAyarlari.Ayar ayar;
        private BusVehicle otobus;
        private Rigidbody otobusGovdesi;
        private BusCameraRig kamera;
        private GameObject frenLambasi;
        private AudioSource yagmurSesi;
        private Transform yagmurEfekti;
        private Material gokKopyasi;
        private Transform dolguIsigi;

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
            kar = OyunSecimi.Kar;
            ayar = ZamanAyarlari.Al(zaman);
            var tracker = FindAnyObjectByType<RouteTracker>();
            otobus = tracker != null ? tracker.GetComponent<BusVehicle>() : null;
            otobusGovdesi = otobus != null ? otobus.GetComponent<Rigidbody>() : null;
            kamera = FindAnyObjectByType<BusCameraRig>();
            frenLambasi = Bul(otobus, "Lamba_Fren");

            if (zaman != OyunSecimi.Zaman.Gunduz)
                ZamaniUygula();
            if (yagmur)
                YagmuruUygula();
            if (kar)
                KariUygula();
            // yol tutuşu: ıslak yolda biraz, karda belirgin kayar (BusVehicle tekerlerin sürtünme sertliğine uygular)
            BusVehicle.HavaTutusu = kar ? 0.6f : yagmur ? 0.85f : 1f;
            if (zaman != OyunSecimi.Zaman.Gunduz || yagmur || kar)
                PaletiDegistir();
        }

        private void OnDestroy()
        {
            if (gokKopyasi != null)
                Destroy(gokKopyasi);
            BusVehicle.HavaTutusu = 1f;
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
            if (ruzgarSesi != null)
            {
                bool icerde = kamera != null && kamera.IsInside;
                ruzgarSesi.volume = Mathf.MoveTowards(ruzgarSesi.volume, icerde ? 0.08f : 0.3f, Time.deltaTime);
            }
            if (dolguIsigi != null && Camera.main != null)
            {
                // kameranın baktığı yöne, 35° yukarıdan: otobüsün görünen yüzü aydınlanır
                var ileri = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
                if (ileri.sqrMagnitude > 1e-4f)
                    dolguIsigi.rotation = Quaternion.LookRotation(ileri.normalized) * Quaternion.Euler(35f, 0f, 0f);
            }
            if (karEfekti != null && Camera.main != null)
            {
                // taneler dünya uzayında ~5 sn'de göz hizasına iner: yayıcı otobüsün gideceği yere kaydırılmazsa
                // sürerken kar geride kalır ve kameranın çevresi boşalır
                var hiz = otobusGovdesi != null ? Vector3.ProjectOnPlane(otobusGovdesi.linearVelocity, Vector3.up) : Vector3.zero;
                karEfekti.position = Camera.main.transform.position + Vector3.up * 10f + Vector3.ClampMagnitude(hiz * 4.5f, 80f);
            }
        }

        // ------------------------------------------------------------ zaman

        private void ZamaniUygula()
        {
            string sahne = SceneManager.GetActiveScene().name;
            var set = Resources.Load<IsikSeti>(IsikSeti.Yol(sahne, zaman));
            if (set != null)
                set.Uygula(zaman == OyunSecimi.Zaman.Gece ? GeceProbCarpani : AksamProbCarpani);
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
                // ana ışık açıkça bu olsun: URP aksi halde en parlak yönlü ışığı seçer, gece otobüs dolgu ışığı (DolguIsigi) olabilirdi
                RenderSettings.sun = gunes;
                gunes.transform.rotation = Quaternion.Euler(ayar.gunesAcisi);
                gunes.color = ayar.gunesRengi;
                gunes.intensity = ayar.gunesSiddeti * (yagmur || kar ? 0.5f : 1f);
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
                    ZamanAyarlari.GokyuzuAyarla(gokKopyasi, ayar, yagmur || kar);
                else
                    ZamanAyarlari.GokyuzuAyarla(gokKopyasi, Gunduz(), yagmur || kar);
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
            if (kar)
            {
                // kar: beyazımsı, yakın sis (gece mavimsi koyu); yağan kar görüşü kapatır
                sis = Color.Lerp(sis, zaman == OyunSecimi.Zaman.Gunduz ? new Color(0.83f, 0.85f, 0.88f)
                    : new Color(0.16f, 0.18f, 0.22f) * (zaman == OyunSecimi.Zaman.Gece ? 0.6f : 1.6f), 0.75f);
                // dış kamera otobüsün 15–21 m gerisinde: sis daha yakında başlarsa otobüs grileşir
                bas = 30f;
                bit = Mathf.Min(bit, 320f);
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
                            transform = lamba.localToWorldMatrix * Matrix4x4.TRS(bas, Quaternion.identity, ZamanAyarlari.LambaParlamaBoyu),
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
            // Conecto'da (tools/blender/conecto_lambalar.py) ayrıca yan camlar, arka gövdenin park lambaları ve tavan şeridi
            foreach (var ad in new[] { "Lamba_KisaFar", "Lamba_Park", "Lamba_Ic1", "Lamba_Ic2", "Lamba_Ic3", "Lamba_IcSerit",
                                       "Lamba_Park_Arka", "Lamba_IcSerit_Arka", "Lamba_Pencere_On", "Lamba_Pencere_Arka" })
            {
                var lamba = Bul(otobus, ad);
                if (lamba != null)
                    lamba.SetActive(true);
            }
            var bus = otobus.transform;
            // kabin: kokpit ve yolcu kamerasında içerisi karanlık kalmasın. Her gövdenin (körüklüde ön ve arka) ortasında, boyuna
            // yetecek menzilde bir ışık: tek ışık önde dururken 18 m'lik körüklünün ortası ve arkası griye dönüyordu.
            var koruklu = otobus.GetComponent<KorukluOtobus>();
            var arka = koruklu != null && koruklu.ArkaGovde != null ? koruklu.ArkaGovde.transform : null;
            IcIsik(bus, arka);
            if (arka != null)
                IcIsik(arka, null);
            DolguIsigi(bus, arka);
            // far: Düşük kalitede yalnızca lamba camları yanar (gerçek zamanlı ışık doluluk maliyeti)
            if (GrafikAyarlari.Mevcut == KaliteSeviyesi.Dusuk)
                return;
            var far = new GameObject("Far").AddComponent<Light>();
            far.transform.SetParent(bus, false);
            // otobüsün ön ucunun hemen önünde (18 m'lik körüklüde eski sabit 6,2 m ışığı gövdenin içine koyuyordu)
            far.transform.localPosition = new Vector3(0f, 1.0f, OtobusOlcusu.Olc(otobus).On + 0.3f);
            far.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            far.type = LightType.Spot;
            far.range = 45f;
            far.spotAngle = 75f;
            far.innerSpotAngle = 40f;
            far.intensity = zaman == OyunSecimi.Zaman.Gece ? 3.5f : 1.8f;
            far.color = new Color(1f, 0.96f, 0.88f);
            far.shadows = LightShadows.None;
        }

        /// <summary>Yalnız otobüsü aydınlatan rendering layer biti (URP Light Layers; üç kalite ayarında açık).</summary>
        public const int OtobusIsikKatmani = 6;

        /// <summary>
        /// Akşam/gece otobüsün dışı seyrek prob ve kısık ay ışığıyla gri kalıyordu (binalar lightmap'le aydınlık). Yalnız
        /// otobüsün renderer'larını aydınlatan, gölgesiz, sıcak bir yönlü ışık: kameranın arkasından ve üstünden gelir,
        /// kaplama rengi (kırmızı/mavi) seçilsin. Binalara ve trafiğe etkisi yok (rendering layer).
        /// </summary>
        private void DolguIsigi(Transform bus, Transform arka)
        {
            uint bit = 1u << OtobusIsikKatmani;
            foreach (var kok in new[] { bus, arka })
                if (kok != null)
                    foreach (var r in kok.GetComponentsInChildren<Renderer>(true))
                        r.renderingLayerMask |= bit;
            var isik = new GameObject("OtobusDolguIsigi").AddComponent<Light>();
            isik.type = LightType.Directional;
            isik.color = new Color(1f, 0.88f, 0.74f);
            isik.intensity = zaman == OyunSecimi.Zaman.Gece ? 0.4f : 0.22f;
            isik.shadows = LightShadows.None;
            isik.renderingLayerMask = (int)bit;
            // URP ışığın katmanını ek ışık verisinden okur: yalnız Light.renderingLayerMask ile binalar da aydınlanıyordu (Y18)
            var urp = isik.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>()
                      ?? isik.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            urp.renderingLayers = bit;
            dolguIsigi = isik.transform;
        }

        /// <summary>Gövdenin çarpışma kutularının boyuna ortasına tavan altında beyaz nokta ışık (gölgesiz; dış yüzler ışığa
        /// sırtını döndüğü için dışarıyı aydınlatmaz).</summary>
        private static void IcIsik(Transform govde, Transform haric)
        {
            float on = float.MinValue, son = float.MaxValue, tavan = 2.8f;
            foreach (var c in govde.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!c.name.StartsWith("Carpisma_") || (haric != null && c.transform.IsChildOf(haric)))
                    continue;
                foreach (float z in new[] { c.center.z - c.size.z * 0.5f, c.center.z + c.size.z * 0.5f })
                {
                    float yerel = govde.InverseTransformPoint(c.transform.TransformPoint(new Vector3(c.center.x, c.center.y, z))).z;
                    on = Mathf.Max(on, yerel);
                    son = Mathf.Min(son, yerel);
                }
                // gövde kökleri farklı yükseklikte (körüklünün arka gövdesi mafsal yüksekliğinde): tavanı kutudan oku
                tavan = govde.InverseTransformPoint(c.transform.TransformPoint(c.center + Vector3.up * c.size.y * 0.5f)).y;
            }
            if (on < son)
            {
                on = 5.9f;
                son = -6.1f;
            }
            float boy = on - son;
            var ic = new GameObject("IcIsik").AddComponent<Light>();
            ic.transform.SetParent(govde, false);
            ic.transform.localPosition = new Vector3(0f, tavan - 0.45f, (on + son) * 0.5f);
            ic.type = LightType.Point;
            ic.range = Mathf.Max(9f, boy * 0.75f);
            ic.intensity = 2f;
            ic.color = new Color(0.92f, 0.96f, 1f);
            ic.shadows = LightShadows.None;
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

        // ------------------------------------------------------------ kar

        private void KariUygula()
        {
            if (zaman == OyunSecimi.Zaman.Gunduz)
            {
                // karlı gündüz: güneş kısık ve gölgeler soluk (kapalı hava), gök beyazımsı gri
                var gunes = RenderSettings.sun;
                if (gunes != null)
                {
                    gunes.intensity *= 0.55f;
                    gunes.shadowStrength *= 0.5f;
                }
                GokyuzuVeSis();
            }
            var prefab = Resources.Load<GameObject>(KaynakKlasoru + "Kar");
            if (prefab != null)
            {
                karEfekti = Instantiate(prefab).transform;
                karEfekti.name = "Kar";
            }
            else
                Debug.LogWarning("[HavaVeZaman] Kar prefabı yok (Ankara Bus > Hava ve Gece Malzemelerini Kur).");

            ruzgarSesi = gameObject.AddComponent<AudioSource>();
            ruzgarSesi.clip = RuzgarSesiUret();
            ruzgarSesi.loop = true;
            ruzgarSesi.spatialBlend = 0f;
            ruzgarSesi.volume = 0f;
            ruzgarSesi.Play();
        }

        /// <summary>8 sn'lik döngüsel hafif rüzgâr: süzülmüş gürültü, yavaşça kabarıp sönen (karlı havanın uğultusu).</summary>
        private static AudioClip RuzgarSesiUret()
        {
            const int hz = 22050;
            int n = hz * 8;
            var veri = new float[n];
            var rnd = new System.Random(11);
            float d1 = 0f, d2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float g = (float)(rnd.NextDouble() * 2.0 - 1.0);
                d1 += 0.02f * (g - d1);          // alçak geçiren: uğultu
                d2 += 0.004f * (d1 - d2);
                float t = (float)i / n;
                // döngü başı ve sonu aynı olsun diye zarf sinüslerden
                float zarf = 0.55f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f) + 0.15f * Mathf.Sin(t * Mathf.PI * 6f + 1f);
                veri[i] = (d1 - d2) * 6f * zarf;
            }
            // uçları yumuşat (döngü tıkırtısız)
            int yum = hz / 10;
            for (int i = 0; i < yum; i++)
            {
                float k = (float)i / yum;
                veri[i] *= k;
                veri[n - 1 - i] *= k;
            }
            var clip = AudioClip.Create("Ruzgar", n, 1, hz, false);
            clip.SetData(veri, 0);
            return clip;
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
            string ad = kar ? (gece ? "M_AnkaraPalet_GeceKarli" : "M_AnkaraPalet_Karli")
                : gece && yagmur ? "M_AnkaraPalet_GeceIslak" : gece ? "M_AnkaraPalet_Gece" : "M_AnkaraPalet_Islak";
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
