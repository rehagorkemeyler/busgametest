using System.IO;
using AnkaraBus.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Android APK build'leri. Performans build'i Development modunda alınır ve her sahneye
    /// build sırasında PerfBenchmark eklenir (sahne dosyaları değişmez): TestTrack bitince Hat 1 yüklenir.
    /// Menü: Ankara Bus > Android > Performans APK'sı
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.AndroidBuild.PerfBuild
    /// </summary>
    public static class AndroidBuild
    {
        public const string PerfApkPath = "Builds/AnkaraBus_perf.apk";

        private static readonly string[] PerfScenes = { TestPistiKurucu.ScenePath, Hat1SahneKurucu.ScenePath };

        internal static bool PerfMode { get; private set; }

        [MenuItem("Ankara Bus/Android/Performans APK'sı")]
        public static void PerfBuildMenu() => PerfBuild();

        public static void PerfBuild()
        {
            ProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PerfApkPath));

            var options = new BuildPlayerOptions
            {
                scenes = PerfScenes,
                locationPathName = PerfApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            PlayerSettings.enableFrameTimingStats = true;
            PerfMode = true;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PerfMode = false;
            }

            var summary = report.summary;
            Debug.Log($"[AndroidBuild] {summary.result} {summary.outputPath} " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalMinutes:F1} dk, " +
                      $"{summary.totalErrors} hata");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Performans build'inde sahnelere ölçüm turunu ekler.</summary>
        private class PerfInjector : IProcessSceneWithReport
        {
            public int callbackOrder => 0;

            public void OnProcessScene(Scene scene, BuildReport report)
            {
                if (!PerfMode || report == null)
                    return;

                int index = System.Array.IndexOf(PerfScenes, scene.path);
                if (index < 0)
                    return;

                var go = new GameObject("PerfBenchmark");
                SceneManager.MoveGameObjectToScene(go, scene);
                var benchmark = go.AddComponent<PerfBenchmark>();
                var so = new SerializedObject(benchmark);
                so.FindProperty("label").stringValue = scene.name;
                so.FindProperty("nextScene").stringValue =
                    index + 1 < PerfScenes.Length ? Path.GetFileNameWithoutExtension(PerfScenes[index + 1]) : "";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
