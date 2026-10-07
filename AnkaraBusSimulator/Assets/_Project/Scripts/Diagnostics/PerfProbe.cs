using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Bir ölçüm aralığında kare sürelerini ve render/bellek sayaçlarını toplar.
    /// Render sayaçları yalnızca Development build'de dolar.
    /// </summary>
    public sealed class PerfProbe : IDisposable
    {
        private readonly List<float> frameTimes = new List<float>(2048);
        private readonly (string name, ProfilerRecorder recorder)[] counters;
        private readonly double[] sums;
        private int samples;

        public PerfProbe()
        {
            counters = new[]
            {
                ("draw", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count")),
                ("batch", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count")),
                ("setpass", ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count")),
                ("ucgen", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count")),
                ("bellek_mb", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory")),
                ("gfx_mb", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory")),
                ("sistem_mb", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory")),
            };
            sums = new double[counters.Length];
        }

        public void Reset()
        {
            frameTimes.Clear();
            Array.Clear(sums, 0, sums.Length);
            samples = 0;
        }

        public void Sample()
        {
            frameTimes.Add(Time.unscaledDeltaTime);
            for (int i = 0; i < counters.Length; i++)
                sums[i] += counters[i].recorder.Valid ? counters[i].recorder.LastValue : 0;
            samples++;
        }

        /// <summary>Tek satırlık, logcat'ten ayıklanabilir rapor.</summary>
        public string Report(string label)
        {
            if (frameTimes.Count == 0)
                return $"[Perf] {label} veri yok";

            var sorted = new List<float>(frameTimes);
            sorted.Sort();
            float total = 0f;
            foreach (var t in frameTimes) total += t;
            float avgFps = frameTimes.Count / total;
            float p95 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))] * 1000f;
            float low1 = 1f / sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];

            var ci = CultureInfo.InvariantCulture;
            var line = new System.Text.StringBuilder();
            line.Append("[Perf] ").Append(label)
                .Append(" fps=").Append(avgFps.ToString("F1", ci))
                .Append(" fps_1low=").Append(low1.ToString("F1", ci))
                .Append(" kare_p95_ms=").Append(p95.ToString("F1", ci));
            for (int i = 0; i < counters.Length; i++)
            {
                if (!counters[i].recorder.Valid)
                    continue;
                double avg = sums[i] / Mathf.Max(1, samples);
                if (counters[i].name.EndsWith("_mb"))
                    avg /= 1024d * 1024d;
                line.Append(' ').Append(counters[i].name).Append('=').Append(avg.ToString("F0", ci));
            }
            return line.ToString();
        }

        public void Dispose()
        {
            foreach (var c in counters)
                c.recorder.Dispose();
        }
    }
}
