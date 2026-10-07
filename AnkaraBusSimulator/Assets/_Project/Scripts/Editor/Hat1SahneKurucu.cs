using AnkaraBus.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 1 haritasını yeni bir sahneye kurar, otobüsü başlangıç noktasına koyar, hattı bağlar.
    /// Işık ve occlusion bake'i henüz yok (Y5).
    /// Menü: Ankara Bus > Hat 1 Sahnesini Kur
    /// </summary>
    public static class Hat1SahneKurucu
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hat1_KizilayAtakule.unity";

        [MenuItem("Ankara Bus/Hat 1 Sahnesini Kur")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Hat1Sahne] Önce Ankara Bus > BMC Procity Prefabını Kur.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            HaritaKurucu.Build();

            var spawn = GameObject.Find("OtobusBaslangic");
            if (spawn == null)
            {
                Debug.LogError("[Hat1Sahne] Harita kurulamadı (OtobusBaslangic yok).");
                return;
            }

            var vehicle = TestPistiKurucu.AddPlayer(prefab, spawn.transform.position + Vector3.up * 0.05f, spawn.transform.rotation);
            Camera.main.farClipPlane = 2000f;

            var tracker = vehicle.GetComponent<RouteTracker>();
            var so = new SerializedObject(tracker);
            so.FindProperty("route").objectReferenceValue = Object.FindAnyObjectByType<BusRoute>();
            so.ApplyModifiedPropertiesWithoutUndo();

            if (TestPistiKurucu.SaveScene(scene, ScenePath))
                Debug.Log("[Hat1Sahne] Sahne kuruldu: " + ScenePath);
        }
    }
}
