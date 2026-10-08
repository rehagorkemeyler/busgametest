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
    /// Sürüş testi build'i Hat 1 ve Hat 2'yi içerir; OtomatikPilot eklenir, Hat 1 bitince Hat 2'ye geçer.
    /// Menü: Ankara Bus > Android > Performans APK'sı / Sürüş Testi APK'sı
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.AndroidBuild.PerfBuild (veya DriveTestBuild)
    /// </summary>
    public static class AndroidBuild
    {
        public const string PerfApkPath = "Builds/AnkaraBus_perf.apk";
        public const string DriveTestApkPath = "Builds/AnkaraBus_surus_testi.apk";

        private enum Mode { None, Perf, DriveTest, SesPuan, MenuKamera }

        public const string MenuKameraApkPath = "Builds/AnkaraBus_menu_kamera_testi.apk";

        /// <summary>Oyun APK'sı + menüde MenuKameraTesti; sonuçlar logcat'te "[MenuTest]".</summary>
        [MenuItem("Ankara Bus/Android/Menü ve Kamera Testi APK'sı")]
        public static void MenuKameraBuild() => Build(Mode.MenuKamera, OyunSahneleri, MenuKameraApkPath);

        public const string OyunApkPath = "Builds/AnkaraBus.apk";
        private static readonly string[] OyunSahneleri = { AnaMenuKurucu.ScenePath, Hat1SahneKurucu.ScenePath, Hat2SahneKurucu.ScenePath };

        /// <summary>Oynanabilir build: ana menü + Hat 1 + Hat 2, ölçüm ya da otomatik pilot eklenmez.</summary>
        [MenuItem("Ankara Bus/Android/Oyun APK'sı")]
        public static void OyunBuild() => Build(Mode.None, OyunSahneleri, OyunApkPath);

        public const string SesPuanApkPath = "Builds/AnkaraBus_ses_puan_testi.apk";

        /// <summary>Ses ve puan senaryosu (SesPuanSenaryosu) Hat 1'de; sonuçlar logcat'te "[SesPuan]".</summary>
        [MenuItem("Ankara Bus/Android/Ses ve Puan Testi APK'sı")]
        public static void SesPuanBuild() => Build(Mode.SesPuan, new[] { Hat1SahneKurucu.ScenePath }, SesPuanApkPath);

        private static readonly string[] PerfScenes = { TestPistiKurucu.ScenePath, Hat1SahneKurucu.ScenePath };

        private static Mode mode;

        [MenuItem("Ankara Bus/Android/Performans APK'sı")]
        public static void PerfBuildMenu() => PerfBuild();

        public static void PerfBuild() => Build(Mode.Perf, PerfScenes, PerfApkPath);

        // Ses ve puan sisteminin maliyeti: Düşük kalitede, sistemler açık / kapalı iki APK
        private static int forceQuality = -1;
        private static bool stripAudioAndScore;

        public static void PerfBuildDusukSesli() => BuildPerfVariant(false, "Builds/AnkaraBus_perf_dusuk_sesli.apk");

        public static void PerfBuildDusukSessiz() => BuildPerfVariant(true, "Builds/AnkaraBus_perf_dusuk_sessiz.apk");

        private static void BuildPerfVariant(bool strip, string apkPath)
        {
            forceQuality = (int)KaliteSeviyesi.Dusuk;
            stripAudioAndScore = strip;
            try
            {
                Build(Mode.Perf, PerfScenes, apkPath);
            }
            finally
            {
                forceQuality = -1;
                stripAudioAndScore = false;
            }
        }

        [MenuItem("Ankara Bus/Android/Sürüş Testi APK'sı")]
        public static void DriveTestBuildMenu() => DriveTestBuild();

        private static readonly string[] DriveTestScenes = { Hat1SahneKurucu.ScenePath, Hat2SahneKurucu.ScenePath };

        public static void DriveTestBuild() => Build(Mode.DriveTest, DriveTestScenes, DriveTestApkPath);

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
                // ölçüm ve test build'leri Development (profiler sayaçları); oyun APK'sı normal build
                options = buildMode == Mode.None ? BuildOptions.None : BuildOptions.Development,
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

                if (mode == Mode.MenuKamera)
                {
                    if (scene.path == AnaMenuKurucu.ScenePath)
                    {
                        var test = new GameObject("MenuKameraTesti");
                        SceneManager.MoveGameObjectToScene(test, scene);
                        test.AddComponent<MenuKameraTesti>();
                    }
                    return;
                }

                if (mode == Mode.SesPuan)
                {
                    var senaryo = new GameObject("SesPuanSenaryosu");
                    SceneManager.MoveGameObjectToScene(senaryo, scene);
                    senaryo.AddComponent<SesPuanSenaryosu>();
                    return;
                }

                if (mode == Mode.DriveTest)
                {
                    var pilot = new GameObject("OtomatikPilot");
                    SceneManager.MoveGameObjectToScene(pilot, scene);
                    int i = System.Array.IndexOf(DriveTestScenes, scene.path);
                    string next = i >= 0 && i + 1 < DriveTestScenes.Length
                        ? Path.GetFileNameWithoutExtension(DriveTestScenes[i + 1]) : null;
                    pilot.AddComponent<OtomatikPilot>().Configure(null, next);
                    return;
                }

                int index = System.Array.IndexOf(PerfScenes, scene.path);
                if (index < 0)
                    return;

                if (stripAudioAndScore)
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var c in root.GetComponentsInChildren<AnkaraBus.UI.PuanGostergesi>(true)) Object.DestroyImmediate(c);
                        foreach (var c in root.GetComponentsInChildren<AnkaraBus.Gameplay.SeferPuanlama>(true)) Object.DestroyImmediate(c);
                        foreach (var c in root.GetComponentsInChildren<AnkaraBus.Vehicle.BusAudio>(true)) Object.DestroyImmediate(c);
                    }

                var go = new GameObject("PerfBenchmark");
                SceneManager.MoveGameObjectToScene(go, scene);
                var benchmark = go.AddComponent<PerfBenchmark>();
                var so = new SerializedObject(benchmark);
                so.FindProperty("label").stringValue = scene.name;
                so.FindProperty("zorlaKalite").intValue = forceQuality;
                so.FindProperty("nextScene").stringValue =
                    index + 1 < PerfScenes.Length ? Path.GetFileNameWithoutExtension(PerfScenes[index + 1]) : "";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
