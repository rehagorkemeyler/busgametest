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
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private double cpuSum, gpuSum;
        private int timingSamples;
        private int samples;

        public PerfProbe()
        {
            counters = new[]
            {
                // Unity 6'da draw call'lar türe göre ayrı sayaçlarda; "draw" bunların toplamı
                ("draw_srp", ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Draw Calls Count")),
                ("draw_std", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Draw Calls Count")),
                ("draw_inst", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Instanced Draw Calls Count")),
                ("golge", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count")),
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
            cpuSum = gpuSum = 0d;
            timingSamples = 0;
            samples = 0;
        }

        public void Sample()
        {
            frameTimes.Add(Time.unscaledDeltaTime);

            // Gerçek CPU/GPU süresi: 60 FPS sınırına takılsa bile ne kadar pay kaldığını gösterir
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0d)
            {
                cpuSum += timing[0].cpuMainThreadFrameTime;
                gpuSum += timing[0].gpuFrameTime;
                timingSamples++;
            }
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
            if (timingSamples > 0)
                line.Append(" cpu_ms=").Append((cpuSum / timingSamples).ToString("F1", ci))
                    .Append(" gpu_ms=").Append((gpuSum / timingSamples).ToString("F1", ci));
            double draws = 0d;
            for (int i = 0; i < counters.Length; i++)
                if (counters[i].name.StartsWith("draw_"))
                    draws += sums[i] / Mathf.Max(1, samples);
            line.Append(" draw=").Append(draws.ToString("F0", ci));
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
