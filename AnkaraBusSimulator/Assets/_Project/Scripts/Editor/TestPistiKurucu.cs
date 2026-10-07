using System.Collections.Generic;
using AnkaraBus.UI;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Sürüş denemesi için test pisti sahnesi: düz zemin, %10 eğimli 100 m rampa, düzlük ve iniş,
    /// şerit çizgileri, otobüs, kamera ve dokunmatik arayüz.
    /// Menü: Ankara Bus > Test Pisti Sahnesini Kur
    /// </summary>
    public static class TestPistiKurucu
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestTrack.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/";

        private const float RampStartZ = 200f;
        private const float RampLength = 100f;
        private const float RampGrade = 0.10f;
        private const float PlateauLength = 40f;
        private const float TrackWidth = 14f;

        [MenuItem("Ankara Bus/Test Pisti Sahnesini Kur")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[TestPisti] Önce Ankara Bus > BMC Procity Prefabını Kur.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var asphalt = EnsureMaterial("M_TestAsfalt", new Color(0.23f, 0.24f, 0.26f));
            var ramp = EnsureMaterial("M_TestRampa", new Color(0.36f, 0.33f, 0.3f));
            var paint = EnsureMaterial("M_TestCizgi", new Color(0.95f, 0.95f, 0.92f));

            var track = new GameObject("TestPisti").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.SetParent(track);
            ground.transform.localScale = new Vector3(240f, 1f, 1000f);
            ground.transform.position = new Vector3(0f, -0.5f, 300f);
            ground.GetComponent<Renderer>().sharedMaterial = asphalt;
            ground.isStatic = true;

            // Rampa: çıkış, düzlük, iniş. Yüzeyler uç uca birleşir.
            float run = RampLength / Mathf.Sqrt(1f + RampGrade * RampGrade);
            float rise = run * RampGrade;
            var a = new Vector3(0f, 0f, RampStartZ);
            var b = a + new Vector3(0f, rise, run);
            var c = b + new Vector3(0f, 0f, PlateauLength);
            var d = c + new Vector3(0f, -rise, run);
            Slab(track, "Rampa_Cikis_%10", a, b, ramp);
            Slab(track, "Rampa_Duzluk", b, c, ramp);
            Slab(track, "Rampa_Inis_%10", c, d, ramp);

            // Şerit çizgileri (her 10 m'de bir), rampada da yüzeyi izler
            var lines = new GameObject("SeritCizgileri").transform;
            lines.SetParent(track);
            var points = new List<Vector3> { new Vector3(0f, 0f, -40f), a, b, c, d, new Vector3(0f, 0f, 700f) };
            for (int i = 0; i < points.Count - 1; i++)
                Dashes(lines, points[i], points[i + 1], paint);

            AddPlayer(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            Camera.main.farClipPlane = 1500f;

            if (!SaveScene(scene, ScenePath))
                return;
            Debug.Log("[TestPisti] Sahne kuruldu: " + ScenePath);
        }

        /// <summary>Otobüsü, takip kamerasını ve dokunmatik arayüzü açık sahneye ekler.</summary>
        public static BusVehicle AddPlayer(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var bus = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bus.transform.SetPositionAndRotation(position, rotation);
            var vehicle = bus.GetComponent<BusVehicle>();

            var cameraGo = Camera.main.gameObject;
            var rig = cameraGo.AddComponent<BusCameraRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("target").objectReferenceValue = vehicle;
            rigSo.ApplyModifiedPropertiesWithoutUndo();
            cameraGo.transform.position = bus.transform.TransformPoint(new Vector3(0f, 4.5f, -14f));
            cameraGo.transform.LookAt(bus.transform.TransformPoint(new Vector3(0f, 1.8f, 4f)));

            var controls = new GameObject("DokunmatikKontroller").AddComponent<BusTouchControls>();
            var controlsSo = new SerializedObject(controls);
            controlsSo.FindProperty("input").objectReferenceValue = bus.GetComponent<BusInput>();
            controlsSo.FindProperty("cameraRig").objectReferenceValue = rig;
            controlsSo.ApplyModifiedPropertiesWithoutUndo();
            return vehicle;
        }

        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError("[Sahne] Kaydedilemedi: " + path);
                return false;
            }
            AddToBuild(path);
            return true;
        }

        private static void Slab(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            const float thickness = 2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            Vector3 along = to - from;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            go.transform.SetPositionAndRotation((from + to) * 0.5f - rotation * Vector3.up * (thickness * 0.5f), rotation);
            go.transform.localScale = new Vector3(TrackWidth, thickness, along.magnitude);
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
        }

        private static void Dashes(Transform parent, Vector3 from, Vector3 to, Material material)
        {
            Vector3 along = to - from;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            int count = Mathf.FloorToInt(along.magnitude / 10f);
            for (int i = 0; i < count; i++)
            {
                foreach (float x in new[] { -1.75f, 1.75f })
                {
                    var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dash.name = "Cizgi";
                    Object.DestroyImmediate(dash.GetComponent<Collider>());
                    dash.transform.SetParent(parent);
                    Vector3 p = from + along.normalized * (i * 10f + 2.5f) + rotation * new Vector3(x, 0.01f, 0f);
                    dash.transform.SetPositionAndRotation(p, rotation);
                    dash.transform.localScale = new Vector3(0.15f, 0.02f, 4f);
                    dash.GetComponent<Renderer>().sharedMaterial = material;
                    dash.isStatic = true;
                }
            }
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            if (!AssetDatabase.IsValidFolder(MaterialFolder.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
