using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// BMC Procity FBX'inden sürülebilir otobüs prefabını ve BusDefinition asset'ini kurar.
    /// Parça adları docs/OTOBUS_BMC_PROCITY.md'deki hiyerarşiye göredir.
    /// Menü: Ankara Bus > BMC Procity Prefabını Kur
    /// </summary>
    public static class OtobusKurucu
    {
        private const string Folder = "Assets/_Project/Buses/BMC_Procity_12LF/";
        private const string ModelPath = Folder + "BMC_Procity_12LF.fbx";
        public const string PrefabPath = Folder + "BMC_Procity_12LF.prefab";
        private const string DefinitionPath = Folder + "BMC_Procity_12LF.asset";

        private static readonly (string name, bool front, bool left)[] WheelParts =
        {
            ("Teker_OnSol", true, true),
            ("Teker_OnSag", true, false),
            ("Teker_ArkaSol", false, true),
            ("Teker_ArkaSag", false, false),
        };

        private static readonly (string name, string prefix)[] DoorGroups =
        {
            ("Ön kapı", "Kapi_1_"),
            ("Orta kapı", "Kapi_2_"),
            ("Arka kapı", "Kapi_3_"),
        };

        [MenuItem("Ankara Bus/BMC Procity Prefabını Kur")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[OtobusKurucu] Model bulunamadı: " + ModelPath);
                return;
            }

            var definition = AssetDatabase.LoadAssetAtPath<BusDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BusDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var root = new GameObject("BMC_Procity_12LF");
            try
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                modelInstance.name = "Model";
                var parts = new Dictionary<string, Transform>();
                foreach (var t in modelInstance.GetComponentsInChildren<Transform>(true))
                    parts[t.name] = t;

                var body = root.AddComponent<Rigidbody>();
                body.mass = definition.physics.massKg;

                var hull = new GameObject("Carpisma_Govde").AddComponent<BoxCollider>();
                hull.transform.SetParent(root.transform, false);
                hull.center = new Vector3(0f, 1.78f, -0.215f);
                hull.size = new Vector3(2.5f, 2.7f, 11.9f);

                var vehicle = root.AddComponent<BusVehicle>();
                SetupWheels(root.transform, vehicle, parts, definition.physics);
                SetupSteeringWheel(root.transform, vehicle, parts);
                SetupDoors(root, parts);

                foreach (var pair in parts)
                    if (pair.Key.StartsWith("Lamba_"))
                        pair.Value.gameObject.SetActive(false);

                root.AddComponent<BusInput>();
                root.AddComponent<RouteTracker>();
                root.tag = "Player"; // trafik (TrafficSpawner) oyuncuyu bu etiketle bulur
                SetupLivery(root);

                var so = new SerializedObject(vehicle);
                so.FindProperty("definition").objectReferenceValue = definition;
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                definition.prefab = prefab;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                Debug.Log("[OtobusKurucu] Prefab kuruldu: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetupLivery(GameObject root)
        {
            var items = new (string name, string body, string roof)[]
            {
                ("EGO kırmızı", "Textures/caroserie.png", "Textures/cngtank.png"),
                ("EGO mavi", "Textures/Kaplamalar/ego_mavi.png", "Textures/Kaplamalar/cngtank_beyaz.png"),
                ("Özel Halk", "Textures/Kaplamalar/ozel_halk.png", "Textures/Kaplamalar/cngtank_beyaz.png"),
            };
            var livery = root.AddComponent<BusLivery>();
            var so = new SerializedObject(livery);
            var list = so.FindProperty("liveries");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = items[i].name;
                element.FindPropertyRelative("body").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + items[i].body);
                element.FindPropertyRelative("roofModule").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + items[i].roof);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupWheels(Transform root, BusVehicle vehicle, Dictionary<string, Transform> parts, BusPhysicsSpec spec)
        {
            var holder = new GameObject("Tekerlekler").transform;
            holder.SetParent(root, false);

            var so = new SerializedObject(vehicle);
            var list = so.FindProperty("wheels");
            list.arraySize = 0;

            foreach (var (name, front, left) in WheelParts)
            {
                if (!parts.TryGetValue(name, out var visual))
                {
                    Debug.LogWarning("[OtobusKurucu] Tekerlek bulunamadı: " + name);
                    continue;
                }

                // Statik yükte tekerlek, süspansiyonun hedef konumunda (yolun ortası) durur
                var go = new GameObject("WC_" + name.Substring("Teker_".Length));
                go.transform.SetParent(holder, false);
                go.transform.position = visual.position + root.up * (spec.suspensionDistance * 0.5f);
                var collider = go.AddComponent<WheelCollider>();
                collider.radius = spec.wheelRadius;
                collider.suspensionDistance = spec.suspensionDistance;

                int i = list.arraySize++;
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("collider").objectReferenceValue = collider;
                element.FindPropertyRelative("visual").objectReferenceValue = visual;
                element.FindPropertyRelative("front").boolValue = front;
                element.FindPropertyRelative("left").boolValue = left;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupSteeringWheel(Transform root, BusVehicle vehicle, Dictionary<string, Transform> parts)
        {
            if (!parts.TryGetValue("Direksiyon", out var wheel))
                return;

            var so = new SerializedObject(vehicle);
            so.FindProperty("steeringWheel").objectReferenceValue = wheel;
            var filter = wheel.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                // Simidin dönme ekseni: köşelerin en az yayıldığı yön (diskin normali), sürücüye bakacak şekilde
                Vector3 axis = ThinnestAxis(filter.sharedMesh.vertices);
                if (Vector3.Dot(wheel.TransformDirection(axis), -root.forward) < 0f)
                    axis = -axis;
                so.FindProperty("steeringWheelAxis").vector3Value = axis;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 ThinnestAxis(Vector3[] vertices)
        {
            Vector3 mean = Vector3.zero;
            foreach (var v in vertices) mean += v;
            mean /= Mathf.Max(1, vertices.Length);

            Vector3 best = Vector3.forward;
            float bestVariance = float.MaxValue;
            const int steps = 60;
            for (int i = 0; i <= steps; i++)
            {
                float theta = Mathf.PI * 0.5f * i / steps;
                for (int j = 0; j < steps * 4; j++)
                {
                    float phi = Mathf.PI * 2f * j / (steps * 4);
                    var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float variance = 0f;
                    for (int k = 0; k < vertices.Length; k += 4)
                    {
                        float d = Vector3.Dot(vertices[k] - mean, dir);
                        variance += d * d;
                    }
                    if (variance < bestVariance)
                    {
                        bestVariance = variance;
                        best = dir;
                    }
                }
            }
            return best;
        }

        private static void SetupDoors(GameObject root, Dictionary<string, Transform> parts)
        {
            var controller = root.AddComponent<BusDoorController>();
            var so = new SerializedObject(controller);
            var groups = so.FindProperty("doors");
            groups.arraySize = 0;

            foreach (var (groupName, prefix) in DoorGroups)
            {
                var leafParts = new List<Transform>();
                foreach (var pair in parts)
                    if (pair.Key.StartsWith(prefix))
                        leafParts.Add(pair.Value);
                if (leafParts.Count == 0)
                    continue;

                Vector3 doorCenter = Vector3.zero;
                foreach (var leaf in leafParts)
                    doorCenter += LeafCenter(leaf);
                doorCenter /= leafParts.Count;

                var leaves = new List<BusDoor>();
                foreach (var leaf in leafParts)
                    leaves.Add(SetupLeaf(root.transform, leaf, doorCenter));
                leaves.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

                int i = groups.arraySize++;
                var group = groups.GetArrayElementAtIndex(i);
                group.FindPropertyRelative("name").stringValue = groupName;
                var leafList = group.FindPropertyRelative("leaves");
                leafList.arraySize = leaves.Count;
                for (int j = 0; j < leaves.Count; j++)
                    leafList.GetArrayElementAtIndex(j).objectReferenceValue = leaves[j];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 LeafCenter(Transform leaf)
        {
            var renderer = leaf.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.center : leaf.position;
        }

        private static BusDoor SetupLeaf(Transform root, Transform leaf, Vector3 doorCenter)
        {
            // Kanatlar dikey menteşe ekseni etrafında içeri döner; her kanat kapı boşluğunun
            // ortasından uzaklaşacak yöne açılır ki geçiş açık kalsın
            Vector3 arm = LeafCenter(leaf) - leaf.position;
            Vector3 up = root.up;
            float Spread(float degrees) =>
                Vector3.Distance(leaf.position + Quaternion.AngleAxis(degrees, up) * arm, doorCenter);
            float sign = Spread(90f) >= Spread(-90f) ? 1f : -1f;

            var door = leaf.gameObject.AddComponent<BusDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("motion").enumValueIndex = (int)BusDoor.Motion.Rotate;
            so.FindProperty("openEulerOffset").vector3Value = LocalYOffset(root, leaf, 90f * sign);
            so.FindProperty("duration").floatValue = 1.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        /// <summary>Otobüsün dikey ekseni etrafındaki dönüşü kanadın yerel Euler farkına çevirir.</summary>
        private static Vector3 LocalYOffset(Transform root, Transform leaf, float degrees)
        {
            // BusDoor Euler farkını açıklık oranıyla ölçekler; bu yüzden açılar -180..180 aralığında kalmalı
            Vector3 e = Quaternion.AngleAxis(degrees, leaf.InverseTransformDirection(root.up)).eulerAngles;
            return new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));
        }
    }
}
