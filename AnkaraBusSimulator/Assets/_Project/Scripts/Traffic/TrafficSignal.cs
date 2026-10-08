using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Kavşak trafik ışığı denetleyicisi. Fazlar sırayla döner: her fazda bazı gruplar yeşil yanar,
    /// fazın sonunda sarı, ardından kısa bir tüm-kırmızı. Şeritler bir gruba ve durma çizgisine bağlanır
    /// (TrafficLane.SetSignal). Işık kafaları "Lamba_Kirmizi/Sari/Yesil" adlı çocuk objeleri açıp kapatır.
    /// </summary>
    public class TrafficSignal : MonoBehaviour
    {
        [Serializable]
        public class Phase
        {
            public int[] greenGroups;
            public float green = 18f;
            public float yellow = 3f;
            public float allRed = 1.5f;
        }

        [Serializable]
        public class Head
        {
            public Transform head;
            public int group;
        }

        public enum SignalState { Red, Yellow, Green }

        [SerializeField] private Phase[] phases;
        [SerializeField] private Head[] heads;

        private int phase;
        private float timer;
        private readonly List<(Head head, GameObject red, GameObject yellow, GameObject green)> lamps =
            new List<(Head, GameObject, GameObject, GameObject)>();

        public void Configure(Phase[] newPhases, Head[] newHeads)
        {
            phases = newPhases;
            heads = newHeads;
        }

        private void Start()
        {
            foreach (var h in heads ?? Array.Empty<Head>())
            {
                if (h.head == null)
                    continue;
                lamps.Add((h, Find(h.head, "Lamba_Kirmizi"), Find(h.head, "Lamba_Sari"), Find(h.head, "Lamba_Yesil")));
            }
            timer = 0f;
            Refresh();
        }

        private void Update()
        {
            if (phases == null || phases.Length == 0)
                return;
            timer += Time.deltaTime;
            var p = phases[phase];
            if (timer >= p.green + p.yellow + p.allRed)
            {
                timer = 0f;
                phase = (phase + 1) % phases.Length;
            }
            Refresh();
        }

        public SignalState State(int group)
        {
            if (phases == null || phases.Length == 0)
                return SignalState.Green;
            var p = phases[phase];
            if (Array.IndexOf(p.greenGroups, group) < 0)
                return SignalState.Red;
            if (timer < p.green)
                return SignalState.Green;
            return timer < p.green + p.yellow ? SignalState.Yellow : SignalState.Red;
        }

        /// <summary>Kırmızıda her zaman, sarıda yalnızca güvenle durabiliyorsa durulur.</summary>
        public bool MustStop(int group, bool canStop)
        {
            var state = State(group);
            return state == SignalState.Red || (state == SignalState.Yellow && canStop);
        }

        private void Refresh()
        {
            foreach (var (head, red, yellow, green) in lamps)
            {
                var state = State(head.group);
                if (red) red.SetActive(state == SignalState.Red);
                if (yellow) yellow.SetActive(state == SignalState.Yellow);
                if (green) green.SetActive(state == SignalState.Green);
            }
        }

        private static GameObject Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(name))
                    return t.gameObject;
            return null;
        }
    }
}
