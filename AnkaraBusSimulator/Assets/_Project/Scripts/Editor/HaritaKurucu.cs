using System;
using System.Collections.Generic;
using System.IO;
using AnkaraBus.Route;
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
        private class Layout
        {
            public string lineNumber;
            public string lineName;
            public float[] spawnPos;
            public float spawnRotY;
            public Item[] items;
            public Stop[] stops;
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

            var spawn = new GameObject("OtobusBaslangic");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetPositionAndRotation(ToVector(layout.spawnPos), Quaternion.Euler(0f, layout.spawnRotY, 0f));

            Selection.activeGameObject = root;
            foreach (var model in missing)
                Debug.LogWarning($"[Hat 1] Model bulunamadı: {MapsRoot}{model}.fbx");
            Debug.Log($"[Hat 1] {placed} obje ve {layout.stops.Length} durak yerleştirildi. " +
                      "Otobüsü 'OtobusBaslangic' noktasına koyup RouteTracker'a 'Hat_" + layout.lineNumber + "' objesini bağlayın.");
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
