using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnkaraBus.Passengers;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Quaternius yolcu karakterlerini (Y20; tools/blender/yolcu_quaternius.py → Passengers/Quaternius) oyuna kurar:
    /// <list type="bullet">
    /// <item>içe aktarma: Generic rig, karakter dosyalarında animasyon yok, Yolcu_E/K_Animasyon.fbx'te klipler (Bekle, Yuru, Otur,
    /// Tutun; döngülü), tek malzeme M_Yolcu (URP/Lit, yolcu_palet.png, noktasal filtre)</item>
    /// <item>Yolcu_E.controller / Yolcu_K.controller: dört durum (geçişsiz; Passenger CrossFade ile geçer)</item>
    /// <item>Resources/Yolcular/&lt;karakter&gt;.prefab: model + Animator + Passenger (PassengerManager buradan yükler)</item>
    /// </list>
    /// Menü: Ankara Bus > Yolcu Karakterlerini Kur.
    /// </summary>
    public static class YolcuKurucu
    {
        private const string Klasor = "Assets/_Project/Passengers/Quaternius/";
        private const string Cikti = "Assets/_Project/Resources/Yolcular";
        private static readonly string[] Durumlar = { "Bekle", "Yuru", "Otur", "Tutun" };

        [MenuItem("Ankara Bus/Yolcu Karakterlerini Kur")]
        public static void Kur()
        {
            var mat = Malzeme();
            var denetleyiciler = new Dictionary<string, AnimatorController>();
            foreach (var kod in new[] { "E", "K" })
            {
                string animYol = Klasor + $"Yolcu_{kod}_Animasyon.fbx";
                if (!File.Exists(animYol))
                {
                    Debug.LogError("[YolcuKurucu] Animasyon dosyası yok: " + animYol);
                    return;
                }
                Ayarla(animYol, mat, true);
                denetleyiciler[kod] = Denetleyici(kod, animYol);
            }
            if (!AssetDatabase.IsValidFolder(Cikti))
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Yolcular");

            int sayi = 0;
            foreach (var yol in Directory.GetFiles(Klasor, "Yolcu_*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                string ad = Path.GetFileNameWithoutExtension(yol);
                if (ad.EndsWith("_Animasyon"))
                    continue;
                Ayarla(yol, mat, false);
                Prefab(yol, ad, denetleyiciler[ad.Substring(6, 1)]);
                sayi++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[YolcuKurucu] {sayi} yolcu prefabı: {Cikti}");
        }

        /// <summary>Komut satırı: -executeMethod AnkaraBus.EditorTools.YolcuKurucu.KurBatch</summary>
        public static void KurBatch()
        {
            Kur();
            EditorApplication.Exit(0);
        }

        private static Material Malzeme()
        {
            string dokuYol = Klasor + "Textures/yolcu_palet.png";
            if (AssetImporter.GetAtPath(dokuYol) is TextureImporter ti)
            {
                // düz renk hücreleri: karışmasın (noktasal filtre, mipmap yok, sıkıştırma yok; 64 px)
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            string yol = Klasor + "M_Yolcu.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Yolcu" };
                AssetDatabase.CreateAsset(mat, yol);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dokuYol));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Ayarla(string yol, Material mat, bool animasyon)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(yol);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = animasyon;
            imp.importBlendShapes = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Yolcu"), mat);
            if (animasyon)
            {
                var klipler = new List<ModelImporterClipAnimation>();
                foreach (var k in imp.defaultClipAnimations)
                {
                    string kisa = k.name.Split('|').Last();
                    if (!Durumlar.Contains(kisa))
                        continue;
                    k.name = kisa;
                    k.loopTime = true;
                    klipler.Add(k);
                }
                imp.clipAnimations = klipler.ToArray();
            }
            imp.SaveAndReimport();
        }

        private static AnimatorController Denetleyici(string kod, string animYol)
        {
            var klipler = AssetDatabase.LoadAllAssetsAtPath(animYol).OfType<AnimationClip>()
                                       .Where(c => !c.name.StartsWith("__preview__")).ToList();
            string yol = Klasor + $"Yolcu_{kod}.controller";
            AssetDatabase.DeleteAsset(yol);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(yol);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var durum in Durumlar)
            {
                var klip = klipler.FirstOrDefault(c => c.name == durum);
                if (klip == null)
                {
                    Debug.LogWarning($"[YolcuKurucu] {animYol}: '{durum}' klibi yok");
                    continue;
                }
                var st = sm.AddState(durum);
                st.motion = klip;
                if (durum == "Bekle")
                    sm.defaultState = st;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static void Prefab(string yol, string ad, AnimatorController ctrl)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(yol);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                go.name = ad;
                var animator = go.GetComponent<Animator>();
                if (animator == null)
                    animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = ctrl;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(yol).OfType<Avatar>().FirstOrDefault();
                animator.applyRootMotion = false;
                // görünmeyen yolcunun iskeleti güncellenmez (otobüsün içi/dışı kamerada değilse)
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.updateWhenOffscreen = false;
                    r.skinnedMotionVectors = false;
                }
                if (go.GetComponent<Passenger>() == null)
                    go.AddComponent<Passenger>();
                PrefabUtility.SaveAsPrefabAsset(go, $"{Cikti}/{ad}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
