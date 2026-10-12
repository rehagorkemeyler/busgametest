using AnkaraBus.Diagnostics;
using AnkaraBus.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Y18: Conecto prefabını kurar; kapı (KapiTesti) ve gece (GeceTesti) testlerini Editor'de Play modunda Hat 1'de koşar.
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.Y18Kurulum.TestBatch -gorev kapi|gece -zaman Gunduz|Aksam|Gece
    /// -hava Acik|Kar -kalite 0|1|2. Görüntüler Builds/Y18, bitince Unity kapanır.
    /// </summary>
    public static class Y18Kurulum
    {
        public static void ConectoBatch()
        {
            OtobusKurucu.BuildConecto();
            EditorApplication.Exit(0);
        }

        private static double baslangic;

        public static void TestBatch()
        {
            string Arg(string ad, string vars)
            {
                var a = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(a, ad);
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : vars;
            }
            Y10Baslatici.Gorev = Arg("-gorev", "kapi");
            OyunSecimi.SeciliZaman = System.Enum.Parse<OyunSecimi.Zaman>(Arg("-zaman", "Gunduz"));
            OyunSecimi.SeciliHava = System.Enum.Parse<OyunSecimi.Hava>(Arg("-hava", "Acik"));
            OyunSecimi.Otobus = 2;
            OyunSecimi.Kaplama = 0;
            GrafikAyarlari.Uygula((KaliteSeviyesi)int.Parse(Arg("-kalite", "1")), kaydet: true);
            EditorSceneManager.OpenScene(Hat1SahneKurucu.ScenePath);
            new GameObject("Y18Test").AddComponent<Y11Hat>();
            SceneManager.sceneLoaded += (s, m) =>
            {
                if (Object.FindAnyObjectByType<Y11Hat>() == null)
                    new GameObject("Y18Test").AddComponent<Y11Hat>();
            };
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (msg.EndsWith("BITTI hepsi"))
                    EditorApplication.Exit(0);
                else if (type == LogType.Exception)
                    Debug.Log("[Y18] istisna: " + msg + "\n" + stack);
            };
            baslangic = EditorApplication.timeSinceStartup;
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup - baslangic > 900d)
                {
                    Debug.Log("[Y18] zaman aşımı");
                    EditorApplication.Exit(1);
                }
            };
            EditorApplication.EnterPlaymode();
        }
    }
}
