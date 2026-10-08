using System;
using UnityEngine;

namespace AnkaraBus.Passengers
{
    /// <summary>
    /// Yürüyen/bekleyen yolcu. İskelet animasyonu yok: "Bacak_*" ve "Kol_*" parçaları omuz ve kalçadaki
    /// pivotları etrafında sallanır (tools/blender/yolcu_kit.py).
    /// </summary>
    public class Passenger : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float swingDegrees = 28f;

        private Transform legLeft, legRight, armLeft, armRight;
        private Vector3 target;
        private Action onArrive;
        private float phase;

        public bool IsWalking { get; private set; }

        private void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Bacak_Sol")) legLeft = t;
                else if (t.name.StartsWith("Bacak_Sag")) legRight = t;
                else if (t.name.StartsWith("Kol_Sol")) armLeft = t;
                else if (t.name.StartsWith("Kol_Sag")) armRight = t;
            }
        }

        /// <summary>Hedefe yürür; varınca 'arrived' çağrılır.</summary>
        public void WalkTo(Vector3 destination, Action arrived)
        {
            target = destination;
            onArrive = arrived;
            IsWalking = true;
        }

        /// <summary>Yerinde durur ve verilen yöne döner.</summary>
        public void Stand(Vector3 facing)
        {
            IsWalking = false;
            onArrive = null;
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(facing);
            Pose(0f);
        }

        private void Update()
        {
            if (!IsWalking)
                return;

            var position = transform.position;
            var toTarget = target - position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.15f)
            {
                IsWalking = false;
                Pose(0f);
                var callback = onArrive;
                onArrive = null;
                callback?.Invoke();
                return;
            }

            float step = Mathf.Min(distance, walkSpeed * Time.deltaTime);
            var direction = toTarget / distance;
            position += direction * step;
            position.y = Mathf.Lerp(position.y, target.y, step / distance);
            transform.position = position;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                                                  10f * Time.deltaTime);
            phase += step * 3.4f;
            Pose(Mathf.Sin(phase));
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
