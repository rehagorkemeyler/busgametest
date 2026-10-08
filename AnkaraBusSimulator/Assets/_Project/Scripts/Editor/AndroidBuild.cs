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
    /// Sürüş testi build'i yalnızca Hat 1'i içerir; OtomatikPilot eklenir ve hattı baştan sona sürer.
    /// Menü: Ankara Bus > Android > Performans APK'sı / Sürüş Testi APK'sı
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.AndroidBuild.PerfBuild (veya DriveTestBuild)
    /// </summary>
    public static class AndroidBuild
    {
        public const string PerfApkPath = "Builds/AnkaraBus_perf.apk";
        public const string DriveTestApkPath = "Builds/AnkaraBus_surus_testi.apk";

        private enum Mode { None, Perf, DriveTest }

        private static readonly string[] PerfScenes = { TestPistiKurucu.ScenePath, Hat1SahneKurucu.ScenePath };

        private static Mode mode;

        [MenuItem("Ankara Bus/Android/Performans APK'sı")]
        public static void PerfBuildMenu() => PerfBuild();

        public static void PerfBuild() => Build(Mode.Perf, PerfScenes, PerfApkPath);

        [MenuItem("Ankara Bus/Android/Sürüş Testi APK'sı")]
        public static void DriveTestBuildMenu() => DriveTestBuild();

        public static void DriveTestBuild() => Build(Mode.DriveTest, new[] { Hat1SahneKurucu.ScenePath }, DriveTestApkPath);

        private static void Build(Mode buildMode, string[] scenes, string apkPath)
        {
            ProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(apkPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            PlayerSettings.enableFrameTimingStats = true;
            mode = buildMode;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                mode = Mode.None;
            }

            var summary = report.summary;
            Debug.Log($"[AndroidBuild] {summary.result} {summary.outputPath} " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalMinutes:F1} dk, " +
                      $"{summary.totalErrors} hata");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Test build'lerinde sahnelere ölçüm turunu veya otomatik pilotu ekler.</summary>
        private class PerfInjector : IProcessSceneWithReport
        {
            public int callbackOrder => 0;

            public void OnProcessScene(Scene scene, BuildReport report)
            {
                if (mode == Mode.None || report == null)
                    return;

                if (mode == Mode.DriveTest)
                {
                    var pilot = new GameObject("OtomatikPilot");
                    SceneManager.MoveGameObjectToScene(pilot, scene);
                    pilot.AddComponent<OtomatikPilot>();
                    return;
                }

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
