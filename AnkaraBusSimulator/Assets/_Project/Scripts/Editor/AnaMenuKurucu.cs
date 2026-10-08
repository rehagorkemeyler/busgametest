using System.Collections.Generic;
using System;
using System.Linq;
using AnkaraBus.UI;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Ana menü sahnesini kurar: döner platformda otobüsün görsel kopyası (fizik, ses ve oyun bileşenleri
    /// çıkarılmış), arkada Atakule ve apartmanlar, AnaMenu + MenuVitrini. Sahneyi Build Settings'te ilk sıraya koyar.
    /// Menü: Ankara Bus > Ana Menü Sahnesini Kur. Ayrıntı: docs/ANA_MENU.md
    /// </summary>
    public static class AnaMenuKurucu
    {
        public const string ScenePath = "Assets/_Project/Scenes/AnaMenu.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/";
        private const string BuildingFolder = "Assets/_Project/Maps/Buildings/Apartmanlar";
        private const string AtakulePath = "Assets/_Project/Maps/Landmarks/Atakule.fbx";

        // Vitrin kopyasında kalan bileşenler; gerisi (Rigidbody, collider, ses, sürüş, puan...) çıkarılır
        private static readonly Type[] Keep =
        {
            typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(LODGroup),
            typeof(BusLivery), typeof(BusKaliteModeli),
        };

        [MenuItem("Ankara Bus/Ana Menü Sahnesini Kur")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[AnaMenu] Önce Ankara Bus > BMC Procity Prefabını Kur.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500f;

            var sun = Object.FindAnyObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.87f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 900f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.84f);

            var zemin = Primitive(PrimitiveType.Plane, "Zemin", null, new Vector3(0f, 0f, 0f), new Vector3(200f, 1f, 200f),
                Lit("M_MenuZemin", new Color(0.32f, 0.33f, 0.31f), 0.15f));
            zemin.isStatic = true;

            var platform = new GameObject("Platform").transform;
            Primitive(PrimitiveType.Cylinder, "Podyum", platform, new Vector3(0f, 0.06f, 0f), new Vector3(15f, 0.06f, 15f),
                Lit("M_MenuPodyum", new Color(0.2f, 0.21f, 0.23f), 0.55f));
            Primitive(PrimitiveType.Cylinder, "PodyumKenar", platform, new Vector3(0f, 0.02f, 0f), new Vector3(15.6f, 0.02f, 15.6f),
                Lit("M_MenuSari", new Color(0.98f, 0.76f, 0.18f), 0.4f));

            var bus = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(bus, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            bus.name = "VitrinOtobusu";
            bus.tag = "Untagged";
            bus.transform.SetParent(platform, false);
            bus.transform.localPosition = new Vector3(0f, 0.12f, 0.2f);
            Strip(bus);
            var livery = bus.GetComponent<BusLivery>();
            if (livery != null)
            {
                var lso = new SerializedObject(livery);
                lso.FindProperty("useMenuChoice").boolValue = false; // menü kendisi önizler
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            Backdrop();

            var menuGo = new GameObject("AnaMenu");
            var menu = menuGo.AddComponent<AnaMenu>();
            var mso = new SerializedObject(menu);
            mso.FindProperty("vitrinKaplamasi").objectReferenceValue = livery;
            mso.ApplyModifiedPropertiesWithoutUndo();
            var vitrin = menuGo.AddComponent<MenuVitrini>();
            var vso = new SerializedObject(vitrin);
            vso.FindProperty("platform").objectReferenceValue = platform;
            vso.FindProperty("vitrinKamerasi").objectReferenceValue = camera;
            vso.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[AnaMenu] Sahne kaydedilemedi: " + ScenePath);
                return;
            }
            PutFirstInBuild(ScenePath);
            Debug.Log("[AnaMenu] Sahne kuruldu ve Build Settings'te ilk sıraya kondu: " + ScenePath);
        }

        /// <summary>Atakule ve kameranın baktığı yönde yarım halka apartmanlar.</summary>
        private static void Backdrop()
        {
            var palette = HaritaKurucu.EnsurePaletteMaterial();
            var root = new GameObject("Arkaplan").transform;
            // MenuVitrini kamerayı otobüsün önü-sağına (+X, +Z) koyar; arka plan ters yönde
            Vector3 toCamera = new Vector3(0.55f, 0f, 0.84f).normalized;
            Vector3 back = -toCamera;
            Vector3 right = Vector3.Cross(Vector3.up, back);

            var atakule = AssetDatabase.LoadAssetAtPath<GameObject>(AtakulePath);
            if (atakule != null)
                Place(atakule, root, back * 520f + right * 140f, palette);

            var buildings = AssetDatabase.FindAssets("t:Model", new[] { BuildingFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null).OrderBy(g => g.name).ToArray();
            if (buildings.Length == 0)
                return;
            var random = new System.Random(6);
            for (int i = 0; i < 14; i++)
            {
                float angle = Mathf.Lerp(-80f, 80f, i / 13f) + (float)(random.NextDouble() * 6.0 - 3.0);
                float radius = 70f + (float)random.NextDouble() * 45f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * back;
                Place(buildings[i % buildings.Length], root, dir * radius, palette);
            }
        }

        private static void Place(GameObject model, Transform parent, Vector3 position, Material material)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.position = position;
            Vector3 toCenter = -position;
            toCenter.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up); // ön cephe (+Z) merkeze
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>())
                t.gameObject.isStatic = true;
        }

        /// <summary>Görsel olmayan bileşenleri çıkarır; RequireComponent bağımlılıkları yüzünden bağımlı olanlardan başlar.</summary>
        private static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                var remove = root.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && !Keep.Any(k => k.IsInstanceOfType(c))).ToList();
                if (remove.Count == 0)
                    break;
                foreach (var c in remove)
                    if (!IsRequired(c))
                        Object.DestroyImmediate(c);
            }
            // tekerlek ve kapı bağlantı noktaları gibi boş objeler kalabilir; zararsız
        }

        private static bool IsRequired(Component component)
        {
            foreach (var other in component.GetComponents<Component>())
            {
                if (other == null || other == component)
                    continue;
                foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    foreach (var t in new[] { req.m_Type0, req.m_Type1, req.m_Type2 })
                        if (t != null && t.IsInstanceOfType(component))
                            return true;
            }
            return false;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material Lit(string name, Color color, float smoothness)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                if (!AssetDatabase.IsValidFolder(MaterialFolder.TrimEnd('/')))
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            material.color = color;
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Build sırası: ana menü, OyunSecimi.Hatlar sırasıyla hat sahneleri, en sonda diğerleri (TestTrack).</summary>
        private static void PutFirstInBuild(string path)
        {
            var others = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            int Rank(EditorBuildSettingsScene s)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
                int i = System.Array.FindIndex(AnkaraBus.Gameplay.OyunSecimi.Hatlar, h => h.sahne == name);
                return i >= 0 ? i : 1000;
            }
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            scenes.AddRange(others.OrderBy(Rank));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
