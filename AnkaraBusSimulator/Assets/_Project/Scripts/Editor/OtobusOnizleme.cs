using System.IO;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Katalogdaki her otobüsü her kaplamada sağdan, soldan, önden ve arkadan; tam kalite modeli de içeriden (kokpit ve yolcu
    /// gözüyle) PNG'ye çizer. Kaplama, logo, plaka ve gövde deliklerini telefonsuz kontrol etmek için.
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.OtobusOnizleme.CizBatch -cikti klasör
    /// </summary>
    public static class OtobusOnizleme
    {
        public static void CizBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-cikti");
            string cikti = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/Onizleme";
            Directory.CreateDirectory(cikti);
            try
            {
                Ciz(cikti);
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Ciz(string cikti)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var isik = new GameObject("Gunes").AddComponent<Light>();
            isik.type = LightType.Directional;
            isik.intensity = 1.3f;
            isik.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            var zemin = GameObject.CreatePrimitive(PrimitiveType.Plane);
            zemin.transform.localScale = Vector3.one * 10f;

            var kamera = new GameObject("Kamera").AddComponent<Camera>();
            kamera.clearFlags = CameraClearFlags.SolidColor;
            kamera.backgroundColor = new Color(0.72f, 0.78f, 0.84f);
            kamera.fieldOfView = 40f;
            kamera.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1280, 720, 24);
            kamera.targetTexture = rt;

            var katalog = AssetDatabase.LoadAssetAtPath<OtobusKatalogu>(OtobusKurucu.KatalogPath);
            foreach (var tanim in katalog.otobusler)
            {
                var otobus = (GameObject)PrefabUtility.InstantiatePrefab(tanim.prefab);
                float uzun = OtobusOlcusu.Olc(otobus.transform).Uzunluk;
                float merkezZ = (OtobusOlcusu.Olc(otobus.transform).On - OtobusOlcusu.Olc(otobus.transform).Arka) * 0.5f;
                var kaplama = otobus.GetComponent<BusLivery>();
                int adet = kaplama != null ? Mathf.Max(1, kaplama.Count) : 1;
                for (int k = 0; k < adet; k++)
                {
                    kaplama?.Select(k);
                    float d = uzun * 1.25f;
                    var bakis = new (string ad, Vector3 konum)[]
                    {
                        ("sag", new Vector3(d, 2.2f, merkezZ)),
                        ("sol", new Vector3(-d, 2.2f, merkezZ)),
                        ("on", new Vector3(3.5f, 2.4f, merkezZ + uzun * 0.5f + 9f)),
                        ("arka", new Vector3(-3.5f, 2.4f, merkezZ - uzun * 0.5f - 9f)),
                    };
                    foreach (var (ad, konum) in bakis)
                    {
                        kamera.transform.position = konum;
                        kamera.transform.LookAt(new Vector3(0f, 1.5f, merkezZ + (ad == "on" ? uzun * 0.35f : ad == "arka" ? -uzun * 0.35f : 0f)));
                        Kaydet(kamera, rt, Path.Combine(cikti, $"{tanim.prefab.name}_{k}_{ad}.png"));
                    }
                }
                Object.DestroyImmediate(otobus);

                // tam kalite görsel: içeriden (delik ve eksik parça kontrolü)
                var tam = Resources.Load<GameObject>(tanim.prefab.name + "_TamKalite");
                if (tam != null)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(tam);
                    var olcu = OtobusOlcusu.Olc(tanim.prefab.transform);
                    var ic = new (string ad, Vector3 konum, Vector3 hedef)[]
                    {
                        ("ic_kokpit", new Vector3(-0.7f, 2.3f, olcu.On - 1.6f), new Vector3(-0.3f, 1.6f, olcu.On + 5f)),
                        ("ic_yolcu", new Vector3(0f, 1.9f, olcu.On - 3f), new Vector3(0f, 1.4f, -olcu.Arka)),
                        // koridordan yan duvarlara yakın bakış (etiketler, afişler, okuyucular)
                        ("ic_on_sag", new Vector3(-0.3f, 1.7f, olcu.On - 3.5f), new Vector3(1.3f, 1.5f, olcu.On - 2.5f)),
                        ("ic_on_sol", new Vector3(0.3f, 1.7f, olcu.On - 3.5f), new Vector3(-1.3f, 1.5f, olcu.On - 2.5f)),
                        ("ic_orta_sag", new Vector3(-0.3f, 1.7f, olcu.On - 7f), new Vector3(1.3f, 1.5f, olcu.On - 6f)),
                        ("ic_orta_sol", new Vector3(0.3f, 1.7f, olcu.On - 7f), new Vector3(-1.3f, 1.5f, olcu.On - 6f)),
                        ("ic_arka", new Vector3(0f, 1.9f, -olcu.Arka + 4f), new Vector3(0f, 1.5f, olcu.On)),
                        ("tam_sag", new Vector3(olcu.Uzunluk * 1.1f, 1.6f, (olcu.On - olcu.Arka) * 0.5f), new Vector3(0f, 1.4f, (olcu.On - olcu.Arka) * 0.5f)),
                    };
                    foreach (var (ad, konum, hedef) in ic)
                    {
                        kamera.transform.position = konum;
                        kamera.transform.LookAt(hedef);
                        kamera.fieldOfView = ad.StartsWith("ic") ? 70f : 40f;
                        Kaydet(kamera, rt, Path.Combine(cikti, $"{tanim.prefab.name}_{ad}.png"));
                    }
                    kamera.fieldOfView = 40f;
                    Object.DestroyImmediate(g);
                }
            }
            Debug.Log("[Onizleme] bitti: " + cikti);
        }

        private static void Kaydet(Camera kamera, RenderTexture rt, string yol)
        {
            kamera.Render();
            var onceki = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = onceki;
            File.WriteAllBytes(yol, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
