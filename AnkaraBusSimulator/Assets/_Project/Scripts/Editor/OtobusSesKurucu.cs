using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// BMC Procity seslerini içe aktarma ayarlarına göre hazırlar ve otobüs prefabına BusAudio ekleyip doldurur.
    /// OtobusKurucu prefabı her kurduğunda bunu da çağırır; yalnızca sesleri yenilemek için menüden çalıştırılabilir.
    /// Menü: Ankara Bus > Otobüs Seslerini Kur. Ayrıntı: docs/SESLER.md
    /// </summary>
    public static class OtobusSesKurucu
    {
        private const string SoundFolder = "Assets/_Project/Buses/BMC_Procity_12LF/Sounds/";

        // (dosya, kaydedildiği devir, kokpit seti mi, yük katmanı mı, ses düzeyi)
        private static readonly (string file, float rpm, bool interior, bool load, float volume)[] EngineLayers =
        {
            // Düzeyler kayıtların RMS'ine göre eşitlendi: devirle hafifçe (~6 dB) yükselen düzgün bir eğri
            ("D_2566UH@575", 575f, true, false, 0.4f),
            ("D_2566UH@758", 758f, true, false, 0.73f),
            ("D_2566UHx@1214", 1214f, true, false, 0.23f),
            ("D_2566UH@1939", 1939f, true, false, 0.83f),
            ("D_2566UH@2230", 2230f, true, false, 0.31f),
            ("D_2566UHx@722", 722f, false, false, 1f),
            ("D_2566UHx@1214", 1214f, false, false, 0.23f),
            ("D_2566UHx@1653", 1653f, false, false, 0.81f),
            ("D_2566UHx@2296", 2296f, false, false, 0.36f),
            ("D_2566UHx@1280_Last", 1280f, false, true, 0.45f),
        };

        // döngüsel ya da 3B çalınan sesler mono yapılır (3B konumlandırma tek kanal ister, bellek de yarıya iner)
        private static readonly string[] StereoAllowed = { "D_2566UH_ein", "D_2566UH_aus", "IBIS_piep", "stop" };

        [MenuItem("Ankara Bus/Otobüs Seslerini Kur")]
        public static void Build()
        {
            ConfigureImporters();
            var root = PrefabUtility.LoadPrefabContents(OtobusKurucu.PrefabPath);
            try
            {
                Setup(root);
                PrefabUtility.SaveAsPrefabAsset(root, OtobusKurucu.PrefabPath);
                Debug.Log("[OtobusSesKurucu] Sesler kuruldu: " + OtobusKurucu.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Ankara Bus/Otobüs Seslerini Kur", true)]
        private static bool Validate() => AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath) != null;

        /// <summary>Otobüs kökünde BusAudio yoksa ekler ve klipleri bağlar.</summary>
        public static void Setup(GameObject root)
        {
            var audio = root.GetComponent<BusAudio>();
            if (audio == null)
                audio = root.AddComponent<BusAudio>();
            var so = new SerializedObject(audio);

            var engine = so.FindProperty("engine");
            engine.arraySize = EngineLayers.Length;
            for (int i = 0; i < EngineLayers.Length; i++)
            {
                var l = EngineLayers[i];
                var e = engine.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("clip").objectReferenceValue = Clip(l.file);
                e.FindPropertyRelative("nativeRpm").floatValue = l.rpm;
                e.FindPropertyRelative("interior").boolValue = l.interior;
                e.FindPropertyRelative("load").boolValue = l.load;
                e.FindPropertyRelative("volume").floatValue = l.volume;
            }

            Set(so, "engineStart", "D_2566UH_ein");
            Set(so, "engineStop", "D_2566UH_aus");
            Set(so, "retarder", "D_Retarder_257");
            Set(so, "transmission", "Voith_Roll");
            Set(so, "rattle", "Rattle");
            Set(so, "brakeApply", "D_bremse_treten");
            Set(so, "brakeRelease", "D_bremse_loesen");
            Set(so, "handbrakeOn", "PBrake_On");
            Set(so, "handbrakeOff", "PBrake_Off");
            Set(so, "gearButton", "gangwahltaster");
            Set(so, "reverseBeep", "reverse");
            Set(so, "horn", "KLAKSON");
            Set(so, "validatorBeep", "IBIS_piep");
            Set(so, "stopRequest", "stop");
            SetArray(so, "doorOpen", "DoorOpen1", "DoorOpen2", "DoorOpen3");
            SetArray(so, "doorClose", "DoorClose1", "DoorClose2", "DoorClose3");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Android için: Vorbis, kısa sesler belleğe açık, uzun döngüler sıkıştırılmış halde bellekte.
        /// </summary>
        public static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundFolder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                    continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                bool mono = System.Array.IndexOf(StereoAllowed, name) < 0;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                bool shortClip = clip != null && clip.length < 2f;

                var settings = importer.defaultSampleSettings;
                settings.loadType = shortClip ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                settings.preloadAudioData = true;

                bool changed = importer.forceToMono != mono
                    || importer.defaultSampleSettings.loadType != settings.loadType
                    || importer.defaultSampleSettings.compressionFormat != settings.compressionFormat
                    || !Mathf.Approximately(importer.defaultSampleSettings.quality, settings.quality);
                if (!changed)
                    continue;
                importer.forceToMono = mono;
                importer.loadInBackground = false;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + name + ".wav");
            if (clip == null)
                Debug.LogWarning("[OtobusSesKurucu] Ses bulunamadı: " + SoundFolder + name + ".wav");
            return clip;
        }

        private static void Set(SerializedObject so, string field, string file) =>
            so.FindProperty(field).objectReferenceValue = Clip(file);

        private static void SetArray(SerializedObject so, string field, params string[] files)
        {
            var p = so.FindProperty(field);
            p.arraySize = files.Length;
            for (int i = 0; i < files.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = Clip(files[i]);
        }
    }
}
