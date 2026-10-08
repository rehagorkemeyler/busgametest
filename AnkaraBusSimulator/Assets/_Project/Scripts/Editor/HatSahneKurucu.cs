using AnkaraBus.Route;
using AnkaraBus.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Hat 1 sahnesi. Işık ve occlusion bake'i: Ankara Bus > Hat 1 Işık ve Occlusion Bake (HatIsikBake).
    /// Menü: Ankara Bus > Hat 1 Sahnesini Kur
    /// </summary>
    public static class Hat1SahneKurucu
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hat1_KizilayAtakule.unity";

        [MenuItem("Ankara Bus/Hat 1 Sahnesini Kur")]
        public static void Build() => HatSahneKurucu.Kur(ScenePath, HaritaKurucu.Build, "Hat 1");
    }

    /// <summary>
    /// Hat 2 sahnesi (Kızılay → Ulus). Işık ve occlusion bake'i: Ankara Bus > Hat 2 Işık ve Occlusion Bake.
    /// Menü: Ankara Bus > Hat 2 Sahnesini Kur
    /// </summary>
    public static class Hat2SahneKurucu
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hat2_KizilayUlus.unity";

        [MenuItem("Ankara Bus/Hat 2 Sahnesini Kur")]
        public static void Build() => HatSahneKurucu.Kur(ScenePath, HaritaKurucu.BuildHat2, "Hat 2");
    }

    /// <summary>
    /// Bir hattın sahnesini sıfırdan kurar: harita (HaritaKurucu), otobüs başlangıç noktasında, kamera,
    /// dokunmatik arayüz, RouteTracker → hattın rotası, BusHud. Sahneyi Build Settings'e ekler.
    /// Sahne yeniden kurulunca ışık bake'i silinir; ardından HatIsikBake çalıştırılmalı.
    /// </summary>
    public static class HatSahneKurucu
    {
        public static void Kur(string scenePath, System.Action haritayiKur, string etiket)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[{etiket} Sahne] Önce Ankara Bus > BMC Procity Prefabını Kur.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            haritayiKur();

            var spawn = GameObject.Find("OtobusBaslangic");
            if (spawn == null)
            {
                Debug.LogError($"[{etiket} Sahne] Harita kurulamadı (OtobusBaslangic yok).");
                return;
            }

            var vehicle = TestPistiKurucu.AddPlayer(prefab, spawn.transform.position + Vector3.up * 0.05f, spawn.transform.rotation);
            Camera.main.farClipPlane = 2000f;

            var route = Object.FindAnyObjectByType<BusRoute>();
            var tracker = vehicle.GetComponent<RouteTracker>();
            var so = new SerializedObject(tracker);
            so.FindProperty("route").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddHud(tracker);

            if (TestPistiKurucu.SaveScene(scene, scenePath))
                Debug.Log($"[{etiket} Sahne] Sahne kuruldu: {scenePath}, rota {route?.name}");
        }

        /// <summary>Sol üstte hat, sıradaki durak ve hız (BusHud).</summary>
        private static void AddHud(RouteTracker tracker)
        {
            var canvasGo = new GameObject("HatGostergesi", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(30f, -250f);
            panel.sizeDelta = new Vector2(560f, 210f);
            var image = panel.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.11f, 0.55f);
            image.raycastTarget = false;

            var hud = canvasGo.AddComponent<BusHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("tracker").objectReferenceValue = tracker;
            so.FindProperty("lineText").objectReferenceValue = Text(panel, "Hat", -20f, 40, FontStyles.Bold);
            so.FindProperty("nextStopText").objectReferenceValue = Text(panel, "SiradakiDurak", -75f, 32, FontStyles.Normal);
            so.FindProperty("speedText").objectReferenceValue = Text(panel, "Hiz", -122f, 28, FontStyles.Normal);
            so.FindProperty("passengerText").objectReferenceValue = Text(panel, "Yolcu", -162f, 28, FontStyles.Normal);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text Text(RectTransform parent, string name, float y, float size, FontStyles style)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, y);
            rect.sizeDelta = new Vector2(-48f, size + 12f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
    }
}
