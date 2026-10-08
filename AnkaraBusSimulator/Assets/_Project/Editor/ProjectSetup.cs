using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Faz 0 proje ayarlarını tek seferde uygular. Menüden ya da komut satırından
    /// (-executeMethod AnkaraBus.EditorTools.ProjectSetup.Apply) çalıştırılabilir.
    /// </summary>
    public static class ProjectSetup
    {

        [MenuItem("Ankara Bus/Proje Ayarlarını Uygula")]
        public static void Apply()
        {
            // Sürüm kontrolü
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            // Uygulama kimliği
            PlayerSettings.companyName = "AnkaraBus";
            PlayerSettings.productName = "Ankara Bus Simulator";

            // Android oynatıcı ayarları
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.ankarabus.simulator");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // Yalnızca yatay ekran
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Düşük / Normal / Yüksek kalite seviyeleri
            KaliteKurulumu.Kur();

            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                if (!switched)
                    Debug.LogWarning("[ProjectSetup] Android'e geçilemedi. Unity Hub'dan Android Build Support modülünü kur.");
            }

            Debug.Log("[ProjectSetup] Ayarlar uygulandı.");
        }
    }
}
