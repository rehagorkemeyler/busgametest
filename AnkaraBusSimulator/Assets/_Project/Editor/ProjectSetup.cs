using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Faz 0 proje ayarlarını tek seferde uygular. Menüden ya da komut satırından
    /// (-executeMethod AnkaraBus.EditorTools.ProjectSetup.Apply) çalıştırılabilir.
    /// </summary>
    public static class ProjectSetup
    {
        private const string MobileRpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Ankara Bus/Proje Ayarlarını Uygula")]
        public static void Apply()
        {
            // Sürüm kontrolü
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            // Android oynatıcı ayarları
            var android = NamedBuildTarget.Android;
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

            // Mobil URP: gölge ~60 m, MSAA 2x, HDR kapalı
            var mobileRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpAssetPath);
            if (mobileRp != null)
            {
                mobileRp.shadowDistance = 60f;
                mobileRp.msaaSampleCount = 2;
                mobileRp.supportsHDR = false;
                EditorUtility.SetDirty(mobileRp);
            }
            else
            {
                Debug.LogWarning($"[ProjectSetup] {MobileRpAssetPath} bulunamadı.");
            }

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
