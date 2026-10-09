using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Otobüs FBX'inden sürülebilir otobüs prefabını ve BusDefinition asset'ini kurar (BMC Procity, Caio Millennium II).
    /// Parça adları docs/OTOBUS_BMC_PROCITY.md'deki hiyerarşiye göredir (tools/blender/bmc_donustur.py, millennium_donustur.py).
    /// Menü: Ankara Bus > BMC Procity Prefabını Kur / Caio Millennium Prefabını Kur
    /// </summary>
    public static class OtobusKurucu
    {
        /// <summary>Bir otobüs modelinin klasörü ve modele özgü kurulum değerleri.</summary>
        private sealed class Tanim
        {
            public string Ad;
            public string GorunenAd;
            public string Uretici;
            public int Kapasite = 90;
            // gövde çarpışma kutusu (otobüs yerel); boşsa modelin sınırlarından
            public Vector3? GovdeMerkez, GovdeBoyut;
            public (string name, string body, string roof)[] Kaplamalar;
            // fizik: BMC değerleri kopyalanır, bunlar üzerine yazılır
            public System.Action<BusPhysicsSpec> Fizik;
            // körüklü: iki gövde (Govde_On / Govde_Arka, mafsal Govde_Arka'nın pivotunda), 6 teker, kapı parçası yok
            public bool Koruklu;
            // kapı parçası olmayan modelde kapı noktaları (otobüs yerel; z < MafsalZ olanlar arka gövdede)
            public Vector3[] KapiNoktalari;
            // dokusuz gövde parçasının (ön/arka yüz) kaplamaya göre rengi (Kaplamalar sırasıyla)
            public string RenkMalzeme;
            // kaplamadaki ikinci dokunun (Kaplamalar.roof) gideceği malzeme adı parçası; boşsa BusLivery'nin varsayılanı (cngtank)
            public string IkinciMalzeme;
            public Color[] Renkler;
            public Vector3? MotorKonumu;
            public string Folder => "Assets/_Project/Buses/" + Ad + "/";
            public string ModelPath => Folder + Ad + ".fbx";
            public string PrefabPath => Folder + Ad + ".prefab";
            public string DefinitionPath => Folder + Ad + ".asset";
            public string MaterialFolder => Folder + "Materials";
            // Yüksek görüntü kalitesi için tam kalite model (docs/OTOBUS_BMC_PROCITY.md → Grafik ayarları için iki model)
            public string TamKaliteKaynak => Ad + "_TamKalite";
            public string TamKaliteModelPath => Folder + TamKaliteKaynak + ".fbx";
            public string TamKaliteGorselPath => Folder + "Resources/" + TamKaliteKaynak + ".prefab";
        }

        private static readonly Tanim Bmc = new Tanim
        {
            Ad = "BMC_Procity_12LF", GorunenAd = "BMC Procity 12LF", Uretici = "BMC",
            GovdeMerkez = new Vector3(0f, 1.78f, -0.215f), GovdeBoyut = new Vector3(2.5f, 2.7f, 11.9f),
            Kaplamalar = new[]
            {
                ("EGO kırmızı", "Textures/caroserie.png", "Textures/cngtank.png"),
                ("EGO mavi", "Textures/Kaplamalar/ego_mavi.png", "Textures/Kaplamalar/cngtank_beyaz.png"),
                ("Özel Halk", "Textures/Kaplamalar/ozel_halk.png", "Textures/Kaplamalar/cngtank_beyaz.png"),
            },
        };

        private static readonly Tanim Millennium = new Tanim
        {
            Ad = "Caio_Millennium_II", GorunenAd = "Mercedes-Benz O500M · Caio Millennium II", Uretici = "Mercedes-Benz / Caio",
            Kapasite = 90,
            Kaplamalar = new[]
            {
                ("EGO kırmızı", "Textures/caroserie.png", (string)null),
                ("EGO mavi", "Textures/Kaplamalar/ego_mavi.png", null),
                ("Özel Halk", "Textures/Kaplamalar/ozel_halk.png", null),
            },
            // dingil mesafesi 6.6 m (BMC 5.9 m): aynı kavşaklardan dönebilsin diye daha büyük direksiyon açısı
            Fizik = f => { f.wheelRadius = 0.525f; f.maxSteerAngle = 48f; },
        };

        // Mercedes O530G Conecto (körüklü, 18,2 m): tools/blender/conecto_donustur.py, kaplama tools/kaplama_conecto.py
        private static readonly Tanim Conecto = new Tanim
        {
            Ad = "MB_Conecto_G", GorunenAd = "Mercedes-Benz O530G Conecto (körüklü)", Uretici = "Mercedes-Benz",
            Kapasite = 150,
            Koruklu = true,
            Kaplamalar = new[]
            {
                ("EGO kırmızı", "Textures/caroserie.png", "Textures/onarka.png"),
                ("EGO mavi", "Textures/Kaplamalar/ego_mavi.png", "Textures/Kaplamalar/onarka_mavi.png"),
                ("Özel Halk", "Textures/Kaplamalar/ozel_halk.png", "Textures/Kaplamalar/onarka_ozel.png"),
            },
            // ön/arka yüz (ön cam, tabela, plaka, stoplar) ayrı dokuda: BusLivery'nin ikinci doku yuvası (roofModule)
            IkinciMalzeme = "onarka",
            RenkMalzeme = "M_Govde",
            Renkler = new[]
            {
                new Color(0.84f, 0.11f, 0.13f), new Color(0.11f, 0.25f, 0.63f), new Color(0.2f, 0.59f, 0.87f),
            },
            // sağdaki 4 kapının ortası (kaynak y aralıkları + 2,87 m; conecto_donustur.KAPILAR)
            KapiNoktalari = new[]
            {
                new Vector3(1.3f, 1.0f, 7.78f), new Vector3(1.3f, 1.0f, 1.89f),
                new Vector3(1.3f, 1.0f, -4.23f), new Vector3(1.3f, 1.0f, -7.43f),
            },
            MotorKonumu = new Vector3(0f, 1.0f, -8.2f),
            // 18 t (arka gövde %40), ağırlık merkezi ön ile orta aks arasında; dingil 5,9 m (BMC gibi)
            Fizik = f =>
            {
                f.wheelRadius = 0.51f; f.maxSteerAngle = 45f; f.massKg = 18000f; f.trailerMassRatio = 0.4f;
                f.centerOfMass = new Vector3(0f, 0.6f, 2.6f);
            },
        };

        /// <summary>Oyunda seçilebilen otobüsler, menüdeki sırayla (OyunSecimi.Otobus).</summary>
        private static readonly Tanim[] Katalog = { Bmc, Millennium, Conecto };

        // Build sırasında kurulan otobüs; aşağıdaki yardımcılar bunu okur
        private static Tanim aktif = Bmc;
        private static string Folder => aktif.Folder;
        private static string ModelPath => aktif.ModelPath;
        private static string DefinitionPath => aktif.DefinitionPath;
        private static string MaterialFolder => aktif.MaterialFolder;
        private static string TamKaliteModelPath => aktif.TamKaliteModelPath;
        private static string TamKaliteKaynak => aktif.TamKaliteKaynak;
        private static string TamKaliteGorselPath => aktif.TamKaliteGorselPath;

        /// <summary>Hat ve menü sahnelerine konan varsayılan otobüs (BMC); seçilen başkaysa oyunda değiştirilir.</summary>
        public const string PrefabPath = "Assets/_Project/Buses/BMC_Procity_12LF/BMC_Procity_12LF.prefab";
        public const string KatalogPath = "Assets/_Project/Resources/OtobusKatalogu.asset";
        private static readonly Color GlassColor = new Color(0.1f, 0.13f, 0.15f, 0.35f);

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
        public static void Build() => Build(Bmc);

        [MenuItem("Ankara Bus/Caio Millennium Prefabını Kur")]
        public static void BuildMillennium() => Build(Millennium);

        [MenuItem("Ankara Bus/Mercedes Conecto (Körüklü) Prefabını Kur")]
        public static void BuildConecto() => Build(Conecto);

        /// <summary>Komut satırı: tüm otobüs prefabları ve katalog.</summary>
        public static void BuildAllBatch()
        {
            foreach (var t in Katalog)
                Build(t);
            EditorApplication.Exit(0);
        }

        private static void Build(Tanim tanim)
        {
            aktif = tanim;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[OtobusKurucu] Model bulunamadı: " + ModelPath);
                return;
            }

            PrepareMaterials();
            bool tamKaliteVar = BuildTamKaliteGorsel();

            var definition = AssetDatabase.LoadAssetAtPath<BusDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BusDefinition>();
                if (tanim != Bmc)
                {
                    // sürüş değerleri BMC'den (bulutta ayarlandı); modele özgü olanlar üzerine
                    var bmc = AssetDatabase.LoadAssetAtPath<BusDefinition>(Bmc.DefinitionPath);
                    if (bmc != null)
                        definition.physics = JsonUtility.FromJson<BusPhysicsSpec>(JsonUtility.ToJson(bmc.physics));
                    tanim.Fizik?.Invoke(definition.physics);
                    definition.displayName = tanim.GorunenAd;
                    definition.manufacturer = tanim.Uretici;
                    definition.passengerCapacity = tanim.Kapasite;
                }
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var root = new GameObject(tanim.Ad);
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
                if (tanim.GovdeMerkez.HasValue)
                {
                    hull.center = tanim.GovdeMerkez.Value;
                    hull.size = tanim.GovdeBoyut.Value;
                }
                else
                {
                    if (parts.TryGetValue(tanim.Koruklu ? "Govde_On" : "Govde", out var govde))
                        GovdeKutusu(hull, root.transform, govde);
                    Debug.Log($"[OtobusKurucu] {tanim.Ad} gövde kutusu: merkez {hull.center}, boyut {hull.size}");
                }

                var vehicle = root.AddComponent<BusVehicle>();
                Rigidbody arka = tanim.Koruklu ? SetupArkaGovde(root, body, parts) : null;
                SetupWheels(root.transform, vehicle, parts, definition.physics, arka);
                SetupSteeringWheel(root.transform, vehicle, parts);
                if (tanim.KapiNoktalari != null)
                    SetupKapiNoktalari(root, arka, tanim.KapiNoktalari);
                else
                    SetupDoors(root, parts);
                if (tanim.Koruklu)
                    SetupKoruklu(root, arka, parts);

                foreach (var pair in parts)
                    if (pair.Key.StartsWith("Lamba_"))
                        pair.Value.gameObject.SetActive(false);

                root.AddComponent<BusInput>();
                root.AddComponent<RouteTracker>();
                root.AddComponent<RotaRehberi>();
                root.AddComponent<AnkaraBus.UI.MiniHarita>();
                root.AddComponent<AnkaraBus.Gameplay.SeferPuanlama>();
                root.AddComponent<AnkaraBus.UI.PuanGostergesi>();
                root.tag = "Player"; // trafik (TrafficSpawner) oyuncuyu bu etiketle bulur
                SetupShadows(root);
                SetupLivery(root);
                OtobusSesKurucu.ConfigureImporters();
                OtobusSesKurucu.Setup(root);
                if (tanim.MotorKonumu.HasValue)
                {
                    var ses = new SerializedObject(root.GetComponent<BusAudio>());
                    ses.FindProperty("enginePosition").vector3Value = tanim.MotorKonumu.Value;
                    ses.ApplyModifiedPropertiesWithoutUndo();
                }
                if (tamKaliteVar)
                    SetupKaliteModeli(root, modelInstance.transform);

                var so = new SerializedObject(vehicle);
                so.FindProperty("definition").objectReferenceValue = definition;
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, tanim.PrefabPath);
                definition.prefab = prefab;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                KataloguGuncelle();
                Debug.Log("[OtobusKurucu] Prefab kuruldu: " + tanim.PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Gölgeyi yalnızca dışbükey gövde kabuğu (Golge_Govde) verir; asıl parçalar gölge geçişinde
        /// tekrar çizilmez. Draw call'u yaklaşık yarıya indirir (docs/PERFORMANS.md).
        /// </summary>
        private static void SetupShadows(GameObject root)
        {
            // Gölge kabuğu olmayan modelde (tam kalite) her parça kendi gölgesini verir; dokunma
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (!System.Array.Exists(renderers, r => r.name.StartsWith("Golge_")))
                return;
            foreach (var r in renderers)
                r.shadowCastingMode = r.name.StartsWith("Golge_")
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Tam kalite FBX'in materyallerini Materials/ klasöründeki aynı adlı materyallere bağlar ve
        /// Resources altına görsel prefabını kaydeder (BusKaliteModeli yalnızca Yüksek ayarda yükler).
        /// </summary>
        private static bool BuildTamKaliteGorsel()
        {
            var importer = AssetImporter.GetAtPath(TamKaliteModelPath) as ModelImporter;
            if (importer == null)
                return false;

            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            importer.SaveAndReimport();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(TamKaliteModelPath))
                if (asset is Material embedded)
                    Debug.LogWarning($"[OtobusKurucu] Tam kalite materyali eşleşmedi: {embedded.name}");

            string resources = Folder + "Resources";
            if (!AssetDatabase.IsValidFolder(resources))
                AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Resources");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TamKaliteModelPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                SetupShadows(visual);
                PrefabUtility.SaveAsPrefabAsset(visual, TamKaliteGorselPath);
            }
            finally
            {
                Object.DestroyImmediate(visual);
            }
            return true;
        }

        private static void SetupKaliteModeli(GameObject root, Transform model)
        {
            var kalite = root.AddComponent<BusKaliteModeli>();
            var so = new SerializedObject(kalite);
            so.FindProperty("model").objectReferenceValue = model;
            so.FindProperty("tamKaliteKaynak").stringValue = TamKaliteKaynak;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Resources/OtobusKatalogu: menüde seçilebilen otobüslerin tanımları (prefabları build'e bunun üzerinden girer).
        /// </summary>
        private static void KataloguGuncelle()
        {
            var katalog = AssetDatabase.LoadAssetAtPath<OtobusKatalogu>(KatalogPath);
            if (katalog == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                katalog = ScriptableObject.CreateInstance<OtobusKatalogu>();
                AssetDatabase.CreateAsset(katalog, KatalogPath);
            }
            var list = new List<BusDefinition>();
            foreach (var t in Katalog)
            {
                var d = AssetDatabase.LoadAssetAtPath<BusDefinition>(t.DefinitionPath);
                if (d != null && d.prefab != null)
                    list.Add(d);
            }
            katalog.otobusler = list.ToArray();
            EditorUtility.SetDirty(katalog);
            AssetDatabase.SaveAssets();
        }

        private static void SetupLivery(GameObject root)
        {
            var items = aktif.Kaplamalar;
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
                element.FindPropertyRelative("roofModule").objectReferenceValue = items[i].roof == null ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + items[i].roof);
                element.FindPropertyRelative("color").colorValue =
                    aktif.Renkler != null && i < aktif.Renkler.Length ? aktif.Renkler[i] : new Color(0f, 0f, 0f, 0f);
            }
            if (!string.IsNullOrEmpty(aktif.RenkMalzeme))
                so.FindProperty("colorMaterialName").stringValue = aktif.RenkMalzeme;
            if (!string.IsNullOrEmpty(aktif.IkinciMalzeme))
                so.FindProperty("roofMaterialName").stringValue = aktif.IkinciMalzeme;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Gömülü materyalleri Materials/ klasörüne çıkarır (bir kez) ve adı _Cam ile bitenleri saydam cam yapar.
        /// </summary>
        private static void PrepareMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Materials");

            bool extracted = false;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is not Material embedded)
                    continue;
                string path = MaterialFolder + "/" + embedded.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                    continue;
                string error = AssetDatabase.ExtractAsset(embedded, path);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[OtobusKurucu] {embedded.name} çıkarılamadı: {error}");
                else
                    extracted = true;
            }
            if (extracted)
            {
                AssetDatabase.WriteImportSettingsIfDirty(ModelPath);
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.name.EndsWith("_Cam"))
                    MakeGlass(material);
            }
        }

        private static void MakeGlass(Material m)
        {
            // URP/Lit, Surface Type: Transparent, Blending: Alpha. Harmanlama ayrıntılarını URP kendisi kurar.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            UnityEditor.BaseShaderGUI.SetupMaterialBlendMode(m);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", GlassColor);
            EditorUtility.SetDirty(m);
        }

        // körüklü: ön aks yön verir, orta aks (ön gövdenin arkası) pasif, arka aks (arka gövde) çeker
        private static readonly (string name, bool front, bool left, bool passive)[] KorukluTekerler =
        {
            ("Teker_OnSol", true, true, false), ("Teker_OnSag", true, false, false),
            ("Teker_OrtaSol", false, true, true), ("Teker_OrtaSag", false, false, true),
            ("Teker_ArkaSol", false, true, false), ("Teker_ArkaSag", false, false, false),
        };

        private static void SetupWheels(Transform root, BusVehicle vehicle, Dictionary<string, Transform> parts, BusPhysicsSpec spec,
                                        Rigidbody arka)
        {
            var holder = new GameObject("Tekerlekler").transform;
            holder.SetParent(root, false);
            Transform arkaHolder = null;
            if (arka != null)
            {
                arkaHolder = new GameObject("Tekerlekler").transform;
                arkaHolder.SetParent(arka.transform, false);
            }

            var so = new SerializedObject(vehicle);
            var list = so.FindProperty("wheels");
            list.arraySize = 0;

            var tekerler = new List<(string name, bool front, bool left, bool passive)>();
            if (arka != null)
                tekerler.AddRange(KorukluTekerler);
            else
                foreach (var (n, f, l) in WheelParts)
                    tekerler.Add((n, f, l, false));

            foreach (var (name, front, left, passive) in tekerler)
            {
                if (!parts.TryGetValue(name, out var visual))
                {
                    Debug.LogWarning("[OtobusKurucu] Tekerlek bulunamadı: " + name);
                    continue;
                }

                // Statik yükte tekerlek, süspansiyonun hedef konumunda (yolun ortası) durur
                var go = new GameObject("WC_" + name.Substring("Teker_".Length));
                // çeken aks arka gövdenin Rigidbody'sine bağlı olmalı
                go.transform.SetParent(arkaHolder != null && name.StartsWith("Teker_Arka") ? arkaHolder : holder, false);
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
                element.FindPropertyRelative("passive").boolValue = passive;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gövde parçasının sınırlarından çarpışma kutusu: yandan taşan aynalar hariç (|x| &lt; 1.27 m),
        /// altı tekerleklere değmesin (zeminden 0.43 m yukarıda başlar). Kutu, kendi objesinin yerelinde.</summary>
        private static void GovdeKutusu(BoxCollider hull, Transform uzay, Transform govde)
        {
            var filter = govde.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;
            var b = new Bounds(new Vector3(0f, 1.6f, 0f), Vector3.zero);
            bool ilk = true;
            foreach (var v in filter.sharedMesh.vertices)
            {
                var p = uzay.InverseTransformPoint(govde.TransformPoint(v));
                if (Mathf.Abs(p.x) > 1.27f)
                    continue;
                if (ilk) { b = new Bounds(p, Vector3.zero); ilk = false; }
                else b.Encapsulate(p);
            }
            // yükseklik zeminden (uzay = kök); arka gövdede kök mafsal yüksekliğinde
            float zemin = uzay.InverseTransformPoint(Vector3.zero).y;
            float alt = zemin + 0.43f;
            hull.center = new Vector3(0f, (alt + b.max.y) * 0.5f, b.center.z);
            hull.size = new Vector3(Mathf.Min(b.size.x, 2.5f), b.max.y - alt, b.size.z - 0.1f);
            Debug.Log($"[OtobusKurucu] {govde.name} gövde kutusu: merkez {hull.center}, boyut {hull.size}");
        }

        /// <summary>
        /// Körüklünün arka gövdesi: mafsalda (Govde_Arka'nın pivotu) ayrı Rigidbody, ön gövdeye ConfigurableJoint ile bağlı:
        /// konum kilitli; sapma ±52°, yokuşta eğilme ±10°, yatma ±4°; sapma ve eğilmede sönüm (gerçekte hidrolik).
        /// Oyunda KorukluOtobus onu sahne köküne alır.
        /// </summary>
        private static Rigidbody SetupArkaGovde(GameObject root, Rigidbody on, Dictionary<string, Transform> parts)
        {
            var arkaGo = new GameObject("ArkaGovde");
            arkaGo.transform.SetParent(root.transform, false);
            arkaGo.transform.position = parts.TryGetValue("Govde_Arka", out var gArka) ? gArka.position : root.transform.TransformPoint(0f, 1f, -1.57f);
            arkaGo.transform.rotation = root.transform.rotation;
            arkaGo.layer = root.layer;
            var rb = arkaGo.AddComponent<Rigidbody>();
            rb.mass = 7000f; // BusVehicle oyunda kütleyi trailerMassRatio'dan kurar

            var hull = new GameObject("Carpisma_Arka").AddComponent<BoxCollider>();
            hull.transform.SetParent(arkaGo.transform, false);
            if (gArka != null)
                GovdeKutusu(hull, arkaGo.transform, gArka);

            var j = arkaGo.AddComponent<ConfigurableJoint>();
            j.connectedBody = on;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = Vector3.zero;
            j.connectedAnchor = root.transform.InverseTransformPoint(arkaGo.transform.position);
            j.axis = Vector3.right;
            j.secondaryAxis = Vector3.up;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
            j.lowAngularXLimit = new SoftJointLimit { limit = -10f };
            j.highAngularXLimit = new SoftJointLimit { limit = 10f };
            j.angularYLimit = new SoftJointLimit { limit = 52f };
            j.angularZLimit = new SoftJointLimit { limit = 4f };
            j.rotationDriveMode = RotationDriveMode.XYAndZ;
            j.angularXDrive = new JointDrive { positionSpring = 0f, positionDamper = 60000f, maximumForce = float.MaxValue };
            j.angularYZDrive = new JointDrive { positionSpring = 0f, positionDamper = 25000f, maximumForce = float.MaxValue };
            j.enableCollision = false;
            j.enablePreprocessing = false;
            return rb;
        }

        /// <summary>Kapı parçası olmayan modelde kapılar: hareketsiz "kanat" noktaları (yolcular buraya yürür).</summary>
        private static void SetupKapiNoktalari(GameObject root, Rigidbody arka, Vector3[] noktalar)
        {
            var controller = root.AddComponent<BusDoorController>();
            var so = new SerializedObject(controller);
            var groups = so.FindProperty("doors");
            groups.arraySize = noktalar.Length;
            float mafsalZ = arka != null ? root.transform.InverseTransformPoint(arka.transform.position).z : float.MinValue;
            for (int i = 0; i < noktalar.Length; i++)
            {
                var nokta = new GameObject($"Kapi_{i + 1}");
                nokta.transform.SetParent(arka != null && noktalar[i].z < mafsalZ ? arka.transform : root.transform, false);
                nokta.transform.position = root.transform.TransformPoint(noktalar[i]);
                var door = nokta.AddComponent<BusDoor>();
                var dso = new SerializedObject(door);
                dso.FindProperty("motion").enumValueIndex = (int)BusDoor.Motion.Slide;
                dso.FindProperty("openPositionOffset").vector3Value = Vector3.zero;
                dso.FindProperty("duration").floatValue = 1.2f;
                dso.ApplyModifiedPropertiesWithoutUndo();
                var group = groups.GetArrayElementAtIndex(i);
                group.FindPropertyRelative("name").stringValue = i == 0 ? "Ön kapı" : $"{i + 1}. kapı";
                var leaves = group.FindPropertyRelative("leaves");
                leaves.arraySize = 1;
                leaves.GetArrayElementAtIndex(0).objectReferenceValue = door;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupKoruklu(GameObject root, Rigidbody arka, Dictionary<string, Transform> parts)
        {
            var k = root.AddComponent<KorukluOtobus>();
            var so = new SerializedObject(k);
            so.FindProperty("arkaGovde").objectReferenceValue = arka;
            var gorseller = new List<Transform>();
            foreach (var ad in new[] { "Govde_Arka", "Golge_Arka" })
                if (parts.TryGetValue(ad, out var t))
                    gorseller.Add(t);
            var list = so.FindProperty("arkaGorseller");
            list.arraySize = gorseller.Count;
            for (int i = 0; i < gorseller.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = gorseller[i];
            if (parts.TryGetValue("Koruk", out var koruk))
                so.FindProperty("koruk").objectReferenceValue = koruk;
            so.ApplyModifiedPropertiesWithoutUndo();

            // modelde direksiyon yok: kokpit kamerası sürücü koltuğunda (solda, ön camın 1,25 m gerisi)
            if (root.transform.Find("SurucuGozu") == null && parts.TryGetValue("Govde_On", out var on))
            {
                var r = on.GetComponent<Renderer>();
                float onUc = r != null ? root.transform.InverseTransformPoint(r.bounds.max).z : 8.9f;
                var eye = new GameObject("SurucuGozu").transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = new Vector3(-0.62f, 2.25f, onUc - 1.25f);
            }
        }

        private static void SetupSteeringWheel(Transform root, BusVehicle vehicle, Dictionary<string, Transform> parts)
        {
            if (!parts.TryGetValue("Direksiyon", out var wheel))
                return;

            // Kokpit kamerası: direksiyonun arkasında, sürücü göz hizasında
            var eye = new GameObject("SurucuGozu").transform;
            eye.SetParent(root, false);
            eye.position = wheel.position + root.up * 0.6f - root.forward * 0.6f;
            eye.rotation = root.rotation;

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
