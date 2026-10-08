using System.Collections.Generic;
using System.IO;
using AnkaraBus.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Akşam/gece ve yağmur için gerekenleri üretir (oyunda HavaVeZaman kullanır). Ayrıntı: docs/HAVA_ZAMAN.md
    /// <list type="bullet">
    /// <item>Ankara Bus > Hava ve Gece Malzemelerini Kur: Resources/Hava altında palet malzemesinin gece (pencereler yanan),
    /// ıslak ve gece+ıslak kopyaları, lamba parlama malzemesi, yağmur damlası dokusu/malzemesi ve Yagmur prefabı;
    /// sis shader varyantlarının build'de kalması.</item>
    /// <item>Ankara Bus > Akşam ve Gece Işığını Bake Et > Hat 1 / Hat 2: sahneyi akşam ve gece ayarıyla (sokak lambaları
    /// bake ışığı olarak yanık) iki kez bake eder, lightmap ve probları Resources/IsikSetleri'ne kopyalar, sonra gündüz
    /// bake'ini (HatIsikBake) yeniden yapar. Sahne yeniden kurulup gündüz bake edilince bu da tekrarlanmalı.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class HavaZamanKurucu
    {
        // Malzemeler ve yağmur prefabı yoksa Unity açılınca kendiliğinden üretilir (menüyü unutmak gece/yağmuru bozmasın)
        static HavaZamanKurucu()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EksikseKur();
            };
        }

        /// <summary>Resources/Hava eksikse kurar; build öncesi de çağrılır (HavaMalzemeleriBuildKontrolu).</summary>
        public static void EksikseKur()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(HavaKlasoru + "M_AnkaraPalet_Gece.mat") != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(HavaKlasoru + "Yagmur.prefab") != null)
                return;
            Debug.Log("[HavaZaman] Resources/Hava eksik, kuruluyor.");
            MalzemeleriKur();
        }

        private const string HavaKlasoru = "Assets/_Project/Resources/Hava/";
        private const string IsikKlasoru = "Assets/_Project/Resources/IsikSetleri/";

        // T_AnkaraPalet ile aynı yerleşim (tools/blender/apartman_kit.py: GRID 16, CELL 16, PALETTE sırası)
        private const int Grid = 16, Hucre = 16;
        private static readonly (int indeks, Color renk)[] Pencereler =
        {
            (10, new Color(0.16f, 0.13f, 0.08f)),        // cam: çoğu karanlık, hafif yansıma
            (11, new Color(1f, 0.78f, 0.5f)),            // perde: sıcak, yanık oda
            (12, new Color(0.7f, 0.45f, 0.26f)),         // perde_koyu
            (13, new Color(1f, 0.94f, 0.8f)),            // dukkan_cam: dükkân vitrini
            (24, new Color(0.34f, 0.42f, 0.52f)),        // cam_ofis: soğuk floresan
            (25, new Color(0.22f, 0.32f, 0.28f)),        // cam_ofis_yesil
        };

        [MenuItem("Ankara Bus/Hava ve Gece Malzemelerini Kur")]
        public static void MalzemeleriKur()
        {
            KlasorHazirla(HavaKlasoru);
            var palet = HaritaKurucu.EnsurePaletteMaterial();

            var pencere = DokuKaydet(HavaKlasoru + "T_AnkaraPalet_Pencere.png", PencereDokusu(), true);
            var gece = Kopya(palet, "M_AnkaraPalet_Gece");
            Pencereli(gece, pencere);
            var islak = Kopya(palet, "M_AnkaraPalet_Islak");
            Islak(islak);
            var geceIslak = Kopya(palet, "M_AnkaraPalet_GeceIslak");
            Pencereli(geceIslak, pencere);
            Islak(geceIslak);

            var parlama = Malzeme("M_LambaParlama", "Universal Render Pipeline/Unlit");
            Renk(parlama, ZamanAyarlari.LambaRengi * 1.3f);
            EditorUtility.SetDirty(parlama);

            var damla = DokuKaydet(HavaKlasoru + "T_YagmurDamlasi.png", DamlaDokusu(), false);
            var yagmurMalzeme = Malzeme("M_Yagmur", "Universal Render Pipeline/Particles/Unlit");
            Saydam(yagmurMalzeme);
            yagmurMalzeme.SetTexture("_BaseMap", damla);
            Renk(yagmurMalzeme, new Color(0.78f, 0.82f, 0.9f, 0.45f));
            EditorUtility.SetDirty(yagmurMalzeme);
            YagmurPrefabi(yagmurMalzeme);

            SisVaryantlariniTut();
            AssetDatabase.SaveAssets();
            Debug.Log("[HavaZaman] Malzemeler ve yağmur prefabı kuruldu: " + HavaKlasoru);
        }

        [MenuItem("Ankara Bus/Akşam ve Gece Işığını Bake Et/Hat 1")]
        public static void BakeHat1Menu() => BakeMenu(Hat1SahneKurucu.ScenePath);

        [MenuItem("Ankara Bus/Akşam ve Gece Işığını Bake Et/Hat 2")]
        public static void BakeHat2Menu() => BakeMenu(Hat2SahneKurucu.ScenePath);

        public static void BakeHat1Batch() => EditorApplication.Exit(Bake(Hat1SahneKurucu.ScenePath) ? 0 : 1);

        public static void BakeHat2Batch() => EditorApplication.Exit(Bake(Hat2SahneKurucu.ScenePath) ? 0 : 1);

        private static void BakeMenu(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Bake(scenePath);
        }

        /// <summary>Akşam ve gece setlerini bake edip kopyalar, sonra gündüzü yeniden bake eder.</summary>
        public static bool Bake(string scenePath)
        {
            KlasorHazirla(IsikKlasoru);
            var scene = EditorSceneManager.OpenScene(scenePath);
            Light gunes = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional)
                    gunes = l;
            if (gunes == null || LightmapSettings.lightmaps.Length == 0)
            {
                Debug.LogError($"[HavaZaman] {scene.name}: önce gündüz bake'i (Ankara Bus > Hat Işık ve Occlusion Bake).");
                return false;
            }
            var gok = RenderSettings.skybox;
            bool tamam = true;

            foreach (var zaman in new[] { OyunSecimi.Zaman.Aksam, OyunSecimi.Zaman.Gece })
            {
                var a = ZamanAyarlari.Al(zaman);
                gunes.transform.rotation = Quaternion.Euler(a.gunesAcisi);
                gunes.color = a.gunesRengi;
                gunes.intensity = a.gunesSiddeti;
                gunes.shadowStrength = a.golgeGucu;
                var gecici = gok != null ? new Material(gok) : null;
                ZamanAyarlari.GokyuzuAyarla(gecici, a, false);
                RenderSettings.skybox = gecici;
                var lambalar = LambaIsiklari(a.lamba);

                var sure = System.Diagnostics.Stopwatch.StartNew();
                bool bake = Lightmapping.Bake();
                Debug.Log($"[HavaZaman] {scene.name} {zaman}: bake {(bake ? "tamam" : "BAŞARISIZ")}, " +
                          $"{sure.Elapsed.TotalMinutes:F1} dk, {lambalar.Count} lamba ışığı");
                if (bake)
                    SetiKaydet(scene.name, zaman);
                tamam &= bake;

                foreach (var l in lambalar)
                    Object.DestroyImmediate(l.gameObject);
                RenderSettings.skybox = gok;
                if (gecici != null)
                    Object.DestroyImmediate(gecici);
            }
            AssetDatabase.SaveAssets();

            // gündüz: sahneyi kaydedilmemiş değişiklikleriyle bırak, HatIsikBake yeniden açıp güneşi kurar ve bake eder
            bool gunduz = HatIsikBake.Bake(scenePath);
            return tamam && gunduz;
        }

        /// <summary>Sokak lambası başlarına bake ışıkları (yalnızca bake süresince).</summary>
        private static List<Light> LambaIsiklari(float guc)
        {
            var sonuc = new List<Light>();
            if (guc <= 0f)
                return sonuc;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform grup in root.transform)
                {
                    if (grup.name != "Lambalar")
                        continue;
                    foreach (Transform lamba in grup)
                        foreach (var bas in ZamanAyarlari.LambaBaslari)
                        {
                            var l = new GameObject("BakeLamba").AddComponent<Light>();
                            l.transform.position = lamba.TransformPoint(bas + Vector3.down * 0.5f); // fenerin hemen altı
                            l.type = LightType.Point;
                            l.lightmapBakeType = LightmapBakeType.Baked;
                            l.color = ZamanAyarlari.LambaRengi;
                            l.intensity = 3.2f * guc;
                            l.range = 22f;
                            l.shadows = LightShadows.Soft;
                            l.shadowRadius = 0.3f;
                            sonuc.Add(l);
                        }
                }
            return sonuc;
        }

        private static void SetiKaydet(string sahne, OyunSecimi.Zaman zaman)
        {
            var maps = LightmapSettings.lightmaps;
            var renk = new Texture2D[maps.Length];
            var yon = new Texture2D[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                renk[i] = Kopyala(maps[i].lightmapColor, $"{IsikKlasoru}{sahne}_{zaman}_{i}_renk", TextureImporterType.Lightmap);
                yon[i] = Kopyala(maps[i].lightmapDir, $"{IsikKlasoru}{sahne}_{zaman}_{i}_yon", TextureImporterType.DirectionalLightmap);
            }
            string yol = $"Assets/_Project/Resources/{IsikSeti.Yol(sahne, zaman)}.asset";
            var set = AssetDatabase.LoadAssetAtPath<IsikSeti>(yol);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<IsikSeti>();
                AssetDatabase.CreateAsset(set, yol);
            }
            set.renk = renk;
            set.yon = yon;
            set.problar = IsikSeti.ProblariOku();
            EditorUtility.SetDirty(set);
        }

        private static Texture2D Kopyala(Texture2D kaynak, string hedefYolUzantisiz, TextureImporterType tur)
        {
            if (kaynak == null)
                return null;
            string kaynakYol = AssetDatabase.GetAssetPath(kaynak);
            string hedef = hedefYolUzantisiz + Path.GetExtension(kaynakYol);
            AssetDatabase.DeleteAsset(hedef);
            if (!AssetDatabase.CopyAsset(kaynakYol, hedef))
            {
                Debug.LogError("[HavaZaman] Kopyalanamadı: " + kaynakYol);
                return null;
            }
            if (AssetImporter.GetAtPath(hedef) is TextureImporter imp && imp.textureType != tur)
            {
                imp.textureType = tur;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(hedef);
        }

        // ------------------------------------------------------------ malzemeler

        private static Texture2D PencereDokusu()
        {
            int boy = Grid * Hucre;
            var t = new Texture2D(boy, boy, TextureFormat.RGB24, false);
            var px = new Color[boy * boy];
            foreach (var (i, renk) in Pencereler)
            {
                int col = i % Grid, row = i / Grid;
                int y0 = boy - (row + 1) * Hucre; // satır 0 en üstte
                for (int y = y0; y < y0 + Hucre; y++)
                    for (int x = col * Hucre; x < (col + 1) * Hucre; x++)
                        px[y * boy + x] = renk;
            }
            t.SetPixels(px);
            return t;
        }

        /// <summary>İnce dikey damla izi: ortası opak, kenarları ve uçları saydam.</summary>
        private static Texture2D DamlaDokusu()
        {
            const int w = 8, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float yan = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float boy = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(yan * yan * boy)));
                }
            return t;
        }

        private static Texture2D DokuKaydet(string yol, Texture2D doku, bool nokta)
        {
            File.WriteAllBytes(yol, doku.EncodeToPNG());
            Object.DestroyImmediate(doku);
            AssetDatabase.ImportAsset(yol, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(yol) is TextureImporter imp)
            {
                imp.mipmapEnabled = false;
                imp.filterMode = nokta ? FilterMode.Point : FilterMode.Bilinear;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = !nokta;
                imp.textureCompression = nokta ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
        }

        private static Material Kopya(Material kaynak, string ad)
        {
            string yol = HavaKlasoru + ad + ".mat";
            AssetDatabase.DeleteAsset(yol);
            var m = new Material(kaynak) { name = ad };
            AssetDatabase.CreateAsset(m, yol);
            return m;
        }

        private static Material Malzeme(string ad, string shader)
        {
            string yol = HavaKlasoru + ad + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            var s = Shader.Find(shader);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, yol);
            }
            else if (s != null)
                m.shader = s;
            return m;
        }

        private static void Pencereli(Material m, Texture2D pencere)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", pencere);
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; // bake'e girmez, gerçek zamanlı parlar
            EditorUtility.SetDirty(m);
        }

        private static void Islak(Material m)
        {
            // ıslak: biraz koyu, parlak (Simple Lit'te parlama özel renk ile açılır; Lit'te düzgünlük yeter)
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.82f, 1f));
            if (m.HasProperty("_SpecularHighlights"))
                m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_SpecColor"))
                m.SetColor("_SpecColor", new Color(0.32f, 0.32f, 0.34f, 1f));
            m.EnableKeyword("_SPECULAR_COLOR");
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.78f);
            EditorUtility.SetDirty(m);
        }

        private static void Renk(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            m.color = c;
        }

        private static void Saydam(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Kameranın 12 m üstünde 40×40 m alandan aşağı yağan damlalar (dünya uzayında, kamerayı izler).</summary>
        private static void YagmurPrefabi(Material malzeme)
        {
            var go = new GameObject("Yagmur");
            try
            {
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.startLifetime = 1.1f;
                main.startSpeed = 20f;
                main.startSize = 0.03f;
                main.startColor = new Color(1f, 1f, 1f, 0.6f);
                main.maxParticles = 1200;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                main.prewarm = true;
                var emission = ps.emission;
                emission.rateOverTime = 1000f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(40f, 40f, 0.1f);
                shape.rotation = new Vector3(90f, 0f, 0f); // kutunun ileri yönü aşağı
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1f;
                r.sharedMaterial = malzeme;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                PrefabUtility.SaveAsPrefabAsset(go, HavaKlasoru + "Yagmur.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Sis yalnızca çalışma anında açıldığı için build'de doğrusal sis varyantı çıkarılmasın.</summary>
        private static void SisVaryantlariniTut()
        {
            var ayarlar = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (ayarlar.Length == 0)
                return;
            var so = new SerializedObject(ayarlar[0]);
            var strip = so.FindProperty("m_FogStripping");
            var keep = so.FindProperty("m_FogKeepLinear");
            if (strip == null || keep == null)
                return;
            strip.intValue = 1; // Custom
            keep.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void KlasorHazirla(string yol)
        {
            yol = yol.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(yol))
                return;
            string ust = Path.GetDirectoryName(yol).Replace('\\', '/');
            KlasorHazirla(ust);
            AssetDatabase.CreateFolder(ust, Path.GetFileName(yol));
        }
    }

    /// <summary>Her build'den önce gece/yağmur malzemeleri yerinde mi (yoksa build'e girmez, gece ve yağmur bozuk görünür).</summary>
    public class HavaMalzemeleriBuildKontrolu : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => HavaZamanKurucu.EksikseKur();
    }
}
