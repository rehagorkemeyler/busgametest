using AnkaraBus.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 1'i Play modunda otomatik pilotla baştan sona sürer ve durakların tamamlandığını doğrular.
    /// Menü: Ankara Bus > Hat 1 Sürüş Testi (otomatik pilot)
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.SurusTestiEditor.RunBatch (bitince Unity kapanır)
    /// </summary>
    [InitializeOnLoad]
    public static class SurusTestiEditor
    {
        private const string ActiveKey = "AnkaraBus.SurusTesti.Aktif";
        private const string BatchKey = "AnkaraBus.SurusTesti.Batch";
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
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Start(batch: false);
        }

        public static void RunBatch() => Start(batch: true);

        private static void Start(bool batch)
        {
            EditorSceneManager.OpenScene(Hat1SahneKurucu.ScenePath);
            new GameObject("OtomatikPilot").AddComponent<OtomatikPilot>();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(BatchKey, batch);
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
            if (!message.StartsWith("[Surus]"))
            {
                if (type == LogType.Exception)
                    problems++;
                return;
            }
            if (message.Contains("TAKILDI") || message.Contains("tamamlanmadı"))
                problems++;
            if (message.Contains("HAT TAMAMLANDI"))
                Finish(problems == 0, message);
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
            Debug.Log($"[SurusTesti] {(ok ? "BAŞARILI" : "BAŞARISIZ")}: {message}, sorun sayısı {problems}");

            if (batch)
                EditorApplication.Exit(ok ? 0 : 1);
            else
                EditorApplication.ExitPlaymode();
        }
    }
}
