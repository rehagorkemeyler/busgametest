using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Yürüyen/bekleyen/oturan yolcu.
    /// <list type="bullet">
    /// <item>Quaternius karakterleri (Y20, Resources/Yolcular; YolcuKurucu): Animator'da "Bekle", "Yuru", "Otur", "Tutun" durumları.</item>
    /// <item>Eski parçalı modeller (tools/blender/yolcu_kit.py): Animator yok, "Bacak_*" ve "Kol_*" parçaları sallanır.</item>
    /// </list>
    /// Yol noktaları bir uzaya (Transform) göre verilebilir: otobüsün içinde yürüyen yolcu otobüse bağlıdır, otobüs kalkarsa
    /// onunla gider (BusPassengers).
    /// </summary>
    public class Passenger : MonoBehaviour
    {
        /// <summary>Bir uzaya (null: dünya) göre yol noktası.</summary>
        public struct Nokta
        {
            public Transform uzay;
            public Vector3 yerel;

            public Nokta(Transform uzay, Vector3 yerel)
            {
                this.uzay = uzay;
                this.yerel = yerel;
            }

            public Vector3 Dunya => uzay != null ? uzay.TransformPoint(yerel) : yerel;
        }

        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float swingDegrees = 28f;
        [Tooltip("Animatörlü modelde yürüme klibinin hızı bu yürüme hızına göre ölçeklenir (m/s).")]
        [SerializeField] private float klipHizi = 1.3f;

        private static readonly int DBekle = Animator.StringToHash("Bekle");
        private static readonly int DYuru = Animator.StringToHash("Yuru");
        private static readonly int DOtur = Animator.StringToHash("Otur");
        private static readonly int DTutun = Animator.StringToHash("Tutun");

        private Animator animator;
        private int durum;
        private Transform legLeft, legRight, armLeft, armRight;
        private readonly Queue<Nokta> yol = new Queue<Nokta>();
        private Action onArrive;
        private float phase;

        public bool IsWalking { get; private set; }
        public bool Oturuyor { get; private set; }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Bacak_Sol")) legLeft = t;
                else if (t.name.StartsWith("Bacak_Sag")) legRight = t;
                else if (t.name.StartsWith("Kol_Sol")) armLeft = t;
                else if (t.name.StartsWith("Kol_Sag")) armRight = t;
            }
        }

        private void OnEnable()
        {
            durum = 0;
            Oyna(DBekle, 0f);
        }

        /// <summary>Hedefe (dünya) yürür; varınca 'arrived' çağrılır.</summary>
        public void WalkTo(Vector3 destination, Action arrived) => Yuru(new[] { new Nokta(null, destination) }, arrived);

        /// <summary>Noktaları sırayla yürür; sonuncusuna varınca 'arrived' çağrılır.</summary>
        public void Yuru(IEnumerable<Nokta> noktalar, Action arrived)
        {
            yol.Clear();
            foreach (var n in noktalar)
                yol.Enqueue(n);
            onArrive = arrived;
            Oturuyor = false;
            IsWalking = yol.Count > 0;
            if (IsWalking)
                Oyna(DYuru, 0.2f);
        }

        /// <summary>Yerinde durur ve verilen yöne (dünya) döner.</summary>
        public void Stand(Vector3 facing)
        {
            Durdur();
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(facing);
            Oyna(DBekle, 0.2f);
        }

        /// <summary>Koltuğa oturur: koltuk noktasının çocuğu olur (kök noktada, noktanın yönüne bakar).</summary>
        public void Oturt(Transform koltuk)
        {
            Durdur();
            transform.SetParent(koltuk, true);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Oturuyor = true;
            Oyna(DOtur, 0.25f);
        }

        /// <summary>Ayakta, tavan borusuna tutunarak durur (noktanın çocuğu olur).</summary>
        public void Tutunarak(Transform yer)
        {
            Durdur();
            transform.SetParent(yer, true);
            transform.localPosition = Vector3.zero;
            Oyna(DTutun, 0.25f);
        }

        private void Durdur()
        {
            IsWalking = false;
            Oturuyor = false;
            onArrive = null;
            yol.Clear();
            Pose(0f);
        }

        private void Oyna(int ad, float gecis)
        {
            if (animator == null || animator.runtimeAnimatorController == null || durum == ad)
                return;
            durum = ad;
            animator.speed = 1f;
            if (gecis > 0f && animator.isActiveAndEnabled)
                animator.CrossFadeInFixedTime(ad, gecis);
            else
                animator.Play(ad);
        }

        private void Update()
        {
            if (!IsWalking)
                return;
            if (yol.Count == 0)
            {
                Var();
                return;
            }

            var hedef = yol.Peek().Dunya;
            var position = transform.position;
            var toTarget = hedef - position;
            var duz = new Vector3(toTarget.x, 0f, toTarget.z);
            float distance = duz.magnitude;
            if (distance < 0.12f)
            {
                transform.position = new Vector3(position.x, hedef.y, position.z);
                yol.Dequeue();
                if (yol.Count == 0)
                    Var();
                return;
            }

            float step = Mathf.Min(distance, walkSpeed * Time.deltaTime);
            var direction = duz / distance;
            position += direction * step;
            position.y = Mathf.Lerp(position.y, hedef.y, Mathf.Clamp01(step / distance));
            transform.position = position;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
            if (animator != null)
                animator.speed = walkSpeed / Mathf.Max(klipHizi, 0.1f);
            phase += step * 3.4f;
            Pose(Mathf.Sin(phase));
        }

        private void Var()
        {
            IsWalking = false;
            Pose(0f);
            Oyna(DBekle, 0.2f);
            var callback = onArrive;
            onArrive = null;
            callback?.Invoke();
        }

        private void Pose(float swing)
        {
            float a = swing * swingDegrees;
            if (legLeft) legLeft.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (legRight) legRight.localRotation = Quaternion.Euler(-a, 0f, 0f);
            if (armLeft) armLeft.localRotation = Quaternion.Euler(-a * 0.8f, 0f, 0f);
            if (armRight) armRight.localRotation = Quaternion.Euler(a * 0.8f, 0f, 0f);
        }
    }
}
