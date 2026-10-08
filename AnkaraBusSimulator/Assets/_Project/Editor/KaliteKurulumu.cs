using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Düşük / Normal / Yüksek kalite seviyelerini ve her birinin URP ayar dosyasını kurar.
    /// Değerler telefonda ölçülen ayarlardır (docs/PERFORMANS.md):
    /// Yüksek = Galaxy S24 FE ölçümündeki ayar, Düşük = Galaxy A32'de denenen optimize ayar.
    /// ProjectSetup.Apply içinden çağrılır.
    /// </summary>
    public static class KaliteKurulumu
    {
        private const string SourceAsset = "Assets/Settings/Mobile_RPAsset.asset";

        private struct Seviye
        {
            public string ad;
            public string asset;
            public float renderScale;
            public int msaa;
        }

        private static readonly Seviye[] Seviyeler =
        {
            new Seviye { ad = GrafikAyarlari.SeviyeAdlari[0], asset = "Assets/Settings/URP_Dusuk.asset", renderScale = 0.6f, msaa = 1 },
            new Seviye { ad = GrafikAyarlari.SeviyeAdlari[1], asset = "Assets/Settings/URP_Normal.asset", renderScale = 0.7f, msaa = 1 },
            new Seviye { ad = GrafikAyarlari.SeviyeAdlari[2], asset = "Assets/Settings/URP_Yuksek.asset", renderScale = 0.8f, msaa = 2 },
        };

        public static void Kur()
        {
            var assets = new UniversalRenderPipelineAsset[Seviyeler.Length];
            for (int i = 0; i < Seviyeler.Length; i++)
                assets[i] = PipelineAsset(Seviyeler[i]);

            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            // Mevcut ilk seviye (şablonun "Mobile" ayarı) temel alınır; ölçümler onunla yapıldı
            levels.arraySize = 1;
            levels.arraySize = Seviyeler.Length;
            for (int i = 0; i < Seviyeler.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = Seviyeler[i].ad;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                level.FindPropertyRelative("excludedTargetPlatforms").arraySize = 0;
                level.FindPropertyRelative("vSyncCount").intValue = 0;
            }

            // Varsayılan: Normal. Oyun açılınca GrafikAyarlari cihaza göre seçer.
            so.FindProperty("m_CurrentQuality").intValue = 1;
            var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
                defaults.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static UniversalRenderPipelineAsset PipelineAsset(Seviye seviye)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(seviye.asset);
            if (asset == null)
            {
                AssetDatabase.CopyAsset(SourceAsset, seviye.asset);
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(seviye.asset);
            }

            // Ölçülen ortak ayarlar: gölge 60 m, 1024 çözünürlük, tek kademe, yumuşak gölge yok, HDR yok
            var so = new SerializedObject(asset);
            so.FindProperty("m_RenderScale").floatValue = seviye.renderScale;
            so.FindProperty("m_MSAA").intValue = seviye.msaa;
            so.FindProperty("m_ShadowDistance").floatValue = 60f;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = 1024;
            so.FindProperty("m_ShadowCascadeCount").intValue = 1;
            so.FindProperty("m_SoftShadowsSupported").boolValue = false;
            so.FindProperty("m_SupportsHDR").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
