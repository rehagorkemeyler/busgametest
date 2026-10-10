using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Körüklü (iki gövdeli) otobüs. Ön gövde otobüsün kökü (BusVehicle'ın Rigidbody'si); arka gövde ayrı bir Rigidbody,
    /// mafsalda ConfigurableJoint ile öne bağlı ve çeken aks onda (BusVehicle tekerlekleri iki gövdeye dağıtır).
    /// Prefabda arka gövde kökün çocuğudur; oyun başlayınca sahne köküne alınır (iç içe Rigidbody olmasın).
    /// Görseller kökteki Model altında kalır (kalite modeli ve kaplama böyle çalışır): arka gövde parçaları her karede
    /// arka Rigidbody'nin pozuna taşınır, körük mafsal açısının yarısı kadar döner.
    /// Kurulum: Editor/OtobusKurucu (Mercedes Conecto). Ayrıntı: docs/OTOBUSLER.md
    /// </summary>
    [RequireComponent(typeof(BusVehicle))]
    public class KorukluOtobus : MonoBehaviour
    {
        [SerializeField] private Rigidbody arkaGovde;
        [Tooltip("Arka gövdeyle birlikte hareket eden görseller (Govde_Arka, Golge_Arka).")]
        [SerializeField] private Transform[] arkaGorseller = new Transform[0];
        [SerializeField] private Transform koruk;
        [Tooltip("Arka gövdenin ağırlık merkezi (arka gövde yerel; kök mafsalda).")]
        [SerializeField] private Vector3 arkaAgirlikMerkezi = new Vector3(0f, 0.65f, -3.3f);

        // fırlama koruması: mafsal noktası bu kadar ayrılırsa ya da arka gövde ön gövdeden bu kadar hızlı giderse
        // (çözücü patlaması: içine doğduğu bir engel, ışınlama) arka gövde ön gövdenin arkasına düz oturtulur
        private const float AzamiAyrilma = 0.6f;
        private const float AzamiHizFarki = 15f;
        private const float AzamiHiz = 40f;   // 144 km/s: otobüs 85 km/s'yi geçmez, üstü fizik hatasıdır

        private Rigidbody onGovde;
        private ConfigurableJoint mafsal;
        private Vector3[] gorselKonum;
        private Quaternion[] gorselDonus;
        private Quaternion korukDinlenme;
        private bool ayrildi;

        public Rigidbody ArkaGovde => arkaGovde;
        /// <summary>Arka gövdenin çarpışmaları (ön gövdeninkiler kökün OnCollisionEnter'ına gelir; puanlama ikisini de dinler).</summary>
        public event System.Action<Collision> ArkaCarpti;
        /// <summary>Mafsal açısı (derece, sağa kırılınca +).</summary>
        public float MafsalAcisi { get; private set; }

        private void Awake()
        {
            if (arkaGovde == null)
                return;
            // görsellerin arka gövdeye göre duruşu (prefabda düz dururken)
            var arka = arkaGovde.transform;
            gorselKonum = new Vector3[arkaGorseller.Length];
            gorselDonus = new Quaternion[arkaGorseller.Length];
            for (int i = 0; i < arkaGorseller.Length; i++)
            {
                if (arkaGorseller[i] == null)
                    continue;
                gorselKonum[i] = arka.InverseTransformPoint(arkaGorseller[i].position);
                gorselDonus[i] = Quaternion.Inverse(arka.rotation) * arkaGorseller[i].rotation;
            }
            if (koruk != null)
                korukDinlenme = Quaternion.Inverse(transform.rotation) * koruk.rotation;

            onGovde = GetComponent<Rigidbody>();
            mafsal = arkaGovde.GetComponent<ConfigurableJoint>();
            foreach (var rb in new[] { onGovde, arkaGovde })
            {
                if (rb == null)
                    continue;
                // mafsallı iki gövde: daha çok çözücü adımı, yumuşak itme (içine girilen engelden fırlatmasın), hız sınırı
                rb.solverIterations = Mathf.Max(rb.solverIterations, 12);
                rb.solverVelocityIterations = Mathf.Max(rb.solverVelocityIterations, 4);
                rb.maxDepenetrationVelocity = 2f;
                rb.maxLinearVelocity = AzamiHiz;
            }
            arkaGovde.interpolation = RigidbodyInterpolation.Interpolate;
            arkaGovde.centerOfMass = arkaAgirlikMerkezi;
            arkaGovde.angularDamping = 0.05f;
            arkaGovde.gameObject.AddComponent<CarpismaAktarici>().sahip = this;
            arka.SetParent(null, true);
            if (gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(arka.gameObject, gameObject.scene);
            ayrildi = true;
        }

        private void OnEnable()
        {
            if (ayrildi && arkaGovde != null)
                arkaGovde.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (ayrildi && arkaGovde != null)
                arkaGovde.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ayrildi && arkaGovde != null)
                Destroy(arkaGovde.gameObject);
        }

        /// <summary>Mafsalın ön gövdedeki noktası (dünya).</summary>
        private Vector3 MafsalNoktasi => mafsal != null ? transform.TransformPoint(mafsal.connectedAnchor) : arkaGovde.position;

        private void FixedUpdate()
        {
            if (arkaGovde == null || onGovde == null || !ayrildi)
                return;
            Vector3 nokta = MafsalNoktasi;
            float ayrilma = (arkaGovde.position - nokta).magnitude;
            float hizFarki = (arkaGovde.linearVelocity - onGovde.GetPointVelocity(nokta)).magnitude;
            if (ayrilma > AzamiAyrilma || hizFarki > AzamiHizFarki)
            {
                Debug.LogWarning($"[Koruklu] mafsal ayrıldı ({ayrilma:F2} m, hız farkı {hizFarki:F1} m/s): arka gövde yeniden oturtuldu");
                ArkayiHizala();
            }
        }

        /// <summary>
        /// Arka gövdeyi ön gövdenin arkasına düz (mafsal 0°) yerleştirir, hızını ön gövdeninkine eşitler.
        /// Otobüs ışınlanınca (testler) ve mafsal koptuğunda çağrılır.
        /// </summary>
        public void ArkayiHizala()
        {
            if (arkaGovde == null)
                return;
            Vector3 nokta = MafsalNoktasi;
            arkaGovde.position = nokta;
            arkaGovde.rotation = transform.rotation;
            arkaGovde.transform.SetPositionAndRotation(nokta, transform.rotation);
            arkaGovde.linearVelocity = onGovde != null ? onGovde.GetPointVelocity(nokta) : Vector3.zero;
            arkaGovde.angularVelocity = onGovde != null ? onGovde.angularVelocity : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (arkaGovde == null)
                return;
            var arka = arkaGovde.transform;
            for (int i = 0; i < arkaGorseller.Length; i++)
                if (arkaGorseller[i] != null)
                    arkaGorseller[i].SetPositionAndRotation(arka.TransformPoint(gorselKonum[i]), arka.rotation * gorselDonus[i]);

            var bagil = Quaternion.Inverse(transform.rotation) * arka.rotation;
            MafsalAcisi = Mathf.DeltaAngle(0f, bagil.eulerAngles.y);
            if (koruk != null)
                koruk.rotation = transform.rotation * Quaternion.Slerp(Quaternion.identity, bagil, 0.5f) * korukDinlenme;
        }

        /// <summary>Arka gövdenin objesinde: çarpışmayı KorukluOtobus'a iletir.</summary>
        private sealed class CarpismaAktarici : MonoBehaviour
        {
            public KorukluOtobus sahip;

            private void OnCollisionEnter(Collision c)
            {
                if (sahip != null)
                    sahip.ArkaCarpti?.Invoke(c);
            }
        }
    }
}
