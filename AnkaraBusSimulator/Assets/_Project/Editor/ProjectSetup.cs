using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Aksiyon planı Faz 0'daki editör ve Android ayarlarını tek tıkla uygular.
    /// Menü: Ankara Bus > Proje Ayarlarını Uygula
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Ankara Bus/Proje Ayarlarını Uygula")]
        public static void Apply()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            AssetDatabase.SaveAssets();
            Debug.Log("Ankara Bus: Force Text, Visible Meta Files, Android (IL2CPP, ARM64, API 26, Vulkan + GLES3) uygulandı.");
        }
    }
}
