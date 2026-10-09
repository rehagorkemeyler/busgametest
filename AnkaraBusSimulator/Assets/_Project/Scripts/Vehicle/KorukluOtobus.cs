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
