using UnityEngine;

namespace AnkaraBus
{
    /// <summary>
    /// Oyun açılırken bir kez uygulanan çalışma zamanı ayarları.
    /// </summary>
    public static class OyunAyarlari
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Mobilde varsayılan sınır 30 FPS
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
