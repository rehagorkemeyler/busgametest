using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 1 sahnesinin ışığını ve occlusion culling'ini bake eder.
    /// Işık: Mixed güneş, Subtractive mod. Bina ve yol gölgeleri lightmap'e işlenir; gerçek zamanlı gölgeyi
    /// yalnızca hareketli objeler (otobüs, trafik) verir. Mobilde gölge maliyetini otobüse indirir.
    /// Menü: Ankara Bus > Hat 1 Işık ve Occlusion Bake
    /// </summary>
    public static class Hat1IsikBake
    {
        private const string MapsFolder = "Assets/_Project/Maps";
        private const string LightingSettingsPath = "Assets/_Project/Scenes/Hat1_Isik.lighting";

        [MenuItem("Ankara Bus/Hat 1 Işık ve Occlusion Bake")]
        public static void BakeMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Bake();
        }

        public static void BakeBatch()
        {
            bool ok = Bake();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Bake()
        {
            EnableLightmapUVs();

            var scene = EditorSceneManager.OpenScene(Hat1SahneKurucu.ScenePath);
            SetupSun();
            SetupLightmapScales();
            SetupLightProbes();
            Lightmapping.lightingSettings = LightingSettingsAsset();

            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool baked = Lightmapping.Bake();
            Debug.Log($"[Hat1Bake] Işık bake {(baked ? "tamam" : "BAŞARISIZ")}, {watch.Elapsed.TotalMinutes:F1} dk, " +
                      $"{LightmapSettings.lightmaps.Length} lightmap");

            // Occlusion: yalnızca binalar ve simge yapılar örter; ağaç, lamba, yol yalnızca örtülür
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if (flags == 0)
                    continue;
                bool occluder = IsUnder(r.transform, "Binalar") || IsUnder(r.transform, "SimgeYapilar");
                flags = occluder ? flags | StaticEditorFlags.OccluderStatic : flags & ~StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.OccludeeStatic);
            }
            StaticOcclusionCulling.smallestOccluder = 6f;
            StaticOcclusionCulling.smallestHole = 0.3f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            watch.Restart();
            bool occlusion = StaticOcclusionCulling.Compute();
            Debug.Log($"[Hat1Bake] Occlusion bake {(occlusion ? "tamam" : "BAŞARISIZ")}, {watch.Elapsed.TotalMinutes:F1} dk");

            EditorSceneManager.SaveScene(scene);
            return baked && occlusion;
        }

        /// <summary>Harita FBX'lerinde Generate Lightmap UVs (docs/MODEL_KITLERI.md).</summary>
        private static void EnableLightmapUVs()
        {
            var changed = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { MapsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.generateSecondaryUV)
                {
                    importer.generateSecondaryUV = true;
                    changed.Add(path);
                }
            }
            if (changed.Count == 0)
                return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in changed)
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                foreach (var path in changed)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[Hat1Bake] {changed.Count} modelde lightmap UV açıldı");
        }

        private static void SetupSun()
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional)
                    sun = light;
            if (sun == null)
                sun = new GameObject("Directional Light").AddComponent<Light>();

            // Ankara, ekim öğleden sonra: güneş güney-batıdan, ~40° yükseklikte
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, 35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.bounceIntensity = 1f;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.47f, 0.56f);
        }

        /// <summary>
        /// Lightmap alanını önemli yere ayır: gölgenin en çok göründüğü yol tam çözünürlük,
        /// binalar daha düşük, geniş zemin ve küçük objeler çok düşük.
        /// </summary>
        private static void SetupLightmapScales()
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                if ((GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) == 0)
                    continue;
                float scale = 1f;
                if (IsUnder(r.transform, "Zemin")) scale = 0.08f;
                else if (IsUnder(r.transform, "Agaclar") || IsUnder(r.transform, "Lambalar")) scale = 0.2f;
                else if (IsUnder(r.transform, "Binalar") || IsUnder(r.transform, "SimgeYapilar")) scale = 0.45f;
                var so = new SerializedObject(r);
                so.FindProperty("m_ScaleInLightmap").floatValue = scale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Otobüs ve trafik için yol boyunca ışık probları (şerit noktaları üzerinde, iki yükseklikte).</summary>
        private static void SetupLightProbes()
        {
            var existing = GameObject.Find("IsikProblari");
            if (existing != null)
                Object.DestroyImmediate(existing);

            var positions = new List<Vector3>();
            foreach (var lane in Object.FindObjectsByType<AnkaraBus.Traffic.TrafficLane>())
            {
                if (!lane.name.Contains("Gidis_1"))
                    continue;
                for (float d = 0f; d <= lane.Length; d += 25f)
                {
                    lane.Sample(d, out var p, out var forward);
                    var right = Vector3.Cross(Vector3.up, forward).normalized;
                    foreach (float side in new[] { -12f, 12f })
                    foreach (float h in new[] { 1f, 5f })
                        positions.Add(p + right * side + Vector3.up * h);
                }
            }
            var group = new GameObject("IsikProblari").AddComponent<LightProbeGroup>();
            group.probePositions = positions.ToArray();
        }

        private static LightingSettings LightingSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.Subtractive;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 2f;
            settings.lightmapPadding = 4;
            settings.lightmapMaxSize = 2048;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.ao = false;
            settings.maxBounces = 1;
            settings.directSampleCount = 16;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 128;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static bool IsUnder(Transform t, string groupName)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name == groupName)
                    return true;
            return false;
        }
    }
}
