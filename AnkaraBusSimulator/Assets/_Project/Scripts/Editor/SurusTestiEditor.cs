using AnkaraBus.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Bir hattı Play modunda otomatik pilotla baştan sona sürer ve durakların tamamlandığını doğrular.
    /// Menü: Ankara Bus > Hat 1 / Hat 2 Sürüş Testi (otomatik pilot), Ses ve Puan Testi (Hat 1)
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.SurusTestiEditor.RunHat1Batch (veya RunHat2Batch);
    /// bitince Unity kapanır
    /// </summary>
    [InitializeOnLoad]
    public static class SurusTestiEditor
    {
        private const string ActiveKey = "AnkaraBus.SurusTesti.Aktif";
        private const string BatchKey = "AnkaraBus.SurusTesti.Batch";
        private const string EndKey = "AnkaraBus.SurusTesti.Bitis";
        private const double TimeoutSeconds = 1200d;

        private static double startTime;
        private static int problems;

        static SurusTestiEditor()
        {
            // Play'e girerken domain yeniden yüklenir; test sürüyorsa dinlemeye devam et
            if (SessionState.GetBool(ActiveKey, false))
                Hook();
        }

        [MenuItem("Ankara Bus/Hat 1 Sürüş Testi (otomatik pilot)")]
        public static void RunHat1Menu() => RunMenu(Hat1SahneKurucu.ScenePath);

        [MenuItem("Ankara Bus/Hat 2 Sürüş Testi (otomatik pilot)")]
        public static void RunHat2Menu() => RunMenu(Hat2SahneKurucu.ScenePath);

        public static void RunHat1Batch() => Start(Hat1SahneKurucu.ScenePath, batch: true);

        public static void RunHat2Batch() => Start(Hat2SahneKurucu.ScenePath, batch: true);

        [MenuItem("Ankara Bus/Ses ve Puan Testi (Hat 1)")]
        public static void RunSesPuanMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Start(Hat1SahneKurucu.ScenePath, batch: false, sesPuan: true);
        }

        public static void RunSesPuanBatch() => Start(Hat1SahneKurucu.ScenePath, batch: true, sesPuan: true);

        private static void RunMenu(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Start(scenePath, batch: false);
        }

        private static void Start(string scenePath, bool batch, bool sesPuan = false)
        {
            EditorSceneManager.OpenScene(scenePath);
            if (sesPuan)
                new GameObject("SesPuanSenaryosu").AddComponent<SesPuanSenaryosu>();
            else
                new GameObject("OtomatikPilot").AddComponent<OtomatikPilot>();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetString(EndKey, sesPuan ? "[SesPuan] BITTI" : "[Surus] BITTI");
            Hook();
            EditorApplication.EnterPlaymode();
        }

        private static void Hook()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            startTime = EditorApplication.timeSinceStartup;
            problems = 0;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            string end = SessionState.GetString(EndKey, "[Surus] BITTI");
            if (message.StartsWith(end))
            {
                Finish(problems == 0, message);
                return;
            }
            if (!message.StartsWith("[Surus]"))
            {
                if (type == LogType.Exception)
                    problems++;
                return;
            }
            if (message.Contains("TAKILDI") || message.Contains("tamamlanmadı"))
                problems++;
        }

        private static void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup - startTime > TimeoutSeconds)
                Finish(false, $"[SurusTesti] Zaman aşımı ({TimeoutSeconds / 60d:F0} dk)");
        }

        private static void Finish(bool ok, string message)
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(BatchKey);
            SessionState.EraseString(EndKey);
            Debug.Log($"[SurusTesti] {(ok ? "BAŞARILI" : "BAŞARISIZ")}: {message}, sorun sayısı {problems}");

            if (batch)
                EditorApplication.Exit(ok ? 0 : 1);
            else
                EditorApplication.ExitPlaymode();
        }
    }
}
