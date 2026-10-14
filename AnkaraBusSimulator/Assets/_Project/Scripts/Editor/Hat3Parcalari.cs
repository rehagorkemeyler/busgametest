using System.Collections.Generic;
using System.IO;
using AnkaraBus.Harita;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 3 (EGO 408 Kızılay → Güneşevler; docs/PLAN_HAT3_408.md) harita parçaları: tools/harita/hat3 + tools/blender/harita_hat3.py
    /// çıktısı Maps/Hat3/Parcalar/H3_i_j.fbx (250 m kareler) ve H3U_i_j.fbx (1 km'lik uzak şehir halkası).
    /// Her FBX'in çocukları grup adıyla biter (H3_3_12_Yol, …_Zemin, …_Binalar, …_BinalarLOD1, …_BinalarUzak,
    /// …_BinalarCarpisma, …_Agaclar, …_SimgeYapilar, H3U_…_Ufuk). Kurulum:
    /// <list type="bullet">
    /// <item>çocuklar haritanın grup nesnelerine taşınır (Yol, Zemin, Binalar, Agaclar, SimgeYapilar, Ufuk): diğer sistemler
    /// (MiniHarita, HavaVeZaman, HatIsikBake) grupları adlarıyla bulur</item>
    /// <item>çarpışma: Yol ve Zemin MeshCollider; binalar ayrı basit çarpışma ağıyla (görünmez, BinalarCarpisma); simge
    /// yapılar MeshCollider; ağaç ve uzak şehir çarpışmasız</item>
    /// <item>LOD: parça başına binalar LOD0 (bölme başına cephe) / LOD1 (uzak cephe); ağaçlar uzakta kesilir</item>
    /// <item>ParcaAkisi: otobüse uzak parçaları kapatır</item>
    /// </list>
    /// Malzemeler Maps/Hat3/Materials (Hat3Malzemeler), FBX içe aktarma ayarları Hat3IceAktarma.
    /// </summary>
    public static class Hat3Parcalari
    {
        private const string MapsRoot = "Assets/_Project/Maps/";

        public static void Kur(Transform kok, HaritaKurucu.ParcaBilgi[] parcalar, HaritaKurucu.HaritaDokusuBilgi haritaDokusu)
        {
            Hat3Malzemeler.HepsiniKur();
            var gruplar = new Dictionary<string, Transform>();
            Transform Grup(string ad)
            {
                if (gruplar.TryGetValue(ad, out var g))
                    return g;
                g = kok.Find(ad);
                if (g == null)
                {
                    g = new GameObject(ad).transform;
                    g.SetParent(kok, false);
                }
                gruplar[ad] = g;
                return g;
            }

            var akis = new List<ParcaAkisi.Parca>();
            int eksik = 0;
            try
            {
                for (int i = 0; i < parcalar.Length; i++)
                {
                    var p = parcalar[i];
                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar("Hat 3", p.ad, (float)i / parcalar.Length);
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MapsRoot + p.model + ".fbx");
                    if (asset == null)
                    {
                        eksik++;
                        continue;
                    }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    // çocuklar FBX'te parça merkezinde (harita_hat3.py nesne konumu); kök dünya başlangıcında kalır
                    var nesneler = new List<GameObject>();
                    MeshRenderer lod0 = null, lod1 = null;
                    var cocuklar = new List<Transform>();
                    foreach (Transform c in inst.transform)
                        cocuklar.Add(c);
                    foreach (var c in cocuklar)
                    {
                        string grup = c.name.Length > p.ad.Length + 1 ? c.name.Substring(p.ad.Length + 1) : c.name;
                        var mr = c.GetComponent<MeshRenderer>();
                        var mf = c.GetComponent<MeshFilter>();
                        switch (grup)
                        {
                            case "Yol":
                            case "Zemin":
                                c.SetParent(Grup(grup), true);
                                Statik(c.gameObject, true);
                                c.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                                if (grup == "Zemin")
                                    mr.shadowCastingMode = ShadowCastingMode.Off;
                                nesneler.Add(c.gameObject);
                                break;
                            case "Binalar":
                                lod0 = mr;
                                break;
                            case "BinalarLOD1":
                                lod1 = mr;
                                mr.shadowCastingMode = ShadowCastingMode.Off;
                                break;
                            case "BinalarUzak":
                                c.SetParent(Grup("Binalar"), true);
                                Statik(c.gameObject, true);
                                mr.shadowCastingMode = ShadowCastingMode.Off;
                                nesneler.Add(c.gameObject);
                                break;
                            case "BinalarCarpisma":
                                c.SetParent(Grup("Binalar"), true);
                                Object.DestroyImmediate(mr);
                                c.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                                Object.DestroyImmediate(mf);
                                GameObjectUtility.SetStaticEditorFlags(c.gameObject, (StaticEditorFlags)0);
                                nesneler.Add(c.gameObject);
                                break;
                            case "Agaclar":
                            {
                                var kap = new GameObject(p.ad + "_Agaclar").transform;
                                kap.SetParent(Grup("Agaclar"), false);
                                kap.position = c.position;
                                c.SetParent(kap, true);
                                Statik(c.gameObject, false);
                                var lg = kap.gameObject.AddComponent<LODGroup>();
                                lg.SetLODs(new[] { new LOD(0.35f, new Renderer[] { mr }) });   // ~600 m'den uzakta ağaçlar çizilmez
                                lg.RecalculateBounds();
                                nesneler.Add(kap.gameObject);
                                break;
                            }
                            case "SimgeYapilar":
                                c.SetParent(Grup("SimgeYapilar"), true);
                                Statik(c.gameObject, true);
                                c.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                                nesneler.Add(c.gameObject);
                                break;
                            case "Ufuk":
                                c.SetParent(Grup("Ufuk"), true);
                                Statik(c.gameObject, false);
                                mr.shadowCastingMode = ShadowCastingMode.Off;
                                mr.receiveShadows = false;
                                nesneler.Add(c.gameObject);
                                break;
                            default:
                                Debug.LogWarning($"[Hat 3] Bilinmeyen parça grubu: {c.name}");
                                c.SetParent(Grup("Diger"), true);
                                nesneler.Add(c.gameObject);
                                break;
                        }
                    }
                    if (lod0 != null || lod1 != null)
                    {
                        // parça başına bina LOD'u: yakında bölme başına cephe, uzakta 4×4 bölmelik basit cephe
                        var kap = new GameObject(p.ad + "_Binalar").transform;
                        kap.SetParent(Grup("Binalar"), false);
                        kap.position = (lod0 != null ? lod0.transform : lod1.transform).position;
                        var lodlar = new List<LOD>();
                        if (lod0 != null)
                        {
                            lod0.transform.SetParent(kap, true);
                            Statik(lod0.gameObject, true);
                            lodlar.Add(new LOD(lod1 != null ? 0.42f : 0.01f, new Renderer[] { lod0 }));
                        }
                        if (lod1 != null)
                        {
                            lod1.transform.SetParent(kap, true);
                            Statik(lod1.gameObject, true);
                            lodlar.Add(new LOD(0.004f, new Renderer[] { lod1 }));
                        }
                        var lg = kap.gameObject.AddComponent<LODGroup>();
                        lg.SetLODs(lodlar.ToArray());
                        lg.RecalculateBounds();
                        nesneler.Add(kap.gameObject);
                    }
                    Object.DestroyImmediate(inst);
                    akis.Add(new ParcaAkisi.Parca
                    {
                        ad = p.ad,
                        merkez = new Vector3(p.pos[0], p.pos[1], p.pos[2]),
                        uzak = p.uzak,
                        nesneler = nesneler.ToArray(),
                    });
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var bilesen = kok.gameObject.AddComponent<ParcaAkisi>();
            bilesen.Ayarla(akis.ToArray());
            EditorUtility.SetDirty(bilesen);

            if (haritaDokusu != null && !string.IsNullOrEmpty(haritaDokusu.dosya))
            {
                string yol = MapsRoot + haritaDokusu.dosya;
                if (AssetImporter.GetAtPath(yol) is TextureImporter ti && (!ti.isReadable || ti.mipmapEnabled))
                {
                    ti.isReadable = true;
                    ti.mipmapEnabled = false;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.maxTextureSize = 4096;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
                var hd = kok.gameObject.AddComponent<HaritaDokusu>();
                hd.doku = AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
                hd.min = new Vector2(haritaDokusu.min[0], haritaDokusu.min[1]);
                hd.pikselMetre = haritaDokusu.pikselMetre;
                if (hd.doku == null)
                    Debug.LogWarning("[Hat 3] Mini harita dokusu bulunamadı: " + yol);
            }
            Debug.Log($"[Hat 3] {akis.Count} parça kuruldu" + (eksik > 0 ? $", {eksik} parça FBX'i yok (Maps/Hat3/Parcalar)" : ""));
        }

        /// <summary>Statik bayraklar. BatchingStatic yok: parçalar zaten birleşik ağ (malzeme başına bir çizim); 9 km'lik
        /// haritada statik birleştirme telefonda yüzlerce MB ek köşe belleği demek. URP SRP Batcher çizim maliyetini düşürür.</summary>
        private static void Statik(GameObject go, bool engel)
        {
            var bayrak = StaticEditorFlags.OccludeeStatic;
            if (engel)
                bayrak |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(go, bayrak);
        }
    }

    /// <summary>
    /// Hat 3 malzemeleri (Maps/Hat3/Materials): FBX'teki malzeme adına göre (H3_Asfalt, H3_Cephe, H3_Yaprak…), URP Simple Lit
    /// + Maps/Hat3/Textures dokuları. Doku içe aktarma ayarları da burada.
    /// </summary>
    public static class Hat3Malzemeler
    {
        public const string Klasor = "Assets/_Project/Maps/Hat3/Materials";
        private const string Dokular = "Assets/_Project/Maps/Hat3/Textures/";

        // ad → (doku, düzgünlük, alfa kesme)
        private static readonly Dictionary<string, (string doku, float duz, bool alfa)> Tanim = new Dictionary<string, (string, float, bool)>
        {
            { "H3_Asfalt", ("asfalt.png", 0.12f, false) },
            { "H3_Kaldirim", ("kaldirim.png", 0.08f, false) },
            { "H3_Refuj", ("refuj.png", 0.02f, false) },
            { "H3_Bordur", ("bordur.png", 0.05f, false) },
            { "H3_Cizgi", (null, 0.15f, false) },
            { "H3_Zemin", ("zemin.png", 0.02f, false) },
            { "H3_Cim", ("cim.png", 0.02f, false) },
            { "H3_Cephe", ("cephe_atlas.png", 0.12f, false) },
            { "H3_Cati", ("cati_atlas.png", 0.05f, false) },
            { "H3_Govde", ("agac_govde.png", 0.02f, false) },
            { "H3_Yaprak", ("agac_yaprak.png", 0.05f, true) },
            { "H3_Su", ("su.png", 0.85f, false) },
            { "H3_Carpisma", (null, 0f, false) },
        };

        public static void HepsiniKur()
        {
            if (!AssetDatabase.IsValidFolder(Klasor))
                AssetDatabase.CreateFolder("Assets/_Project/Maps/Hat3", "Materials");
            foreach (var ad in Tanim.Keys)
                Malzeme(ad);
            AssetDatabase.SaveAssets();
        }

        public static Material Malzeme(string ad)
        {
            int at = ad.IndexOf('@');
            if (at >= 0)
                ad = ad.Substring(0, at);
            string yol = $"{Klasor}/M_{ad}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m != null)
                return m;
            if (!Tanim.TryGetValue(ad, out var t))
                return null;
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { name = "M_" + ad, enableInstancing = true };
            if (t.doku != null)
            {
                string dyol = Dokular + t.doku;
                if (AssetImporter.GetAtPath(dyol) is TextureImporter ti)
                {
                    bool atlas = t.doku.Contains("atlas") || t.doku.StartsWith("agac_");
                    ti.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    ti.mipmapEnabled = true;
                    ti.anisoLevel = atlas ? 2 : 4;
                    ti.maxTextureSize = t.doku == "cephe_atlas.png" ? 2048 : 1024;
                    ti.alphaIsTransparency = t.alfa;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dyol);
                m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
            }
            m.SetColor("_BaseColor", ad == "H3_Cizgi" ? new Color(0.92f, 0.92f, 0.9f) : Color.white);
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", t.duz);
            if (t.alfa)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(m, yol);
            return m;
        }
    }

    /// <summary>Maps/Hat3/Parcalar FBX'lerinin içe aktarma ayarları ve malzeme eşlemesi (FBX malzeme adı → Hat3Malzemeler).</summary>
    public class Hat3IceAktarma : AssetPostprocessor
    {
        private const string Parcalar = "Assets/_Project/Maps/Hat3/Parcalar/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Parcalar))
                return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.generateSecondaryUV = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.indexFormat = ModelImporterIndexFormat.UInt32;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!assetPath.StartsWith(Parcalar))
                return null;
            return Hat3Malzemeler.Malzeme(material.name);
        }
    }
}
