using AnkaraBus.Diagnostics;
using AnkaraBus.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Y18/Y19: kapı (KapiTesti) ve gece (GeceTesti) testlerini Editor'de Play modunda Hat 1'de koşar.
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.Y18Kurulum.TestBatch -gorev kapi|gece -zaman Gunduz|Aksam|Gece
    /// -hava Acik|Kar -kalite 0|1|2. Görüntüler Builds/Y18, bitince Unity kapanır.
    /// </summary>
    public static class Y18Kurulum
    {


        /// <summary>Y19: körüklü prefabların yapısı (körük derisi, arka görseller, kapı grupları, takip) ve katalog.</summary>
        public static void YapiBatch()
        {
            var katalog = AssetDatabase.LoadAssetAtPath<AnkaraBus.Vehicle.OtobusKatalogu>(OtobusKurucu.KatalogPath);
            Debug.Log("[Y19] katalog: " + string.Join(", ", System.Array.ConvertAll(katalog.otobusler, t => t.prefab != null ? t.prefab.name : "null")));
            foreach (var t in katalog.otobusler)
            {
                var p = t.prefab;
                var sb = new System.Text.StringBuilder($"[Y19] {p.name}: ");
                var kor = p.GetComponent<AnkaraBus.Vehicle.KorukluOtobus>();
                if (kor != null)
                {
                    var so = new SerializedObject(kor);
                    var ag = so.FindProperty("arkaGorseller");
                    var adlar = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < ag.arraySize; i++)
                        adlar.Add(ag.GetArrayElementAtIndex(i).objectReferenceValue ? ag.GetArrayElementAtIndex(i).objectReferenceValue.name : "null");
                    sb.Append($"arkaGorseller=[{string.Join(",", adlar)}] koruk={(so.FindProperty("koruk").objectReferenceValue ? so.FindProperty("koruk").objectReferenceValue.name : "boş")} ");
                }
                foreach (var smr in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    sb.Append($"deri={smr.name} kemikler=[{string.Join(",", System.Array.ConvertAll(smr.bones, b => b ? b.name : "null"))}] kök={(smr.rootBone ? smr.rootBone.name : "-")} köşe={(smr.sharedMesh ? smr.sharedMesh.vertexCount : 0)} ");
                var dc = p.GetComponentInChildren<AnkaraBus.Vehicle.BusDoorController>(true);
                if (dc != null)
                {
                    var so = new SerializedObject(dc);
                    var g = so.FindProperty("doors");
                    sb.Append($"kapı grupları={g.arraySize}: ");
                    for (int i = 0; i < g.arraySize; i++)
                    {
                        var e = g.GetArrayElementAtIndex(i);
                        var l = e.FindPropertyRelative("leaves");
                        sb.Append($"\"{e.FindPropertyRelative("name").stringValue}\"(");
                        for (int k = 0; k < l.arraySize; k++)
                        {
                            var d = l.GetArrayElementAtIndex(k).objectReferenceValue as AnkaraBus.Vehicle.BusDoor;
                            var dso = new SerializedObject(d);
                            var tk = dso.FindProperty("takip").objectReferenceValue;
                            sb.Append($"{d.name}:{(AnkaraBus.Vehicle.BusDoor.Motion)dso.FindProperty("motion").enumValueIndex}{(tk ? ",takip=" + tk.name : "")} ");
                        }
                        sb.Append(") ");
                    }
                }
                int tri = 0, rend = 0;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) tri += mf.sharedMesh.triangles.Length / 3;
                foreach (var r in p.GetComponentsInChildren<Renderer>(true)) rend++;
                sb.Append($"mobil üçgen={tri} renderer={rend}");
                var gomulu = new System.Collections.Generic.HashSet<string>();
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && AssetDatabase.GetAssetPath(m).EndsWith(".fbx")) gomulu.Add(m.name);
                sb.Append($" gömülü_malzeme={(gomulu.Count == 0 ? "yok" : string.Join(",", gomulu))}");
                Debug.Log(sb.ToString());
            }
            EditorApplication.Exit(0);
        }


        /// <summary>Y19: zemin ve ayna denetimi. Zeminsiz, macenta arka planlı sahnede otobüsü içeriden aşağı, alttan yukarı ve
        /// aynaları yakından çizer (macenta görünen yer delik). Ayrıca zemin/ayna adaylarının malzeme ve doku bilgisi.</summary>
        public static void ZeminBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var isik = new GameObject("Gunes").AddComponent<Light>();
            isik.type = LightType.Directional;
            isik.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            var cam = new GameObject("Kamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.magenta;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 70f;
            var katalog = AssetDatabase.LoadAssetAtPath<AnkaraBus.Vehicle.OtobusKatalogu>(OtobusKurucu.KatalogPath);
            string klasor = System.IO.Path.Combine(Application.dataPath, "..", "Builds", "Y19", "zemin");
            System.IO.Directory.CreateDirectory(klasor);
            foreach (var t in katalog.otobusler)
            {
                var o = (GameObject)PrefabUtility.InstantiatePrefab(t.prefab);
                var sb = new System.Text.StringBuilder($"[Zemin] {t.prefab.name}: ");
                foreach (var r in o.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) { sb.Append($"{r.name}[{i}]=NULL "); continue; }
                        var ad = m.name.ToLowerInvariant();
                        if (ad.Contains("piso") || ad.Contains("podea") || ad.Contains("floor") || ad.Contains("espelho") || ad.Contains("mirror") || ad.Contains("retrov") || ad.Contains("oglinda"))
                            sb.Append($"{r.name}[{i}]={m.name}/{m.shader.name}/{(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "dokusuz")} ");
                    }
                }
                Debug.Log(sb.ToString());
                var olcu = AnkaraBus.Vehicle.OtobusOlcusu.Olc(o.transform);
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                void Ciz(string ad, Vector3 konum, Vector3 bak)
                {
                    cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.forward));
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    int mac = 0;
                    foreach (var c in tex.GetPixels32()) if (c.r > 240 && c.g < 20 && c.b > 240) mac++;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(klasor, $"{t.prefab.name}_{ad}.png"), tex.EncodeToPNG());
                    Debug.Log($"[Zemin] {t.prefab.name} {ad}: macenta (delik) %{100f * mac / (1280f * 720f):F1}");
                    Object.DestroyImmediate(tex);
                }
                for (float z = olcu.On - 2f; z > -olcu.Arka + 1f; z -= 3f)
                    Ciz($"asagi_z{z:F0}", o.transform.TransformPoint(0f, 2.3f, z), o.transform.TransformPoint(0f, 0f, z));
                Ciz("alttan", o.transform.TransformPoint(0f, -6f, 0f), o.transform.TransformPoint(0f, 1f, 0f));
                // aynalar: sol ve sağ ön köşe, önden ve yandan; her kaplamada (ayna kaplamadan bağımsız olmalı)
                var kaplama = o.GetComponent<AnkaraBus.Vehicle.BusLivery>();
                int kAdet = kaplama != null ? Mathf.Max(1, kaplama.Count) : 1;
                for (int k = 0; k < kAdet; k++)
                foreach (float x in new[] { -1f, 1f })
                {
                    kaplama?.Select(k);
                    var ayna = o.transform.TransformPoint(x * (olcu.YariGenislik + 0.3f), 2.1f, olcu.On - 0.2f);
                    cam.fieldOfView = 35f;
                    Ciz($"k{k}_ayna_{(x < 0 ? "sol" : "sag")}_on", ayna + o.transform.forward * 2.5f + Vector3.up * 0.3f, ayna);
                    Ciz($"k{k}_ayna_{(x < 0 ? "sol" : "sag")}_arka", ayna - o.transform.forward * 2.5f + o.transform.right * x * 0.8f, ayna);
                    cam.fieldOfView = 70f;
                }
                Object.DestroyImmediate(o);
            }
            EditorApplication.Exit(0);
        }

        private static double baslangic;

        public static void TestBatch()
        {
            string Arg(string ad, string vars)
            {
                var a = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(a, ad);
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : vars;
            }
            Y10Baslatici.Gorev = Arg("-gorev", "kapi");
            OyunSecimi.SeciliZaman = System.Enum.Parse<OyunSecimi.Zaman>(Arg("-zaman", "Gunduz"));
            OyunSecimi.SeciliHava = System.Enum.Parse<OyunSecimi.Hava>(Arg("-hava", "Acik"));
            OyunSecimi.Otobus = int.Parse(Arg("-otobus", "2"));
            OyunSecimi.Kaplama = 0;
            GrafikAyarlari.Uygula((KaliteSeviyesi)int.Parse(Arg("-kalite", "1")), kaydet: true);
            EditorSceneManager.OpenScene(Hat1SahneKurucu.ScenePath);
            new GameObject("Y18Test").AddComponent<Y11Hat>();
            SceneManager.sceneLoaded += (s, m) =>
            {
                if (Object.FindAnyObjectByType<Y11Hat>() == null)
                    new GameObject("Y18Test").AddComponent<Y11Hat>();
            };
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (msg.EndsWith("BITTI hepsi") || msg.StartsWith("[Tutus] BITTI"))
                    EditorApplication.Exit(0);
                else if (type == LogType.Exception)
                    Debug.Log("[Y18] istisna: " + msg + "\n" + stack);
            };
            baslangic = EditorApplication.timeSinceStartup;
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup - baslangic > double.Parse(Arg("-sure", "900")))
                {
                    Debug.Log("[Y18] zaman aşımı");
                    EditorApplication.Exit(1);
                }
            };
            EditorApplication.EnterPlaymode();
        }
    }
}
