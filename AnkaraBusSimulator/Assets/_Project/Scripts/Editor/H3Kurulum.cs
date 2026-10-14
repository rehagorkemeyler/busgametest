using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 3 (408) yerel denetimi, Editor (oynatmadan): sahne hiyerarşisi, malzemeler (pembe), güzergâh boyunca (RotaRehberi'nin
    /// şerit yolu, 2 m'de bir) yol yüzeyi: delik, yolun üstüne çıkan arazi, yolda engel, köprü altı açıklığı, eğim; durak noktasının ve
    /// sundurmanın zemin malzemesi; anons klipleri; simge yapıların yeri; görüntüler (sissiz: geometri için). Satırlar "[H3]",
    /// görüntüler Builds/H3/editor.
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.H3Kurulum.AnalizBatch
    /// </summary>
    public static class H3Kurulum
    {
        private static void Log(string s) => Debug.Log("[H3] " + s);

        private static string Klasor(string ad)
        {
            string k = Path.Combine(Application.dataPath, "..", "Builds", "H3", ad);
            Directory.CreateDirectory(k);
            return k;
        }

        private static string Grup(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.parent != null && t.parent.name == "Hat3_KizilayGunesevler")
                    return t.name;
            return "?";
        }

        /// <summary>Çarpışma üçgeninin malzeme adı (MeshCollider alt ağ eşlemesi; Renderer aynı nesnede).</summary>
        private static string Malzeme(RaycastHit h)
        {
            var mc = h.collider as MeshCollider;
            var r = h.collider.GetComponent<Renderer>();
            if (mc == null || mc.sharedMesh == null || r == null || h.triangleIndex < 0)
                return "-";
            var m = mc.sharedMesh;
            int idx = h.triangleIndex * 3;
            for (int i = 0; i < m.subMeshCount; i++)
            {
                var sm = m.GetSubMesh(i);
                if (idx >= sm.indexStart && idx < sm.indexStart + sm.indexCount)
                    return i < r.sharedMaterials.Length && r.sharedMaterials[i] != null ? r.sharedMaterials[i].name : "null";
            }
            return "?";
        }

        /// <summary>Komut satırı: Hat 2 sahnesini kurar (bake yok).</summary>
        public static void Hat2Batch()
        {
            Hat2SahneKurucu.Build();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Komut satırı: Hat 3 parça FBX'lerini yeniden içe aktarır (ilk içe aktarmada Maps/Hat3/Materials henüz yoktu, FBX'ler
        /// kendi dokusuz malzemelerini aldı), sonra Hat 3 ve Hat 2 sahnelerini kurar.
        /// </summary>
        public static void YenidenAktarBatch()
        {
            Hat3Malzemeler.HepsiniKur();
            var yollar = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Maps/Hat3/Parcalar" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var t0 = System.DateTime.Now;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var y in yollar)
                    AssetDatabase.ImportAsset(y, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Log($"{yollar.Length} FBX yeniden içe aktarıldı, {(System.DateTime.Now - t0).TotalSeconds:F0} sn");
            Hat3SahneKurucu.Build();
            Hat2SahneKurucu.Build();
            EditorApplication.Exit(0);
        }

        private static float[] yuzeyler;
        private static List<(string tur, Vector3 p, Vector3 f)> kesitListesi;
        private static List<(Vector3 p, Vector3 f)> kopruListesi;

        /// <summary>Komut satırı: H3_NOKTA="x,z;x,z" noktalarında yukarıdan ışın (arka yüzler dahil/hariç) ve o noktayı içeren renderer'lar.</summary>
        public static void NoktaBatch()
        {
            EditorSceneManager.OpenScene(Hat3SahneKurucu.ScenePath);
            Physics.SyncTransforms();
            foreach (var ns in System.Environment.GetEnvironmentVariable("H3_NOKTA").Split(';'))
            {
                var xz = ns.Split(',').Select(float.Parse).ToArray();
                var p = new Vector3(xz[0], 100f, xz[1]);
                foreach (bool arka in new[] { false, true })
                {
                    Physics.queriesHitBackfaces = arka;
                    Log($"nokta ({xz[0]},{xz[1]}) arka_yüz={arka}: " + string.Join(" ", Physics.RaycastAll(p, Vector3.down, 300f).OrderBy(h => h.distance)
                        .Select(h => $"{h.collider.name}[{Grup(h.transform)}/{Malzeme(h)}] y={h.point.y:F2} n={h.normal.y:F2}")));
                }
                Physics.queriesHitBackfaces = false;
                var kok = GameObject.Find("Hat3_KizilayGunesevler");
                foreach (var r in kok.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = r.bounds;
                    if (xz[0] < b.min.x || xz[0] > b.max.x || xz[1] < b.min.z || xz[1] > b.max.z) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    // noktanın altındaki/üstündeki üçgenler (renderer ağı)
                    var m = mf.sharedMesh;
                    var v = m.vertices;
                    var ys = new List<string>();
                    for (int si = 0; si < m.subMeshCount; si++)
                    {
                        var tr = m.GetTriangles(si);
                        for (int t = 0; t < tr.Length; t += 3)
                        {
                            Vector3 a = r.transform.TransformPoint(v[tr[t]]), bb = r.transform.TransformPoint(v[tr[t + 1]]), c = r.transform.TransformPoint(v[tr[t + 2]]);
                            float d = (bb.z - c.z) * (a.x - c.x) + (c.x - bb.x) * (a.z - c.z);
                            if (Mathf.Abs(d) < 1e-9f) continue;
                            float l1 = ((bb.z - c.z) * (xz[0] - c.x) + (c.x - bb.x) * (xz[1] - c.z)) / d;
                            float l2 = ((c.z - a.z) * (xz[0] - c.x) + (a.x - c.x) * (xz[1] - c.z)) / d;
                            if (l1 < 0 || l2 < 0 || l1 + l2 > 1) continue;
                            float y = l1 * a.y + l2 * bb.y + (1 - l1 - l2) * c.y;
                            float ny = Vector3.Cross(bb - a, c - a).normalized.y;
                            ys.Add($"{r.sharedMaterials[si].name}:y={y:F2},n={ny:F2}");
                        }
                    }
                    if (ys.Count > 0)
                        Log($"   renderer {r.name} [{Grup(r.transform)}] çarpıştırıcı={(r.GetComponent<Collider>() != null)} aktif={r.gameObject.activeInHierarchy}: {string.Join(" ", ys)}");
                }
            }
            EditorApplication.Exit(0);
        }

        public static void AnalizBatch()
        {
            EditorSceneManager.OpenScene(Hat3SahneKurucu.ScenePath);
            Physics.SyncTransforms();
            var kok = GameObject.Find("Hat3_KizilayGunesevler");

            // ---------------------------------------------------------------- hiyerarşi
            var sb = new StringBuilder("hiyerarşi:");
            foreach (Transform c in kok.transform)
                sb.Append($" {c.name}({c.childCount})");
            Log(sb.ToString());
            var route = Object.FindAnyObjectByType<BusRoute>();
            Log($"rota {route?.name} hat={route?.LineNumber} '{route?.LineName}' durak={route?.StopCount} şerit={Object.FindObjectsByType<TrafficLane>().Length} " +
                $"ışık={Object.FindObjectsByType<TrafficSignal>().Length} spawner={Object.FindObjectsByType<TrafficSpawner>().Length}");

            // ---------------------------------------------------------------- malzemeler
            var say = new Dictionary<string, int>();
            int pembe = 0, rend = 0;
            var pembeler = new List<string>();
            foreach (var r in kok.GetComponentsInChildren<Renderer>(true))
            {
                rend++;
                foreach (var m in r.sharedMaterials)
                {
                    string ad = m == null ? "NULL" : m.name;
                    bool kotu = m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader") || !m.shader.isSupported;
                    if (kotu)
                    {
                        pembe++;
                        if (pembeler.Count < 20) pembeler.Add($"{r.name}:{ad}");
                    }
                    say[ad] = say.TryGetValue(ad, out int n) ? n + 1 : 1;
                }
            }
            Log($"renderer={rend} pembe/null={pembe} {string.Join(" ", pembeler)}");
            Log("malzemeler: " + string.Join(", ", say.OrderByDescending(k => k.Value).Select(k => $"{k.Key}×{k.Value}")));
            var fbxMalz = new Dictionary<string, int>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Maps/Hat3/Parcalar" }))
            {
                var yol = AssetDatabase.GUIDToAssetPath(g);
                foreach (var r in AssetDatabase.LoadAssetAtPath<GameObject>(yol).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        string ad = m == null ? "NULL" : (AssetDatabase.GetAssetPath(m).Contains("Hat3/Materials") ? "Hat3/Materials" : AssetDatabase.GetAssetPath(m) + ":" + m.name);
                        fbxMalz[ad] = fbxMalz.TryGetValue(ad, out int n) ? n + 1 : 1;
                    }
            }
            Log("FBX malzeme bağları: " + string.Join(", ", fbxMalz.Select(k => $"{k.Key}×{k.Value}")));

            // ---------------------------------------------------------------- anonslar
            var eksikAnons = new List<string>();
            var durakAdlari = new List<string>();
            for (int i = 0; i < route.StopCount; i++)
            {
                var ad = route.GetStop(i).StopName;
                durakAdlari.Add(ad);
                string dosya = i == route.StopCount - 1 ? null : AnkaraBus.Vehicle.AnonsSistemi.AnonsAdi(route.GetStop(i + 1).StopName);
                if (dosya != null && Resources.Load<AudioClip>("Anonslar/sonraki_" + dosya) == null && Resources.Load<AudioClip>("Anonslar/sonraki_son_" + dosya) == null)
                    eksikAnons.Add(dosya);
            }
            Log($"duraklar: {string.Join(" | ", durakAdlari)}");
            Log($"anons klibi eksik: {(eksikAnons.Count == 0 ? "yok" : string.Join(", ", eksikAnons))}");

            // ---------------------------------------------------------------- güzergâh
            var bus = Object.FindAnyObjectByType<AnkaraBus.Vehicle.BusVehicle>();
            var tracker = bus.GetComponent<RouteTracker>();
            var rehber = bus.GetComponent<RotaRehberi>() ?? bus.gameObject.AddComponent<RotaRehberi>();
            typeof(RotaRehberi).GetField("tracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(rehber, tracker);
            // Editor'de Awake çalışmaz: şerit uzunlukları (TrafficLane.Build) elle kurulur
            var build = typeof(TrafficLane).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var l in Object.FindObjectsByType<TrafficLane>())
                build.Invoke(l, null);
            rehber.Kur();
            var yol3 = rehber.Yol.ToList();
            Log($"güzergâh: {yol3.Count} nokta, {rehber.Uzunluk:F0} m; otobüs başlangıcı {bus.transform.position}");
            bus.gameObject.SetActive(false);   // ışınlar otobüse çarpmasın

            // 2 m'de bir örnek
            var ornek = new List<(Vector3 p, Vector3 f, float s)>();
            float sToplam = 0f;
            for (int i = 1; i < yol3.Count; i++)
            {
                var a = yol3[i - 1];
                var b = yol3[i];
                float l = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                var f = new Vector3(b.x - a.x, 0, b.z - a.z).normalized;
                for (float t = 0; t < l; t += 2f)
                    ornek.Add((Vector3.Lerp(a, b, t / l), f, sToplam + t));
                sToplam += l;
            }

            string Yakin(Vector3 p)
            {
                int en = 0;
                float d = float.MaxValue;
                for (int i = 0; i < route.StopCount; i++)
                {
                    float x = Vector3.Distance(p, route.GetStop(i).transform.position);
                    if (x < d) { d = x; en = i; }
                }
                return $"{route.GetStop(en).StopName}+{d:F0}m";
            }

            int delik = 0, araziUstte = 0;
            var yuzey = new float[ornek.Count];
            var tur = new string[ornek.Count];
            var kayma = new float[ornek.Count];
            // şeridin yanında en yakın asfalt (sağa +, sola −; 8 m'de yoksa NaN)
            float AsfaltaUzaklik(Vector3 p, Vector3 sag)
            {
                for (float d = 0.5f; d <= 8f; d += 0.5f)
                    foreach (float yon in new[] { 1f, -1f })
                        foreach (var h in Physics.RaycastAll(p + sag * d * yon + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                            if (Grup(h.transform) == "Yol" && Malzeme(h).Contains("Asfalt"))
                                return d * yon;
                return float.NaN;
            }
            var malzSay = new Dictionary<string, int>();
            var kopruler = new List<(string metin, Vector3 p, Vector3 f)>();
            float kopruBas = -1f, kopruOrta = 99f, kopruYan = 99f;
            string kopruNe = "";
            Vector3 kopruP = Vector3.zero, kopruF = Vector3.forward;
            for (int i = 0; i < ornek.Count; i++)
            {
                var (p, f, s) = ornek[i];
                var sag = Vector3.Cross(Vector3.up, f);
                // yol yüzeyi: yukarıdan aşağı bütün yüzeyler; güzergâh (şerit) yüksekliğine en yakın Yol yüzeyi
                // RaycastAll çarpıştırıcı başına tek isabet verir (köprü tabliyesi ve altındaki yol aynı Yol ağında): şeridin 1,5 m üstünden
                var hits = Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit? yolHit = null, zeminHit = null;
                foreach (var h in hits)
                {
                    string gr = Grup(h.transform);
                    if (gr == "Yol" && Mathf.Abs(h.point.y - p.y) < 1.5f && (yolHit == null || Mathf.Abs(h.point.y - p.y) < Mathf.Abs(yolHit.Value.point.y - p.y)))
                        yolHit = h;
                    if (gr == "Zemin" && Mathf.Abs(h.point.y - p.y) < 1.5f && (zeminHit == null || h.point.y > zeminHit.Value.point.y))
                        zeminHit = h;
                }
                if (yolHit == null)
                {
                    kayma[i] = AsfaltaUzaklik(p, sag);
                    yuzey[i] = zeminHit?.point.y ?? float.NaN;
                    tur[i] = zeminHit != null ? "YOL YOK (arazi)" : "DELİK";
                    delik++;
                    continue;
                }
                var yh = yolHit.Value;
                string mz = Malzeme(yh);
                malzSay[mz] = malzSay.TryGetValue(mz, out int n) ? n + 1 : 1;
                yuzey[i] = yh.point.y;
                if (zeminHit != null && zeminHit.Value.point.y > yh.point.y + 0.03f)
                {
                    araziUstte++;
                    tur[i] = "ARAZİ yolun üstünde";
                }
                else if (mz.Contains("Kaldirim") || mz.Contains("Refuj") || mz.Contains("Bordur"))
                    tur[i] = "güzergâh " + mz.Replace("M_H3_", "") + " üstünde";
                if (tur[i] != null && !tur[i].StartsWith("ARAZİ"))
                    kayma[i] = AsfaltaUzaklik(p, sag);
                else
                    tur[i] = null;

                // üst açıklık: orta ve yanlar (±1,25 m) ayrı, yüzeyin 0,1 m üstünden yukarı
                float Acik(float x, out string ne)
                {
                    ne = "";
                    var bas = new Vector3(p.x, yh.point.y + 0.1f, p.z) + sag * x;
                    if (Physics.Raycast(bas, Vector3.up, out var u, 15f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        ne = $"{u.collider.name}({Grup(u.transform)})";
                        return u.distance + 0.1f;
                    }
                    return 99f;
                }
                float orta = Acik(0f, out var ortaNe);
                float yan = Mathf.Min(Acik(-1.25f, out _), Acik(1.25f, out _));
                if (Mathf.Min(orta, yan) < 15f)
                {
                    if (kopruBas < 0f) { kopruBas = s; kopruOrta = 99f; kopruYan = 99f; }
                    if (orta < kopruOrta) { kopruOrta = orta; kopruNe = ortaNe; kopruP = p; kopruF = f; }
                    if (yan < kopruYan && kopruOrta >= 99f) { kopruP = p; kopruF = f; kopruNe = "(yan)"; }
                    kopruYan = Mathf.Min(kopruYan, yan);
                }
                else if (kopruBas >= 0f)
                {
                    kopruler.Add(($"s={kopruBas:F0}–{s:F0} ({s - kopruBas:F0} m) açıklık orta={kopruOrta:F2} yan(±1,25)={kopruYan:F2} m {kopruNe} @({kopruP.x:F0},{kopruP.y:F1},{kopruP.z:F0}) {Yakin(kopruP)}", kopruP, kopruF));
                    kopruBas = -1f;
                }
            }
            Log($"yol yüzeyi: örnek={ornek.Count} yol_yok={delik} arazi_üstte={araziUstte}; güzergâh altındaki yol malzemesi: " +
                string.Join(", ", malzSay.OrderByDescending(k => k.Value).Select(k => $"{k.Key}×{k.Value}")));
            // ardışık aynı türdeki örnekler kesit olur (2 örneğe kadar boşluk birleşir)
            var kesitler = new List<(string tur, int a, int b)>();
            for (int i = 0; i < ornek.Count; i++)
            {
                if (tur[i] == null) continue;
                if (kesitler.Count > 0 && kesitler[^1].tur == tur[i] && i - kesitler[^1].b <= 3)
                    kesitler[^1] = (tur[i], kesitler[^1].a, i);
                else
                    kesitler.Add((tur[i], i, i));
            }
            Log($"güzergâh kesitleri ({kesitler.Count}):");
            for (int k = 0; k < kesitler.Count; k++)
            {
                var (t, a, b) = kesitler[k];
                var p = ornek[(a + b) / 2].p;
                string ek = "";
                if (t.StartsWith("DELİK") || t.StartsWith("YOL YOK"))
                    ek = " ışın: " + string.Join(" ", Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 3f).OrderBy(h => h.distance)
                        .Select(h => $"{Grup(h.transform)}/{Malzeme(h).Replace("M_H3_", "")}{h.point.y - p.y:+0.0;-0.0}"));
                var ks = Enumerable.Range(a, b - a + 1).Select(i => kayma[i]).Where(x => x != 0f).ToList();
                string ky = ks.Count == 0 ? "" : ks.Any(float.IsNaN) && ks.All(float.IsNaN) ? " asfalt 8 m içinde YOK" : $" en yakın asfalt {ks.Where(x => !float.IsNaN(x)).Select(Mathf.Abs).DefaultIfEmpty(0).Max():F1} m {(ks.Where(x => !float.IsNaN(x)).Average() > 0 ? "sağda" : "solda")}";
                Log($"  K{k:00} {t}: s={ornek[a].s:F0}–{ornek[b].s:F0} ({ornek[b].s - ornek[a].s + 2f:F0} m) @({p.x:F0},{p.y:F1},{p.z:F0}) {Yakin(p)}{ky}{ek}");
            }
            Log($"üst geçen yapılar (otobüs üstünde, {kopruler.Count}):");
            foreach (var k in kopruler) Log("  " + k.metin);

            // eğim: 20 m pencerede
            float enEgim = 0f;
            int enI = 0;
            var egimler = new List<(float e, int i)>();
            for (int i = 10; i < ornek.Count; i++)
            {
                if (float.IsNaN(yuzey[i]) || float.IsNaN(yuzey[i - 10])) continue;
                float e = (yuzey[i] - yuzey[i - 10]) / (ornek[i].s - ornek[i - 10].s);
                egimler.Add((e, i));
                if (e > enEgim) { enEgim = e; enI = i; }
            }
            Log($"en dik çıkış (20 m): %{enEgim * 100f:F1} s={ornek[enI].s:F0} {ornek[enI].p} ({Yakin(ornek[enI].p)}); %7 üstü örnek: {egimler.Count(e => e.e > 0.07f)}, %10 üstü: {egimler.Count(e => e.e > 0.10f)}");
            foreach (var (e, i) in egimler.Where(x => x.e > 0.09f))
                Log($"  dik: %{e * 100f:F1} s={ornek[i].s:F0} ({Yakin(ornek[i].p)})");
            // ani basamak: ardışık örnekte 0,15 m'den fazla yükseklik farkı
            for (int i = 1; i < ornek.Count; i++)
                if (!float.IsNaN(yuzey[i]) && !float.IsNaN(yuzey[i - 1]) && Mathf.Abs(yuzey[i] - yuzey[i - 1]) > 0.15f + 0.1f * (ornek[i].s - ornek[i - 1].s))
                    Log($"  BASAMAK s={ornek[i].s:F0} {yuzey[i - 1]:F2}→{yuzey[i]:F2} {ornek[i].p} ({Yakin(ornek[i].p)})");
            yuzeyler = yuzey;
            kesitListesi = kesitler.Select(k => (k.tur, ornek[(k.a + k.b) / 2].p, ornek[(k.a + k.b) / 2].f)).ToList();
            kopruListesi = kopruler.Select(k => (k.p, k.f)).ToList();

            // ---------------------------------------------------------------- duraklar ve sundurmalar
            var duraklarGrubu = kok.transform.Find("Duraklar");
            var sundurmalar = duraklarGrubu != null ? duraklarGrubu.Cast<Transform>().ToList() : new List<Transform>();
            for (int i = 0; i < route.StopCount; i++)
            {
                var d = route.GetStop(i);
                var p = d.transform.position;
                string zemin = Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 8f) ? $"{Grup(h.transform)}/{Malzeme(h)} y={h.point.y - p.y:+0.00;-0.00}" : "YOK";
                Transform enYakin = null;
                float dist = float.MaxValue;
                foreach (var t in sundurmalar)
                {
                    float x = Vector3.Distance(t.position, p);
                    if (x < dist) { dist = x; enYakin = t; }
                }
                string sz = "-";
                if (enYakin != null)
                {
                    // sundurma çatı merkezinden aşağı: sundurmanın kendisi değil, altındaki ilk yüzey
                    var bounds = enYakin.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var c = bounds.center;
                    var alt = Physics.RaycastAll(new Vector3(c.x, bounds.max.y + 1f, c.z), Vector3.down, bounds.size.y + 6f)
                        .Where(x => !x.transform.IsChildOf(enYakin)).OrderBy(x => x.distance).ToArray();
                    sz = alt.Length > 0 ? $"{Grup(alt[0].transform)}/{Malzeme(alt[0])} taban−zemin={bounds.min.y - alt[0].point.y:+0.00;-0.00}" : "zemin YOK";
                    var sag = d.transform.right;
                    sz += $" yana={Vector3.Dot(bounds.center - p, sag):F1} m";
                }
                Log($"durak {i:00} {d.StopName}: nokta zemini={zemin}; sundurma {enYakin?.name} {dist:F0} m: {sz}");
            }

            // ---------------------------------------------------------------- simge yapılar
            var simge = kok.transform.Find("SimgeYapilar");
            if (simge != null)
            {
                var ozet = new StringBuilder($"simge yapılar ({simge.childCount}):");
                foreach (Transform t in simge)
                {
                    var rs = t.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var b = rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                    // tabanı zemine oturuyor mu: merkezin altında zemin
                    bool zem = Physics.Raycast(new Vector3(b.center.x, b.min.y + 50f, b.center.z), Vector3.down, out var h, 100f);
                    ozet.Append($"\n  {t.name} @({b.center.x:F0},{b.min.y:F1},{b.center.z:F0}) boy={b.size.y:F0} {Yakin(b.center)}");
                    if (b.min.y < -1000f)
                        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
                        {
                            var m = mf.sharedMesh;
                            var v = m.vertices;
                            var tr = mf.transform;
                            int dusuk = 0;
                            for (int vi = 0; vi < v.Length; vi++)
                                if (tr.TransformPoint(v[vi]).y < -1000f) dusuk++;
                            var mr = mf.GetComponent<Renderer>();
                            var alt = new List<string>();
                            for (int si = 0; si < m.subMeshCount; si++)
                            {
                                var idx = m.GetIndices(si);
                                int sd = idx.Count(ix => tr.TransformPoint(v[ix]).y < -1000f);
                                if (sd > 0) alt.Add($"{(mr != null && si < mr.sharedMaterials.Length ? mr.sharedMaterials[si].name : si.ToString())}:{sd}");
                            }
                            ozet.Append($" — {mf.name}: köşe {v.Length}, -1000 m altında {dusuk} ({string.Join(" ", alt)})");
                        }
                }
                Log(ozet.ToString());
            }

            // ---------------------------------------------------------------- görüntüler
            bus.gameObject.SetActive(true);
            var cam = new GameObject("H3Kamera").AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 5000f;
            string klasor = Klasor("editor");
            for (int i = 0; i < route.StopCount; i++)
            {
                var d = route.GetStop(i).transform;
                var p = d.position;
                Ciz(cam, Path.Combine(klasor, $"durak_{i:00}_ust.png"), p - d.forward * 30f - d.right * 25f + Vector3.up * 30f, p);
                Ciz(cam, Path.Combine(klasor, $"durak_{i:00}_surucu.png"), p - d.forward * 25f + Vector3.up * 2.4f, p + d.forward * 40f + Vector3.up * 1.5f);
            }
            for (int k = 0; k < kopruListesi.Count; k++)
            {
                var (p, f) = kopruListesi[k];
                Ciz(cam, Path.Combine(klasor, $"kopru_{k:00}_yaklasma.png"), p - f * 35f + Vector3.up * 2.6f, p + Vector3.up * 3f);
                Ciz(cam, Path.Combine(klasor, $"kopru_{k:00}_alti.png"), p - f * 6f + Vector3.up * 2.2f, p + f * 10f + Vector3.up * 2.5f);
            }
            for (int k = 0; k < kesitListesi.Count; k++)
            {
                var (t, p, f) = kesitListesi[k];
                Ciz(cam, Path.Combine(klasor, $"kesit_{k:00}.png"), p - f * 12f + Vector3.up * 28f, p);
                Ciz(cam, Path.Combine(klasor, $"kesit_{k:00}_surucu.png"), p - f * 18f + Vector3.up * (2.4f + (yuzeyler != null ? 0f : 0f)), p + f * 10f + Vector3.up * 1.2f);
            }
            // havadan: güzergâh boyunca 500 m'de bir, 150 m yüksekten
            for (int i = 0; i < ornek.Count; i += 250)
            {
                var (p, f, s) = ornek[i];
                Ciz(cam, Path.Combine(klasor, $"hava_s{s:0000}.png"), p - f * 120f + Vector3.up * 150f, p + f * 100f);
            }
            Log("editör görüntüleri: " + klasor);
            EditorApplication.Exit(0);
        }

        private static void Ciz(Camera cam, string dosya, Vector3 konum, Vector3 bak, int w = 1280, int h = 720)
        {
            cam.transform.SetPositionAndRotation(konum, Quaternion.LookRotation(bak - konum, Vector3.up));
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(dosya, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
