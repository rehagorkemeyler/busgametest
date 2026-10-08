using System;
using System.Collections.Generic;
using System.IO;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Traffic;
using UnityEditor;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// tools/blender/harita_hat1.py'nin ürettiği yerleşim dosyasından Hat 1 haritasını sahneye kurar.
    /// Menü: Ankara Bus > Hat 1 Haritasını Kur
    /// </summary>
    public static class HaritaKurucu
    {
        private const string MapsRoot = "Assets/_Project/Maps/";
        private const string LayoutPath = MapsRoot + "Hat1/Hat1_Yerlesim.json";
        private const string PalettePath = "Assets/_Project/Materials/T_AnkaraPalet.png";
        private const string MaterialPath = "Assets/_Project/Materials/M_AnkaraPalet.mat";
        private const string VehiclesFolder = "Assets/_Project/Traffic/Vehicles";
        private const string PassengersFolder = "Assets/_Project/Passengers/Models";

        [Serializable]
        private class Item
        {
            public string model;
            public float[] pos;
            public float rotY;
            public string group;
        }

        [Serializable]
        private class Stop
        {
            public string name;
            public float[] pos;
            public float rotY;
            public float radius;
        }

        [Serializable]
        private class LaneExit
        {
            public string target;
            public float at;
            public float targetAt;
            public float probability;
        }

        [Serializable]
        private class Lane
        {
            public string name;
            public float limitKmh;
            public float[] points;
            public LaneExit[] exits;
            public float stopLine = -1f;
            public int group;
            public string signal;
        }

        [Serializable]
        private class SignalPhase
        {
            public int[] greenGroups;
            public float green;
            public float yellow;
            public float allRed;
        }

        [Serializable]
        private class SignalHead
        {
            public string model;
            public float[] pos;
            public float rotY;
            public int group;
        }

        [Serializable]
        private class Signal
        {
            public string name;
            public SignalPhase[] phases;
            public SignalHead[] heads;
        }

        [Serializable]
        private class Layout
        {
            public string lineNumber;
            public string lineName;
            public float[] spawnPos;
            public float spawnRotY;
            public Item[] items;
            public Stop[] stops;
            public Lane[] lanes;
            public Signal[] signals;
        }

        [MenuItem("Ankara Bus/Hat 1 Haritasını Kur")]
        public static void Build()
        {
            if (!File.Exists(LayoutPath))
            {
                EditorUtility.DisplayDialog("Hat 1", "Yerleşim dosyası bulunamadı:\n" + LayoutPath, "Tamam");
                return;
            }

            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var material = EnsurePaletteMaterial();
            var root = new GameObject("Hat1_KizilayAtakule");
            Undo.RegisterCreatedObjectUndo(root, "Hat 1 Haritasını Kur");

            var groups = new Dictionary<string, Transform>();
            var missing = new HashSet<string>();
            int placed = 0;
            try
            {
                for (int i = 0; i < layout.items.Length; i++)
                {
                    var item = layout.items[i];
                    if (i % 50 == 0)
                        EditorUtility.DisplayProgressBar("Hat 1", item.model, (float)i / layout.items.Length);

                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MapsRoot + item.model + ".fbx");
                    if (asset == null)
                    {
                        missing.Add(item.model);
                        continue;
                    }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, GroupFor(root.transform, groups, item.group));
                    go.transform.SetPositionAndRotation(ToVector(item.pos), Quaternion.Euler(0f, item.rotY, 0f));
                    Prepare(go, material, item.group == "Yol" || item.group == "Zemin");
                    placed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            BuildRoute(root.transform, layout);
            BuildTraffic(root.transform, layout, material);
            BuildPassengers(root.transform, material);

            var spawn = new GameObject("OtobusBaslangic");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetPositionAndRotation(ToVector(layout.spawnPos), Quaternion.Euler(0f, layout.spawnRotY, 0f));

            Selection.activeGameObject = root;
            foreach (var model in missing)
                Debug.LogWarning($"[Hat 1] Model bulunamadı: {MapsRoot}{model}.fbx");
            Debug.Log($"[Hat 1] {placed} obje ve {layout.stops.Length} durak yerleştirildi. " +
                      "Otobüsü 'OtobusBaslangic' noktasına koyup RouteTracker'a 'Hat_" + layout.lineNumber + "' objesini bağlayın; " +
                      "trafik için otobüsün etiketini 'Player' yapın.");
        }

        private static Transform GroupFor(Transform root, Dictionary<string, Transform> groups, string name)
        {
            if (groups.TryGetValue(name, out var group))
                return group;
            group = new GameObject(name).transform;
            group.SetParent(root, false);
            groups[name] = group;
            return group;
        }

        private static void Prepare(GameObject go, Material material, bool collider)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                                            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

            if (material != null)
            {
                foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = material;
            }

            if (collider)
            {
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mc = filter.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = filter.sharedMesh;
                }
            }
        }

        private static void BuildRoute(Transform root, Layout layout)
        {
            var routeGo = new GameObject("Hat_" + layout.lineNumber);
            routeGo.transform.SetParent(root, false);

            var stops = new List<BusStop>();
            foreach (var s in layout.stops)
            {
                var go = new GameObject("Durak_" + s.name);
                go.transform.SetParent(routeGo.transform, false);
                go.transform.SetPositionAndRotation(ToVector(s.pos), Quaternion.Euler(0f, s.rotY, 0f));
                var stop = go.AddComponent<BusStop>();
                var so = new SerializedObject(stop);
                so.FindProperty("stopName").stringValue = s.name;
                so.FindProperty("radius").floatValue = s.radius;
                so.ApplyModifiedPropertiesWithoutUndo();
                stops.Add(stop);
            }

            var route = routeGo.AddComponent<BusRoute>();
            var rso = new SerializedObject(route);
            rso.FindProperty("lineNumber").stringValue = layout.lineNumber;
            rso.FindProperty("lineName").stringValue = layout.lineName;
            var array = rso.FindProperty("stops");
            array.arraySize = stops.Count;
            for (int i = 0; i < stops.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = stops[i];
            rso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPassengers(Transform root, Material material)
        {
            var models = new List<GameObject>();
            if (AssetDatabase.IsValidFolder(PassengersFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PassengersFolder }))
                    models.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            if (models.Count == 0)
            {
                Debug.LogWarning("[Hat 1] Yolcu modeli bulunamadı: " + PassengersFolder);
                return;
            }

            var go = new GameObject("Yolcular");
            go.transform.SetParent(root, false);
            var manager = go.AddComponent<PassengerManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("paletteMaterial").objectReferenceValue = material;
            var array = so.FindProperty("models");
            array.arraySize = models.Count;
            for (int i = 0; i < models.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTraffic(Transform root, Layout layout, Material material)
        {
            if (layout.lanes == null || layout.lanes.Length == 0)
                return;

            var trafficGo = new GameObject("Trafik");
            trafficGo.transform.SetParent(root, false);

            // trafik ışıkları
            var signals = new Dictionary<string, TrafficSignal>();
            foreach (var sg in layout.signals ?? Array.Empty<Signal>())
            {
                var sgo = new GameObject("Isik_" + sg.name);
                sgo.transform.SetParent(trafficGo.transform, false);
                var signal = sgo.AddComponent<TrafficSignal>();
                var heads = new List<TrafficSignal.Head>();
                foreach (var h in sg.heads ?? Array.Empty<SignalHead>())
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MapsRoot + h.model + ".fbx");
                    if (asset == null)
                    {
                        Debug.LogWarning($"[Hat 1] Trafik ışığı modeli bulunamadı: {MapsRoot}{h.model}.fbx");
                        continue;
                    }
                    var head = (GameObject)PrefabUtility.InstantiatePrefab(asset, sgo.transform);
                    head.transform.SetPositionAndRotation(ToVector(h.pos), Quaternion.Euler(0f, h.rotY, 0f));
                    foreach (var r in head.GetComponentsInChildren<MeshRenderer>(true))
                        r.sharedMaterial = material;
                    heads.Add(new TrafficSignal.Head { head = head.transform, group = h.group });
                }
                var phases = new List<TrafficSignal.Phase>();
                foreach (var ph in sg.phases ?? Array.Empty<SignalPhase>())
                    phases.Add(new TrafficSignal.Phase { greenGroups = ph.greenGroups, green = ph.green, yellow = ph.yellow, allRed = ph.allRed });
                signal.Configure(phases.ToArray(), heads.ToArray());
                EditorUtility.SetDirty(signal);
                signals[sg.name] = signal;
            }

            // şeritler, ardından çıkışlar (dönüşler) ve durma çizgileri
            var lanes = new Dictionary<string, TrafficLane>();
            foreach (var l in layout.lanes)
            {
                var go = new GameObject("Serit_" + l.name);
                go.transform.SetParent(trafficGo.transform, false);
                var points = new Vector3[l.points.Length / 3];
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector3(l.points[i * 3], l.points[i * 3 + 1], l.points[i * 3 + 2]);
                var lane = go.AddComponent<TrafficLane>();
                lane.SetPoints(points, l.limitKmh);
                lanes[l.name] = lane;
            }
            foreach (var l in layout.lanes)
            {
                var lane = lanes[l.name];
                var exits = new List<TrafficLane.Exit>();
                foreach (var e in l.exits ?? Array.Empty<LaneExit>())
                    if (lanes.TryGetValue(e.target, out var target))
                        exits.Add(new TrafficLane.Exit { at = e.at, target = target, targetAt = e.targetAt, probability = e.probability });
                lane.SetExits(exits.ToArray());
                if (!string.IsNullOrEmpty(l.signal) && signals.TryGetValue(l.signal, out var sig))
                    lane.SetSignal(sig, l.group, l.stopLine);
                EditorUtility.SetDirty(lane);
            }

            // Araç türleri: taksiler toplamın %30'u, dolmuşlar %10'u, diğerleri %60'ı
            var models = new List<GameObject>();
            if (AssetDatabase.IsValidFolder(VehiclesFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { VehiclesFolder }))
                    models.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            int taxis = models.FindAll(m => m.name.StartsWith("Taksi")).Count;
            int dolmus = models.FindAll(m => m.name.StartsWith("Dolmus")).Count;
            int others = models.Count - taxis - dolmus;

            var spawner = trafficGo.AddComponent<TrafficSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("paletteMaterial").objectReferenceValue = material;
            var array = so.FindProperty("vehicles");
            array.arraySize = models.Count;
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                float weight = m.name.StartsWith("Taksi") ? 30f / Mathf.Max(1, taxis)
                    : m.name.StartsWith("Dolmus") ? 10f / Mathf.Max(1, dolmus)
                    : 60f / Mathf.Max(1, others);
                var entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = m;
                entry.FindPropertyRelative("weight").floatValue = weight;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if (models.Count == 0)
                Debug.LogWarning("[Hat 1] Trafik aracı bulunamadı: " + VehiclesFolder);
        }

        /// <summary>Tüm harita modellerinin paylaştığı palet materyali; yoksa oluşturur.</summary>
        private static Material EnsurePaletteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            if (AssetImporter.GetAtPath(PalettePath) is TextureImporter importer)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { enableInstancing = true };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else
                material.mainTexture = texture;
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Vector3 ToVector(float[] v) => new Vector3(v[0], v[1], v[2]);
    }
}
