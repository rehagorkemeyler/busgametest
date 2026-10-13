using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnkaraBus.Passengers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Y20: Quaternius yolcularının ve otobüs koltuk noktalarının denetimi (Editor, Play modu yok).
    /// <list type="bullet">
    /// <item>KarakterBatch: her karakterde boy, bakış yönü (ayak → parmak), dört durumda kol/bacak açıları, klibin sürmediği kemikler;
    /// önden/yandan görüntüler (Builds/Y20/karakter).</item>
    /// <item>KoltukBatch: dört otobüste her koltuğa oturan, her ayakta yere tutunan yolcu; minder/zemin ışın ölçümü (havada/gömük),
    /// yön, gövde (körüklüde ArkaGovde); içeriden görüntüler (Builds/Y20/koltuk).</item>
    /// </list>
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.Y20Kurulum.KarakterBatch | KoltukBatch (-nographics olmadan).
    /// </summary>
    public static class Y20Kurulum
    {
        private const string Yolcular = "Assets/_Project/Resources/Yolcular";
        private static readonly string[] Durumlar = { "Bekle", "Yuru", "Otur", "Tutun" };

        private static string Klasor(string ad)
        {
            string k = Path.Combine(Application.dataPath, "..", "Builds", "Y20", ad);
            Directory.CreateDirectory(k);
            return k;
        }

        private static GameObject[] Prefablar() => AssetDatabase.FindAssets("t:Prefab", new[] { Yolcular })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(p => p.name).ToArray();

        private static AnimationClip Klip(GameObject yolcu, string durum)
        {
            var ctrl = yolcu.GetComponent<Animator>().runtimeAnimatorController;
            return ctrl.animationClips.FirstOrDefault(c => c.name == durum);
        }

        private static void Poz(GameObject yolcu, string durum, float oran = 0.3f)
        {
            var k = Klip(yolcu, durum);
            if (k != null)
                k.SampleAnimation(yolcu, k.length * oran);
        }

        /// <summary>Deri meshlerinin pişirilmiş köşelerinden dünya sınırları.</summary>
        private static Bounds Sinir(GameObject go)
        {
            var b = new Bounds();
            bool ilk = true;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = new Mesh();
                r.BakeMesh(m, true);
                foreach (var v in m.vertices)
                {
                    var w = r.transform.TransformPoint(v);
                    if (ilk) { b = new Bounds(w, Vector3.zero); ilk = false; }
                    else b.Encapsulate(w);
                }
                Object.DestroyImmediate(m);
            }
            return b;
        }

        private static Transform Kemik(GameObject go, params string[] adlar)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>())
                foreach (var a in adlar)
                    if (t.name == a)
                        return t;
            return null;
        }

        /// <summary>Kemiğin (baş → çocuğu) aşağı yönle açısı (derece).</summary>
        private static float Aci(Transform kemik)
        {
            if (kemik == null || kemik.childCount == 0)
                return float.NaN;
            var d = kemik.GetChild(0).position - kemik.position;
            return Vector3.Angle(d, Vector3.down);
        }

        private static Camera Sahne(Color arka)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var isik = new GameObject("Gunes").AddComponent<Light>();
            isik.type = LightType.Directional;
            isik.intensity = 1.2f;
            isik.transform.rotation = Quaternion.Euler(45f, 160f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
            var cam = new GameObject("Kamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = arka;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 40f;
            return cam;
        }

        private static void Ciz(Camera cam, string dosya, Vector3 konum, Vector3 bak, int w = 1280, int h = 720)
        {
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Mathf.Abs((bak - konum).normalized.y) > 0.95f ? Vector3.forward : Vector3.up));
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            File.WriteAllBytes(dosya, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        public static void KarakterBatch()
        {
            var cam = Sahne(new Color(0.55f, 0.6f, 0.65f));
            var zemin = GameObject.CreatePrimitive(PrimitiveType.Plane);
            zemin.transform.localScale = Vector3.one * 3f;
            string klasor = Klasor("karakter");
            var prefablar = Prefablar();
            Debug.Log($"[Y20] {prefablar.Length} yolcu prefabı");

            // sayısal denetim
            foreach (var p in prefablar)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                var an = go.GetComponent<Animator>();
                var sb = new StringBuilder($"[Y20] {p.name}: ctrl={(an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "YOK")} avatar={(an.avatar ? an.avatar.name : "YOK")} " +
                                           $"passenger={(go.GetComponent<Passenger>() != null)} ");
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                sb.Append($"deri={go.GetComponentsInChildren<SkinnedMeshRenderer>().Length} köşe={(smr ? smr.sharedMesh.vertexCount : 0)} kemik={(smr ? smr.bones.Length : 0)} " +
                          $"malzeme={string.Join(",", go.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m ? m.name + "/" + m.shader.name : "null").Distinct())} ");
                foreach (var d in Durumlar)
                {
                    var klip = Klip(go, d);
                    if (klip == null) { sb.Append($"| {d}: KLİP YOK "); continue; }
                    Poz(go, d);
                    var b = Sinir(go);
                    var ayak = Kemik(go, "Foot.L", "foot.L", "LeftFoot");
                    var parmak = ayak != null && ayak.childCount > 0 ? ayak.GetChild(0) : null;
                    var bakis = parmak != null ? parmak.position - ayak.position : Vector3.zero;
                    sb.Append($"| {d} ({klip.length:F2} sn, döngü={klip.isLooping}): boy={b.size.y:F2} alt={b.min.y:F2} merkez=({b.center.x:F2},{b.center.z:F2}) " +
                              $"ayak→parmak=({bakis.x:F2},{bakis.z:F2}) " +
                              $"kol L/R={Aci(Kemik(go, "UpperArm.L")):F0}/{Aci(Kemik(go, "UpperArm.R")):F0} " +
                              $"önkol L/R={Aci(Kemik(go, "LowerArm.L")):F0}/{Aci(Kemik(go, "LowerArm.R")):F0} " +
                              $"uyluk L/R={Aci(Kemik(go, "UpperLeg.L")):F0}/{Aci(Kemik(go, "UpperLeg.R")):F0} " +
                              $"baldır L/R={Aci(Kemik(go, "LowerLeg.L")):F0}/{Aci(Kemik(go, "LowerLeg.R")):F0} ");
                    // klibin hiç sürmediği iskelet kemikleri (bağ pozunda kalır)
                    var yollar = new HashSet<string>(AnimationUtility.GetCurveBindings(klip).Where(c => c.type == typeof(Transform)).Select(c => c.path));
                    if (smr != null)
                    {
                        var bos = smr.bones.Where(t => t != null && !yollar.Contains(AnimationUtility.CalculateTransformPath(t, go.transform))).Select(t => t.name).ToList();
                        sb.Append($"sürülmeyen kemik={bos.Count}{(bos.Count > 0 ? "[" + string.Join(",", bos.Take(12)) + "]" : "")} ");
                    }
                }
                Debug.Log(sb.ToString());
                if (p == prefablar[0])
                    Debug.Log("[Y20] kemikler: " + string.Join(", ", smr.bones.Select(t => t.name)));
                Object.DestroyImmediate(go);
            }

            // görüntüler: her cinsiyetten iki karakter, dört durum yan yana; önden, yandan, arkadan
            foreach (var cins in new[] { "E", "K" })
            {
                var secim = prefablar.Where(x => x.name.StartsWith("Yolcu_" + cins)).Take(2).ToArray();
                var olanlar = new List<GameObject>();
                for (int s = 0; s < secim.Length; s++)
                    for (int i = 0; i < Durumlar.Length; i++)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(secim[s]);
                        go.transform.position = new Vector3((i - 1.5f) * 1.1f, 0f, s * -1.6f);
                        Poz(go, Durumlar[i]);
                        olanlar.Add(go);
                    }
                Ciz(cam, Path.Combine(klasor, $"{cins}_onden.png"), new Vector3(0f, 1.6f, 6.5f), new Vector3(0f, 0.8f, -0.8f));
                Ciz(cam, Path.Combine(klasor, $"{cins}_yandan.png"), new Vector3(7.5f, 1.4f, -0.8f), new Vector3(0f, 0.8f, -0.8f));
                Ciz(cam, Path.Combine(klasor, $"{cins}_arkadan.png"), new Vector3(0f, 1.6f, -8f), new Vector3(0f, 0.8f, -0.8f));
                foreach (var go in olanlar)
                    Object.DestroyImmediate(go);
            }
            // bütün karakterler, Bekle, önden; yanında 1,7 m'lik çubuk
            var cubuk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubuk.transform.localScale = new Vector3(0.05f, 1.7f, 0.05f);
            cubuk.transform.position = new Vector3(-prefablar.Length * 0.35f - 0.4f, 0.85f, 0f);
            for (int i = 0; i < prefablar.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefablar[i]);
                go.transform.position = new Vector3((i - (prefablar.Length - 1) * 0.5f) * 0.7f, 0f, 0f);
                Poz(go, "Bekle");
            }
            Ciz(cam, Path.Combine(klasor, "hepsi_onden.png"), new Vector3(0f, 1.2f, 14f), new Vector3(0f, 0.9f, 0f), 1920, 640);
            Debug.Log("[Y20] karakter görüntüleri: " + klasor);
            EditorApplication.Exit(0);
        }

        public static void KoltukBatch()
        {
            var cam = Sahne(Color.magenta);
            cam.fieldOfView = 70f;
            string klasor = Klasor("koltuk");
            var prefablar = Prefablar();
            var katalog = AssetDatabase.LoadAssetAtPath<AnkaraBus.Vehicle.OtobusKatalogu>(OtobusKurucu.KatalogPath);
            int n = 0;
            foreach (var t in katalog.otobusler)
            {
                var o = (GameObject)PrefabUtility.InstantiatePrefab(t.prefab);
                var kol = o.GetComponent<OtobusKoltuklari>();
                if (kol == null) { Debug.LogError($"[Y20] {t.prefab.name}: OtobusKoltuklari YOK"); Object.DestroyImmediate(o); continue; }
                var kor = o.GetComponent<AnkaraBus.Vehicle.KorukluOtobus>();
                var arka = kor != null && kor.ArkaGovde != null ? kor.ArkaGovde.transform : null;
                // dikey ışın: otobüsün görünen meshlerinin dünya üçgenleri (editörde fizik sorgusu çalışmadı)
                var ucgen = new List<Vector3>();
                foreach (var mf in o.GetComponentsInChildren<MeshFilter>())
                {
                    var r = mf.GetComponent<Renderer>();
                    if (mf.sharedMesh == null || r == null || !r.enabled)
                        continue;
                    var v = mf.sharedMesh.vertices;
                    var tr = mf.sharedMesh.triangles;
                    for (int i = 0; i < tr.Length; i++)
                        ucgen.Add(mf.transform.TransformPoint(v[tr[i]]));
                }

                float Isin(Vector3 bas, float boy, out string ne)
                {
                    ne = "-";
                    float en = float.NaN;
                    for (int i = 0; i < ucgen.Count; i += 3)
                    {
                        Vector3 a = ucgen[i], b = ucgen[i + 1], c = ucgen[i + 2];
                        if (bas.x < Mathf.Min(a.x, b.x, c.x) || bas.x > Mathf.Max(a.x, b.x, c.x) ||
                            bas.z < Mathf.Min(a.z, b.z, c.z) || bas.z > Mathf.Max(a.z, b.z, c.z))
                            continue;
                        float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                        if (Mathf.Abs(d) < 1e-9f)
                            continue;
                        float l1 = ((b.z - c.z) * (bas.x - c.x) + (c.x - b.x) * (bas.z - c.z)) / d;
                        float l2 = ((c.z - a.z) * (bas.x - c.x) + (a.x - c.x) * (bas.z - c.z)) / d;
                        float l3 = 1f - l1 - l2;
                        if (l1 < 0f || l2 < 0f || l3 < 0f)
                            continue;
                        float y = l1 * a.y + l2 * b.y + l3 * c.y;
                        if (y <= bas.y && y >= bas.y - boy && (float.IsNaN(en) || y > en))
                            en = y;
                    }
                    return en;
                }

                var sb = new StringBuilder($"[Y20] {t.prefab.name}: {kol.Koltuklar.Count} koltuk, {kol.Ayakta.Count} ayakta\n");
                int sorun = 0;
                var sorunlu = new List<int>();
                for (int i = 0; i < kol.Koltuklar.Count; i++)
                {
                    var k = kol.Koltuklar[i];
                    var nk = k.nokta;
                    var yerel = o.transform.InverseTransformPoint(nk.position);
                    var f = o.transform.InverseTransformDirection(nk.forward);
                    string yon = Mathf.Abs(f.z) > 0.7f ? (f.z > 0 ? "öne" : "arkaya") : (f.x > 0 ? "sağa" : "sola");
                    // minder: kökün 0,13 m gerisi, kökten 0,45 m yukarıda olmalı; zemin: kökün altında (ayaklar)
                    var minderBas = nk.position - nk.forward * 0.13f + Vector3.up * 0.8f;
                    float minder = Isin(minderBas, 1.2f, out var minderNe) - nk.position.y;
                    float zemin = Isin(nk.position + Vector3.up * 0.25f, 1.0f, out _) - nk.position.y;
                    bool kotu = float.IsNaN(minder) || Mathf.Abs(minder - 0.45f) > 0.08f || (!float.IsNaN(zemin) && zemin > 0.08f);
                    if (kotu) { sorun++; sorunlu.Add(i); }
                    string govde = nk.parent == o.transform ? "ön" : nk.parent == arka ? "ArkaGovde" : nk.parent.name;
                    sb.Append($"  {nk.name}{(kotu ? " !!" : "")}: yerel=({yerel.x:F2},{yerel.y:F2},{yerel.z:F2}) yön={yon} gövde={govde} " +
                              $"minder−kök={minder:F2} ({minderNe}) zemin−kök={zemin:F2} koridor.x={k.koridor.x:F2}\n");
                }
                for (int i = 0; i < kol.Ayakta.Count; i++)
                {
                    var a = kol.Ayakta[i].nokta;
                    var yerel = o.transform.InverseTransformPoint(a.position);
                    float zemin = Isin(a.position + Vector3.up * 0.3f, 1.0f, out _) - a.position.y;
                    bool kotu = float.IsNaN(zemin) || Mathf.Abs(zemin) > 0.08f;
                    if (kotu) sorun++;
                    string govde = a.parent == o.transform ? "ön" : a.parent == arka ? "ArkaGovde" : a.parent.name;
                    sb.Append($"  {a.name}{(kotu ? " !!" : "")}: yerel=({yerel.x:F2},{yerel.y:F2},{yerel.z:F2}) gövde={govde} zemin−nokta={zemin:F2}\n");
                }
                sb.Append($"  sorunlu nokta: {sorun}");
                Debug.Log(sb.ToString());

                // her koltuğa oturan, her ayakta yere tutunan yolcu
                foreach (var k in kol.Koltuklar)
                {
                    var y = (GameObject)PrefabUtility.InstantiatePrefab(prefablar[n++ % prefablar.Length]);
                    y.transform.SetParent(k.nokta, false);
                    Poz(y, "Otur");
                }
                var ayaktakiler = new List<GameObject>();
                foreach (var a in kol.Ayakta)
                {
                    var y = (GameObject)PrefabUtility.InstantiatePrefab(prefablar[n++ % prefablar.Length]);
                    ayaktakiler.Add(y);
                    y.transform.SetParent(a.nokta, false);
                    Poz(y, "Tutun");
                }

                var olcu = AnkaraBus.Vehicle.OtobusOlcusu.Olc(o.transform);
                string ad = t.prefab.name;
                for (float z = olcu.On - 1.5f; z > -olcu.Arka + 0.5f; z -= 3f)
                    Ciz(cam, Path.Combine(klasor, $"{ad}_ust_z{z:F0}.png"), o.transform.TransformPoint(0f, 2.35f, z), o.transform.TransformPoint(0f, 0f, z));
                cam.fieldOfView = 60f;
                foreach (var y in ayaktakiler) y.SetActive(false);
                Ciz(cam, Path.Combine(klasor, $"{ad}_onden_arkaya.png"), o.transform.TransformPoint(0f, 1.7f, olcu.On - 1.2f), o.transform.TransformPoint(0f, 1.0f, -olcu.Arka));
                Ciz(cam, Path.Combine(klasor, $"{ad}_arkadan_one.png"), o.transform.TransformPoint(0f, 1.7f, -olcu.Arka + 0.8f), o.transform.TransformPoint(0f, 1.0f, olcu.On));
                // her sorunlu koltuk (ve karşılaştırma için ilk iki koltuk): koridorun karşı yanından, yolcunun yanından
                cam.fieldOfView = 75f;
                foreach (int i in sorunlu.Concat(new[] { 0, kol.Koltuklar.Count - 1 }).Distinct())
                {
                    var nk = kol.Koltuklar[i].nokta;
                    var yerel = o.transform.InverseTransformPoint(nk.position);
                    float karsi = -Mathf.Sign(yerel.x) * 0.35f;
                    var bak = o.transform.TransformPoint(yerel.x, yerel.y + 0.5f, yerel.z);
                    var kon = o.transform.TransformPoint(karsi, Mathf.Max(yerel.y + 0.9f, 1.4f), yerel.z + 0.05f);
                    Ciz(cam, Path.Combine(klasor, $"{ad}_{nk.name}{(sorunlu.Contains(i) ? "_sorun" : "")}.png"), kon, bak, 960, 720);
                }
                foreach (var y in ayaktakiler) y.SetActive(true);
                // yandan (gövdenin sağ duvarı kesik değil: pencereden) birkaç kesit
                for (float z = olcu.On - 3f; z > -olcu.Arka + 1f; z -= 4f)
                    Ciz(cam, Path.Combine(klasor, $"{ad}_koridor_z{z:F0}.png"), o.transform.TransformPoint(0.1f, 1.5f, z + 1.5f), o.transform.TransformPoint(0f, 0.8f, z - 2f));
                cam.fieldOfView = 70f;
                Object.DestroyImmediate(o);
            }
            Debug.Log("[Y20] koltuk görüntüleri: " + klasor);
            EditorApplication.Exit(0);
        }

        /// <summary>Hat 1'deki durak sundurmalarının çarpışıcıları: tür, boyut, durağa göre konum (sundurma kaçınması için).</summary>
        public static void DurakBatch()
        {
            EditorSceneManager.OpenScene(Hat1SahneKurucu.ScenePath);
            var duraklar = Object.FindObjectsByType<AnkaraBus.Route.BusStop>();
            foreach (var d in duraklar)
            {
                var t = d.transform;
                var sb = new StringBuilder($"[Durak] {d.StopName} konum={t.position} ileri={t.forward}: ");
                foreach (var c in Object.FindObjectsByType<Collider>())
                {
                    if (c.attachedRigidbody != null || (c.bounds.center - t.position).sqrMagnitude > 15f * 15f) continue;
                    if (c.bounds.size.y < 1.3f) continue;
                    var yer = t.InverseTransformPoint(c.bounds.center);
                    string ek = c is BoxCollider bx ? $" kutu={bx.size} merkez={bx.center} ölçek={c.transform.lossyScale}" : "";
                    var yakin = t.InverseTransformPoint(c.ClosestPoint(t.position + t.right * 0.5f + Vector3.up));
                    sb.Append($"\n  {c.transform.parent?.name}/{c.name} [{c.GetType().Name}] sınır={c.bounds.size} merkez(durak uzayında)=({yer.x:F2},{yer.y:F2},{yer.z:F2}) " +
                              $"yol tarafına en yakın nokta x={yakin.x:F2}{ek}");
                }
                Debug.Log(sb.ToString());
            }
            EditorApplication.Exit(0);
        }
    }
}
